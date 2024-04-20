import _ from 'lodash';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { INodeConfig } from '../../data/NodeData';
import { NodeManager } from './NodeManager';
import { FDGContainer } from '../FDG/FDGContainer';
import { FDGLink } from '../FDG/FDGLink';
import { INodeSave, PlantNode } from '../nodes/PlantNode';
import { CrawlerModel } from './Parts/CrawlerModel';
import { ICrawlerSave } from 'src/data/SaveData';
import { GameKnowledge } from './GameKnowledge';
import { GameEvents } from '../../services/GameEvents';
import { CrawlerSlug, ICrawlerConfig } from '../../data/CrawlerData';
import { Firework } from '../../JMGE/effects/Firework';
import { TransferBlock } from '../Transfers/_TransferBlock';

export class GameController {
  public onNodeAdded = new JMEventListener<PlantNode>();
  public onNodeRemoved = new JMEventListener<PlantNode>();
  public onNodeClaimed = new JMEventListener<{claim: boolean, node: PlantNode, claimer: CrawlerModel}>();
  public onCrawlerAdded: JMEventListener<CrawlerModel> = new JMEventListener();
  public onCrawlerRemoved: JMEventListener<CrawlerModel> = new JMEventListener();

  public nodes: PlantNode[] = [];
  public crawlers: CrawlerModel[] = [];
  public knowledge: GameKnowledge;
  private exists = true;

  constructor(private container: FDGContainer, private nodeManager: NodeManager, scores: number[]) {
    this.knowledge = new GameKnowledge(this, nodeManager);
  }

  public destroy() {
    this.knowledge.destroy();
    this.exists = false;
  }

  public addNewNode(config: INodeConfig): PlantNode {
    let node = new PlantNode(config, this.transferPower);
    
    this.container.addNode(node);
    this.nodes.push(node);

    this.onNodeAdded.publish(node);

    return node;
  }

  public removeNode = (node: PlantNode, explosionSize: number = 0) => {
    _.pull(this.nodes, node);

    this.crawlers.forEach(crawler => {
      if (crawler.cLoc === node) {
        this.killCrawler(crawler);
      }
    });

    this.disconnectNode(node);

    this.container.removeNode(node);

    if (explosionSize > 0) {
      Firework.makeExplosion(this.container, {x: node.view.x, y: node.view.y, tint: node.config.color, mag_min: 0.5, mag_max: 2, count: node.config.radius});
      // interface IExplosion {
      //   x: number;
      //   y: number;
      //   count?: number;
      //   angle_min?: number;
      //   angle_max?: number;
      //   mag_min?: number;
      //   mag_max?: number;
      //   fade?: number;
      //   size_min?: number;
      //   size_max?: number;
      //   tint?: number;
      // }
    }

    node.destroy();
    if (node.flagCallOnRemove) this.onNodeRemoved.publish(node);
  }

  public addCrawler(config: ICrawlerConfig, node: PlantNode): CrawlerModel {
    if (!config) {
      config = this.determineNextCrawler();
    }
    let crawler = new CrawlerModel(config, node, this.knowledge);
    crawler.onNodeClaimed.addListener(this.onNodeClaimed.publish);
    this.crawlers.push(crawler);
    this.container.addCrawler(crawler.view);
    this.onCrawlerAdded.publish(crawler);
    return crawler;
  }

  public removeCrawler(crawler: CrawlerModel) {
    crawler.destroy();
    _.pull(this.crawlers, crawler);
    this.container.removeCrawler(crawler.view);
    this.onCrawlerRemoved.publish(crawler);
  }

  public linkNodes(origin: PlantNode, target: PlantNode): FDGLink {
    origin.linkNode(target);
    target.linkNode(origin, true);
    
    this.setCoreDistance();

    return this.container.addLink(origin, target);
  }

  public setCoreDistance() {
    let core = this.nodes.find(node => node.slug === 'core');
    let open = [core];
    let closed: PlantNode[] = [];

    while (open.length > 0) {
      let current = open.shift();

      current.outlets.forEach(outlet => {
        if (!closed.includes(outlet) && !open.includes(outlet)) {
          outlet.distanceCore = current.distanceCore + 1;
          open.push(outlet);
        }
      });

      closed.push(current);
    }
  }

  public onTick(ticks: number) {
    this.crawlers.forEach(this.updateCrawler);
    this.nodes.forEach(this.updateNode);

    if (ticks > 1) {
      this.onTick(ticks - 1);
    }
  }

  public updateNode = (node: PlantNode) => {
    if (node.flagDestroy) {
      this.removeNode(node, node.flagExplode ? 1 : 0);
      return;
    } else if (node.flagUnlink) {
      node.flagUnlink = false;
      this.disconnectNode(node);
      if (node.flagCallOnRemove) {
        this.onNodeRemoved.publish(node);
        node.flagCallOnRemove = false;
      }
      return;
    }
    node.tickPower();

    if (node.power.fruitSpawn >= 1) {
      node.power.fruitSpawn--;
      if (node.fruits.length < node.config.maxFruits) {
        let fruitConfig = this.nodeManager.getNodeConfig(node.power.fruitType);
        let fruit = this.addNewNode(fruitConfig);
        this.linkNodes(node, fruit);
        fruit.view.position.set(node.view.x, node.view.y);

        if (node.slug === 'home' && this.crawlers.length < this.nodes.filter(node2 => node2.slug === 'home').length) {
          this.addCrawler(null, node);
        }
      } else {
        let fruit = node.fruits.find(fruit2 => fruit2.canSpawnFruit());
        if (fruit) {
          fruit.receiveFruitPower(1);
        }
      }
    }
  }

  public disconnectNode = (node: PlantNode) => {
    this.container.removeAllLinksFor(node);

    while (node.fruits.length > 0) {
      let fruit = node.fruits.shift();
      fruit.removeNode(node);
      this.removeNode(fruit, 1);
    }
    while (node.outlets.length > 0) {
      let outlet = node.outlets.shift();
      outlet.removeNode(node);
      if (!outlet.isConnectedToCore()) {
        this.removeNode(outlet, 5);
      }
    }
  }

  public updateCrawler = (crawler: CrawlerModel) => {
    if (crawler.health <= 0) {
      this.killCrawler(crawler);
      return;
    } else if (crawler.health > crawler.config.breedThreshold) {
      crawler.health /= 2;
      this.addCrawler(_.defaults({health: crawler.health}, this.determineNextCrawler()), crawler.cLoc);
    }

    crawler.update();
  }

  public killCrawler = (crawler: CrawlerModel) => {
    crawler.destroy();
    _.pull(this.crawlers, crawler);
    crawler.view.animateDie(() => {
      this.container.removeCrawler(crawler.view);
      this.onCrawlerRemoved.publish(crawler);
    });
  }

  public saveNodes(): INodeSave[] {
    let saves: INodeSave[] = this.nodes.filter(node => (node.slug === 'core' || node.outlets.length > 0)).map(node => {
      let outlets: number[] = this.container.links.filter(l => l.origin === node).map(l => l.target.uid);
      let save: INodeSave = {
        uid: node.uid,
        slug: node.slug,
        powerCurrent: Math.round(node.power.powerCurrent),
        outlets,
        x: Math.round(node.view.x),
        y: Math.round(node.view.y),
      };

      if (node.slug === 'seedling') {
        save.researchCurrent = node.power.researchCurrent;
      } else if (node.slug === 'hub') {
        save.researchCurrent = node.power.researchCurrent;
        save.fruitCurrent = node.power.fruitCurrent;
        save.buffCurrent = node.power.buffCurrent;
        save.storedPowerCurrent = node.power.storedPowerCurrent;
        save.receiveFruit = node.power.canReceiveFruit;
        save.receiveResearch = node.power.canReceiveResearch;
        save.receivePower = node.power.canStorePower;
      }
      return save;
    });

    return saves;
  }

  public saveCrawlers(): ICrawlerSave[] {
    let saves: ICrawlerSave[] = this.crawlers.map(crawler => {
      return {
        slug: crawler.slug,
        preference: crawler.preference,
        health: crawler.health,
        location: crawler.cLoc ? crawler.cLoc.uid : undefined,
      };
    });

    return saves;
  }

  public loadSaves(saves: INodeSave[], crawlerSaves: ICrawlerSave[]) {
    let nodes = saves.map(save => this.importSave(save));
    this.nodes = nodes;

    nodes.forEach((node, i) => {
      this.container.addNode(node);
      this.onNodeAdded.publish(node);

      saves[i].outlets.forEach(uid => {
        this.linkNodes(node, nodes.find(node2 => node2.uid === uid));
      });
    });

    crawlerSaves.forEach((save, i) => {
      if (!save.location) return;
      this.addCrawler(_.defaults({health: save.health, preference: save.preference}, this.nodeManager.getCrawlerConfig(save.slug)), nodes.find(node2 => node2.uid === save.location));
    });
  }

  public importSave(save: INodeSave): PlantNode {
    let config = this.nodeManager.getNodeConfig(save.slug);

    let node = new PlantNode(config, this.transferPower);
    node.power.powerCurrent = save.powerCurrent;
    node.uid = save.uid;
    node.view.position.set(save.x, save.y);
    node.view.setIntensity(node.power.powerPercent, true);

    if (node.slug === 'seedling') {
      node.power.researchCurrent = save.researchCurrent;
    } else if (node.slug === 'hub') {
      node.power.researchCurrent = save.researchCurrent;
      node.power.fruitCurrent = save.fruitCurrent;
      node.power.buffCurrent = save.buffCurrent;
      node.power.storedPowerCurrent = save.storedPowerCurrent;
      node.power.canReceiveFruit = save.receiveFruit;
      node.power.canReceiveResearch = save.receiveResearch;
      node.power.canStorePower = save.receivePower;  
    }

    PlantNode.addUid(save.uid);
    return node;
  }

  public transferPower = (block: TransferBlock) => {
    let link = this.container.getLink(block.origin, block.target);

    if (block.type === 'grow') {
      block.finishTransferGrow();

      if (block.removeOrigin) {
        this.removeNode(block.origin);
      } else {
        link.flash();
      }
    } else {
      GameEvents.ACTIVITY_LOG.publish({slug: 'BLOB', data: {add: true, type: block.type}});
      link.zip(block, () => {
        if (!this.exists) return;
        GameEvents.ACTIVITY_LOG.publish({slug: 'BLOB', data: {add: false, type: block.type}});

        let transferComplete: boolean;
        if (block.type === 'research') {
          transferComplete = block.finishTransferResearch();
        } else if (block.type === 'fruit') {
          transferComplete = block.finishTransferFruit();
        } else if (block.type === 'buff') {
          transferComplete = block.finishTransferBuff();
        }

        if (!transferComplete) {
          let target2 = block.selectNextTarget();

          if (target2) {
            block.setSource(block.target, target2);
            this.transferPower(block);
          }
        }
      });
    }
  }

  private determineNextCrawler(): ICrawlerConfig {
    let available = this.nodeManager.availableCrawlers.filter(slug => {
      let config = this.nodeManager.getCrawlerConfig(slug);
      if (!config.maxCount || this.knowledge.sortedCrawlers[slug].length < config.maxCount) {
        return true;
      }

      return false;
    });
    if (available.length > 0) {
      return this.nodeManager.getCrawlerConfig(_.sample(available));
    }
  }
}

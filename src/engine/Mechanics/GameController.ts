import * as _ from 'lodash';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { Config } from '../../Config';
import { Colors } from '../../data/Colors';
import { INodeConfig } from '../../data/NodeData';
import { NodeManager } from './NodeManager';
import { FDGContainer } from '../FDG/FDGContainer';
import { FDGLink } from '../FDG/FDGLink';
import { INodeSave, PlantNode } from '../nodes/PlantNode';
import { ITransferBlock } from '../nodes/PlantNodePower';
import { CrawlerModel } from './Parts/CrawlerModel';
import { ICrawlerSave } from 'src/data/SaveData';
import { GameKnowledge } from './GameKnowledge';
import { GameEvents } from '../../services/GameEvents';
import { CrawlerSlug, ICrawlerConfig } from '../../data/CrawlerData';

export class GameController {
  public onNodeAdded = new JMEventListener<PlantNode>();
  public onNodeRemoved = new JMEventListener<PlantNode>();
  public onNodeClaimed = new JMEventListener<{claim: boolean, node: PlantNode, claimer: CrawlerModel}>();
  public onCrawlerAdded: JMEventListener<CrawlerModel> = new JMEventListener();
  public onCrawlerRemoved: JMEventListener<CrawlerModel> = new JMEventListener();

  public nodes: PlantNode[] = [];
  public crawlers: CrawlerModel[] = [];
  public knowledge: GameKnowledge;

  private rBlobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && (outlet.outlets.length >= 2 || outlet.config.slug === 'seedling')))),
  ];

  private fBlobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && (outlet.outlets.length >= 2 || outlet.canSpawnFruit())))),
  ];

  constructor(private container: FDGContainer, private nodeManager: NodeManager, scores: number[]) {
    this.knowledge = new GameKnowledge(this, nodeManager);
  }

  public destroy() {
    this.knowledge.destroy();
  }

  public addNewNode(config: INodeConfig): PlantNode {
    let node = new PlantNode(config, this.transferPower);
    this.container.addNode(node);
    this.nodes.push(node);

    this.onNodeAdded.publish(node);

    return node;
  }

  public removeNode = (node: PlantNode) => {
    _.pull(this.nodes, node);

    this.crawlers.forEach(crawler => {
      if (crawler.cLoc === node) {
        this.killCrawler(crawler);
      }
    });

    this.disconnectNode(node);

    this.container.removeNode(node);

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

    // origin.distanceCore = target.distanceCore + 1;
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
      this.removeNode(node);
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

    if (node.power.fruitSpawn >= 1 && node.canSpawnFruit()) {
      node.power.fruitSpawn--;

      let fruitConfig = this.nodeManager.getNodeConfig(node.power.fruitType);
      let fruit = this.addNewNode(fruitConfig);
      this.linkNodes(node, fruit);
      fruit.view.position.set(node.view.x, node.view.y);

      if (node.slug === 'home' && this.crawlers.length < this.nodes.filter(node2 => node2.slug === 'home').length) {
        this.addCrawler(null, node);
      }
    }
  }

  public disconnectNode = (node: PlantNode) => {
    this.container.removeAllLinksFor(node);

    while (node.fruits.length > 0) {
      let fruit = node.fruits.shift();
      fruit.removeNode(node);
      this.removeNode(fruit);
    }
    while (node.outlets.length > 0) {
      let outlet = node.outlets.shift();
      outlet.removeNode(node);
      if (!outlet.isConnectedToCore()) {
        this.removeNode(outlet);
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

      return {
        uid: node.uid,
        slug: node.slug,
        powerCurrent: Math.round(node.power.powerCurrent),
        researchCurrent: node.slug === 'seedling' ? node.power.researchCurrent : 0,
        outlets,
        x: Math.round(node.view.x),
        y: Math.round(node.view.y),
      };
    });

    return saves;
  }

  public saveCrawlers(): ICrawlerSave[] {
    let saves: ICrawlerSave[] = this.crawlers.map(crawler => {
      return {
        slug: crawler.slug,
        preference: crawler.preference,
        health: crawler.health,
        location: crawler.cLoc.uid,
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
      this.addCrawler(_.defaults({health: save.health, preference: save.preference}, this.nodeManager.getCrawlerConfig(save.slug)), nodes.find(node2 => node2.uid === save.location));
    });
  }

  public importSave(save: INodeSave): PlantNode {
    let config = this.nodeManager.getNodeConfig(save.slug);

    let node = new PlantNode(config, this.transferPower);
    node.power.powerCurrent = save.powerCurrent;
    node.power.researchCurrent = save.researchCurrent;
    node.uid = save.uid;
    node.view.position.set(save.x, save.y);
    node.view.setIntensity(node.power.powerPercent, true);

    PlantNode.addUid(save.uid);
    return node;
  }

  public transferPower = (origin: PlantNode, target: PlantNode, block: ITransferBlock) => {
    // if (origin.outlets.indexOf(target) === -1 && origin.fruits.indexOf(target) === -1) return;
    let link = this.container.getLink(origin, target);

    if (block.type === 'grow') {
      target.power.powerCurrent += block.amount;

      if (block.removeOrigin) {
        this.removeNode(origin);
      } else {
        link.flash();
      }
    } else if (block.type === 'research') {
      GameEvents.ACTIVITY_LOG.publish({slug: 'BLOB', data: true});
      link.zip(origin, Colors.Node.purple, block.fade, () => {
        GameEvents.ACTIVITY_LOG.publish({slug: 'BLOB', data: false});
        if (target.slug === 'seedling') {
          target.receiveResearch(block.amount);
        } else {
          block.fade--;
          if (block.fade <= 0) return;
          let target2 = this.rBlobAI[Config.NODE.BLOB_AI](origin, target);
          if (!target2) target2 = _.sample(target.outlets.filter(outlet => (outlet.active)));
          if (target2) {
            this.transferPower(target, target2, block);
          }
        }
      });
    } else if (block.type === 'fruit') {
      GameEvents.ACTIVITY_LOG.publish({slug: 'BLOB', data: true});
      link.zip(origin, Colors.Node.orange, block.fade, () => {
        GameEvents.ACTIVITY_LOG.publish({slug: 'BLOB', data: false});
        if (target.canSpawnFruit() && Math.random() < Config.NODE.FRUIT_APPLY) {
          target.receiveFruitPower(block.amount);
          // add research
        } else {
          block.fade--;
          if (block.fade <= 0) return;
          let target2 = this.fBlobAI[Config.NODE.BLOB_AI](origin, target);
          if (!target2) target2 = _.sample(target.outlets.filter(outlet => (outlet.active)));
          if (target2) {
            this.transferPower(target, target2, block);
          }
        }
      });
    }
  }

  private determineNextCrawler(): ICrawlerConfig {
    if (false && this.knowledge.sortedCrawlers.chieftain.length === 0) {
      return this.nodeManager.getCrawlerConfig('chieftain');
    } else {
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
}

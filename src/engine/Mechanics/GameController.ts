import * as _ from 'lodash';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { Config } from '../../Config';
import { Colors } from '../../data/Colors';
import { INodeConfig } from '../../data/NodeData';
import { NodeManager } from '../../services/NodeManager';
import { FDGContainer } from '../FDG/FDGContainer';
import { FDGLink } from '../FDG/FDGLink';
import { INodeSave, PlantNode } from '../nodes/PlantNode';
import { ITransferBlock } from '../nodes/PlantNodePower';
import { CrawlerModel, ICrawler } from './Parts/CrawlerModel';
import { ICrawlerSave } from 'src/data/SaveData';
import { GameKnowledge } from './GameKnowledge';

export class GameController {
  public onNodeAdded = new JMEventListener<PlantNode>();
  public onNodeRemoved = new JMEventListener<PlantNode>();
  public onNodeClaimed = new JMEventListener<{claim: boolean, node: PlantNode, claimer: CrawlerModel}>();
  public onCrawlerAdded: JMEventListener<CrawlerModel> = new JMEventListener();
  public onCrawlerRemoved: JMEventListener<CrawlerModel> = new JMEventListener();
  public nodes: PlantNode[] = [];
  public crawlers: CrawlerModel[] = [];
  public knowledge: GameKnowledge;

  constructor(private container: FDGContainer, private nodeManager: NodeManager) {
    this.knowledge = new GameKnowledge(this, nodeManager);
  }

  public destroy() {

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

  public addCrawler(config: ICrawler, node: PlantNode): CrawlerModel {
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

    return this.container.addLink(origin, target);
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
        this.addCrawler(this.nodeManager.crawlerConfig, node);
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
    } else if (crawler.health > 1.5) {
      crawler.health /= 2;
      this.addCrawler(_.defaults({health: crawler.health}, this.nodeManager.crawlerConfig), crawler.cLoc);
    }

    crawler.update();
  }

  public killCrawler = (crawler: CrawlerModel) => {
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
    console.log('LOAD_SAVE', saves.map(save => save.slug));

    nodes.forEach((node, i) => {
      this.container.addNode(node);
      this.onNodeAdded.publish(node);

      saves[i].outlets.forEach(uid => {
        this.linkNodes(node, nodes.find(node2 => node2.uid === uid));
      });
    });

    crawlerSaves.forEach((save, i) => {
      this.addCrawler(_.defaults({health: save.health, preference: save.preference}, this.nodeManager.crawlerConfig), nodes.find(node2 => node2.uid === save.location));
    });
  }

  public importSave(save: INodeSave): PlantNode {
    let config = this.nodeManager.getNodeConfig(save.slug);

    let node = new PlantNode(config, this.transferPower);
    node.power.powerCurrent = save.powerCurrent;
    node.power.researchCurrent = save.researchCurrent;
    node.uid = save.uid;
    node.view.position.set(save.x, save.y);

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
      link.zip(origin, Colors.Node.purple, block.fade, () => {
        if (target.slug === 'seedling') {
          target.receiveResearch(block.amount);
        } else {
          block.fade--;
          if (block.fade <= 0) return;
          let target2 = _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin)));
          // let target2 = _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && (outlet.outlets.length >= 2 || outlet.config.slug === 'seedling'))));
          if (!target2) target2 = _.sample(target.outlets.filter(outlet => (outlet.active)));
          if (target2) {
            this.transferPower(target, target2, block);
          }
        }
      });
    } else if (block.type === 'fruit') {
      link.zip(origin, Colors.Node.orange, block.fade, () => {
        if (target.canSpawnFruit() && Math.random() < Config.NODE.FRUIT_APPLY) {
          target.receiveFruitPower(block.amount);
          // add research
        } else {
          block.fade--;
          if (block.fade <= 0) return;
          let target2 = _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin)));
          if (!target2) target2 = _.sample(target.outlets.filter(outlet => (outlet.active)));
          if (target2) {
            this.transferPower(target, target2, block);
          }
        }
      });
    }
  }
}

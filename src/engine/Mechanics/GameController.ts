import * as _ from 'lodash';
import { Config } from '../../Config';
import { Colors } from '../../data/Colors';
import { INodeConfig } from '../../data/NodeData';
import { NodeManager } from '../../services/NodeManager';
import { FDGContainer } from '../FDG/FDGContainer';
import { FDGLink } from '../FDG/FDGLink';
import { INodeSave, PlantNode } from '../nodes/PlantNode';
import { ITransferBlock } from '../nodes/PlantNodePower';
import { AIType, CrawlerModel } from './Parts/CrawlerModel';

export class GameController {
  public nodes: PlantNode[] = [];
  public crawlers: CrawlerModel[] = [];

  constructor(private container: FDGContainer, private nodeManager: NodeManager) {

  }

  public destroy() {

  }

  public addNewNode(config: INodeConfig): PlantNode {
    let node = new PlantNode(config, this.transferPower);
    this.container.addNode(node);
    this.nodes.push(node);

    return node;
  }

  public removeNode = (node: PlantNode) => {
    _.pull(this.nodes, node);
  }

  public addCrawler(config: any, node: PlantNode): CrawlerModel {
    let crawler = new CrawlerModel(node);
    this.crawlers.push(crawler);
    this.container.addCrawler(crawler.view);
    return crawler;
  }

  public removeCrawler(crawler: CrawlerModel) {
    _.pull(this.crawlers, crawler);
    this.container.removeCrawler(crawler.view);
  }

  public linkNodes(origin: PlantNode, target: PlantNode): FDGLink {
    origin.linkNode(target);
    target.linkNode(origin, true);

    return this.container.addLink(origin, target);
  }

  public onTick(ticks: number) {
    this.nodes.forEach(this.updateNode);
    this.crawlers.forEach(this.updateCrawler);

    if (ticks > 1) {
      this.onTick(ticks - 1);
    }
  }

  public updateNode(node: PlantNode) {
    node.tickPower();

    if (node.power.fruitSpawn >= 1 && node.canSpawnFruit()) {
      node.power.fruitSpawn--;

      let fruitConfig = this.nodeManager.getNodeConfig(node.power.fruitType);
      let fruit = this.addNewNode(fruitConfig);
      this.linkNodes(node, fruit);
      fruit.view.position.set(node.view.x, node.view.y);
    }
  }

  public updateCrawler(crawler: CrawlerModel) {
    switch (crawler.aiType) {
      case AIType.WANDER:
        crawler.magnitude += crawler.speed;
        if (crawler.magnitude > 1) {
          crawler.magnitude = 0;
          crawler.cLoc = crawler.nextLoc;
          crawler.nextLoc = null;
          crawler.randomizeAi();
        }
        break;
      case AIType.IDLE:
        crawler.magnitude += crawler.speed / crawler.cLoc.view.radius * 10;
        if (crawler.magnitude > crawler.aiExtra) {
          crawler.magnitude = crawler.aiExtra;
          crawler.speed = - crawler.speed;
        } else if (crawler.magnitude < 0) {
          crawler.magnitude = 0;
          crawler.setAi();
        }
        break;
      case AIType.GO_CENTER:
        crawler.magnitude -= crawler.speed / crawler.cLoc.view.radius * 10;
        if (crawler.magnitude < 0) {
          crawler.magnitude = 0;
          crawler.setAi();
        }
        break;
    }

    crawler.update();
    console.log('crawler', crawler);
  }

  public saveNodes(): INodeSave[] {
    let saves: INodeSave[] = this.nodes.map(node => {
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

  public loadSaves(saves: INodeSave[]) {
    let nodes = saves.map(save => this.importSave(save));
    this.nodes = nodes;
    console.log('LOAD_SAVE', saves.map(save => save.slug));

    nodes.forEach((node, i) => {
      this.container.addNode(node);

      saves[i].outlets.forEach(uid => {
        this.linkNodes(node, nodes.find(node2 => node2.uid === uid));
      });
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
    let link = this.container.getLink(origin, target);
    if (!link) return;

    if (block.type === 'grow') {
      target.power.powerCurrent += block.amount;

      if (block.removeOrigin) {
        this.container.removeNode(origin);
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

  public transferGrowPower = (origin: PlantNode, target: PlantNode, block: ITransferBlock) => {
  }
  public transferResearchPower = (origin: PlantNode, target: PlantNode, block: ITransferBlock) => {
  }
  public transferFruitPower = (origin: PlantNode, target: PlantNode, block: ITransferBlock) => {
  }

}

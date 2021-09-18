import * as _ from 'lodash';
import { NodeConfig } from '../../data/NodeData';
import { NodeManager } from '../../services/NodeManager';
import { TextureCache } from '../../services/TextureCache';
import { FDGContainer } from '../FDG/FDGContainer';
import { FDGLink } from '../FDG/FDGLink';
import { FDGNode } from '../FDG/FDGNode';
import { GameNode, NodeSave } from './Parts/GameNode';

export class GameController {

  nodes: GameNode[] = [];

  constructor(private container: FDGContainer) {
    
  }

  public addNewNode(config: NodeConfig): FDGNode {
    let node: FDGNode = new FDGNode(TextureCache.getNodeGraphicTexture(config.shape,config.radius), config);
    this.container.addNode(node);
    this.addNode(node);

    return node;
  }

  public addNode = (view: FDGNode) => {
    let node = new GameNode(view, view.config, this.transferPower);
    console.log('node', node.uid, node.config.name);
    this.nodes.push(node);
  }

  public removeNodeByView = (view: FDGNode) => {
    let node = view.data;
    _.pull(this.nodes, node);
  }

  public destroy() {

  }

  public onTick(ticks: number) {
    this.nodes.forEach(node => {
      node.onTick();
      if (node.fruitSpawn > 1 && node.fruits.length < node.maxFruits) {
        node.fruitSpawn -= 1;
        let fruitConfig = NodeManager.getNodeConfig(node.fruitType);

        let fruit = this.addNewNode(fruitConfig);
        fruit.data.fruitType = fruit.data.fruitType || node.fruitType;
        fruit.data.fruitChain = node.fruitChain - 1;
        fruit.data.maxFruits = fruit.data.maxFruits || node.maxFruits;

        this.container.linkNodes(node.view, fruit);
        fruit.position.set(node.view.x, node.view.y);
      }
    });

    if (ticks > 1) {
      this.onTick(ticks - 1);
    }
  }

  public saveNodes (): NodeSave[] {
    let saves = this.nodes.map(node => {
      let outlets: number[] = this.container.links.filter(l => l.origin === node.view).map(l => l.target.data.uid);

      return {
        uid: node.uid,
        slug: node.config.name,
        powerCurrent: Math.round(node.powerCurrent),
        outlets,
        x: Math.round(node.view.x),
        y: Math.round(node.view.y),
      };
    });

    return saves;
  }

  public loadSaves (saves: NodeSave[]) {
    let nodes = saves.map(save => GameNode.importSave(save, this.transferPower));
    this.nodes = nodes;
    console.log('LOAD_SAVE', saves.map(save => save.slug));

    nodes.forEach((node, i) => {
      this.container.addNode(node.view);

      saves[i].outlets.forEach(uid => {
        this.container.linkNodes(node.view, nodes.find(node2 => node2.uid === uid).view);
      });
    });
  } 

  public transferPower = (origin: GameNode, target: GameNode, amount: number, andRemove = false) => {
    target.powerCurrent += amount;

    if (andRemove) {
      this.container.removeNode(origin.view);
    } else {
      let link = this.container.getLink(origin.view, target.view);
      if (link) {
        link.flash();
      }
    }
  }
}

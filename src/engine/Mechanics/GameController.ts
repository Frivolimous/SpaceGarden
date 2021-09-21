import * as _ from 'lodash';
import { Config } from '../../Config';
import { Colors } from '../../data/Colors';
import { NodeConfig } from '../../data/NodeData';
import { NodeManager } from '../../services/NodeManager';
import { TextureCache } from '../../services/TextureCache';
import { FDGContainer } from '../FDG/FDGContainer';
import { FDGLink } from '../FDG/FDGLink';
import { FDGNode } from '../FDG/FDGNode';
import { GameNode, NodeSave, TransferBlock } from './Parts/GameNode';

export class GameController {
  nodes: GameNode[] = [];

  constructor(private container: FDGContainer, private nodeManager: NodeManager) {
    
  }

  public addNewNode(config: NodeConfig): FDGNode {
    let node: FDGNode = new FDGNode(TextureCache.getNodeGraphicTexture(config.shape,config.radius), config);
    
    this.container.addNode(node);
    this.addNode(node);

    return node;
  }

  public addNode = (view: FDGNode) => {
    let node = new GameNode(view, view.config, this.transferPower);
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
        node.fruitSpawn--;
        let fruitConfig = this.nodeManager.getNodeConfig(node.fruitType);
        
        let fruit = this.addNewNode(fruitConfig);

        this.container.linkNodes(node.view, fruit);
        fruit.position.set(node.view.x, node.view.y);
      }
    });

    if (ticks > 1) {
      this.onTick(ticks - 1);
    }
  }

  public saveNodes (): NodeSave[] {
    let saves: NodeSave[] = this.nodes.map(node => {
      let outlets: number[] = this.container.links.filter(l => l.origin === node.view).map(l => l.target.data.uid);

      return {
        uid: node.uid,
        slug: node.config.slug,
        powerCurrent: Math.round(node.powerCurrent),
        researchCurrent: node.config.slug === 'seedling' ? node.researchCurrent : 0,
        outlets,
        x: Math.round(node.view.x),
        y: Math.round(node.view.y),
      };
    });

    return saves;
  }

  public loadSaves (saves: NodeSave[]) {
    let nodes = saves.map(save => this.importSave(save));
    this.nodes = nodes;
    console.log('LOAD_SAVE', saves.map(save => save.slug));

    nodes.forEach((node, i) => {
      this.container.addNode(node.view);

      saves[i].outlets.forEach(uid => {
        this.container.linkNodes(node.view, nodes.find(node2 => node2.uid === uid).view);
      });
    });
  }

  public importSave(save: NodeSave): GameNode {
    let config = this.nodeManager.getNodeConfig(save.slug);

    let texture = TextureCache.getNodeGraphicTexture(config.shape, config.radius);

    let m = new GameNode(new FDGNode(texture, config), config, this.transferPower);
    m.powerCurrent = save.powerCurrent;
    m.researchCurrent = save.researchCurrent;
    
    m.uid = save.uid;
    
    m.view.position.set(save.x, save.y);

    GameNode.addUid(save.uid);
    return m;
  }

  public transferPower = (origin: GameNode, target: GameNode, block: TransferBlock) => {
    if (block.type === 'grow') {
      target.powerCurrent += block.amount;

      if (block.removeOrigin) {
        this.container.removeNode(origin.view);
      } else {
        let link = this.container.getLink(origin.view, target.view);
        if (link) {
          link.flash();
        }
      }
    } else if (block.type === 'research') {
      let link = this.container.getLink(origin.view, target.view);
      if (link) {
        link.zip(origin.view, Colors.Node.purple, block.fade, () => {
          if (target.config.slug === 'seedling') {
            target.receiveResearch(block.amount);
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
    } else if (block.type === 'fruit') {
      let link = this.container.getLink(origin.view, target.view);
      if (link) {
        link.zip(origin.view, Colors.Node.orange, block.fade, () => {
          if (target.canSpawnFruit() && Math.random() < Config.NODE.FRUIT_APPLY) {
            target.receiveFruitPower(block.amount, Colors.Node.orange);
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
}

import _ from 'lodash';
import { GOD_MODE } from '../../services/_Debug';
import { NodeSlug } from '../../data/NodeData';
import { NodeManager } from '../../services/NodeManager';
import { PlantNode } from '../nodes/PlantNode';
import { GameController } from './GameController';
import { CrawlerModel } from './Parts/CrawlerModel';

export class GameKnowledge {
  public nodes: PlantNode[] = [];
  public normalNodes: PlantNode[] = [];
  public fruitNodes: PlantNode[] = [];
  public sortedNodes: {[key in NodeSlug]: PlantNode[]} = {
    'home': [],
    'lab': [],
    'generator': [],
    'grove': [],
    'stem': [],
    'core': [],
    'seedling': [],
    'enemy-core': [],
    'enemy-box': [],
    'food': [],
    'research': [],
    'battery': [],
    'gen': [],
    'burr': [],
    'big-evil': [],
    'small-evil': [],
    'leaf': [],
  };

  public crawlerCount: number = 0;

  public totalGen: number = 0;
  public totalDrain: number = 0;
  public totalPower: number = 0;
  public totalMaxPower: number = 0;
  public seedlingPower: number = 0;
  public seedlingMaxPower: number = 0;

  private fpsCounter: number;
  private frames: number = 0;
  private fps: number = 0;

  constructor(private gameC: GameController, private manager: NodeManager) {
    gameC.onCrawlerAdded.addListener(this.crawlerAdded);
    gameC.onCrawlerRemoved.addListener(this.crawlerRemoved);
    gameC.onNodeAdded.addListener(this.nodeAdded);
    gameC.onNodeRemoved.addListener(this.nodeRemoved);
    gameC.onNodeClaimed.addListener(this.nodeClaimed);
    this.startFpsCounter();
  }

  public destroy() {
    window.clearTimeout(this.fpsCounter);
  }

  public update() {
    this.seedlingMaxPower = 0;
    let gen = 0;
    let drain = 0;
    let power = 0;
    let maxPower = 0;
    this.normalNodes.forEach(node => {
      if (node.slug === 'seedling') {
        this.seedlingPower = node.power.powerCurrent;
        this.seedlingMaxPower = node.config.powerMax;
      } else {
        power += node.power.powerCurrent;
        maxPower += node.config.powerMax;
      }
      if (node.power.powerGen > 0) {
        gen += node.power.powerGen;
      } else {
        drain -= node.power.powerGen;
      }
    });

    this.totalPower = power;
    this.totalGen = gen;
    this.totalDrain = drain;
    this.totalMaxPower = maxPower;

    this.frames++;
  }

  public numFruitsPerNode(slug: NodeSlug) {
    return this.manager.getNodeConfig(slug).maxFruits;
  }

  public toString(): string {
    let m = `<div class='node-title'>Plant Overview</div>`;
    m += `<br>Power: ${Math.round(this.totalPower)} / ${Math.round(this.totalMaxPower)}`;
    if (this.seedlingMaxPower) {
      m += `<br>Seedling Power: ${Math.round(this.seedlingPower)} / ${Math.round(this.seedlingMaxPower)}`;
    }
    m += `<br>Gen: ${(this.totalGen * 60).toFixed(0)}/s, Drain: ${(this.totalDrain * 60).toFixed(0)}/s`;
    m += `<br><br>Crawlers: ${this.crawlerCount}`;
    m += `<br>Nodes: ${this.normalNodes.length}`;

    if (GOD_MODE) {
      m += '<br> --- <br> DEV STUFF <br><br>';
      let keys = Object.keys(this.sortedNodes);
      keys.forEach(key => {
        let value = this.sortedNodes[key as NodeSlug].length;
        if (value !== 0) {
          m += `<br>${key}: ${value}`;
        }
      });
      m += `<br>FPS: ${this.fps}`;
      // if (this.crawlerCount > 0) {
      //   m += `<br>Claimed`;
      //   keys = Object.keys(this.sortedNodes);
      //   keys.forEach(key => {
      //     let value = this.sortedNodes[key as NodeSlug].filter(node => node.claimedBy).length;
      //     if (value !== 0) {
      //       m += `<br>${key}: ${value}`;
      //     }
      //   });
      // }
    }

    return m;
  }

  private crawlerAdded = (crawler: CrawlerModel) => {
    this.crawlerCount++;
  }

  private crawlerRemoved = (crawler: CrawlerModel) => {
    this.crawlerCount--;
  }

  private nodeAdded = (node: PlantNode) => {
    this.sortedNodes[node.slug].push(node);
    this.nodes.push(node);
    if (node.isFruit()) {
      this.fruitNodes.push(node);
    } else {
      this.normalNodes.push(node);
    }
  }

  private nodeRemoved = (node: PlantNode) => {
    _.pull(this.sortedNodes[node.slug], node);
    _.pull(this.nodes, node);
    if (node.isFruit()) {
      _.pull(this.fruitNodes, node);
    } else {
      _.pull(this.normalNodes, node);
    }
  }

  private nodeClaimed = (e: {claim: boolean, node: PlantNode, claimer: CrawlerModel}) => {
    // if (e.claim) {
    //   this.nodeClaims[e.node.slug]++;
    // } else {
    //   this.nodeClaims[e.node.slug]--;
    // }
  }

  private startFpsCounter() {
    this.fpsCounter = window.setTimeout(() => {
      this.fps = this.frames;
      this.frames = 0;
      this.startFpsCounter();
    }, 1000);
  }
}

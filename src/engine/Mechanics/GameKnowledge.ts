import _ from 'lodash';
import { NodeSlug } from '../../data/NodeData';
import { NodeManager } from '../../services/NodeManager';
import { FDGContainer } from '../FDG/FDGContainer';
import { PlantNode } from '../nodes/PlantNode';
import { GameController } from './GameController';
import { CrawlerModel } from './Parts/CrawlerModel';

export class GameKnowledge {
  public nodes: PlantNode[] = [];
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

  constructor(private gameC: GameController, private manager: NodeManager) {
    gameC.onCrawlerAdded.addListener(this.crawlerAdded);
    gameC.onCrawlerRemoved.addListener(this.crawlerRemoved);
    gameC.onNodeAdded.addListener(this.nodeAdded);
    gameC.onNodeRemoved.addListener(this.nodeRemoved);
    gameC.onNodeClaimed.addListener(this.nodeClaimed);
  }

  public update() {
    let gen = 0;
    let drain = 0;
    let power = 0;
    let maxPower = 0;
    this.nodes.forEach(node => {
      power += node.power.powerCurrent;
      maxPower += node.config.powerMax;
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
  }

  public numFruitsPerNode(slug: NodeSlug) {
    return this.manager.getNodeConfig(slug).maxFruits;
  }

  public toString(): string {
    let m = `<div class='node-title'>Knowledge</div>`;
    m += `<br>Power: ${Math.round(this.totalPower)} / ${Math.round(this.totalMaxPower)}
          <br>Gen: ${this.totalGen.toFixed(2)}, Drain: ${this.totalDrain.toFixed(2)}`;
    m += `<br><br>Crawlers: ${this.crawlerCount}<br>`;

    let keys = Object.keys(this.sortedNodes);
    keys.forEach(key => {
      let value = this.sortedNodes[key as NodeSlug].length;
      if (value !== 0) {
        m += `<br>${key}: ${value}`;
      }
    });
    if (this.crawlerCount > 0) {
      m += `<br>Claimed`;
      keys = Object.keys(this.sortedNodes);
      keys.forEach(key => {
        let value = this.sortedNodes[key as NodeSlug].filter(node => node.claimedBy).length;
        if (value !== 0) {
          m += `<br>${key}: ${value}`;
        }
      });
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
  }

  private nodeRemoved = (node: PlantNode) => {
    _.pull(this.sortedNodes[node.slug], node);
    _.pull(this.nodes, node);
  }

  private nodeClaimed = (e: {claim: boolean, node: PlantNode, claimer: CrawlerModel}) => {
    // if (e.claim) {
    //   this.nodeClaims[e.node.slug]++;
    // } else {
    //   this.nodeClaims[e.node.slug]--;
    // }
  }
}

import _ from 'lodash';
import { NodeSlug } from '../../data/NodeData';
import { NodeManager } from '../../services/NodeManager';
import { FDGContainer } from '../FDG/FDGContainer';
import { PlantNode } from '../nodes/PlantNode';
import { GameController } from './GameController';
import { CrawlerModel } from './Parts/CrawlerModel';

export class GameKnowledge {
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

  constructor(private gameC: GameController, private container: FDGContainer, private manager: NodeManager) {
    gameC.onCrawlerAdded.addListener(this.crawlerAdded);
    gameC.onCrawlerRemoved.addListener(this.crawlerRemoved);
    gameC.onNodeAdded.addListener(this.nodeAdded);
    gameC.onNodeRemoved.addListener(this.nodeRemoved);
    gameC.onNodeClaimed.addListener(this.nodeClaimed);
  }

  public numFruitsPerNode(slug: NodeSlug) {
    return this.manager.getNodeConfig(slug).maxFruits;
  }

  public toString(): string {
    let m = `<div class='node-title'>Knowledge</div>`;
    m += `<br>Crawlers: ${this.crawlerCount}`;

    let keys = Object.keys(this.sortedNodes);
    keys.forEach(key => {
      let value = this.sortedNodes[key as NodeSlug].length;
      if (value !== 0) {
        m += `<br>${key}: ${value}`;
      }
    });
    m += `<br>Claimed`;
    keys = Object.keys(this.sortedNodes);
    keys.forEach(key => {
      let value = this.sortedNodes[key as NodeSlug].filter(node => node.claimedBy).length;
      if (value !== 0) {
        m += `<br>${key}: ${value}`;
      }
    });

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
  }

  private nodeRemoved = (node: PlantNode) => {
    _.pull(this.sortedNodes[node.slug], node);
  }

  private nodeClaimed = (e: {claim: boolean, node: PlantNode, claimer: CrawlerModel}) => {
    // if (e.claim) {
    //   this.nodeClaims[e.node.slug]++;
    // } else {
    //   this.nodeClaims[e.node.slug]--;
    // }
  }
}

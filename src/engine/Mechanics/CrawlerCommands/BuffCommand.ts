import { BaseCommand } from './_BaseCommand';
import { CommandType } from './_CommandTypes';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { JMTween, JMEasing } from '../../../JMGE/JMTween';
import _ from 'lodash';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { ICommandConfig } from '../../../data/CrawlerData';
import { GameKnowledge } from '../GameKnowledge';

export class BuffCommand extends BaseCommand {
  // private state: 'buff' | 'walk' | 'return';
  // private walking = false;
  // private animating = false;

  private hopping: boolean;

  private danceTicks: number;

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.BUFF;
    this.color = Colors.Node.white;
  }

  public initialize() {
    this.isComplete = false;

    this.startBuff();
  }

  public genPriority(): number {
    let node = this.crawler.cLoc;
    let numHere = this.knowledge.sortedCrawlers.crawler.filter(other => other.cLoc === node && !other.hasBuff()).length;
    return 1.2 - numHere * 0.1 - (this.crawler.preference === this.type ? 0.15 : 0);
  }

  public update() {
    if (this.isComplete) return;

    this.standStill();
  }

  public startBuff() {
    let crawlers = this.knowledge.sortedCrawlers.crawler.filter(other => other.cLoc === this.crawler.cLoc);

    crawlers.forEach(crawler => crawler.addBuff(10));
  }
}

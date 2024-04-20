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
  private state: 'buff' | 'end';
  // private walking = false;
  // private animating = false;

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
    let numHere = this.knowledge.sortedCrawlers.crawler.filter(this.canBuff).length;

    if (numHere === 0) return 2;
    return 1 - numHere * 0.1 - (this.crawler.preference === this.type ? 0.15 : 0);
  }

  public update() {
    if (this.isComplete) return;

    this.crawler.view.vibrate(this.crawler.cLoc.view);
  }

  public startBuff() {
    let crawlers = this.knowledge.sortedCrawlers.crawler.filter(this.canBuff);
    let target = _.sample(crawlers);

    target.addBuff(this.config.buffMult, this.config.buffTime);

    new JMTween({per: 0}, 1000).to({per: 1}).start()
      .onComplete(() => this.isComplete = true);
  }

  public canBuff = (crawler: CrawlerModel) => {
    return (!crawler.isBuffed && 
      (crawler.cLoc === this.crawler.cLoc || crawler.currentCommand.nextLoc === this.crawler.cLoc)
    );
  }
}

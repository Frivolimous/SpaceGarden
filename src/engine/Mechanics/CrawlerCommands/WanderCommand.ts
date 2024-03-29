import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { GameKnowledge } from '../GameKnowledge';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { ICommandConfig } from '../../../data/CrawlerData';
import { BaseCommand } from './_BaseCommand';
import { CommandType } from './_CommandTypes';

export class WanderCommand extends BaseCommand {
  private repeatCount: number;

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.WANDER;
    this.color = Colors.Node.green;
  }

  public initialize() {
    this.isComplete = false;
    this.repeatCount = Math.ceil(Math.random() * this.config.wanderRepeat);
    let start = this.crawler.cLoc;
    this.startPath(node => node !== start, this.finishCommand, this.cancelPath);
  }

  public genPriority(): number {
    return 1 + Math.random() * 0.15 - (this.crawler.preference === this.type ? 0.10 : 0);
  }

  public update() {
    if (this.isComplete) return;

    this.updatePath(0.5);
  }

  private cancelPath = () => {
    this.isComplete = true;
    this.crawler.setCommand(CommandType.FRUSTRATED);
    this.crawler.frustratedBy = CommandType[this.type];
  }

  private finishCommand = () => {
    this.repeatCount--;
    if (this.repeatCount > 0) {
      let start = this.crawler.cLoc;
      this.startPath(node => node !== start, this.finishCommand, this.cancelPath);
      } else {
      this.isComplete = true;
    }
  }
}

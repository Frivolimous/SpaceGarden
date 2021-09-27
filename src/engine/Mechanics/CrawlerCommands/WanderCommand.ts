import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { GameKnowledge } from '../GameKnowledge';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class WanderCommand extends BaseCommand {
  private repeatCount: number;

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.WANDER;
    this.color = Colors.Node.green;
  }

  public initialize() {
    this.isComplete = false;
    this.repeatCount = this.config.wanderRepeat;
    let start = this.crawler.cLoc;
    this.startPath(node => node !== start, this.finishCommand, this.cancelPath);
  }

  public genPriority(): number {
    return 0.9 + Math.random() * 0.3 - (this.crawler.preference === this.type ? 0.15 : 0);
  }

  public update() {
    if (this.isComplete) return;

    this.updatePath();
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

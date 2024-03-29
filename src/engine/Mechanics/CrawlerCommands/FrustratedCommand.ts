import { BaseCommand } from './_BaseCommand';
import { CommandType } from './_CommandTypes';
import { Colors } from '../../../data/Colors';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { ICommandConfig } from '../../../data/CrawlerData';
import { GameKnowledge } from '../GameKnowledge';

export class FrustratedCommand extends BaseCommand {
  private repeatCount: number;

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.FRUSTRATED;
    this.color = Colors.Node.red;
  }

  public genPriority(): number {
    return 20;
  }

  public initialize() {
    this.isComplete = false;
    this.repeatCount = this.config.frustratedRepeat;

    this.startIdleLoop();
  }

  public update() {
    if (this.isComplete) return;

    this.updateIdle(this.idleComplete);
  }

  public idleComplete = () => {
    this.repeatCount--;
    if (this.repeatCount > 0) {
      this.startIdleLoop();
    } else {
      this.isComplete = true;
    }
  }
}

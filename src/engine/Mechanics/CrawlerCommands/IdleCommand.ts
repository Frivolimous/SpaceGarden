import { BaseCommand } from './_BaseCommand';
import { CommandType } from './_CommandTypes';
import { Colors } from '../../../data/Colors';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { GameKnowledge } from '../GameKnowledge';
import { ICommandConfig } from '../../../data/CrawlerData';

export class IdleCommand extends BaseCommand {
  private repeatCount: number;

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.IDLE;
    this.color = Colors.Node.green;
  }

  public genPriority(): number {
    return 1 + Math.random() * 0.15;
  }

  public initialize() {
    this.isComplete = false;
    this.repeatCount = Math.ceil(Math.random()*this.config.idleRepeat);

    this.startIdleLoop();
  }

  public update() {
    if (this.isComplete) return;

    this.updateIdle(this.idleComplete, 0.5);
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

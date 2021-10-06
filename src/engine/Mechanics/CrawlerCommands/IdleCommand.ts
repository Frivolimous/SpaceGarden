import { BaseCommand, CommandType } from './_BaseCommand';
import { Colors } from '../../../data/Colors';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { GameKnowledge } from '../GameKnowledge';

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
    this.repeatCount = this.config.idleRepeat;

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

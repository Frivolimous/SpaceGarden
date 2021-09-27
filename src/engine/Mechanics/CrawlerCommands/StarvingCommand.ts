import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { GameKnowledge } from '../GameKnowledge';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class StarvingCommand extends BaseCommand {
  private state: 'idle' | 'walk';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.STARVING;
    this.color = Colors.Node.darkblue;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.makesFood, this.finishPath, this.cancelPath);
  }

  public genPriority(): number {
    return 20;
  }

  public update() {
    if (this.isComplete) return;
    if (this.state === 'idle') {
      this.updateIdle(this.idleComplete);
    } else {
      this.updatePath();
    }
  }

  private makesFood(node: PlantNode): boolean {
    return (node.power.fruitType === 'food');
  }

  private finishPath = () => {
    if (this.currentPath) {
      this.isComplete = true;
    } else {
      this.state = 'idle';
      this.startIdleLoop();
    }
  }

  private cancelPath = () => {
    this.state = 'idle';
    this.startIdleLoop();
  }

  private idleComplete = () => {
    this.isComplete = true;
  }
}

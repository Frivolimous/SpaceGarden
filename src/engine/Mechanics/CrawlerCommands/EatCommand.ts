import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { JMTween } from '../../../JMGE/JMTween';
import { PlantNode } from '../../nodes/PlantNode';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class EatCommand extends BaseCommand {
  private state: 'eat' | 'walk';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig) {
    super(crawler, config);
    this.type = CommandType.EAT;
    this.color = Colors.Node.blue;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.hasFood, this.eatHere, this.cancelPath);
  }

  public genPriority(): number {
    if (this.crawler.health < 0.5) return this.crawler.health / 2;
    return 5;
  }

  public update() {
    if (this.isComplete) return;
    if (this.state === 'eat') {
      this.standStill();
      this.dragFruit();
    } else {
      this.updatePath();
    }
  }

  private hasFood(node: PlantNode): boolean {
    return (node.power.fruitType === 'food' && node.hasHarvestableFruit());
  }

  private eatHere = () => {
    this.state = 'eat';
    let fruit = this.crawler.cLoc.harvestFruit();

    this.grabFruit(fruit, () => {
      this.deliverFruit(() => {
        this.isComplete = true;
        this.crawler.health += fruit.power.powerCurrent * this.config.eatRatio;
      });
    });
  }

  private cancelPath = () => {
    this.isComplete = true;
    this.crawler.setCommand(CommandType.STARVING);
  }
}

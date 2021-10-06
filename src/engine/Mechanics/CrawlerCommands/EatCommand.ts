import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { JMTween } from '../../../JMGE/JMTween';
import { PlantNode } from '../../nodes/PlantNode';
import { GameKnowledge } from '../GameKnowledge';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class EatCommand extends BaseCommand {
  private state: 'eat' | 'walk';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);
    this.type = CommandType.EAT;
    this.color = Colors.Node.blue;
  }

  public initialize() {
    this.isComplete = false;
    this.fruit = null;

    this.state = 'walk';
    this.startPath(this.hasFood, this.eatHere, this.cancelPath, true);
  }

  public genPriority(): number {
    if (this.crawler.health < 0.5) return this.crawler.health / 2;
    return 20;
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
    let fruit = this.crawler.claimedNode || this.crawler.cLoc.harvestFruit();

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

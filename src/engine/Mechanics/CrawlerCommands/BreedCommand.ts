import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class BreedCommand extends BaseCommand {
  private state: 'eat' | 'walk';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig) {
    super(crawler, config);
    this.type = CommandType.BREED;
    this.color = Colors.Node.lightblue;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.hasFood, this.eatHere, this.cancelPath);
  }

  public genPriority(): number {
    if (this.crawler.health > this.config.breedHealthMin && this.crawler.cLoc.findNode(this.hasFood)) {
      return this.crawler.health - Math.random() * 0.25 - (this.crawler.preference === this.type ? this.crawler.preferenceAmount : 0);
    }
    return 10;
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

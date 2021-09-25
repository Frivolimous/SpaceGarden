import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class BreedCommand extends BaseCommand {
  constructor(crawler: CrawlerModel) {
    super(crawler);
    this.type = CommandType.BREED;
    this.color = Colors.Node.blue;
  }

  public initialize() {
    this.isComplete = true;
    this.crawler.setCommand(CommandType.EAT);
  }

  public genPriority(): number {
    if (this.crawler.health > 0.9 && this.crawler.cLoc.findNode(this.hasFood)) {
      return this.crawler.health - Math.random() * 0.25 - (this.crawler.preference === this.type ? this.crawler.preferenceAmount : 0);
    }
    return 10;
  }

  public update() {

  }

  private hasFood(node: PlantNode): boolean {
    return (node.power.fruitType === 'food' && node.hasHarvestableFruit());
  }
}

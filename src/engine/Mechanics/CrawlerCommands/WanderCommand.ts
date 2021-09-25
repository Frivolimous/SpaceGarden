import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class WanderCommand extends BaseCommand {
  private magnitude: number;
  private nextLoc: PlantNode;

  constructor(crawler: CrawlerModel) {
    super(crawler);

    this.type = CommandType.WANDER;
    this.color = Colors.Node.green;
  }

  public initialize() {
    this.isComplete = false;

    if (this.crawler.cLoc.outlets.length === 0) {
      this.crawler.setCommand(CommandType.FRUSTRATED);
    } else {
      this.nextLoc = _.sample(this.crawler.cLoc.outlets);
      this.magnitude = 0;
    }
  }

  public genPriority(): number {
    return 0.9 + Math.random() * 0.3 - (this.crawler.preference === this.type ? this.crawler.preferenceAmount : 0);
  }

  public update() {
    if (this.isComplete) return;

    this.magnitude += this.crawler.speed;
    if (this.magnitude > 1) {
      this.magnitude = 0;
      this.crawler.cLoc = this.nextLoc;
      this.nextLoc = null;
      this.isComplete = true;
    } else {
      this.crawler.view.x = this.crawler.cLoc.view.x + (this.nextLoc.view.x - this.crawler.cLoc.view.x) * this.magnitude;
      this.crawler.view.y = this.crawler.cLoc.view.y + (this.nextLoc.view.y - this.crawler.cLoc.view.y) * this.magnitude;
    }
  }
}

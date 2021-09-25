import { BaseCommand, CommandType } from './_BaseCommand';
import { Colors } from '../../../data/Colors';
import { CrawlerModel } from '../Parts/CrawlerModel';

export class FrustratedCommand extends BaseCommand {
  private magnitude: number;
  private angle: number;
  private targetMagnitude: number;

  private return = false;

  constructor(crawler: CrawlerModel) {
    super(crawler);

    this.type = CommandType.FRUSTRATED;
    this.color = Colors.Node.red;
  }

  public genPriority(): number {
    return 20;
  }

  public initialize() {
    this.isComplete = false;
    this.return = false;

    this.magnitude = 0;
    this.targetMagnitude = 0.3 + Math.random() * 0.7;
    this.angle = -Math.PI + 2 * Math.PI * Math.random();
  }

  public update() {
    if (this.isComplete) return;

    this.magnitude += this.crawler.speed / this.crawler.cLoc.view.radius * 50 * (this.return ? -1 : 1);
    if (this.magnitude > this.targetMagnitude) {
      this.magnitude = this.targetMagnitude;
      this.return = true;
    } else if (this.magnitude < 0) {
      this.isComplete = true;
      return;
    }

    this.crawler.view.x = this.crawler.cLoc.view.x + this.magnitude * this.crawler.cLoc.view.radius * Math.cos(this.angle);
    this.crawler.view.y = this.crawler.cLoc.view.y + this.magnitude * this.crawler.cLoc.view.radius * Math.sin(this.angle);
  }
}

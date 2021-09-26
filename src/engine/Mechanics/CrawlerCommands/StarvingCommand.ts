import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { JMTween } from '../../../JMGE/JMTween';
import { PlantNode } from '../../nodes/PlantNode';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class StarvingCommand extends BaseCommand {
  private magnitude: number;
  private angle: number;
  private targetMagnitude: number;

  private nextLoc: PlantNode;

  private path: PlantNode[];

  private state: 'idle' | 'return' | 'walk';

  constructor(crawler: CrawlerModel) {
    super(crawler);
    this.type = CommandType.STARVING;
    this.color = Colors.Node.darkblue;
  }

  public initialize() {
    this.isComplete = false;

    this.path = this.crawler.findPath(this.hasFood);
    if (!this.path || this.path.length <= 1) {
      this.state = 'idle';
      this.magnitude = 0;
      this.targetMagnitude = 0.3 + Math.random() * 0.7;
      this.angle = -Math.PI + 2 * Math.PI * Math.random();
    } else {
      this.state = 'walk';
      this.path.shift();
      this.startNextStep();
    }
  }

  public genPriority(): number {
    return 20;
  }

  public update() {
    if (this.isComplete) return;
    if (this.state === 'idle') {
      this.magnitude += this.crawler.speed / this.crawler.cLoc.view.radius * 50;
      if (this.magnitude > this.targetMagnitude) {
        this.magnitude = this.targetMagnitude;
        this.state = 'return';
      }
      this.crawler.view.x = this.crawler.cLoc.view.x + this.magnitude * this.crawler.cLoc.view.radius * Math.cos(this.angle);
      this.crawler.view.y = this.crawler.cLoc.view.y + this.magnitude * this.crawler.cLoc.view.radius * Math.sin(this.angle);
    } else if (this.state === 'return') {
      this.magnitude -= this.crawler.speed / this.crawler.cLoc.view.radius * 50;
      if (this.magnitude < 0) {
        this.isComplete = true;
      } else {
        this.crawler.view.x = this.crawler.cLoc.view.x + this.magnitude * this.crawler.cLoc.view.radius * Math.cos(this.angle);
        this.crawler.view.y = this.crawler.cLoc.view.y + this.magnitude * this.crawler.cLoc.view.radius * Math.sin(this.angle);
      }
    } else {
      this.magnitude += this.crawler.speed;
      if (this.magnitude > 1) {
        this.magnitude = 0;
        this.crawler.cLoc = this.nextLoc;
        this.startNextStep();
      } else {
        this.crawler.view.x = this.crawler.cLoc.view.x + (this.nextLoc.view.x - this.crawler.cLoc.view.x) * this.magnitude;
        this.crawler.view.y = this.crawler.cLoc.view.y + (this.nextLoc.view.y - this.crawler.cLoc.view.y) * this.magnitude;
      }
    }
  }

  private hasFood(node: PlantNode): boolean {
    return (node.power.fruitType === 'food');
  }

  private startNextStep() {
    if (this.path && this.path.length > 0) {
      this.nextLoc = this.path.shift();
      this.magnitude = 0;
    } else {
      this.isComplete = true;
    }
  }
}

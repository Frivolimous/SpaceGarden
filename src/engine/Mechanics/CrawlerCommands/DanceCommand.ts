import { BaseCommand, CommandType } from './_BaseCommand';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { JMTween, JMEasing } from '../../../JMGE/JMTween';
import _ from 'lodash';
import { CrawlerModel } from '../Parts/CrawlerModel';

export class DanceCommand extends BaseCommand {
  private magnitude: number;
  private angle: number;

  private nextLoc: PlantNode;

  private path: PlantNode[];

  private state: 'dance' | 'walk' | 'return';

  private hopping: boolean;

  private danceTicks: number;

  constructor(crawler: CrawlerModel) {
    super(crawler);

    this.type = CommandType.DANCE;
    this.color = Colors.Node.yellow;
  }

  public initialize() {
    this.isComplete = false;
    if (this.isDanceable(this.crawler.cLoc)) {
      this.danceHere();
    } else {
      this.state = 'walk';
      this.path = this.crawler.findPath(this.isDanceable);
      if (!this.path || this.path.length === 0) {
        this.isComplete = true;
        this.crawler.setCommand(CommandType.FRUSTRATED);
      } else {
        this.path.shift();
        this.startNextStep();
      }
    }
  }

  public genPriority(): number {
    let core = this.crawler.cLoc.findCore();
    if (!core) return 10;
    return core.power.powerPercent - Math.random() * 0.25 - (this.crawler.preference === this.type ? this.crawler.preferenceAmount : 0);
  }

  public update() {
    if (this.isComplete) return;
    if (this.state === 'dance') {
      if (this.danceTicks <= 0 && !this.hopping) {
        this.startReturn();
      } else {
        this.danceTicks--;

        if (!this.hopping) {
          this.hop();
        }

        let fruit = _.sample(this.crawler.cLoc.fruits);
        if (fruit) {
          fruit.power.powerCurrent += 5;
        }
      }

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

  public isDanceable(node: PlantNode): boolean {
    return node.slug === 'core' && node.fruits.length > 0;
  }

  public danceHere = () => {
    this.state = 'dance';
    this.danceTicks = 500;
    this.hop();
  }

  public startReturn = () => {
    this.state = 'return';
    let dX = this.crawler.view.x - this.crawler.cLoc.view.x;
    let dY = this.crawler.view.y - this.crawler.cLoc.view.y;
    this.magnitude = Math.sqrt(dX * dX + dY * dY) / 50;
    this.angle = Math.atan2(dY, dX);
  }

  public hop = () => {
    this.hopping = true;

    let node = this.crawler.cLoc;
    let targetX = node.view.x + (- 3 / 4 + 6 / 4 * Math.random()) * node.view.radius;
    let targetY = node.view.y + (- 3 / 4 + 6 / 4 * Math.random()) * node.view.radius;

    let apexY = (node.view.y + targetY) / 2 - 15;
    new JMTween(this.crawler.view, 500).to({y: apexY}).easing(JMEasing.Quadratic.Out).start()
    .chain(this.crawler.view, 500).to({y: targetY}).easing(JMEasing.Quadratic.In);
    new JMTween(this.crawler.view, 1000).to({x: targetX}).easing(JMEasing.Quadratic.InOut).start().onComplete(() => this.hopping = false);
  }

  private startNextStep() {
    if (this.path && this.path.length > 0) {
      this.nextLoc = this.path.shift();
      this.magnitude = 0;
    } else {
      if (this.isDanceable(this.crawler.cLoc)) {
        this.danceHere();
      } else {
        this.isComplete = true;
      }
    }
  }
}

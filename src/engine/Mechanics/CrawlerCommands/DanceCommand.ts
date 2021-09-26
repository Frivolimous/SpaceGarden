import { BaseCommand, CommandType } from './_BaseCommand';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { JMTween, JMEasing } from '../../../JMGE/JMTween';
import _ from 'lodash';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';

export class DanceCommand extends BaseCommand {
  private state: 'dance' | 'walk' | 'return';

  private hopping: boolean;

  private danceTicks: number;

  constructor(crawler: CrawlerModel, protected config: ICommandConfig) {
    super(crawler, config);

    this.type = CommandType.DANCE;
    this.color = Colors.Node.yellow;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.isDanceable, this.danceHere, this.cancelPath);
  }

  public genPriority(): number {
    let core = this.crawler.cLoc.findCore();
    if (!core) return 10;
    return core.power.powerPercent - Math.random() * 0.1 - (this.crawler.preference === this.type ? this.crawler.preferenceAmount : 0);
  }

  public update() {
    if (this.isComplete) return;
    if (this.state === 'dance') {
      this.updateDance();
    } else if (this.state === 'return') {
      this.updateIdle(this.returnComplete);
    } else {
      this.updatePath();
    }
  }

  private updateDance() {
    if (this.danceTicks <= 0 && !this.hopping) {
      this.state = 'return';
      this.startIdleReturn();
    } else {
      this.danceTicks--;

      if (!this.hopping) {
        this.hop();
      }

      let fruit = _.sample(this.crawler.cLoc.fruits);
      if (fruit) {
        fruit.power.powerCurrent += this.config.danceGen;
      }
    }
  }

  private isDanceable(node: PlantNode): boolean {
    return node.slug === 'core' && node.fruits.length > 0;
  }

  private danceHere = () => {
    this.state = 'dance';
    this.danceTicks = this.config.danceTicks;
    this.hop();
  }

  private hop = () => {
    this.hopping = true;

    let node = this.crawler.cLoc;
    let targetX = node.view.x + (- 3 / 4 + 6 / 4 * Math.random()) * node.view.radius;
    let targetY = node.view.y + (- 3 / 4 + 6 / 4 * Math.random()) * node.view.radius;

    let apexY = (node.view.y + targetY) / 2 - 15;
    new JMTween(this.crawler.view, 500).to({y: apexY}).easing(JMEasing.Quadratic.Out).start()
    .chain(this.crawler.view, 500).to({y: targetY}).easing(JMEasing.Quadratic.In);
    new JMTween(this.crawler.view, 1000).to({x: targetX}).easing(JMEasing.Quadratic.InOut).start().onComplete(() => this.hopping = false);
  }

  private cancelPath = () => {
    this.isComplete = true;
    this.crawler.setCommand(CommandType.FRUSTRATED);
  }

  private returnComplete = () => {
    this.isComplete = true;
  }
}

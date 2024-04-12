import { BaseCommand } from './_BaseCommand';
import { CommandType } from './_CommandTypes';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { JMTween, JMEasing } from '../../../JMGE/JMTween';
import _ from 'lodash';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { ICommandConfig } from '../../../data/CrawlerData';
import { GameKnowledge } from '../GameKnowledge';

export class DanceCommand extends BaseCommand {
  private state: 'dance' | 'walk' | 'return';
  // private walking = false;
  // private animating = false;

  private hopping: boolean;

  private danceTicks: number;

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.DANCE;
    this.color = Colors.Node.lightyellow;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.isDanceable, this.danceHere, this.cancelPath);
  }

  public genPriority(): number {
    let core = this.crawler.cLoc.findCore();
    let fruit = _.sample(core.fruits);
    if (!core) return 20;
    return 0.2 + 0.85 * (fruit ? fruit.power.powerPercent : core.power.powerPercent) - (this.crawler.preference === this.type ? 0.15 : 0);
  }

  public update() {
    if (this.isComplete) return;
    // if (this.walking) {
    //   this.updateIdle(() => this.walking = false);
    // }
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
    // if (this.danceTicks <= 0 && !this.walking) {
      this.state = 'return';
      this.startIdleReturn();
    } else {
      this.danceTicks--;

      // if (!this.walking) {
      //   this.startWalk();
      // }
      // if (!this.animating) {
      //   this.startAnimate();
      // }

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

  // private startWalk = () => {
  //   this.walking = true;
  //   this.startIdleLoop();
  // }

  // private startAnimate = () => {
  //   this.animating = true;
  //   if (Math.random() < 0.5) {
  //     new JMTween(this.crawler.view.sprite.scale, 500).to({x: 0.5}).easing(JMEasing.Sinusoidal.Out).start().yoyo(true).onComplete(() => this.animating = false);
  //   } else {
  //     new JMTween(this.crawler.view.sprite.scale, 500).to({y: 0.25}).easing(JMEasing.Sinusoidal.Out).start().yoyo(true).onComplete(() => this.animating = false);
  //   }
  // }

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
    this.crawler.frustratedBy = CommandType[this.type];
  }

  private returnComplete = () => {
    this.isComplete = true;
  }
}

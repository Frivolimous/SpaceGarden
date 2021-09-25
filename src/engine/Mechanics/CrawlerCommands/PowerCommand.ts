import { BaseCommand, CommandType } from './_BaseCommand';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { JMTween, JMEasing } from '../../../JMGE/JMTween';
import _ from 'lodash';
import { CrawlerModel } from '../Parts/CrawlerModel';

export class PowerCommand extends BaseCommand {
  private FRUIT_SPEED = 0.95;
  private POWER_RATIO = 2;

  private magnitude: number;
  private nextLoc: PlantNode;

  private currentPath: {
    condition: (node: PlantNode) => boolean,
    onComplete: () => void,
    path?: PlantNode[],
  };

  private state: 'walk' | 'harvest' | 'carry' | 'deliver';

  private fruit: PlantNode;

  constructor(crawler: CrawlerModel) {
    super(crawler);

    this.type = CommandType.POWER;
    this.color = Colors.Node.yellow;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.hasPower, this.harvestHere);
  }

  // lowest is better
  public genPriority(): number {
    if (!this.crawler.cLoc.findNode(this.hasPower)) {
      return 10;
    }
    let seedling = this.crawler.cLoc.findNode(node => node.slug === 'seedling');
    let closest = this.crawler.cLoc.findNode(node => node.power.powerPercent < 0.5);

    return Math.min(seedling ? seedling.power.powerPercent : 2, closest ? closest.power.powerPercent * 2 : 2) - (this.crawler.preference === this.type ? this.crawler.preferenceAmount : 0);
  }

  public update() {
    if (this.isComplete) return;
    if (this.fruit) {
      this.fruit.view.x = this.crawler.view.x + (this.fruit.view.x - this.crawler.view.x) * this.FRUIT_SPEED;
      this.fruit.view.y = this.crawler.view.y + (this.fruit.view.y - this.crawler.view.y) * this.FRUIT_SPEED;
    }
    if (this.state === 'harvest' || this.state === 'deliver') {
      this.crawler.view.x = this.crawler.cLoc.view.x;
      this.crawler.view.y = this.crawler.cLoc.view.y;
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

  public harvestHere = () => {
    this.state = 'harvest';
    let fruit = this.crawler.cLoc.harvestFruit();
    fruit.flagUnlink = true;
    fruit.active = false;
    fruit.physics.fixed = true;

    this.fruit = fruit;

    window.setTimeout(() => {
      this.state = 'carry';
      this.startPath(this.isDeliverable, this.deliverHere);
    }, 1000);
  }

  public deliverHere = () => {
    this.state = 'deliver';
    let fruit = this.fruit;
    this.fruit = null;
    new JMTween(fruit.view.scale, 500).easing(JMEasing.Back.In).to({x: 0, y: 0}).start().onComplete(() => {
      this.isComplete = true;
      fruit.flagDestroy = true;
      this.crawler.cLoc.power.powerCurrent += (fruit.power.powerCurrent * this.POWER_RATIO);
    });
  }

  public hasPower(node: PlantNode): boolean {
    return node.power.fruitType === 'gen' && node.hasHarvestableFruit();
  }

  public isDeliverable(node: PlantNode): boolean {
    return node.slug !== 'stem' && node.power.powerPercent < 0.7 || node.slug === 'seedling';
  }

  protected startPath(condition: (node: PlantNode) => boolean, onComplete: () => void) {
    if (condition(this.crawler.cLoc)) {
      onComplete();
    } else {
      this.currentPath = {condition, onComplete};
      let path = this.crawler.findPath(condition);
      if (!path || path.length === 0) {
        this.isComplete = true;
        this.crawler.setCommand(CommandType.FRUSTRATED);
      } else {
        path.shift();
        this.currentPath.path = path;
        this.startNextStep();
      }
    }
  }

  private startNextStep = () => {
    if (this.currentPath && this.currentPath.path.length > 0) {
      this.nextLoc = this.currentPath.path.shift();
      this.magnitude = 0;
    } else {
      if (this.currentPath.condition(this.crawler.cLoc)) {
        let path = this.currentPath;
        this.currentPath = null;
        path.onComplete();
      } else {
        if (this.fruit) {
          this.fruit.flagDestroy = true;
          this.fruit = null;
        }
        this.isComplete = true;
      }
    }
  }
}

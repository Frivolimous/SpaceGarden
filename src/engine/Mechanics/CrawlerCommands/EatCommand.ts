import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { JMTween } from '../../../JMGE/JMTween';
import { PlantNode } from '../../nodes/PlantNode';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class EatCommand extends BaseCommand {
  private EAT_RATIO = 0.0075;

  private magnitude: number;
  private nextLoc: PlantNode;

  private path: PlantNode[];

  private state: 'eat' | 'walk';

  constructor(crawler: CrawlerModel) {
    super(crawler);
    this.type = CommandType.EAT;
    this.color = Colors.Node.blue;
  }

  public initialize() {
    this.isComplete = false;

    if (this.hasFood(this.crawler.cLoc)) {
      this.eatHere();
    } else {
      this.path = this.crawler.findPath(this.hasFood);
      if (!this.path || this.path.length === 0) {
        this.isComplete = true;
        this.crawler.setCommand(CommandType.STARVING);
      } else {
        this.state = 'walk';
        this.path.shift();
        this.startNextStep();
      }
    }
  }

  public genPriority(): number {
    if (this.crawler.health < 0.5) return this.crawler.health / 2;
    return 5;
  }

  public update() {
    if (this.isComplete) return;
    if (this.state === 'eat') {
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

  public eatHere() {
    this.state = 'eat';
    let fruit = this.crawler.cLoc.harvestFruit();

    fruit.flagUnlink = true;
    fruit.active = false;
    fruit.physics.fixed = true;

    new JMTween(fruit.view.scale, 300).to({x: 0, y: 0}).start();
    new JMTween(fruit.view, 300).to({x: this.crawler.view.x, y: this.crawler.view.y}).start().onComplete(() => {
      fruit.flagDestroy = true;
      this.crawler.health += fruit.power.powerCurrent * this.EAT_RATIO;
      this.isComplete = true;
    });
  }

  private hasFood(node: PlantNode): boolean {
    return (node.power.fruitType === 'food' && node.hasHarvestableFruit());
  }

  private startNextStep() {
    if (this.path && this.path.length > 0) {
      this.nextLoc = this.path.shift();
      this.magnitude = 0;
    } else {
      if (this.hasFood(this.crawler.cLoc)) {
        this.eatHere();
      } else {
        this.isComplete = true;
      }
    }
  }
}

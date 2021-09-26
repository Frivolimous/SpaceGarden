import { PlantNode } from '../../../engine/nodes/PlantNode';
import { JMEasing, JMTween } from '../../../JMGE/JMTween';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { IdleCommand } from './IdleCommand';
import { WanderCommand } from './WanderCommand';

export enum CommandType {
  NONE,
  WANDER,
  IDLE,
  EAT,
  DANCE,
  RESEARCH,
  POWER,
  FRUSTRATED,
  STARVING,
  BREED,
}

export class BaseCommand {
  public type: CommandType;
  public color: number;
  public isComplete: boolean = false;

  protected fruit: PlantNode;

  protected currentPath: {
    condition: (node: PlantNode) => boolean,
    onComplete: () => void,
    onCancel: (prePath: boolean) => void,
    path?: PlantNode[],
  };

  private nextLoc: PlantNode;

  private magnitude: number;
  private angle: number;
  private targetMagnitude: number;

  private return = false;

  constructor(protected crawler: CrawlerModel, protected config: ICommandConfig) {
  }

  public initialize() {

  }

  public genPriority(): number {
    return 0;
  }

  public update() {

  }

  protected grabFruit = (fruit: PlantNode, onComplete: () => void) => {
    fruit.flagUnlink = true;
    fruit.active = false;
    fruit.physics.fixed = true;

    this.fruit = fruit;

    window.setTimeout(onComplete, 1000);
  }

  protected deliverFruit(onComplete: (fruit: PlantNode) => void) {
    let fruit = this.fruit;
    this.fruit = null;
    new JMTween(fruit.view.scale, 500).easing(JMEasing.Back.In).to({x: 0, y: 0}).start().onComplete(() => {
      fruit.flagDestroy = true;
      onComplete(fruit);
    });
  }

  protected dragFruit() {
    if (this.fruit) {
      this.fruit.view.x = this.crawler.view.x + (this.fruit.view.x - this.crawler.view.x) * this.config.fruitSpeed;
      this.fruit.view.y = this.crawler.view.y + (this.fruit.view.y - this.crawler.view.y) * this.config.fruitSpeed;
    }
  }

  protected destroyFruit() {
    let fruit = this.fruit;
    this.fruit = null;
    new JMTween(fruit.view, 1000).easing(JMEasing.Back.In).to({alpha: 0}).start().onComplete(() => {
      fruit.flagDestroy = true;
    });
  }

  protected standStill() {
    this.crawler.view.x = this.crawler.cLoc.view.x;
    this.crawler.view.y = this.crawler.cLoc.view.y;
  }

  protected startIdleLoop() {
    this.return = false;

    this.magnitude = 0;
    this.targetMagnitude = 0.3 + Math.random() * 0.7;
    this.angle = -Math.PI + 2 * Math.PI * Math.random();
  }

  protected startIdleReturn() {
    this.return = true;
    let dX = this.crawler.view.x - this.crawler.cLoc.view.x;
    let dY = this.crawler.view.y - this.crawler.cLoc.view.y;
    this.targetMagnitude = Infinity;
    this.magnitude = Math.sqrt(dX * dX + dY * dY) / 50;
    this.angle = Math.atan2(dY, dX);

  }

  protected updateIdle(onComplete: () => void) {
    this.magnitude += this.crawler.speed / this.crawler.cLoc.view.radius * 50 * (this.return ? -1 : 1);
    if (this.magnitude > this.targetMagnitude) {
      this.magnitude = this.targetMagnitude;
      this.return = true;
    } else if (this.magnitude < 0) {
      onComplete();
      return;
    }

    this.crawler.view.x = this.crawler.cLoc.view.x + this.magnitude * this.crawler.cLoc.view.radius * Math.cos(this.angle);
    this.crawler.view.y = this.crawler.cLoc.view.y + this.magnitude * this.crawler.cLoc.view.radius * Math.sin(this.angle);
  }

  protected startPath(condition: (node: PlantNode) => boolean, onComplete: () => void, onCancel: (prePath: boolean) => void) {
    if (condition(this.crawler.cLoc)) {
      onComplete();
    } else {
      let path = this.crawler.findPath(condition);
      if (!path || path.length === 0) {
        onCancel(true);
      } else {
        path.shift();
        this.currentPath = {condition, onComplete, onCancel, path};
        this.startNextStep();
      }
    }
  }

  protected updatePath() {
    if (!this.nextLoc.exists) {
      this.crawler.killMe();
      return;
    }
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

  private startNextStep = () => {
    if (this.currentPath && this.currentPath.path.length > 0) {
      this.nextLoc = this.currentPath.path.shift();
      if (!this.nextLoc.exists) {
        this.isComplete = true;
        this.crawler.setCommand(CommandType.FRUSTRATED);
        if (this.fruit) {
          this.destroyFruit();
        }
      }
      this.magnitude = 0;
    } else {
      let path = this.currentPath;
      this.currentPath = null;

      if (path.condition(this.crawler.cLoc)) {
        path.onComplete();
      } else {
        path.onCancel(false);
      }
    }
  }
}

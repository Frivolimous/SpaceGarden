import { ICommandConfig } from '../../../data/CrawlerData';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { JMEasing, JMTween } from '../../../JMGE/JMTween';
import { GameKnowledge } from '../GameKnowledge';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { CommandType } from './_CommandTypes';

export class BaseCommand {
  public type: CommandType;
  public color: number;
  public isComplete: boolean = false;

  public nextLoc: PlantNode;

  protected fruit: PlantNode;

  protected currentPath: {
    condition: (node: PlantNode) => boolean,
    onComplete: () => void,
    onCancel: (prePath: boolean) => void,
    path?: PlantNode[],
  };

  private magnitude: number;
  private angle: number;
  private targetMagnitude: number;

  private return = false;

  constructor(protected crawler: CrawlerModel, protected config: ICommandConfig, protected knowledge: GameKnowledge) {
  }

  public initialize() {

  }

  public genPriority(): number {
    return 0;
  }

  public update() {

  }

  protected grabFruit = (fruit: PlantNode, onComplete: () => void) => {
    if (!fruit) {
      this.crawler.setCommand(CommandType.FRUSTRATED);
      return;
    }
    fruit.flagUnlink = true;
    fruit.active = false;
    fruit.physics.fixed = true;
    // this.crawler.claimNode(fruit);

    this.fruit = fruit;

    window.setTimeout(onComplete, 1000);
  }

  protected dragFruit() {
    this.fruit && this.fruit.view.tickFollow(this.crawler.view, this.config.fruitSpeed);
  }

  protected deliverFruit(onComplete: (fruit: PlantNode) => void) {
    this.crawler.unclaimNode();
    let fruit = this.fruit;
    this.fruit = null;
    new JMTween(fruit.view.scale, 500).easing(JMEasing.Back.In).to({x: 0, y: 0}).start().onComplete(() => {
      fruit.flagDestroy = true;
      onComplete(fruit);
    });
  }

  protected destroyFruit() {
    let fruit = this.fruit;
    this.fruit = null;
    new JMTween(fruit.view, 1000).easing(JMEasing.Back.In).to({alpha: 0}).start().onComplete(() => {
      fruit.flagDestroy = true;
    });
  }

  protected standStill() {
    this.crawler.view.positionAt(this.crawler.cLoc.view);
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

  protected updateIdle(onComplete: () => void, speed: number = 1) {
    this.magnitude += this.crawler.speed * speed / this.crawler.cLoc.view.radius * 50 * (this.return ? -1 : 1);
    if (this.magnitude > this.targetMagnitude) {
      this.magnitude = this.targetMagnitude;
      this.return = true;
    } else if (this.magnitude < 0) {
      onComplete();
      return;
    }

    this.crawler.view.positionAt(this.crawler.cLoc.view, this.magnitude, this.angle);
  }

  protected startPath(condition: (node: PlantNode) => boolean, onComplete: () => void, onCancel: (prePath: boolean) => void, andClaimFruit?: boolean) {
    if (condition(this.crawler.cLoc)) {
      onComplete();
    } else {
      let path = this.crawler.findPath(condition);
      if (!path || path.length === 0) {
        if (this.fruit) this.fruit = null;
        this.crawler.unclaimNode();
        onCancel(true);
      } else {
        path.shift();
        this.currentPath = {condition, onComplete, onCancel, path};
        if (andClaimFruit) {
          let target = path[path.length - 1];
          let fruit = target.harvestFruit();
          this.crawler.claimNode(fruit);
        }

        this.startNextStep();
      }
    }
  }

  protected updatePath(speed: number = 1) {
    if (!this.nextLoc.exists) {
      this.crawler.killMe();
      return;
    }
    this.magnitude += this.crawler.speed * speed;
    if (this.magnitude > 1) {
      this.magnitude = 0;
      this.crawler.cLoc = this.nextLoc;
      this.startNextStep();
    } else {
      this.crawler.view.positionBetween(this.crawler.cLoc.view, this.nextLoc.view, this.magnitude);
    }
  }

  private startNextStep = () => {
    if (this.currentPath && this.currentPath.path.length > 0) {
      this.nextLoc = this.currentPath.path.shift();
      if (!this.nextLoc.exists) {
        this.isComplete = true;
        this.crawler.setCommand(CommandType.FRUSTRATED);
        if (this.fruit) this.fruit = null;
        this.crawler.unclaimNode();
        this.crawler.frustratedBy = 'Stepping ' + CommandType[this.type];
        if (this.fruit) {
          this.destroyFruit();
        }
      }
      this.magnitude = 0;
    } else {
      let path = this.currentPath;
      this.currentPath = null;
      path.onComplete();
      // if (path.condition(this.crawler.cLoc)) {
      //   path.onComplete();
      // } else {
      //   path.onCancel(false);
      // }
    }
  }
}

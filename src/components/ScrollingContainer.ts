import * as PIXI from 'pixi.js';
import { JMEasing, JMTween } from '../JMGE/JMTween';
import { JMRect } from '../JMGE/others/JMRect';
import { Starfield } from './Starfield';

export class ScrollingContainer extends PIXI.Container {
  public innerBounds: JMRect = new JMRect(0, 0, 2000, 1000);
  public outerBounds: JMRect = new JMRect(0, 0, 2000, 1000);
  public background: PIXI.Container;

  public diff: { oX: number, oY: number, tX: number, tY: number };

  private keyV = 1;

  private numStars = 300;
  private v = { x: 0, y: 0 };
  private a = { x: 0, y: 0 };

  private friction = 0.9;

  private zoomTween: JMTween;

  private scrolling = {
    [Direction.LEFT]: false,
    [Direction.RIGHT]: false,
    [Direction.UP]: false,
    [Direction.DOWN]: false,
  };

  constructor(width: number, height: number) {
    super();

    this.innerBounds.width = width;
    this.innerBounds.height = height;

    this.background = new Starfield(this.innerBounds.width, this.innerBounds.height, this.numStars);
    this.background.interactive = true;
    this.addChild(this.background);
  }

  get innerWidth(): number {
    return this.innerBounds.width * this.scale.x;
  }

  get innerHeight(): number {
    return this.innerBounds.height * this.scale.y;
  }

  public onTick = () => {
    // if (this.diff) {
    //     this.x = this.diff.tX - this.diff.oX;
    //     this.y = this.diff.tY - this.diff.oY;
    //     this.diff.tX = this.x;
    //     this.diff.tY = this.y;
    //     return;
    // }

    if (this.innerWidth > this.outerBounds.width) {
      this.v.x *= this.friction;
      this.v.x += this.a.x;
      this.position.x += this.v.x;

      if (this.x > this.outerBounds.x) {
        this.x = this.outerBounds.x;
        this.v.x = 0;
      } else if (this.x < this.outerBounds.right - this.innerWidth) {
        this.x = this.outerBounds.right - this.innerWidth;
        this.v.x = 0;
      }
    } else {
      this.x = this.outerBounds.x + (this.outerBounds.width - this.innerWidth) / 2;
    }

    if (this.innerHeight > this.outerBounds.height) {
      this.v.y *= this.friction;
      this.v.y += this.a.y;
      this.position.y += this.v.y;

      if (this.y > this.outerBounds.y) {
        this.y = this.outerBounds.y;
        this.v.y = 0;
      } else if (this.y < this.outerBounds.bottom - this.innerHeight) {
        this.y = this.outerBounds.bottom - this.innerHeight;
        this.v.y = 0;
      }
    } else {
      this.y = this.outerBounds.y + (this.outerBounds.height - this.innerHeight) / 2;
    }
  }

  public checkBounds() {
    if (this.innerWidth <= this.outerBounds.width) {
      this.x = this.outerBounds.x + (this.outerBounds.width - this.innerWidth) / 2;
    }

    if (this.innerHeight <= this.outerBounds.height) {
      this.y = this.outerBounds.y + (this.outerBounds.height - this.innerHeight) / 2;
    }
  }

  public startPan(direction: Direction) {
    switch (direction) {
      case Direction.LEFT:
        if (!this.scrolling[Direction.LEFT]) {
          this.scrolling[Direction.LEFT] = true;
          this.a.x += this.keyV;
        }
        break;
      case Direction.RIGHT:
        if (!this.scrolling[Direction.RIGHT]) {
          this.scrolling[Direction.RIGHT] = true;
          this.a.x -= this.keyV;
        }
        break;
      case Direction.UP:
        if (!this.scrolling[Direction.UP]) {
          this.scrolling[Direction.UP] = true;
          this.a.y += this.keyV;
        }
        break;
      case Direction.DOWN:
        if (!this.scrolling[Direction.DOWN]) {
          this.scrolling[Direction.DOWN] = true;
          this.a.y -= this.keyV;
        }
        break;
    }
  }

  public endPan(direction: Direction) {
    switch (direction) {
      case Direction.LEFT:
        if (this.scrolling[Direction.LEFT]) {
          this.scrolling[Direction.LEFT] = false;
          this.a.x -= this.keyV;
        }
        break;
      case Direction.RIGHT:
        if (this.scrolling[Direction.RIGHT]) {
          this.scrolling[Direction.RIGHT] = false;
          this.a.x += this.keyV;
        }
        break;
      case Direction.UP:
        if (this.scrolling[Direction.UP]) {
          this.scrolling[Direction.UP] = false;
          this.a.y -= this.keyV;
        }
        break;
      case Direction.DOWN:
        if (this.scrolling[Direction.DOWN]) {
          this.scrolling[Direction.DOWN] = false;
          this.a.y += this.keyV;
        }
        break;
    }
  }

  public zoomBy(n: number) {
    let amount = this.scale.x * n;
    let maxZoom = Math.max(this.outerBounds.width / this.innerBounds.width, this.outerBounds.height / this.innerBounds.height);
    amount = Math.max(amount, maxZoom);

    let x: number = this.x * n;
    let y: number = this.y * n;

    if (this.zoomTween) this.zoomTween.stop();
    this.zoomTween = new JMTween(this.scale, 500).to({ x: amount, y: amount }).easing(JMEasing.Quadratic.Out).start().onUpdate(() => this.checkBounds());
    new JMTween(this.position, 500).to({ x, y }).easing(JMEasing.Quadratic.Out).start();
    this.checkBounds();
  }
}

export enum Direction {
  LEFT,
  RIGHT,
  UP,
  DOWN,
}

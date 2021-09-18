import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { Scrollbar } from './Scrollbar';

interface IScrollWindow {
  scrollColor: number;
  moverColor: number;
}

export class ScrollWindow extends PIXI.Container {
  public paddingTB = 50;
  public container: PIXI.Container;
  public mask: PIXI.Graphics;
  public back: PIXI.Graphics;

  private vY: number = 0;
  private offsetY?: number = null;
  private goalY?: number = null;
  private innerHeight: number;
  private outerHeight: number;
  private scrollbarWidth = 10;
  private scrollbar: Scrollbar;
  private hasListeners = false;

  constructor(width: number, height: number, private config: IScrollWindow = {scrollColor: undefined, moverColor: undefined}) {
    super();
    this.back = new PIXI.Graphics();
    this.back.beginFill(0, 0.1).drawRect(0, 0, width, height);
    this.addChild(this.back);

    this.container = new PIXI.Container();
    this.addChild(this.container);

    this.mask = new PIXI.Graphics();
    this.mask.beginFill(0).drawRect(0, 0, width, height);
    this.addChild(this.mask);

    this.interactive = true;

    this.container.position.set(0, 0);

    this.addListener('pointerdown', this.onDown);
    this.addListener('mouseup', this.onUp);
    this.addListener('mouseupoutside', this.onUp);
    this.addListener('touchend', this.onUp);
    this.addListener('pointermove', this.onMove);

    this.addListeners();

    this.scrollbar = new Scrollbar(this.scrollbarWidth, height, this.scrollTo, this.config.scrollColor, this.config.moverColor);
    this.addChild(this.scrollbar);
    this.scrollbar.x = width - this.scrollbarWidth;

    this.resize(width, height);
  }

  public destroy() {
    this.removeListeners();
    super.destroy();
  }

  public addListeners() {
    if (!this.hasListeners) {
      window.addEventListener('wheel', this.onWheel);
      PIXI.Ticker.shared.add(this.onTick);
      this.hasListeners = true;
    }
  }

  public removeListeners() {
    if (this.hasListeners) {
      window.removeEventListener('wheel', this.onWheel);
      PIXI.Ticker.shared.remove(this.onTick);
      this.hasListeners = false;
    }
  }

  public resize(width: number, height: number) {
    this.back.clear().beginFill(0, 0.01).drawRect(0, 0, width, height);
    this.mask.clear().beginFill(0).drawRect(0, 0, width, height);
    this.outerHeight = height;
    this.calculateInnerHeight();
    this.fixY();
    this.scrollbar.resize(this.scrollbarWidth, height);
    this.scrollbar.x = width - this.scrollbarWidth;
  }

  public calculateInnerHeight() {
    this.innerHeight = _.max(_.map(this.container.children, (obj: any) => obj.y + (obj.getHeight ? obj.getHeight() : obj.height)));
    this.innerHeight += this.paddingTB * 2;
    this.innerHeight = this.innerHeight || 0;
    this.scrollbar && this.scrollbar.drawMover(this.outerHeight / this.innerHeight);
    console.log('draw!', this.outerHeight, this.innerHeight);
  }

  public addObject = (_object: PIXI.Container) => {
    // _object.x -= this.x - this.container.x;
    // _object.y -= this.y - this.container.y;
    this.container.addChild(_object);
    this.calculateInnerHeight();
  }

  public removeObject = (_object: PIXI.Container) => {
    if (_object.parent === this.container) {
      this.removeObjectAt(this.container.getChildIndex(_object));
    }
  }

  public removeObjectAt = (i: number) => {
    this.container.removeChildAt(i);
    this.calculateInnerHeight();
  }

  public removeAllObjects = () => {
    this.container.removeChildren();
    this.calculateInnerHeight();
  }

  public getWidth(withScale = true) {
    return this.back.width * (withScale ? this.scale.x : 1);
  }

  public getHeight(withScale = true) {
    return this.back.height * (withScale ? this.scale.y : 1);
  }

  private onWheel = (e: WheelEvent) => {
    this.vY -= e.deltaY * 0.00008 * this.outerHeight;
  }

  private scrollTo = (p: number) => {
    if (this.innerHeight < this.outerHeight) {
      this.container.y = this.paddingTB;
    } else {
      this.container.y = this.paddingTB + (this.outerHeight - this.innerHeight) * p;
    }
    this.container.y = Math.min(Math.max(this.container.y, this.outerHeight - this.innerHeight + this.paddingTB), this.paddingTB);
  }

  private fixY = () => {
    this.container.y = Math.min(Math.max(this.container.y, this.outerHeight - this.innerHeight + this.paddingTB), this.paddingTB);
    this.scrollbar && this.scrollbar.setPosition(- (this.container.y - this.paddingTB) / (this.innerHeight - this.outerHeight), false);
  }

  private onTick = () => {
    if (this.goalY !== null) {
      this.vY = (this.goalY - this.container.y) / 4;
    }
    if (this.vY !== 0) {
      if (Math.abs(this.vY) < 0.1) {
        this.vY = 0;
      } else {
        let y = this.container.y;
        y = Math.min(y, 0);
        y = Math.max(y, this.outerHeight - this.innerHeight);
        this.vY *= 0.95;
        this.container.y += this.vY;
        this.fixY();
      }
    }
  }

  private onDown = (e: PIXI.interaction.InteractionEvent) => {
    if (e.target === this) {
      let pos = e.data.getLocalPosition(this);
      this.offsetY = pos.y - this.y - this.container.y;
    }
  }

  private onUp = (e: PIXI.interaction.InteractionEvent) => {
    this.goalY = null;
    this.offsetY = null;
  }

  private onMove = (e: PIXI.interaction.InteractionEvent) => {
    if (this.offsetY !== null) {
      let pos = e.data.getLocalPosition(this);
      let _y = pos.y - this.y - this.offsetY;
      this.goalY = pos.y - this.y - this.offsetY;
      this.vY = (_y - this.container.y) / 4;
    }
  }
}

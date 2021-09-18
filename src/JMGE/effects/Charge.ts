import * as PIXI from 'pixi.js';
import { ColorGradient } from '../others/Colors';

export class Charge extends PIXI.Graphics {
  public running: boolean = false;

  private count: number = -1;
  private gradient1: ColorGradient;
  private gradient2: ColorGradient;
  private callback: () => void;

  constructor(public endRadius: number, public time: number, color: number) {
    super();
    this.gradient1 = new ColorGradient(0, color);
    this.gradient2 = new ColorGradient(0xffffff, color);
  }

  public startCharge(callback?: () => void) {
    this.count = 0;
    this.callback = callback;
    this.redraw();
    this.running = true;
  }

  public update(speed: number) {
    if (this.count === -1) return;

    if (this.count < this.time) {
      this.count += speed;
      this.redraw();
    } else {
      this.endCharge();
    }
  }

  private redraw = () => {
    // let color=this.gradient.getColorAt(Math.random()*0.5+0.5);
    let color: number;
    if (Math.random() < 0.5) {
      color = this.gradient1.getColorAt(Math.random() * 0.5 + 0.5);
    } else {
      color = this.gradient2.getColorAt(Math.random() * 0.5 + 0.5);
    }
    this.clear();
    this.beginFill(color);
    this.drawCircle(0, 0, this.endRadius * this.count / this.time);
  }

  private endCharge = () => {
    if (this.callback) this.callback();
    this.callback = null;
    this.count = -1;
    this.clear();
    this.running = false;
  }
}

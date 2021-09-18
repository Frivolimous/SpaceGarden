import * as PIXI from 'pixi.js';
import * as Colors from '../others/Colors';
import { JMTween } from '../JMTween';

export class Laser extends PIXI.Graphics {
  constructor(origin: { x: number, y: number }, target: { x: number, y: number }, color: number = 0xffffff, thickness: number = 1, parent?: PIXI.Container) {
    super();
    if (parent) parent.addChild(this);
    this.lineStyle(thickness * 2, Colors.adjustLightness(color, 0.3));
    this.moveTo(origin.x, origin.y);
    this.lineTo(target.x, target.y);
    this.lineStyle(thickness, color);
    this.lineTo(origin.x, origin.y);
    this.alpha = 2;

    new JMTween<Laser>(this).to({ alpha: 0 }).over(500).onComplete(() => this.destroy()).start();
  }
}

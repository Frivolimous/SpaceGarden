import * as PIXI from 'pixi.js';
import { JMEasing, JMTween } from '../JMTween';
import { Fonts } from '../../data/Fonts';

export class PointingArrow extends PIXI.Graphics {
  tween: JMTween;

  constructor() {
    super();
    this.beginFill(0xdddddd);
    this.lineStyle(1, 0xffffff);
    this.moveTo(0, 0)
      .lineTo(20, -15)
      .lineTo(10, -15)
      .lineTo(10, -50)
      .lineTo(-10, -50)
      .lineTo(-10, -15)
      .lineTo(-20, -15)
      .lineTo(0, 0);

    this.tween = new JMTween(this.position, 500).to({y: -10}).yoyo(true, Infinity).easing(JMEasing.Sinusoidal.InOut).start();
  }

  destroy() {
    super.destroy();
    this.tween.stop();
  }
}
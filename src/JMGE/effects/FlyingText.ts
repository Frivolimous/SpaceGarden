import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { JMTween } from '../JMTween';
import { Fonts } from '../../data/Fonts';

export class FlyingText extends PIXI.Text {
  constructor(s: string, style: PIXI.TextStyleOptions, x: number, y: number, parent?: PIXI.Container) {
    super(s, _.defaults(style, { fontSize: 20, fontWeight: 'bold', dropShadow: true, fill: 0xffffff, fontFamily: Fonts.FLYING, dropShadowDistance: 2 }));
    this.anchor.set(0.5, 0.5);

    this.position.set(x, y);

    if (parent) parent.addChild(this);

    new JMTween<FlyingText>(this).wait(200).to({ alpha: 0}).over(1000).start();
    new JMTween<FlyingText>(this).to({ y: this.y - 100 }).over(1200).onComplete(() => {
      this.destroy();
    }).start();
  }
}

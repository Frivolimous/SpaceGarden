import * as PIXI from 'pixi.js';
import { JMTween, JMEasing } from '../../../JMGE/JMTween';

export abstract class BaseModal extends PIXI.Container {
  public onAppearComplete: () => void;
  protected animating = false;
  protected tween: JMTween;

  protected stageBorder: PIXI.Rectangle;

  constructor() {
    super();
    this.eventMode = 'dynamic';
  }

  public updatePosition(borders: PIXI.Rectangle) {
    this.stageBorder = borders;

    if (!this.animating) {
      this.x = borders.x + borders.width / 2;
      this.y = borders.y + borders.height / 2;
    }
  }

  public destroy() {
    if (this.tween) this.tween.stop();
    super.destroy();
  }

  public startAnimation = (delay: number) => {
    this.animating = true;
    this.tween = new JMTween(this.scale, 300).wait(delay).from({x: 0, y: 0}).easing(JMEasing.Back.Out).start().onComplete(() => {
      this.tween = null;
      this.animating = false;
      this.onAppearComplete && this.onAppearComplete();
    });
  }
}

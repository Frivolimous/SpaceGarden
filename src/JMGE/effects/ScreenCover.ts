import * as PIXI from 'pixi.js';
import { JMTween } from '../JMTween';

export class ScreenCover extends PIXI.Graphics {
  private _onFadeComplete: () => void;

  constructor(rect: PIXI.Rectangle, color: number = 0) {
    super();
    this.beginFill(color);
    this.drawRect(rect.x, rect.y, rect.width, rect.height);
    this.interactive = true;
  }

  public onFadeComplete = (callback: () => void) => {
    this. _onFadeComplete = callback;

    return this;
  }

  public fadeIn = (duration: number, waitPre: number = 0, waitPost: number = 0) => {
    new JMTween<ScreenCover>(this).wait(waitPre).from({alpha: 0}).over(duration).onComplete(() => {
      if (waitPost) {
        new JMTween({}).wait(waitPost).onWaitComplete(() => {
          this.destroy();
          if (this._onFadeComplete) this._onFadeComplete();
        }).start();
      } else {
        this.destroy();
        if (this._onFadeComplete) this._onFadeComplete();
      }
    }).start();

    return this;
  }

  public fadeOut = (duration: number, waitPre: number = 0, waitPost: number = 0) => {
    new JMTween<ScreenCover>(this).wait(waitPre).to({alpha: 0}).over(duration).onComplete(() => {
      if (waitPost) {
        new JMTween({}).wait(waitPost).onWaitComplete(() => {
          this.destroy();
          if (this._onFadeComplete) this._onFadeComplete();
        }).start();
      } else {
        this.destroy();
        if (this._onFadeComplete) this._onFadeComplete();
      }
    }).start();

    return this;
  }
}

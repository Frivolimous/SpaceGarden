import * as PIXI from 'pixi.js';
import { JMEasing, JMTween } from '../../JMGE/JMTween';
import { Fonts } from '../../data/Fonts';
import { colorLuminance } from '../../JMGE/others/Colors';

export class PlantNodeView extends PIXI.Container {
  // public INTENSITY_V: number = 0.01;
  // public INTENSITY_V_INC: number = 0.01;
  // public INTENSITY_V_DEC: number = 0.005;
  public INTENSITY_V_INC: number = 0.1;
  public INTENSITY_V_DEC: number = 0.05;

  public sprite: PIXI.Sprite;
  public _Highlight: PIXI.Graphics;
  public _Highlight2: PIXI.Graphics;
  public _Buff: PIXI.Graphics;
  public text = new PIXI.Text('2/2', { fontSize: 12, fontFamily: Fonts.FLYING, stroke: 0, strokeThickness: 1, fill: 0xffffff });

  public _Intensity: number;
  public targetIntensity: number = 1;

  constructor(texture: PIXI.Texture, public color: number, public radius: number) {
    super();

    this.sprite = new PIXI.Sprite(texture);
    this.sprite.anchor.set(0.5);

    this.text.anchor.set(0.5);
    this.text.visible = false;

    this._Highlight = new PIXI.Graphics();
    this._Highlight.beginFill(0xffff00, 0.5);
    this._Highlight.drawCircle(0, 0, Math.max(this.radius * 1.2, this.radius + 4));
    this._Highlight.visible = false;
    this._Highlight2 = new PIXI.Graphics();
    this._Highlight2.beginFill(0x00ffff, 0.5);
    this._Highlight2.drawCircle(0, 0, Math.max(this.radius * 1.2, this.radius + 4));
    this._Highlight2.visible = false;

    this.addChild(this._Highlight2, this._Highlight, this.sprite, this.text);

    this.setIntensity(1, true);
  }

  get highlight(): boolean {
    return this._Highlight.visible;
  }

  set highlight(b: boolean) {
    this._Highlight.visible = b;
  }

  get highlight2(): boolean {
    return this._Highlight2.visible;
  }

  set highlight2(b: boolean) {
    this._Highlight2.visible = b;
  }

  public setIntensity(n: number, instant?: boolean) {
    // this.targetIntensity = Math.min(Math.max(n, 0.2), 1);
    // if (n > 1) {
    //   this.targetIntensity = Math.min(2, 1 + (n - 1) * 0.1);
    // } else {
    //   this.targetIntensity = Math.max(n, 0.2);
    // }
    this.targetIntensity = n;
    if (!this._Intensity || instant) {
      this._Intensity = this.targetIntensity - 0.01;
    }
  }

  public adjustIntensity = () => {
    if (this._Intensity !== this.targetIntensity) {
      if (this._Intensity < this.targetIntensity) {
        this._Intensity = Math.min(Math.max(this._Intensity + (this.targetIntensity - this._Intensity) * this.INTENSITY_V_INC, 0.1), this.targetIntensity);
      } else if (this._Intensity > this.targetIntensity) {
        this._Intensity = Math.max(this._Intensity + (this.targetIntensity - this._Intensity) * this.INTENSITY_V_DEC, this.targetIntensity);
      }
      let value = this.intensityInterpolation(this._Intensity);
      this.sprite.tint = colorLuminance(this.color, value);
      this.sprite.scale.set(value);
    }
  }

  public pulse(color: number) {
    let pulseCircle = new PIXI.Graphics();
    pulseCircle.beginFill(color);
    pulseCircle.drawCircle(0, 0, Math.max(this.radius * 1.2, this.radius + 4));
    pulseCircle.alpha = 0;
    this.addChildAt(pulseCircle, 0);

    new JMTween(pulseCircle, 100).to({alpha: 0.5}).start().chain(pulseCircle, 300).to({alpha: 0}).onComplete(() => {
      pulseCircle.destroy();
    });
  }

  public showConnectionCount(current: number, max: number) {
    if (max === 0) {
      this.text.visible = false;
    } else {
      this.text.visible = true;
      let remaining = max - current;
      this.text.text = `${current} / ${max}`;
      if (remaining <= 0) {
        this.text.tint = 0xff0000;
      } else if (remaining === 1) {
        this.text.tint = 0xcccc00;
      } else {
        this.text.tint = 0xffffff;
      }
    }
  }

  public tickFollow(target: {x: number, y: number}, mult: number) {
    this.x = target.x + (this.x - target.x) * mult;
    this.y = target.y + (this.y - target.y) * mult;
  }

  public showBuff(b: boolean) {
    if (b &&!this._Buff) {
      this._Buff = new PIXI.Graphics();
      this._Buff.beginFill(0xffffff, 0.2);
      this._Buff.drawCircle(0, 0, this.radius * 1.2);
      this.addChildAt(this._Buff, 0);
    }

    this._Buff.visible = b;
  }

  private intensityInterpolation(k: number): number {
    // if (n > 1) {
    //   this.targetIntensity = Math.min(2, 1 + (n - 1) * 0.1);
    // } else {
    //   this.targetIntensity = Math.max(n, 0.2);
    // }

    if (k > 1) {
      return Math.min(2, 1 + (k - 1) * 0.1);
    } else {
      return Math.max(k, 0.2);
    }

    // return k;
    // if (k <= 0.7) {
    //   return 0.2 + k / 0.7 * 0.3;
    // } else {
    //   return 0.5 + (k - 0.7) / 0.3 * 0.5;
    // }
  }
  
}

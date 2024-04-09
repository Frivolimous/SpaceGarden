import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { colorLuminance } from '../../JMGE/others/Colors';
import { Fonts } from '../../data/Fonts';
import { JMTween } from '../../JMGE/JMTween';
// import { SoundData, SoundIndex } from '../../utils/SoundData';

const defaultConfig: Partial<INodeButton> = { width: 200, height: 50, rounding: 8, color: 0x77ccff, hoverScale: 0.1 };

const defaultLabelStyle: Partial<PIXI.ITextStyle> = { fill: 0, fontFamily: Fonts.UI };

export interface INodeButton {
  color?: number;
  width?: number;
  height?: number;
  rounding?: number;
  label?: string;
  labelStyle?: any;
  onUp?: (e: PIXI.FederatedPointerEvent) => void;
  onDown?: (e: PIXI.FederatedPointerEvent) => void;
  hoverScale?: number;
  maxNodes?: number;
}

export class NodeButton extends PIXI.Container {
  protected background: PIXI.Graphics;

  private label: PIXI.Text;
  private inner: PIXI.Container;
  private numberText: PIXI.Text;
  private color: number;

  private defaultColor: number;
  private disabledColor = 0x999999;
  private selectedColor = 0xddcc33;

  private _Disabled: boolean;
  private _Selected: boolean;

  private _Highlight: PIXI.Graphics;
  private _HighlightTween: JMTween;
  private _Count: number;

  constructor(protected config: INodeButton) {
    super();
    this.config = config = _.defaults(config, defaultConfig);
    this.color = config.color;
    this.defaultColor = config.color;

    this.hitArea = new PIXI.Rectangle(0, 0, config.width, config.height);

    this.inner = new PIXI.Container();
    this.inner.pivot.set(config.width / 2, config.height / 2);
    this.inner.position.set(config.width / 2, config.height / 2);
    this.addChild(this.inner);
    this.background = new PIXI.Graphics();
    this.background.beginFill(0xffffff).drawRoundedRect(0, 0, config.width, config.height, config.rounding);
    this.background.tint = config.color;
    let style = _.defaults(config.labelStyle, defaultLabelStyle);

    this.inner.addChild(this.background);
    this.label = new PIXI.Text(config.label, style);
    this.inner.addChild(this.label);
    this.addLabel();

    if (config.maxNodes && config.maxNodes !== Infinity) {
      this.numberText = new PIXI.Text(' / ', style);
      this.inner.addChild(this.numberText);
      this.count = 0;
    } else {
      this.numberText = new PIXI.Text('∞', style);
      this.inner.addChild(this.numberText);
      this.count = Infinity;
    }

    this.interactive = true;
    this.cursor = 'pointer';

    this.addListener('mouseover', () => {
      if (this.disabled) return;
      this.background.tint = colorLuminance(this.color, 0.8);
      this.inner.scale.set(1 + this.config.hoverScale);
    });
    this.addListener('mouseout', () => {
      if (this.disabled) return;
      this.background.tint = this.color;
      this.inner.scale.set(1);
    });
    this.addListener('mouseup', (e) => {
      if (this.disabled) return;
      this.background.tint = colorLuminance(this.color, 0.8);
      this.inner.scale.set(1);
      config.onUp && config.onUp(e);
    });

    this.addListener('touchend', (e) => {
      if (this.disabled) return;
      this.background.tint = this.color;
      this.inner.scale.set(1);
      config.onUp && config.onUp(e);
    });

    this.addListener('pointerdown', e => {
      if (this.disabled) return;
      this.background.tint = colorLuminance(this.color, 0.8);
      this.inner.scale.set(1 - this.config.hoverScale);
      config.onDown && config.onDown(e);
      // SoundData.playSound(SoundIndex.CLICK);
    });
  }

  public get maxNodes(): number {
    return this.config.maxNodes;
  }

  public set disabled(b: boolean) {
    this._Disabled = b;
    // this.interactive = !b;
    this.cursor = b ? 'auto' : 'pointer';
    if (b) {
      this.color = this.disabledColor;
    } else {
      this.color = this.defaultColor;
    }
    this.background.tint = this.color;
  }

  public get disabled() {
    return this._Disabled;
  }

  public set selected(b: boolean) {
    this._Selected = b;
    // this.interactive = !b;
    if (b) {
      if (!this._Highlight) {
        this._Highlight = new PIXI.Graphics();
        this._Highlight.lineStyle(3, 0xffff00);
        this._Highlight.drawRoundedRect(0, 0, this.getWidth(), this.getHeight(), this.config.rounding);
        this.inner.addChild(this._Highlight);
      }
      // this.color = this.selectedColor;
    } else {
      if (this._Highlight) {
        this._Highlight.destroy();
        this._Highlight = null;
      }
      // this.color = this.defaultColor;
    }
    // this.background.tint = this.color;
  }

  public get selected(): boolean {
    return this._Selected;
  }

  public set count(n: number) {
    if (n === Infinity) {
      this._Count = Infinity;
      this.numberText.text = ' ∞ ';
    } else {
      this._Count = n;
      this.numberText.text = `${n}/${this.config.maxNodes}`;
    }
    this.numberText.width = this.config.width / 3;
    this.numberText.scale.y = this.numberText.scale.x;
    this.numberText.x = this.config.width * 2 / 3 - 2;
    this.numberText.y = this.config.height - this.numberText.height - 2;
  }

  public get count(): number {
    return this._Count;
  }

  public startCustomDraw(clear: boolean = true) {
    if (clear) {
      this.background.clear();
    }
    this.background.beginFill(0xffffff);
    return this.background;
  }

  public setColor(color: number) {
    this.color = color;
    this.background.tint = color;
  }

  public addLabel(s?: string) {
    if (s) {
      this.label.text = s;
    }
    this.label.scale.set(1, 1);

    if (this.label.width > this.background.width * 0.9) {
      this.label.width = this.background.width * 0.9;
    }
    this.label.scale.y = this.label.scale.x;
    this.label.x = (this.background.width - this.label.width) / 2;
    this.label.y = (this.background.height - this.label.height) / 2;
  }

  public getLabel() { return this.label.text; }

  public getWidth(withScale = true) {
    return this.config.width * (withScale ? this.scale.x : 1);
  }

  public getHeight(withScale = true) {
    return this.config.height * (withScale ? this.scale.y : 1);
  }

  public highlight(b: boolean) {
    if (b) {
      if (this._Highlight) return;
      this._Highlight = new PIXI.Graphics();
      this._Highlight.lineStyle(3, 0xffff00);
      this._Highlight.drawRoundedRect(0, 0, this.getWidth(), this.getHeight(), this.config.rounding);
      this._HighlightTween = new JMTween(this._Highlight, 500).to({alpha: 0}).yoyo(true, Infinity).start();
      this.inner.addChild(this._Highlight);
    } else {
      if (this._HighlightTween) {
        this._HighlightTween.stop();
        this._HighlightTween = null;
      }
      if (this._Highlight) {
        this._Highlight.destroy();
        this._Highlight = null;
      }
    }
  }
}

import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { colorLuminance } from '../../JMGE/others/Colors';
import { Fonts } from '../../data/Fonts';
// import { SoundData, SoundIndex } from '../../utils/SoundData';

const defaultConfig: Partial<IToggleButton> = { width: 30, height: 30, rounding: 8, hoverScale: 0.1 };

const defaultLabelStyle: PIXI.TextStyleOptions = { fill: 0, fontFamily: Fonts.UI };

export interface IToggleButton {
  label?: string;
  labelStyle?: any;
  width?: number;
  height?: number;
  rounding?: number;
  onToggle: (b: boolean) => void;
  hoverScale?: number;
  color?: number;
  selectedColor?: number
}

export class ToggleButton extends PIXI.Container {
  protected background: PIXI.Graphics;

  private label: PIXI.Text;
  private inner: PIXI.Container;
  private color: number;

  private defaultColor: number = 0xff0000;
  private selectedColor: number = 0x007700;
  private _Selected: boolean;

  constructor(protected config: IToggleButton) {
    super();
    this.config = config = _.defaults(config, defaultConfig);
    this.defaultColor = this.config.color || this.defaultColor;
    this.selectedColor = this.config.selectedColor || this.selectedColor;
    this.color = this.defaultColor;

    this.hitArea = new PIXI.Rectangle(0, 0, config.width, config.height);

    this.inner = new PIXI.Container();
    this.inner.pivot.set(config.width / 2, config.height / 2);
    this.inner.position.set(config.width / 2, config.height / 2);
    this.addChild(this.inner);
    this.background = new PIXI.Graphics();
    this.background.beginFill(0xffffff).drawRoundedRect(0, 0, config.width, config.height, config.rounding);
    this.background.tint = this.color;

    this.inner.addChild(this.background);

    if (this.config.label) {
      let style = _.defaults(config.labelStyle, defaultLabelStyle);
      this.label = new PIXI.Text(config.label, style);
      this.addLabel();
      this.inner.addChild(this.label);  
    }

    this.interactive = true;
    this.buttonMode = true;

    this.addListener('mouseover', () => {
      this.background.tint = colorLuminance(this.color, 0.8);
      this.inner.scale.set(1 + this.config.hoverScale);
    });
    this.addListener('mouseout', () => {
      this.background.tint = this.color;
      this.inner.scale.set(1);
    });
    this.addListener('mouseup', (e) => {
      this.background.tint = colorLuminance(this.color, 0.8);
      this.inner.scale.set(1);
      if (e.target === this) {
        this.selected = !this.selected;
      }
    });

    this.addListener('touchend', (e) => {
      this.background.tint = this.color;
      this.inner.scale.set(1);
      if (e.target === this) {
        this.selected = !this.selected;
      }
    });

    this.addListener('pointerdown', () => {
      this.background.tint = colorLuminance(this.color, 0.8);
      this.inner.scale.set(1 - this.config.hoverScale);
      // SoundData.playSound(SoundIndex.CLICK);
    });
  }

  public setColor(color: number) {
    this.color = color;
    this.background.tint = color;
  }

  public set selected(b: boolean) {
    if (this._Selected === b) return;

    this._Selected = b;
    if (b) {
      this.color = this.selectedColor;
    } else {
      this.color = this.defaultColor;
    }
    this.background.tint = this.color;

    this.config.onToggle(this._Selected);
  }

  public get selected(): boolean {
    return this._Selected;
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
}

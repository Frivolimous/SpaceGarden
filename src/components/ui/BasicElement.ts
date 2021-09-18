import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { Fonts } from '../../data/Fonts';

const defaultConfig: Partial<IBasicElement> = {width: 200, height: 50, rounding: 5, color: 0x8080ff};
const defaultLabelStyle: PIXI.TextStyleOptions = { fill: 0, fontFamily: Fonts.UI };

export interface IBasicElement {
  color?: number;
  width?: number;
  height?: number;
  rounding?: number;
  label?: string;
  labelStyle?: PIXI.TextStyleOptions;
}

export class BasicElement extends PIXI.Container {
  private background: PIXI.Graphics;
  private label: PIXI.Text;

  constructor(private config: IBasicElement) {
    super();
    this.config = config = _.defaults(config, defaultConfig);

    this.background = new PIXI.Graphics();
    this.addChild(this.background);
    this.background.beginFill(0xffffff).drawRoundedRect(0, 0, config.width, config.height, config.rounding);
    this.background.tint = config.color;

    if (config.label) {
      let style = _.defaults(config.labelStyle, defaultLabelStyle);
      this.label = new PIXI.Text(config.label, style);
      this.addLabel();
      this.addChild(this.label);
    }
  }

  public getWidth() {
    return this.config.width;
  }

  public getHeight() {
    return this.config.height;
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
}

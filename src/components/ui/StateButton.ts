import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { colorLuminance } from '../../JMGE/others/Colors';
import { Fonts } from '../../data/Fonts';
import { JMTween } from '../../JMGE/JMTween';
// import { SoundData, SoundIndex } from '../../utils/SoundData';

const defaultConfig: Partial<IStateButton> = { width: 60, height: 20, rounding: 1 };

const defaultLabelStyle: PIXI.TextStyleOptions = { fill: 0, fontFamily: Fonts.UI, fontSize: 12 };

export const neverStateList: IState[] = [{slug: 'Never', color: 0xf1f1aa}];

export const actionStateList: IState[] = [{slug: 'Action', color: 0xffcc55}, {slug: 'Action', color: 0x55ffcc}, {slug: 'Action', color: 0xcc77ff}];

export interface IStateButton {
  width?: number;
  height?: number;
  rounding?: number;
  labelStyle?: PIXI.TextStyleOptions;
  onToggle: (slug: string) => void;
}

export interface IState {
  slug: string;
  color: number;
}

export class StateButton extends PIXI.Container {
  protected background: PIXI.Graphics;

  private label: PIXI.Text;
  private inner: PIXI.Container;
  private color: number = 0xf1f1aa;
  private disabledColor: number = 0xf1f1aa;

  private _Disabled: boolean;
  private currentState: IState;
  private currentStateIndex: number;

  constructor(private stateList: IState[] = [], protected config?: IStateButton) {
    super();
    this.config = config = _.defaults(config, defaultConfig);

    this.hitArea = new PIXI.Rectangle(0, 0, config.width, config.height);

    this.inner = new PIXI.Container();
    this.inner.pivot.set(config.width / 2, config.height / 2);
    this.inner.position.set(config.width / 2, config.height / 2);
    this.addChild(this.inner);
    this.background = new PIXI.Graphics();
    this.background.beginFill(0xffffff).lineStyle(1).drawRoundedRect(0, 0, config.width, config.height, config.rounding);
    this.background.tint = this.color;
    let style = _.defaults(config.labelStyle, defaultLabelStyle);

    this.inner.addChild(this.background);
    this.label = new PIXI.Text('', style);
    this.addLabel();
    this.inner.addChild(this.label);

    this.interactive = true;
    this.buttonMode = true;

    this.addListener('mouseover', () => {
      this.background.tint = colorLuminance(this.color, 0.8);
    });
    this.addListener('mouseout', () => {
      this.background.tint = this.color;
    });
    this.addListener('mouseup', (e) => {
      this.background.tint = colorLuminance(this.color, 0.8);
      if (e.target === this) {
        this.nextToggle();
      }
    });

    this.addListener('touchend', (e) => {
      this.background.tint = this.color;
      if (e.target === this) {
        this.nextToggle();
      }
    });

    this.addListener('pointerdown', () => {
      this.background.tint = colorLuminance(this.color, 0.8);
    });

    this.setToggle(0);
  }

  public setStateList(list: IState[]) {
    this.stateList = list;
    if (this.currentState) {
      let cIndex = _.findIndex(list, {slug: this.currentState.slug});
      if (cIndex >= 0) {
        this.setToggle(cIndex);
        return;
      }
    }
    this.setToggle(0);
  }

  public nextToggle = () => {
    this.setToggle((this.currentStateIndex + 1) % this.stateList.length);
  }

  public setToggle = (i: number) => {
    if (!this.stateList || this.stateList.length === 0) {
      this.currentStateIndex = 0;
      this.currentState = null;
      this.config.onToggle('None');
      this.addLabel('None');
      this.setColor(this.disabledColor);
      this.disabled = true;
    } else {
      this.currentState = this.stateList[i];
      this.currentStateIndex = i;
      this.addLabel(this.currentState.slug);
      this.setColor(this.currentState.color);
      this.config.onToggle(this.currentState.slug);
      this.disabled = this.stateList.length === 1;
    }
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

  public set disabled(b: boolean) {
    this._Disabled = b;
    this.interactive = !b;
    this.buttonMode = !b;
  }

  public get disabled() {
    return this._Disabled;
  }

  public getWidth(withScale = true) {
    return this.config.width * (withScale ? this.scale.x : 1);
  }

  public getHeight(withScale = true) {
    return this.config.height * (withScale ? this.scale.y : 1);
  }
}

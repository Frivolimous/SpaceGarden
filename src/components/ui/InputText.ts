import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { Fonts } from '../../data/Fonts';

export interface IInputText {
  startingText?: string;
  width?: number;
  height?: number;
  color?: number;
  options?: PIXI.TextStyleOptions;
  maxChars?: number;
}

const dInputText: IInputText = {
  startingText: '',
  width: 100,
  height: 30,
  color: 0xaaaaaa,
  options: {fontFamily: Fonts.UI, fontSize: 20},
  maxChars: 10,
};

export class InputText extends PIXI.Container {
  public focussed = true;
  private background = new PIXI.Graphics();
  private textField: PIXI.Text;

  private timeout: number;

  private caret = new PIXI.Graphics();

  constructor(private config?: IInputText) {
    super();
    this.config = _.defaults(this.config, dInputText);

    this.textField = new PIXI.Text(this.config.startingText, this.config.options);
    this.background.beginFill(this.config.color).lineStyle(1).drawRoundedRect(-2, -2, this.config.width + 4, this.config.height + 2, 2);
    this.caret.lineStyle(2).moveTo(0, 2).lineTo(0, this.config.height - 4);
    this.caret.x = 2;
    this.addChild(this.background, this.textField);
    this.addChild(this.caret);

    window.addEventListener('keydown', this.onKeyDown);
    this.addListener('pointerdown', () => this.focussed = true);
    this.flashCaret();
  }

  public destroy() {
    super.destroy();
    window.removeEventListener('keydown', this.onKeyDown);
    this.endCaret();
  }

  public get text() {
    return this.textField.text;
  }

  public set text(s: string) {
    this.textField.text = s;
  }

  private flashCaret = () => {
    this.caret.visible = !this.caret.visible;
    this.timeout = window.setTimeout(this.flashCaret, 500);
  }

  private endCaret = () => {
    window.clearTimeout(this.timeout);
  }

  private onKeyDown = (e: KeyboardEvent) => {
    console.log('key');
    if (!this.focussed) return;

    if (e.key.length === 1 && e.key !== ' ') {
      if (this.textField.text.length < this.config.maxChars) {
        this.textField.text = this.textField.text + e.key;
        this.caret.position.x = this.textField.x + this.textField.width + 2;
      }
    } else if (e.key === 'Backspace') {
      this.textField.text = this.textField.text.substr(0, this.textField.text.length - 1);
      this.caret.position.x = this.textField.x + this.textField.width + 2;
    }
  }
}

import * as PIXI from 'pixi.js';
import { Fonts } from '../../data/Fonts';

export interface ITooltipPopup {
  levelNumber?: string;
}

type metaTags = 'b' | 'n' | 'h' | 'g' | 't' | 'line' | 'tab' | '/tab';
const fontStyles: Partial<{[key in metaTags]: PIXI.TextStyleOptions}> = {
  b: { fontSize: 13, fontFamily: Fonts.UI, fill: 0xEADF78, wordWrap: true, wordWrapWidth: 195},
  t: { fontSize: 16, fontFamily: Fonts.UI, fill: 0xFFFFA8, wordWrap: true, wordWrapWidth: 195},
  n: { fontSize: 13, fontFamily: Fonts.UI, fill: 0xb1875b, wordWrap: true, wordWrapWidth: 195},
  h: { fontSize: 13, fontFamily: Fonts.UI, fill: 0x43CBFF, wordWrap: true, wordWrapWidth: 195},
  g: { fontSize: 13, fontFamily: Fonts.UI, fill: 0x44CC5E, wordWrap: true, wordWrapWidth: 195},
};
const circleStyle = { fontSize: 25, fontFamily: Fonts.UI, fill: 0xEADF78, wordWrap: true, wordWrapWidth: 195};

const dimensions = {
  width: 200,
  padding: 5,
  tab: 5,
};

const colors = {
  BACK: 0x412315,
  BORDER: 0xb1875b,
  INNER_LINE: 0xb1875b,
  SHADOW: 0x412315,
};

export class TooltipPopup extends PIXI.Container {
  private background: PIXI.Graphics;

  private over = new PIXI.Graphics();

  constructor(title: string, description: string, config: ITooltipPopup) {
    super();

    // this.interactive = true;

    let titleField = new PIXI.Text(title, fontStyles.b);

    let descHeight = this.makeDescription(description, titleField.height);
    this.addChild(titleField);
    this.drawFrame(titleField.height, descHeight);

    if (config.levelNumber) {
      this.drawCircle(config.levelNumber, 30, titleField.height);

      titleField.position.set(30 + dimensions.padding, dimensions.padding);
    } else {
      titleField.position.set(dimensions.padding, dimensions.padding);
    }
    this.drawOver(titleField.x, titleField.y, titleField.width, titleField.height);
    this.addChild(this.over);
  }

  public makeDescription(text: string, titleHeight: number) {
    let lines = text.split('\n').map(str => str.split(/[<>]/).filter(v => v !== ''));
    let tabs = 0;

    console.log('lines', lines);
    let y = titleHeight + dimensions.padding * 3;
    let font: PIXI.TextStyleOptions = fontStyles.n;
    let last: PIXI.Text;

    lines.forEach(line => {
      last = null;
      line.forEach(part => {
        if (part === 'b') {
          font = fontStyles.b;
        } else if (part === 'n') {
          font = fontStyles.n;
        } else if (part === 'h') {
          font = fontStyles.h;
        } else if (part === 't') {
          font = fontStyles.t;
        } else if (part === 'g') {
          font = fontStyles.g;
        } else if (part === 'tab') {
          tabs += dimensions.tab;
        } else if (part === '/tab') {
          tabs -= dimensions.tab;
        } else if (part === 'line') {
          y += Number(font.fontSize);
          this.over.lineStyle(2, colors.INNER_LINE).moveTo(0, y).lineTo(dimensions.width, y);
          y += Number(font.fontSize) / 2;
        } else {
          let field = new PIXI.Text(part, font);
          field.position.set(last ? (last.x + last.width) : (dimensions.padding + tabs), y);
          last = field;
          this.addChild(field);
          this.drawOver(field.x, field.y, field.width, field.height);
        }
      });
      if (last) {
        y = last.y + last.height;
      } else {
        this.drawOver(dimensions.padding, y, 10, Number(font.fontSize), true);
        y += Number(font.fontSize);
      }
    });

    return y;
  }

  public drawOver(x: number, y: number, width: number, height: number, blank?: boolean) {
    if (blank) {
      // this.over.lineStyle(1, 0x00ffff).drawRect(x, y, 10, height);
    } else {
      // this.over.lineStyle(1, 0xff00ff).drawRect(x, y, width, height);
    }
  }

  public drawFrame(titleHeight: number, totalHeight: number) {
    this.background = new PIXI.Graphics();
    this.background.beginFill(colors.SHADOW, 0.5)
      .drawRect(5, 5, dimensions.width, totalHeight + dimensions.padding * 2);
    this.background.beginFill(colors.BACK);
    this.background.lineStyle(3, colors.BORDER);
    this.background.drawRect(0, 0, dimensions.width, titleHeight + dimensions.padding * 2);
    this.background.drawRect(0, titleHeight + dimensions.padding * 2, dimensions.width, totalHeight - titleHeight);
    this.addChildAt(this.background, 0);
  }

  public drawCircle(content: string, indent: number, titleHeight: number) {
    this.background.beginFill(colors.BACK)
      .lineStyle(3, colors.BORDER)
      .drawCircle(indent / 3, titleHeight / 2, indent * 2 / 3);

    let circleText = new PIXI.Text(content, circleStyle);
    this.addChild(circleText);
    circleText.position.set(indent / 3 - circleText.width / 2, titleHeight / 2 - circleText.height / 2);
    this.drawOver(circleText.x, circleText.y, circleText.width, circleText.height);
  }

  public reposition(target: PIXI.Rectangle, borders: PIXI.Rectangle) {
    let rect = new PIXI.Rectangle(0, 0, this.background.width, this.background.height);
    if (target.y + target.height + rect.height > borders.bottom) {
      this.y = target.y - rect.height;
    } else {
      this.y = target.y + target.height;
    }
    if (target.x + rect.width > borders.right) {
      this.x = target.x + target.width - rect.width;
    } else {
      this.x = target.x;
    }
  }
}

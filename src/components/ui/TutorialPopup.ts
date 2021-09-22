import * as PIXI from 'pixi.js';
import { Fonts } from '../../data/Fonts';
export class TutorialPopup extends PIXI.Container {

  constructor(text: string) {
    super();
    let background = new PIXI.Graphics();
    background.beginFill(0xffffff);
    background.drawRect(0, 0, 300, 100);

    let field = new PIXI.Text(text, {fontFamily: Fonts.UI});

    this.addChild(background, field);
  }
}

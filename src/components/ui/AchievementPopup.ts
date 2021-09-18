import * as PIXI from 'pixi.js';
import { JMTween } from '../../JMGE/JMTween';
import { Config } from '../../Config';
import { Fonts } from '../../data/Fonts';

export class AchievementPopup extends PIXI.Container {
  constructor(title: string, text: string, tier: number = 0) {
    super();

    this.x = Config.INIT.SCREEN_WIDTH / 2 - 150;
    this.y = 50;

    let background = new PIXI.Graphics();
    background.beginFill(0xffffff, 0.2);
    background.lineStyle(2, 0xffffff);
    background.drawRect(0, 0, 300, 100);

    let titleField = new PIXI.Text(title, {fill: 0xffffff, fontFamily: Fonts.UI, fontWeight: 'bold', fontSize: 12});

    let field = new PIXI.Text(text, {fill: 0xffffff, fontFamily: Fonts.UI, fontSize: 11});

    this.addChild(background, titleField, field);
    titleField.position.set((300 - titleField.width) / 2, 5);
    field.position.set(5, 20);
    new JMTween<AchievementPopup>(this, 200).from({alpha: 0}).start().chain(this, 1000).to({alpha: 0}).wait(5000).onComplete(() => {
      this.destroy();
    });
  }
}

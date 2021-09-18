import * as PIXI from 'pixi.js';
import { BaseModal } from './_BaseModal';
import { Button } from '../Button';
import { JMTween, JMEasing } from '../../../JMGE/JMTween';
import { Fonts } from '../../../data/Fonts';

const COLOR_BACK = 0x333333;
const COLOR_FRONT = 0x666666;
const CLOSE_TEXT = 'Close';
const HEIGHT = 300;
const WIDTH = 400;

export class SimpleModal extends BaseModal {
  private scoreText: PIXI.Text;
  private closeButton: Button;

  constructor(message: string, private onClose?: () => void) {
    super();

    this.pivot.set(WIDTH / 2, HEIGHT / 2);

    let shadow = new PIXI.Graphics();
    shadow.beginFill(0, 0.4).drawRoundedRect(5, 5, WIDTH + 10, HEIGHT + 10, 10);
    this.addChild(shadow);
    let background = new PIXI.Graphics();
    background.lineStyle(3, COLOR_FRONT).beginFill(COLOR_BACK).drawRoundedRect(0, 0, WIDTH, HEIGHT, 10);
    this.addChild(background);

    this.scoreText = new PIXI.Text(message, { fontSize: 25, fontFamily: Fonts.UI, fill: COLOR_FRONT, wordWrap: true, wordWrapWidth: WIDTH - 100 });
    this.addChild(this.scoreText);
    this.scoreText.height = Math.min(this.scoreText.height, HEIGHT * 5 / 8);
    this.scoreText.scale.x = this.scoreText.scale.y;

    this.scoreText.position.set(50, 50);
    if (onClose !== null) {
      this.closeButton = new Button({
        label: CLOSE_TEXT,
        width: WIDTH / 4,
        height: WIDTH / 8,
        onClick: this.closeModal,
        color: COLOR_FRONT,
        labelStyle: {fill: COLOR_BACK},
      });

      this.addChild(this.closeButton);

      this.closeButton.y = HEIGHT - this.closeButton.height - 20;
      this.closeButton.x = WIDTH / 20 * 2 + WIDTH / 4;
    }
  }

  public closeModal = () => {
    this.animating = true;
    this.tween = new JMTween(this.scale, 300).to({x: 0, y: 0}).easing(JMEasing.Back.In).start().onComplete(() => {
      this.tween = null;
      this.animating = false;
      this.destroy();
      this.onClose && this.onClose();
    });
  }
}

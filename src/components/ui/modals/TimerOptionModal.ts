import * as PIXI from 'pixi.js';
import { BaseModal } from './_BaseModal';
import { Button } from '../Button';
import { JMTween, JMEasing } from '../../../JMGE/JMTween';
import { Fonts } from '../../../data/Fonts';

const COLOR_BACK = 0x333333;
const COLOR_FRONT = 0x666666;
const COLOR_TIMER = 0x999999;
const HEIGHT = 300;
const WIDTH = 400;

export class TimerOptionModal extends BaseModal {
  private scoreText: PIXI.Text;
  private closed: boolean;

  constructor(message: string, options: {label: string, color?: number, onClick?: () => void, timer?: number}[]) {
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
    this.scoreText.width = Math.min(this.scoreText.height, HEIGHT * 6 / 8);
    this.scoreText.scale.x = this.scoreText.scale.y;

    this.scoreText.position.set(50, 50);

    options.forEach((option, i) => {
      let button = new Button({
        label: option.label,
        width: WIDTH / 4,
        height: HEIGHT / 8,
        onClick: () => this.closeModal(option.onClick),
        color: option.color || COLOR_FRONT,
        labelStyle: {fill: COLOR_BACK},
      });

      this.addChild(button);

      button.y = HEIGHT - button.height - 20;
      button.x = (7 + 4 * i - 2 * options.length) * WIDTH * 3 / 40;

      if (option.timer) {
        let timeT = new PIXI.Text(String(option.timer), {fontFamily: Fonts.UI, fontSize: 20, fill: COLOR_TIMER});
        timeT.position.set (button.x + WIDTH / 8, button.y - 25);
        this.addChild(timeT);

        new JMTween({time: option.timer}, option.timer * 1000).to({time: 0}).start().onUpdate(timer => {
          timeT.text = String(Math.ceil(timer.time));
        }).onComplete(timer => {
          this.closeModal(option.onClick);
        });
      }
    });
  }

  public closeModal = (onComplete?: () => void) => {
    if (this.closed) return;

    this.closed = true;
    this.animating = true;
    this.tween = new JMTween(this.scale, 300).to({x: 0, y: 0}).easing(JMEasing.Back.In).start().onComplete(() => {
      this.tween = null;
      this.animating = false;
      this.destroy();
      onComplete && onComplete();
    });
  }
}

import * as PIXI from 'pixi.js';
import { JMTween } from '../../JMGE/JMTween';
import { Fonts } from '../../data/Fonts';

export class PauseOverlay extends PIXI.Container {
  private currentTween: JMTween;
  private state: boolean = false;

  constructor(bounds: PIXI.Rectangle) {
    super();

    let background = new PIXI.Graphics();
    background.beginFill(0x333333, 0.3);
    background.drawRect(bounds.x, bounds.y, bounds.width, bounds.height);

    let text = new PIXI.Text('- PAUSED -', {fill: 0xffff00, fontFamily: Fonts.UI, fontSize: 30, fontWeight: 'bold'});
    text.position.set((bounds.width - text.width) / 2, (bounds.height - text.height) / 2);
    this.addChild(background, text);
    // this.visible = false;
    this.alpha = 0;
  }

  public changeState(b: boolean) {
    if (b !== this.state) {
      this.state = b;
      if (this.currentTween) {
        this.currentTween.stop();
      }
      if (b) {
        this.currentTween = new JMTween<PauseOverlay>(this, 200).to({alpha: 1}).start().onComplete(() => this.currentTween = null);
      } else {
        this.currentTween = new JMTween<PauseOverlay>(this, 200).to({alpha: 0}).start().onComplete(() => this.currentTween = null);
      }
    }
  }
}

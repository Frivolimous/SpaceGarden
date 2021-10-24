import * as PIXI from 'pixi.js';
import { JMTween } from '../../../JMGE/JMTween';
import { Colors } from '../../../data/Colors';
import { TextureCache } from '../../../services/TextureCache';
import { CrawlerSlug } from '../../../data/CrawlerData';

export class CrawlerView extends PIXI.Container {
  public sprite: PIXI.Sprite;
  public _Highlight: PIXI.Graphics;

  constructor(private slug: CrawlerSlug) {
    super();
    this.sprite = new PIXI.Sprite(TextureCache.getGraphicTexture(slug));
    this.sprite.tint = Colors.Node.orange;
    this.sprite.anchor.set(0.5);

    this._Highlight = new PIXI.Graphics();
    this._Highlight.beginFill(0xffff00, 0.5);
    this._Highlight.drawCircle(0, 0, 10);
    this._Highlight.visible = false;

    this.addChild(this._Highlight, this.sprite);
  }

  get highlight(): boolean {
    return this._Highlight.visible;
  }

  set highlight(b: boolean) {
    this._Highlight.visible = b;
  }

  public animateDie(onComplete: () => void) {
    new JMTween(this.sprite, 1000).colorTo({tint: 0}).to({alpha: 0}).start().onComplete(onComplete);
  }
}

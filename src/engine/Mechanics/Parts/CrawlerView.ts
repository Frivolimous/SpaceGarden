import * as PIXI from 'pixi.js';
import { Colors } from '../../../data/Colors';
import { TextureCache } from '../../../services/TextureCache';

export class CrawlerView extends PIXI.Container {
  public sprite: PIXI.Sprite;

  constructor() {
    super();
    this.sprite = new PIXI.Sprite(TextureCache.getGraphicTexture('crawler'));
    this.sprite.tint = Colors.Node.orange;
    this.sprite.anchor.set(0.5);

    this.addChild(this.sprite);
  }
}

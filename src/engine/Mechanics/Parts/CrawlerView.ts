import * as PIXI from 'pixi.js';
import { JMTween } from '../../../JMGE/JMTween';
import { Colors } from '../../../data/Colors';
import { TextureCache } from '../../../services/TextureCache';
import { CrawlerSlug } from '../../../data/CrawlerData';
import { PlantNodeView } from '../../nodes/PlantNodeView';
import { ColorGradient } from '../../../JMGE/others/Colors';

export class CrawlerView extends PIXI.Container {
  public sprite: PIXI.Sprite;
  public _Highlight: PIXI.Graphics;
  public _Buff: PIXI.Graphics;

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

  public colorTo(color: number) {
    new JMTween(this.sprite, 500).colorTo({tint: color}).start();
  }

  public animateDie(onComplete: () => void) {
    new JMTween(this.sprite, 1000).colorTo({tint: 0}).to({alpha: 0}).start().onComplete(onComplete);
  }

  public addBuff() {
    if (!this._Buff) {
      this._Buff = new PIXI.Graphics();
      this._Buff.beginFill(0xffffff, 0.2);
      this._Buff.drawCircle(0, 0, 8);
      this.addChild(this._Buff);
    }

    this._Buff.visible = true;
  }

  public removeBuff() {
    this._Buff.visible = false;
  }

  public positionAt(node: PlantNodeView, offset?: number, angle?: number) {
    if (offset) {
      offset *= node.radius;
      this.x = node.x + offset * Math.cos(angle);
      this.y = node.y + offset * Math.sin(angle);
    } else {
      this.x = node.x;
      this.y = node.y;
    }
  }
  public vibrate(node: PlantNodeView) {
    this.positionAt(node, Math.random() * 0.06, Math.random() * Math.PI * 2);
  }

  public positionBetween(origin: PlantNodeView, target: PlantNodeView, offset: number) {
    this.x = origin.x + (target.x - origin.x) * offset;
    this.y = origin.y + (target.y - origin.y) * offset;
  }
}

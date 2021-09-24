import * as PIXI from 'pixi.js';
import * as _ from 'lodash';

import { INodeConfig } from '../../data/NodeData';
import { Fonts } from '../../data/Fonts';
import { GameNode } from '../Mechanics/Parts/GameNode';
import { colorLuminance } from '../../JMGE/others/Colors';
import { Config } from '../../Config';
import { JMTween } from '../../JMGE/JMTween';

export class FDGNode extends PIXI.Container {
  public data: GameNode;

  public v = new PIXI.Point(0, 0);

  public _iMass = 0;
  public vR = 0.005;

  public fixed = false;
  public hasMass = true;

  public _Intensity: number;
  public targetIntensity: number = 1;

  public sprite: PIXI.Sprite;
  public _Highlight: PIXI.Graphics;
  public text = new PIXI.Text('2/2', { fontSize: 12, fontFamily: Fonts.FLYING, stroke: 0, strokeThickness: 1, fill: 0xffffff });

  constructor(texture: PIXI.Texture, public config: INodeConfig) {
    super();
    _.defaults(config, dFDGNode);

    this.sprite = new PIXI.Sprite(texture);
    this.sprite.anchor.set(0.5);

    this.text.anchor.set(0.5);
    this.text.visible = false;

    this._Highlight = new PIXI.Graphics();
    this._Highlight.beginFill(0xffff00, 0.5);
    this._Highlight.drawCircle(0, 0, Math.max(config.radius * 1.2, config.radius + 4));
    this._Highlight.visible = false;

    this.addChild(this._Highlight, this.sprite, this.text);

    this._iMass = 1 / config.mass;
  }

  get iMass(): number {
    return this.hasMass ? this._iMass : 5;
  }

  set ghostMode(b: boolean) {
    if (b) {
      this.alpha = 0.5;
      this.hasMass = false;
    } else {
      this.alpha = 1;
      this.hasMass = true;
    }
  }

  get highlight(): boolean {
    return this._Highlight.visible;
  }

  set highlight(b: boolean) {
    this._Highlight.visible = b;
  }

  public setIntensity(n: number, instant?: boolean) {
    this.targetIntensity = Math.min(0.3 + 0.7 * n, 1);
    if (!this._Intensity || instant) this._Intensity = this.targetIntensity * 0.9;
  }

  public linkNode(target: FDGNode, forceOutlet = false) {
    this.data.linkNode(target.data, forceOutlet);
  }

  public removeNode(node: FDGNode) {
    this.data.removeNode(node.data);
  }

  public removeAllNodes() {
    this.data.removeAllNodes();
  }

  public moveBody = () => {
    this.rotate();

    if (this.fixed) return;

    this.v.x *= Config.PHYSICS.DAMP;
    this.v.y *= Config.PHYSICS.DAMP;

    if (Math.abs(this.v.x) > Config.PHYSICS.MIN_V || Math.abs(this.v.y) > Config.PHYSICS.MIN_V) {
      this.x += this.v.x;
      this.y += this.v.y;
    } else {
      this.v.set(0, 0);
    }
  }

  public adjustIntensity = () => {
    if (this._Intensity !== this.targetIntensity) {
      if (this._Intensity < this.targetIntensity) {
        this._Intensity = Math.min(this._Intensity + 0.01, this.targetIntensity);
      } else if (this._Intensity > this.targetIntensity) {
        this._Intensity = Math.max(this._Intensity - 0.01, this.targetIntensity);
      }
      this.sprite.tint = colorLuminance(this.config.color, this._Intensity);
      this.sprite.scale.set(this._Intensity);
    }
  }

  public showConnectionCount(show: boolean) {
    if (show) {
      this.text.text = `${this.data.outlets.length}/${this.config.maxLinks}`;
      let remaining = this.config.maxLinks - this.data.outlets.length;
      if (remaining <= 0) {
        this.text.tint = 0xff0000;
      } else if (remaining === 1) {
        this.text.tint = 0xcccc00;
      } else {
        this.text.tint = 0xffffff;
      }
      this.text.visible = true;
    } else {
      this.text.visible = false;
    }
  }

  public isConnectedToCore(): boolean {
    return this.data.checkConnections(node => node.config.slug === 'core');
  }

  public rotate() {
    this.sprite.rotation += this.vR;
    if (this.sprite.rotation > Math.PI) this.sprite.rotation -= Math.PI * 2;
    if (this.sprite.rotation < -Math.PI) this.sprite.rotation += Math.PI * 2;
  }

  public tick = () => {
    this.moveBody();
    this.adjustIntensity();
  }

  public pulse(color: number) {
    let pulseCircle = new PIXI.Graphics();
    pulseCircle.beginFill(color);
    pulseCircle.drawCircle(0, 0, Math.max(this.config.radius * 1.2, this.config.radius + 4));
    pulseCircle.alpha = 0;
    this.addChildAt(pulseCircle, 0);

    new JMTween(pulseCircle, 100).to({alpha: 0.5}).start().chain(pulseCircle, 300).to({alpha: 0}).onComplete(() => {
      pulseCircle.destroy();
    });
  }
}

export interface INodeData {
  outlets: INodeData[];
  view: FDGNode;
}

const dFDGNode: Partial<INodeConfig> = {
  color: 0xffffff,
  radius: 10,
  mass: 1,
  force: 1,
};

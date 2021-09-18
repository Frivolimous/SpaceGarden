import * as PIXI from 'pixi.js';
import * as _ from 'lodash';

import { NodeConfig } from '../../data/NodeData';
import { Fonts } from '../../data/Fonts';
import { GameNode } from '../Mechanics/Parts/GameNode';
import { colorLuminance } from "../../JMGE/others/Colors";
import { Config } from '../../Config';


export class FDGNode extends PIXI.Container {
  public data: GameNode;

  public v = new PIXI.Point(0, 0);

  public _iMass = 0;
  public vR = 0.005;

  public fixed = false;
  public hasMass = true;
  public target: { x: number, y: number };

  public sprite: PIXI.Sprite;
  public text = new PIXI.Text('2/2', { fontSize: 12, fontFamily: Fonts.FLYING, stroke: 0, strokeThickness: 1, fill: 0xffffff });

  constructor(texture: PIXI.Texture, public config: NodeConfig) {
    super();
    _.defaults(config, dFDGNode);

    this.sprite = new PIXI.Sprite(texture);
    this.sprite.anchor.set(0.5, 0.5);

    this.text.anchor.set(0.5);
    this.text.visible = false;

    this.addChild(this.sprite, this.text);

    this._iMass = 1 / config.mass;
  }

  get iMass(): number {
    return this.hasMass ? this._iMass : 5;
  }

  set intensity(n: number) {
    n = 0.3 + 0.7 * n;
    this.sprite.tint = colorLuminance(this.config.color, n);
    this.sprite.scale.set(n);
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

  linkNode(target: FDGNode, forceOutlet = false) {
    this.data.linkNode(target.data, forceOutlet);
  }

  removeNode(node: FDGNode) {
    this.data.removeNode(node.data);
  }

  removeAllNodes() {
    this.data.removeAllNodes();
  }

  moveBody = () => {
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

  showConnectionCount(show: boolean) {
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

  isConnectedToCore(): boolean {
    return this.data.checkConnections(node => node.config.name === 'core');
  }

  rotate() {
    this.sprite.rotation += this.vR;
    if (this.sprite.rotation > Math.PI) this.sprite.rotation -= Math.PI * 2;
    if (this.sprite.rotation < -Math.PI) this.sprite.rotation += Math.PI * 2;
  }

  tick = () => {
    this.moveBody();
  }
}

export interface INodeData {
  outlets: INodeData[];
  view: FDGNode;
}

const dFDGNode: Partial<NodeConfig> = {
  color: 0xffffff,
  radius: 10,
  mass: 1,
  force: 1,
}

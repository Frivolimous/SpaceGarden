import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { JMTween } from '../../JMGE/JMTween';
import { colorLuminance } from '../../JMGE/others/Colors';
import { FDGNode } from './FDGNode';

interface IBlob {
  x: number;
  y: number;
  color: number;
  size: number;
  fade: number;
}
export class FDGLink {
  public onTick = new JMEventListener();
  public length = 40;
  public intensity = 0;
  public active = true;

  private color = 0xFFFFFF;
  private lineStyle = 2;
  private blobs: IBlob[] = [];

  constructor(public origin: FDGNode, public target: FDGNode) {
    this.color = target.config.color;
    if (origin.data.isFruit() || target.data.isFruit()) {
      this.length = (origin.config.radius + target.config.radius);
    } else {
      this.length = (origin.config.radius + target.config.radius) * 1.5;
    }
  }

  public hasNode(node1: FDGNode, node2?: FDGNode): boolean {
    if (this.origin === node1) {
      if (!node2 || this.target === node2) {
        return true;
      }
    } else if (this.target === node1) {
      if (!node2 || this.origin === node2) {
        return true;
      }
    }

    return false;
  }

  public other(node: FDGNode) {
    return this.origin === node ? this.target : this.origin;
  }

  public getColor() {
    return colorLuminance(this.color, 0.3 + 0.7 * this.intensity);
  }

  public flash(): JMTween {
    this.intensity = 1;
    return new JMTween((this as FDGLink), 300).to({ intensity: 0 }).start();
  }

  public zip(origin: FDGNode, color: number, fade: number, onComplete: () => void): JMTween {
    let blob: IBlob = {x: origin.x, y: origin.y, color, size: 2, fade};
    let target = this.other(origin);
    this.blobs.push(blob);
    return new JMTween({percent: 0}, 300).to({percent: 1}).start().onUpdate(data => {
      if (!origin.parent || !target.parent) return;
      blob.x = origin.x + (target.x - origin.x) * data.percent;
      blob.y = origin.y + (target.y - origin.y) * data.percent;
    }).onComplete(() => {
      _.pull(this.blobs, blob);
      onComplete();
    });
  }

  public drawTo(canvas: PIXI.Graphics) {
    canvas.lineStyle(this.lineStyle, this.getColor())
        .moveTo(this.origin.x, this.origin.y)
        .lineTo(this.target.x, this.target.y);

    this.blobs.forEach(blob => {
      canvas.lineStyle(0).beginFill(blob.color).drawCircle(blob.x, blob.y, blob.size);
    });
  }
}

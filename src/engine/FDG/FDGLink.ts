import * as PIXI from 'pixi.js';
import _ from 'lodash';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { JMTween } from '../../JMGE/JMTween';
import { colorLuminance } from '../../JMGE/others/Colors';
import { PlantNode } from '../nodes/PlantNode';

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

  constructor(public origin: PlantNode, public target: PlantNode) {
    this.color = target.view.color;
    if (origin.isFruit() || target.isFruit()) {
      this.length = (origin.view.radius + target.view.radius);
    } else {
      this.length = (origin.view.radius + target.view.radius) * 1.5;
    }
  }

  public hasNode(node1: PlantNode, node2?: PlantNode): boolean {
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

  public other(node: PlantNode) {
    return this.origin === node ? this.target : this.origin;
  }

  public getColor() {
    return colorLuminance(this.color, 0.3 + 0.7 * this.intensity);
  }

  public flash(): JMTween {
    this.intensity = 1;
    return new JMTween((this as FDGLink), 300).to({ intensity: 0 }).start();
  }

  public zip(origin: PlantNode, color: number, fade: number, amped: boolean, onComplete: () => void): JMTween {
    let blob: IBlob = {x: origin.view.x, y: origin.view.y, color, size: amped ? 3 : 2, fade};
    let target = this.other(origin);
    this.blobs.push(blob);
    return new JMTween({percent: 0}, 300).to({percent: 1}).start().onUpdate(data => {
      if (!origin.view.parent || !target.view.parent) return;
      blob.x = origin.view.x + (target.view.x - origin.view.x) * data.percent;
      blob.y = origin.view.y + (target.view.y - origin.view.y) * data.percent;
    }).onComplete(() => {
      _.pull(this.blobs, blob);
      onComplete();
    });
  }

  public drawTo(canvas: PIXI.Graphics) {
    canvas.lineStyle(this.lineStyle, this.getColor())
        .moveTo(this.origin.view.x, this.origin.view.y)
        .lineTo(this.target.view.x, this.target.view.y);

    this.blobs.forEach(blob => {
      canvas.lineStyle(0).beginFill(blob.color).drawCircle(blob.x, blob.y, blob.size);
    });
  }
}

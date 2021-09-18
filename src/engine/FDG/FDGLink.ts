import { JMEventListener } from "../../JMGE/events/JMEventListener";
import { JMTween } from "../../JMGE/JMTween";
import { colorLuminance } from "../../JMGE/others/Colors";
import { FDGNode } from "./FDGNode";

export class FDGLink {
  public onTick = new JMEventListener();
  public length = 40;
  public color = 0xFFFFFF;
  public lineStyle = 2;
  public intensity = 0;
  public active = true;

  constructor(public origin: FDGNode, public target: FDGNode) {
    this.color = target.config.color;
    if (origin.config.type === 'fruit' || target.config.type === 'fruit') {
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

  public getColor() {
    return colorLuminance(this.color, 0.3 + 0.7 * this.intensity);
  }

  public flash() {
    this.intensity = 1;
    new JMTween(<FDGLink>this, 300).to({ intensity: 0 }).start();
  }

  public other(node: FDGNode) {
    return this.origin === node ? this.target : this.origin;
  }
}

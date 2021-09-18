import * as PIXI from 'pixi.js';
import { ScrollingContainer } from '../components/ScrollingContainer';
import { FDGContainer, IPull } from '../engine/FDG/FDGContainer';
import { FDGNode } from '../engine/FDG/FDGNode';
import { JMEventListener } from '../JMGE/events/JMEventListener';

export class MouseController {
  // objects: IPull[] = [];

  public onDelete: JMEventListener = new JMEventListener<FDGNode>();
  public deleteNext = false;

  public currentPull: IPull;

  public down = false;

  public constructor(private canvas: ScrollingContainer, private container: FDGContainer) {
    canvas.background.addListener('pointerdown', this.onMouseDown);
    window.addEventListener('pointerup', this.onMouseUp);
    canvas.background.addListener('pointermove', this.onMouseMove);
  }

  public destroy() {
    this.canvas.background.removeListener('pointerdown', this.onMouseDown);
    window.removeEventListener('pointerup', this.onMouseUp);
    this.canvas.background.removeListener('pointermove', this.onMouseMove);
  }

  public onMouseDown = (e: PIXI.interaction.InteractionEvent) => {
    if (this.deleteNext) {
      let position = e.data.getLocalPosition(this.container);

      let node = this.container.getClosestObject({ x: position.x, y: position.y, notType: 'core', notFruit: true });
      if (node) {
        this.container.removeNode(node);
      }

      this.deleteNext = false;
      this.onDelete.publish(node);

      return;
    }
    if (this.down) return;
    this.down = true;

    let position = e.data.getLocalPosition(this.container);

    let node = this.container.getClosestObject({ x: position.x, y: position.y, notFruit: true });

    if (node) {
      let pull = { x: position.x, y: position.y, node };
      this.currentPull = pull;
      this.container.addPull(pull);
    } else {
      this.canvas.diff = { oX: position.x, oY: position.y, tX: position.x, tY: position.y };
    }
  }

  public startDrag(config: IPull) {
    this.currentPull = config;
    this.container.addPull(config);
  }

  public onMouseUp = () => {
    if (this.currentPull) {
      this.container.removePull(this.currentPull);
      if (this.currentPull.onRelease) {
        // let position = e.data.getLocalPosition(this.container);
        this.currentPull.onRelease();
      }
      this.currentPull = null;
    }
    if (this.canvas.diff) {
      this.canvas.diff = null;
    }

    this.down = false;
  }

  public onMouseMove = (e: PIXI.interaction.InteractionEvent) => {
    if (this.currentPull) {
      let position = e.data.getLocalPosition(this.container);
      this.currentPull.x = position.x;
      this.currentPull.y = position.y;
      this.currentPull.onMove && this.currentPull.onMove(position);
    } else if (this.canvas.diff) {
      let position = e.data.getLocalPosition(this.container);
      this.canvas.diff.tX = position.x;
      this.canvas.diff.tY = position.y;
    }
  }

  public deleteNextClicked = (e: { onComplete: () => void }) => {
    if (e && e.onComplete) {
      this.deleteNext = true;
      this.onDelete.addOnce(e.onComplete);
    } else {
      this.deleteNext = false;
      this.onDelete.clear();
    }
  }
}

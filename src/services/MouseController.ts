import * as PIXI from 'pixi.js';
import { ScrollingContainer } from '../components/ScrollingContainer';
import { FDGContainer, IPull } from '../engine/FDG/FDGContainer';
import { JMEventListener } from '../JMGE/events/JMEventListener';

export class MouseController {
  public onUp: JMEventListener = new JMEventListener<null>();
  public onMove: JMEventListener = new JMEventListener<{x: number, y: number}>();

  private nextClickEvent: { onDown?: (e: {x: number, y: number}) => void, onUp?: () => void};
  private currentPull: IPull;
  private down = false;

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

  public startDrag(config: IPull) {
    this.currentPull = config;
    this.container.addPull(config);
  }

  public setNextClickEvent = (data: { onDown?: (e: {x: number, y: number}) => void, onUp?: () => void}) => {
    this.nextClickEvent = data;
  }

  public clearNextClickEvent = () => {
    this.nextClickEvent = null;
  }

  private onMouseDown = (e: PIXI.FederatedPointerEvent) => {
    let position = e.data.getLocalPosition(this.container);
    if (this.nextClickEvent) {
      if (this.nextClickEvent.onDown) {
        this.nextClickEvent.onDown(position);
        if (!this.nextClickEvent.onUp) {
          this.nextClickEvent = null;
        }
        return;
      }
    }

    if (this.down) return;
    this.down = true;

    let node = this.container.getClosestObject({ x: position.x, y: position.y, notFruit: true });

    if (node) {
      let pull = { x: position.x, y: position.y, node };
      this.currentPull = pull;
      this.container.addPull(pull);
    } else {
      this.canvas.diff = { oX: position.x, oY: position.y, tX: position.x, tY: position.y };
    }
  }

  private onMouseUp = () => {
    this.onUp.publish();
    if (this.nextClickEvent) {
      if (this.nextClickEvent.onUp) {
        this.nextClickEvent.onUp();
        this.nextClickEvent = null;
        return;
      }
    }

    if (this.currentPull) {
      this.container.removePull(this.currentPull);
      if (this.currentPull.onRelease) {
        this.currentPull.onRelease();
      }
      this.currentPull = null;
    }
    if (this.canvas.diff) {
      this.canvas.diff = null;
    }

    this.down = false;
  }

  private onMouseMove = (e: PIXI.FederatedPointerEvent) => {
    let position = e.data.getLocalPosition(this.container);
    if (this.currentPull) {
      this.currentPull.x = position.x;
      this.currentPull.y = position.y;
      this.currentPull.onMove && this.currentPull.onMove(position);
    } else if (this.canvas.diff) {
      this.canvas.diff.tX = position.x;
      this.canvas.diff.tY = position.y;
    } else {
      this.onMove.publish(position);
    }
  }
}

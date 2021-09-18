import * as PIXI from 'pixi.js';

export class Scrollbar extends PIXI.Container {
  private back: PIXI.Graphics;
  private mover: PIXI.Graphics;
  private offsetY: number = null;
  private ratio: number = 1;
  private bottomY: number;
  private dragging = false;
  private backAlpha = 0.5;
  private moverAlpha = 0.5;

  constructor(width: number, height: number, private scrollCallback: (percent: number) => void, private backColor = 0x333333, private moverColor = 0x999999) {
    super();

    this.back = new PIXI.Graphics();
    this.addChild(this.back);
    this.mover = new PIXI.Graphics();
    this.addChild(this.mover);

    this.interactive = true;
    this.buttonMode = true;

    this.back.beginFill(this.backColor, this.backAlpha).drawRoundedRect(0, 0, width, height, width / 2);
    this.drawMover(this.ratio);

    this.addListener('pointerdown', this.startMove);
    this.addListener('mouseup', this.endMove);
    this.addListener('mouseupoutside', this.endMove);
    this.addListener('touchend', this.endMove);
    this.addListener('pointermove', this.mouseMove);
  }

  public resize = (width: number, height: number) => {
    this.back.clear().beginFill(this.backColor, this.backAlpha).drawRoundedRect(0, 0, width, height, width / 2);
    this.drawMover(this.ratio);
  }

  public drawMover = (p: number) => {
    // p = 0-1
    p = Math.min(1, Math.max(0, p));
    this.visible = (p < 1);
    this.mover.clear().beginFill(this.moverColor, this.moverAlpha).drawRoundedRect(0, 0, this.back.width, p * this.back.height, this.back.width / 2);
    this.bottomY = this.back.height - this.mover.height;
  }

  public setPosition = (p: number, andCallback: boolean = true) => {
    // p===0-1
    p = Math.min(Math.max(p, 0), 1);
    let y = p * (this.bottomY - 0) + 0;
    this.mover.y = y;
    andCallback && this.scrollCallback && this.scrollCallback(this.getPosition());
  }

  public getPosition = () => {
    // returns 0-1
    return this.mover.y / this.bottomY;
  }

  private startMove = (e: PIXI.interaction.InteractionEvent) => {
    let pos = e.data.getLocalPosition(this);
    this.offsetY = pos.y - this.y - this.mover.y;
  }

  private mouseMove = (e: PIXI.interaction.InteractionEvent) => {
    if (this.offsetY !== null) {
      let pos = e.data.getLocalPosition(this);
      let _y = pos.y - this.y - this.offsetY;
      _y = Math.max(_y, 0);
      _y = Math.min(_y, this.bottomY);
      this.mover.y = _y;
      this.scrollCallback && this.scrollCallback(this.getPosition());
    }
  }

  private endMove = (e: PIXI.interaction.InteractionEvent) => {
    this.offsetY = null;
  }
}

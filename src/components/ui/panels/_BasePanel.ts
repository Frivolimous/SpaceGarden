import * as PIXI from 'pixi.js';

export class BasePanel extends PIXI.Container {
  private background = new PIXI.Graphics();
  constructor(private bounds: PIXI.Rectangle, private bgColor: number = 0x333333) {
    super();
    this.addChild(this.background);
    this.drawBack();
  }

  public updateBounds(bounds: PIXI.Rectangle) {
    this.bounds = bounds;
    this.drawBack();
  }

  public getWidth() {
    return this.bounds.width;
  }

  public getHeight() {
    return this.bounds.height;
  }

  private drawBack() {
    this.background.clear().beginFill(this.bgColor).lineStyle(2).drawRoundedRect(0, 0, this.bounds.width, this.bounds.height, 10);
  }
}

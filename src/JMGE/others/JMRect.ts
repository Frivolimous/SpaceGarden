import * as PIXI from 'pixi.js';

export class JMRect extends PIXI.Rectangle {

  public set(x: number, y: number, width: number, height: number) {
    this.x = x;
    this.y = y;
    this.width = width;
    this.height = height;
  }

  public setLeft(n: number) {
    this.width += this.x - n;
    this.x = n;
  }

  public setRight(n: number) {
    this.width += n - this.right;
  }

  public setTop(n: number) {
    this.height -= n - this.y;
    this.y = n;
  }

  public setBot(n: number) {
    this.height += n - this.top;
  }
}

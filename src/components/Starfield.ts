import * as PIXI from 'pixi.js';

export class Starfield extends PIXI.Graphics {

  constructor(width: number, height: number, numStars: number) {
    super();
    this.beginFill(0)
      .drawRect(0, 0, width, height);

    this.beginFill(0xaaaaaa);
    for (let i = 0; i < numStars; i++) {
      this.drawCircle(
        Math.random() * width,
        Math.random() * height,
        1 + Math.random() * 1,
      );
    }
  }
}

import * as PIXI from 'pixi.js';
import { RandomSeed } from '../../services/RandomSeed';

interface IBackgroundTile {
  display: PIXI.Sprite;
  index: number;
  reversed: boolean;
}
export class Background  extends PIXI.Container {
  private tiles: IBackgroundTile[] = [];
  private index: number = 0;
  private zone: number = 0;
  private bossIndex = 13;
  private lastIndex = 14;
  private paralaxes: PIXI.Sprite[] = [];
  private paralaxRatio = 0.3;
  private layers: PIXI.Container[] = [new PIXI.Container(), new PIXI.Container(), new PIXI.Graphics()];
  private minX = 50;
  private maxX = 100;

  constructor(private backgroundSource: PIXI.Texture[], private paralaxSource: PIXI.Texture, private bounds: PIXI.Rectangle, private floorHeight = 300) {
    super();
    // this.paralaxSource = TextureCache.getTextureParalax(this.zone);
    this.layers.forEach(layer => this.addChild(layer));

    while (this.getLastX() < bounds.width) {
      this.addDisplay(0);
    }
    while (this.getLastParaX() < bounds.width) {
      this.addParalax();
    }

    this.redraw(bounds, floorHeight);
  }

  public redraw(bounds: PIXI.Rectangle, floorHeight = 300) {
    this.bounds = bounds;
    this.floorHeight = floorHeight;

    let bgHeight = this.tiles[0].display.height;
    this.layers.forEach(layer => {
      layer.y = bounds.y;
      layer.scale.set(bounds.height / bgHeight);
    });

    this.minX = (bounds.x) / this.layers[0].scale.x;
    this.maxX = this.minX + (bounds.width) / this.layers[0].scale.x;

    // (this.layers[2] as any).clear().lineStyle(2, 0xffff00).drawRect(this.minX, 50, this.maxX - this.minX, bgHeight - 100);
  }

  public scrollX(pixels: number) {
    if (this.tiles.length === 0) return;

    this.tiles.forEach(tile => tile.display.x -= pixels);

    while (this.tiles.length > 0 && this.tiles[0].display.x + this.tiles[0].display.width < this.minX) {
      this.tiles.shift().display.destroy();
    }

    while (this.tiles.length > 0 && this.tiles[this.tiles.length - 1].display.x > this.maxX) {
      this.tiles.pop().display.destroy();
    }
    if (this.tiles.length === 0) return;
    while (this.getLastX() < this.maxX) {
      let random = RandomSeed.general.getInt(1, 10);
      // if (this.index >= 4) {
      if (random <= 3 && this.index > 0) {
        this.addDisplay(this.index - 1, false, true);
        this.addDisplay(this.index - 2, false);
        this.index = this.index - 2;
      // } else if (this.index < 4) {
      } else if (random <= 6 && this.index < this.lastIndex) {
        this.addDisplay(this.index + 1);
        this.addDisplay(this.index + 2);
        this.index = this.index + 2;
      } else {
        this.addDisplay(this.index);
      }
    }
    while (this.getFirstX() > this.minX) {
      this.addDisplay(this.index, true);
    }

    while (this.paralaxes[0].x + this.paralaxes[0].width < this.minX) {
      this.paralaxes.shift().destroy();
    }
    while (this.paralaxes[this.paralaxes.length - 1].x > this.maxX) {
      this.paralaxes.pop().destroy();
    }
    this.paralaxes.forEach(p => p.x -= pixels * this.paralaxRatio);
    while (this.getLastParaX() < this.maxX) {
      this.addParalax();
    }
    while (this.getFirstParaX() > this.minX) {
      this.addParalax(true);
    }
  }

  public nextB() {
    this.index++;
    // this.display.texture = TextureCache.getTextureBackground(0, this.index);
  }

  private getFirstX(): number {
    return (this.tiles[0].display.x);
  }

  private getLastX(): number {
    if (this.tiles.length === 0) return this.minX;

    let last = this.tiles[this.tiles.length - 1];
    return last.display.x + last.display.width;
  }

  private addDisplay(index: number, first: boolean = false, reversed: boolean = false) {
    let tile: IBackgroundTile = {
      display: new PIXI.Sprite(this.backgroundSource[index]),
      reversed,
      index,
    };
    if (reversed) {
      tile.display.scale.x = -1;
      tile.display.anchor.set(1, 0);
    }
    this.layers[1].addChild(tile.display);

    if (first) {
      tile.display.x = this.getFirstX() - tile.display.width;
      this.tiles.unshift(tile);
    } else {
      tile.display.x = this.getLastX();
      this.tiles.push(tile);
    }
  }

  private addParalax(first: boolean = false) {
    let display = new PIXI.Sprite(this.paralaxSource);
    this.layers[0].addChild(display);

    if (first) {
      display.x = this.getFirstParaX() - display.width;
      this.paralaxes.unshift(display);
    } else {
      display.x = this.getLastParaX();
      this.paralaxes.push(display);
    }
  }

  private getFirstParaX(): number {
    return (this.paralaxes[0].x);
  }

  private getLastParaX(): number {
    if (this.paralaxes.length === 0) return this.minX;

    let last = this.paralaxes[this.paralaxes.length - 1];
    return last.x + last.width;
  }
}

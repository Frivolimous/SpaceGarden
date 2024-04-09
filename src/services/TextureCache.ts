import * as PIXI from 'pixi.js';
import { Colors } from '../data/Colors';

type TextureName = 'circle' | 'square' | 'triangle' |'thin-rect' | 'fat-rect' | 'pentagon' | 'hexagon' | 'firework' |
    'crawler-power' | 'crawler' | 'chieftain' | 'shaman';

export const enum TextureUrl {
  GHOST = 'www.nowhere.com/ghost.png',
}

export class TextureCache {
  public static initialize(app: PIXI.Application) {
    TextureCache.renderer = app.renderer;
    createGraphicTextures();
  }

  public static addTextureFromGraphic = (id: TextureName, graphic: PIXI.Graphics): PIXI.Texture => {
    if (TextureCache.cache[id]) {
      console.warn('overwriting texture', id);
    }

    // let m: PIXI.Texture = TextureCache.renderer.generateTexture(graphic, PIXI.SCALE_MODES.LINEAR, 1);
    let m = TextureCache.renderer.generateTexture(graphic, {scaleMode: PIXI.SCALE_MODES.LINEAR, resolution: 1});
    TextureCache.cache[id] = m;
    return m;
  }

  public static addTextureFromNodeGraphic = (id: TextureName, index: number, graphic: PIXI.Graphics): PIXI.Texture => {
    let s = `${id}-${index}`;
    if (TextureCache.cache[s]) {
      console.warn('overwriting texture', s);
    }

    let m: PIXI.Texture = TextureCache.renderer.generateTexture(graphic, {scaleMode: PIXI.SCALE_MODES.LINEAR, resolution: 1});
    TextureCache.cache[s] = m;
    return m;
  }

  public static getTextureFromUrl = (url: TextureUrl | string): PIXI.Texture => {
    if (TextureCache.cache[url]) {
      return TextureCache.cache[url];
    } else {
      // let m = PIXI.Texture.from(url, {crossorigin: true});
      let m = PIXI.Texture.WHITE;
      TextureCache.cache[url] = m;
      return m;
    }
  }

  public static getGraphicTexture = (id: TextureName): PIXI.Texture => {
    if (TextureCache.cache[id]) {
      return TextureCache.cache[id];
    } else {
      return PIXI.Texture.WHITE;
    }
  }

  public static getNodeGraphicTexture = (id: TextureName, index: number): PIXI.Texture => {
    index = Math.ceil(index / 5) * 5;
    let s = `${id}-${index}`;
    if (TextureCache.cache[s]) {
      return TextureCache.cache[s];
    } else {
      return TextureCache.cache['circle-5'];
    }
  }

  public static addTextureBackgrounds(i: number, a: string[]) {
    if (!TextureCache.backgrounds[i]) {
      TextureCache.backgrounds[i] = [];
    }

    for (let j = 0; j < a.length; j++) {
      let texture = TextureCache.getTextureFromUrl(a[j]);
      TextureCache.backgrounds[i][j] = texture;
    }
  }

  public static addTextureParalax(i: number, s: string) {
    TextureCache.paralaxes[i] = TextureCache.getTextureFromUrl(s);
  }

  public static getTextureBackgrounds(zone: number) {
    return TextureCache.backgrounds[zone];
  }

  public static getTextureParalax(zone: number) {
    return TextureCache.paralaxes[zone];
  }

  private static renderer: PIXI.IRenderer;
  private static cache: { [key: string]: PIXI.Texture } = {};
  private static backgrounds: PIXI.Texture[][] = [];
  private static paralaxes: PIXI.Texture[] = [];
}

function createGraphicTextures() {
    let graphic = new PIXI.Graphics();
    for (let i = 5; i <= 30; i += 5) {
      graphic.clear()
        .lineStyle(2, 0xffffff)
        .beginFill(0x333333);
      graphic.drawCircle(0, 0, i);
      TextureCache.addTextureFromNodeGraphic('circle', i, graphic);
    }

    let polyNames: TextureName[] = [
      'square',
      'triangle',
      'thin-rect',
      'fat-rect',
      'pentagon',
      'hexagon',
    ];

    let polygons = [
      [-1, -1, -1, 1, 1, 1, 1, -1],
      [-1, 0.75, 0, -1, 1, 0.75],
      [-1, -0.3, -1, 0.3, 1, 0.3, 1, -0.3],
      [-1, -0.7, -1, 0.7, 1, 0.7, 1, -0.7],
      [1, 0, (Math.sqrt(5) - 1) / 4, -Math.sqrt(10 + 2 * Math.sqrt(5)) / 4, -(Math.sqrt(5) + 1) / 4, -Math.sqrt(10 - 2 * Math.sqrt(5)) / 4, -(Math.sqrt(5) + 1) / 4, Math.sqrt(10 - 2 * Math.sqrt(5)) / 4, (Math.sqrt(5) - 1) / 4, Math.sqrt(10 + 2 * Math.sqrt(5)) / 4],
      [1, 0, 1 / 2, Math.sqrt(3) / 2, -1 / 2, Math.sqrt(3) / 2, -1, 0, -1 / 2, -Math.sqrt(3) / 2, 1 / 2, -Math.sqrt(3) / 2],
    ];

    for (let i = 0; i < polygons.length; i++) {
      for (let j = 5; j <= 30; j += 5) {
        graphic.clear()
        .lineStyle(2, 0xffffff)
        .beginFill(0x333333);

        graphic.drawPolygon(transformPolygon(polygons[i], j));
        TextureCache.addTextureFromNodeGraphic(polyNames[i], j, graphic);
      }
    }

    graphic.clear().beginFill(0xffffff)
      .drawCircle(0, 0, 4);
    TextureCache.addTextureFromGraphic('crawler', graphic);
    graphic.clear().beginFill(0xeeeeee)
      .drawCircle(0, 0, 5);
    TextureCache.addTextureFromGraphic('chieftain', graphic);
    graphic.clear().beginFill(0xffffff)
      .drawCircle(0, 0, 4)
      .beginFill(0xcccccc)
      .moveTo(0, -6).lineTo(-6, -1).lineTo(6, -1);
      // .drawRect(-3, -3, 6, 6);
    TextureCache.addTextureFromGraphic('shaman', graphic);

    graphic.clear().beginFill(0xffffff)
      .drawRect(0, 0, 2, 2);
    TextureCache.addTextureFromGraphic('crawler-power', graphic);

    graphic = new PIXI.Graphics();
    graphic.beginFill(0xffffff);
    graphic.drawCircle(0, 0, 5);
    TextureCache.addTextureFromGraphic('firework', graphic);
}

function transformPolygon(poly: number[], scale: number) {
  poly = poly.map(v => v * scale);
  poly.push(poly[0]);
  poly.push(poly[1]);

  return poly;
}

/**
 * @class Use to create a simple even gradient from one color to the next, as retrieved through 'getColorAt'.
 */
export class ColorGradient {
  //
  private R: number;
  private G: number;
  private B: number;
  /**
   * @class Use to create a simple even gradient from one color to the next, as retrieved through 'getColorAt'.
   * @param startColor color at position 0
   * @param endColor color at position 1
   */
  constructor(private startColor: number, endColor: number) {
    this.R = Math.floor(endColor / 0x010000) - Math.floor(startColor / 0x010000);
    this.G = Math.floor((endColor % 0x010000) / 0x000100) - Math.floor((startColor % 0x010000) / 0x000100);
    this.B = Math.floor(endColor % 0x000100) - Math.floor(startColor % 0x000100);
  }
  /**
   * @function get the color value at a specified percent
   * @param percent accepts a number from 0-1.  Numbers lower or higher will be clamped to range
   */
  public getColorAt = (percent: number): number => {
    percent = Math.min(1, Math.max(0, percent));

    return this.startColor + Math.floor(this.R * percent) * 0x010000 + Math.floor(this.G * percent) * 0x000100 + Math.floor(this.B * percent);
  }
}

/**
 * @function multiply all color channels by a specified value
 * @param color the color to multiply
 * @param lum amount to multiply by (ie 0.2 = 20% Lighter, -0.2 = 20% darker)
 */
export function colorLuminance(color: number, lum: number = 0): number {
  // lum: 1 is color, less for darker and more for lighter
  let r = Math.floor(color / 0x010000);
  let g = Math.floor((color % 0x010000) / 0x000100);
  let b = color % 0x000100;

  r = Math.min(Math.max(Math.round(r * lum), 0), 255);
  g = Math.min(Math.max(Math.round(g * lum), 0), 255);
  b = Math.min(Math.max(Math.round(b * lum), 0), 255);

  return r * 0x010000 + g * 0x000100 + b;
}

/**
 * @function increase or decrease the lightness of a color
 * @param color the color to affect
 * @param add amount to increase or decrease (-100 to 100).
 */
export function adjustLightness(color: number | string, add: number): number {
  let obj = new ColorObject(color);
  obj.lightness += add;
  return obj.color;
}

/**
 * @function increase or decrease the saturation of a color
 * @param color the color to affect
 * @param add amount to increase or decrease (-100 to 100).
 */
export function adjustSaturation(color: number | string, add: number): number {
  let obj = new ColorObject(color);
  obj.saturation += add;
  return obj.color;
}

/**
 * @function replace the hue of a color maintaining saturation and lightness
 * @param color the color to affect
 * @param replaceWith the value to change the hue to (0-360, 0=red, 120=green, 240=blue)
 */
export function changeHue(color: number | string, replaceWith: number): number {
  let obj = new ColorObject(color);
  obj.hue = replaceWith;
  return obj.color;
}

/**
 * @class you can store a color here and easily affect its color channels either by RGB (red green blue) or by HSL (hue saturation lightness)
 */
export class ColorObject {
  public R: number;
  public G: number;
  public B: number;

  /**
   * @constructor specify either a number as 0xRRGGBB or string as #RRGGBB
   */
  constructor(color: string | number) {
    color = parseColor(color);
    this.color = color;
  }

  public toNumber() {
    return this.color;
  }

  public setColorFromString(color: string) {
    this.color = parseColor(color);
  }

  set color(n: number) {
    this.R = Math.floor(n / 0x010000);
    this.G = Math.floor((n % 0x010000) / 0x000100);
    this.B = Math.floor(n % 0x000100);
  }

  get color(): number {
    return Math.floor(this.R) * 0x010000 + Math.floor(this.G) * 0x0100 + Math.floor(this.B);
  }

  get hue(): number {
    return this.toHSL()[0];
  }

  set hue(n: number) {
    let hsl = this.toHSL();
    hsl[0] = n;
    this.fromHSL(hsl);
  }

  get saturation(): number {
    return this.toHSL()[1];
  }

  set saturation(n: number) {
    let hsl = this.toHSL();
    hsl[1] = n;
    this.fromHSL(hsl);
  }

  get lightness(): number {
    let lightness = this.toHSL()[2];
    return lightness;
  }

  set lightness(n: number) {
    let hsl = this.toHSL();
    hsl[2] = n;
    this.fromHSL(hsl);
  }

  /**
   * @function get an array of three values representing Hue, Saturation and Luminance for bulk adjustment.
   */
  public toHSL(): [number, number, number] {
    let r = this.R / 255;
    let g = this.G / 255;
    let b = this.B / 255;

    let maxC = Math.max(r, g, b);
    let minC = Math.min(r, g, b);

    let c = maxC - minC;

    let h: number;
    let s: number;
    let l: number;

    l = (maxC + minC) / 2;

    if (c === 0) {
      h = 0;
      s = 0;
    } else {
      switch (maxC) {
        case r:
          h = ((g - b) / c) % 6;
          break;
        case g:
          h = (b - r) / c + 2;
          break;
        case b:
          h = (r - g) / c + 4;
          break;
      }
      h *= 60;
      if (h < 0) {
        h += 360;
      }

      s = c / (1 - Math.abs(2 * l - 1));
    }

    return [h, s * 100, l * 100];
  }

  /**
   * @function takes an array of three values representing Hue, Saturation and Luminance for bulk adjustment.
   */
  public fromHSL([h, s, l]: [number, number, number]): number {
    h /= 360;
    s /= 100;
    l /= 100;
    h = Math.max(0, Math.min(1, h));
    s = Math.max(0, Math.min(1, s));
    l = Math.max(0, Math.min(1, l));
    if (s === 0) {
      this.R = this.G = this.B = l * 255;
    } else {
      let q = l < 0.5 ? l * (1 + s) : l + s - l * s;
      let p = 2 * l - q;
      this.R = this.hue2rgb(p, q, h + 1 / 3) * 255;
      this.G = this.hue2rgb(p, q, h) * 255;
      this.B = this.hue2rgb(p, q, h - 1 / 3) * 255;
    }
    // DebugLog.log("IN:",h,s,l);
    // DebugLog.log("OUT:",this.R,this.G,this.B);
    return this.color;
  }

  private hue2rgb(p: number, q: number, t: number) {
    if (t < 0) {
      t += 1;
    } else if (t > 1) {
      t -= 1;
    }

    if (t < 1 / 6) {
      return p + (q - p) * 6 * t;
    } else if (t < 1 / 2) {
      return q;
    } else if (t < 2 / 3) {
      return p + (q - p) * (2 / 3 - t) * 6;
    } else {
      return p;
    }
  }
}

/**
 * @function simplified form of the main parseColor function
 * @param colorDef specify either a number as 0xRRGGBB or string as #RRGGBB
 */
export function parseColor(color: string | number): number {
  if (typeof color === 'string') {
    let hashI = color.indexOf('#');
    if (hashI >= 0) {
      color = color.substr(hashI + 1);
      let c = parseInt('0x' + color, 16);
      return parseInt('0x' + color, 16);
    } else {
      return parseInt(color, 10);
    }
  }
  return color;
}

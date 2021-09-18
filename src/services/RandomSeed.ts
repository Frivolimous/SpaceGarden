import * as _ from 'lodash';

export class RandomSeed {

  public static makeRandomSeed = () => {
    let slug = '';
    for (let i = 0; i < 5; i++) {
      slug += _.sample(alphabet);
    }
    return new RandomSeed(slug);
  }

  public static general = RandomSeed.makeRandomSeed();

  // === \\

  private current: number = 0;

  constructor(public seed: string) {
    if (seed === '1') {
      this.getRaw = this.getRaw1;
    } else if (seed === '0') {
      this.getRaw = this.getRaw0;
    } else if (seed === 'random1') {
      this.getRaw = this.getRawTrueRandom;
    } else {
      let h = 1779033703 ^ seed.length;
      for(let i = 0; i < seed.length; i++) {
        h = Math.imul(h ^ seed.charCodeAt(i), 3432918353);
        h = h << 13 | h >>> 19;
      }
            
      h = Math.imul(h ^ h >>> 16, 2246822507);
      h = Math.imul(h ^ h >>> 13, 3266489909);
      this.current = (h ^= h >>> 16) >>> 0;
    }
  }

  public resetSeed() {
    this.current = 0;
  }

  public getRaw(): number {
    let t = this.current += 0x6D2B79F5;
    t = Math.imul(t ^ t >>> 15, t | 1);
    t ^= t + Math.imul(t ^ t >>> 7, t | 61);
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  }

  public getRawTrueRandom(): number {
    return Math.random();
  }

  public getRaw1(): number {
    return 1;
  }

  public getRaw0(): number {
    return 0;
  }

  public getInt(min: number, max: number): number {
    return Math.floor(min + (this.getRaw() * (max - min + 1)));
  }

  public get100() {
    let a: number[] = [];
    for (let i = 0; i < 100; i++) {
      a.push(this.getRaw());
    }
    return a;
  }
}

let alphabet = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'];

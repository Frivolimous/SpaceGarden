interface ITweenProperty {
  key: string;
  start?: number;
  end?: number;

  inc?: number;

  isColor?: boolean;
  incR?: number;
  incG?: number;
  incB?: number;

  eased?: boolean;

  from?: number;
  to?: number;
}

export class JMTween<T = any> {
  public static speedFactor = 1;
  private static running = false;
  private static tweens: JMTween[] = [];

  private static _add = (tween: JMTween) => {
    JMTween.tweens.push(tween);
    JMTween._tryRun();
  }

  private static _remove = (tween: JMTween) => {
    let index = JMTween.tweens.indexOf(tween);
    if (index >= 0) {
      JMTween.tweens.splice(index, 1);
    }
  }

  private static _tryRun = () => {
    if (!JMTween.running && JMTween.tweens.length > 0) {
      JMTween.running = true;
      requestAnimationFrame(JMTween._tick);
    }
  }
  private static _tick = (time: number) => {
    JMTween.running = false;
    JMTween.tweens.forEach(tween => tween.tickThis(time));
    if (!JMTween.running && JMTween.tweens.length > 0) {
      JMTween.running = true;
      requestAnimationFrame(JMTween._tick);
    }
  }

  public running = false;
  private tickThis: (time: number) => void;

  private onUpdateCallback: (object: T) => void;
  private onCompleteCallback: (object: T) => void;
  private onWaitCompleteCallback: (object: T) => void;
  private properties: ITweenProperty[] = [];
  private hasWait: boolean;
  private _Yoyo: boolean;
  private _Loop: boolean;
  private _Repeat: number;

  private nextTween: JMTween;

  private _Easing: (percent: number) => number;

  private waitTime: number;

  private startTime: number;
  private endTime: number;

  constructor(private object: T, private totalTime: number = 200) {
    this.tickThis = this.firstTick;
  }

  public onUpdate = (callback: (object: T) => void) => {
    this.onUpdateCallback = callback;

    return this;
  }

  public onComplete = (callback: (object: T) => void) => {
    this.onCompleteCallback = callback;

    return this;
  }

  public onWaitComplete = (callback: (object: T) => void) => {
    this.onWaitCompleteCallback = callback;

    return this;
  }

  public yoyo = (b: boolean = true, repeat: number = 1) => {
    this._Yoyo = b;
    this._Repeat = repeat - 0.5;

    return this;
  }

  public loop = (b: boolean = true, repeat: number = Infinity) => {
    this._Loop = b;
    this._Repeat = repeat;

    return this;
  }

  public stop = (andComplete?: boolean) => {
    if (andComplete) {
      this.complete(this.endTime);
    } else {
      this.running = false;

      JMTween._remove(this);
    }

    return this;
  }

  public reset = () => {
    this.tickThis = this.firstTick;
    if (this.waitTime) this.hasWait = true;

    return this;
  }

  public wait = (time: number) => {
    this.waitTime = time;
    this.hasWait = true;

    return this;
  }

  public over = (time: number) => {
    this.totalTime = time;

    return this;
  }

  public start = () => {
    this.running = true;

    this.properties.forEach(property => {
      if (property.to || property.to === 0) {
        // @ts-ignore
        property.start = this.object[property.key] || 0;
        property.end = property.to;
      } else if (property.from || property.from === 0) {
        property.start = property.from;
        // @ts-ignore
        property.end = this.object[property.key] || 0;
      }

      if (property.isColor) {
        property.incR = Math.floor(property.end / 0x010000) - Math.floor(property.start / 0x010000);
        property.incG = Math.floor((property.end % 0x010000) / 0x000100) - Math.floor((property.start % 0x010000) / 0x000100);
        property.incB = Math.floor(property.end % 0x000100) - Math.floor(property.start % 0x000100);
      } else {
        property.inc = property.end - property.start;
      }

      // @ts-ignore
      this.object[property.key] = property.start;
    });

    JMTween._add(this);

    return this;
  }

  public to = (props: Partial<T>, eased = true) => {
    for (let key of Object.keys(props)) {
      // @ts-ignore
      this.properties.push({ key, eased, to: props[key]});
    }

    return this;
  }

  public from = (props: Partial<T>, eased = true) => {
    for (let key of Object.keys(props)) {
      // @ts-ignore
      this.properties.push({ key, eased, from: props[key]});
    }

    return this;
  }

  public colorTo = (props: Partial<T>, eased = true) => {
    for (let key of Object.keys(props)) {
      // @ts-ignore
      this.properties.push({ key, eased, to: props[key], isColor: true});
    }

    return this;
  }

  public colorFrom = (props: Partial<T>, eased = true) => {
    for (let key of Object.keys(props)) {
      // @ts-ignore
      this.properties.push({ key, eased, from: props[key], isColor: true});
    }

    return this;
  }

  public easing = (func: (percent: number) => number) => {
    this._Easing = func;

    return this;
  }

  public chain<U>(nextObj: U, totalTime?: number) {
    this.nextTween = new JMTween<U>(nextObj, totalTime);

    return this.nextTween;
  }

  public chainTween(tween: JMTween) {
    this.nextTween = tween;

    return tween;
  }

  private complete = (time: number) => {
    this.properties.forEach(property => {
      // @ts-ignore
      this.object[property.key] = property.end;
    });

    if (this._Loop && this._Repeat > 0) {
      this._Repeat--;
      this.reset();
      this.startTime = time;
      this.endTime = this.startTime + (this.totalTime || 0);
    } else if (this._Yoyo && this._Repeat > 0) {
      this._Repeat -= 0.5;
      this.reverseProps();
      this.startTime = time;
      this.endTime = this.startTime + (this.totalTime || 0);
    } else {
      this.running = false;

      JMTween._remove(this);
      this.tickThis = () => {};
      if (this.onCompleteCallback) this.onCompleteCallback(this.object);

      if (this.nextTween) {
        this.nextTween.reset();
        this.nextTween.start();
        this.nextTween.tickThis(time);
      }
    }
    return this;
  }

  private firstTick = (time: number) => {
    if (this.hasWait) {
      this.startTime = time + this.waitTime / (JMTween.speedFactor || 1);
    } else {
      this.startTime = time;
    }
    this.endTime = this.startTime + (this.totalTime / (JMTween.speedFactor || 1) || 0);
    this.tickThis = this.tailTick;
  }

  private tailTick = (time: number) => {
    if (this.hasWait && time > this.startTime) {
      this.hasWait = false;
      if (this.onWaitCompleteCallback) this.onWaitCompleteCallback(this.object);
    }

    if (time > this.endTime) {
      this.complete(time);
    } else if (time > this.startTime) {
      let raw = (time - this.startTime) / (this.endTime - this.startTime);
      let eased: number = this._Easing ? this._Easing(raw) : raw;

      this.properties.forEach(property => {
        let percent = property.eased ? eased : raw;

        if (property.isColor) {
          // @ts-ignore
          (this.object[property.key] as number) = Math.round(property.start +
            Math.floor(property.incR * percent) * 0x010000 +
            Math.floor(property.incG * percent) * 0x000100 +
            Math.floor(property.incB * percent));
        } else {
          // @ts-ignore
          (this.object[property.key] as number) = property.start + property.inc * percent;
        }
      });

      if (this.onUpdateCallback) this.onUpdateCallback(this.object);
    }
  }

  private reverseProps = () => {
    this.properties.forEach(property => {
      let start = property.start;
      property.start = property.end;
      property.end = start;

      if (property.isColor) {
        property.incR = Math.floor(property.end / 0x010000) - Math.floor(property.start / 0x010000);
        property.incG = Math.floor((property.end % 0x010000) / 0x000100) - Math.floor((property.start % 0x010000) / 0x000100);
        property.incB = Math.floor(property.end % 0x000100) - Math.floor(property.start % 0x000100);
      } else {
        property.inc = property.end - property.start;
      }
    });
  }
}

export const JMEasing = {

  Linear: {

    None: (k: number) => {

      return k;

    },

  },

  Quadratic: {

    In: (k: number) => {

      return k * k;

    },

    Out: (k: number) => {

      return k * (2 - k);

    },

    InOut: (k: number) => {
      k *= 2;
      if (k < 1) {
        return 0.5 * k * k;
      }

      return - 0.5 * (--k * (k - 2) - 1);

    },

  },

  Cubic: {

    In: (k: number) => {

      return k * k * k;

    },

    Out: (k: number) => {

      return --k * k * k + 1;

    },

    InOut: (k: number) => {
      k *= 2;
      if (k < 1) {
        return 0.5 * k * k * k;
      }

      return 0.5 * ((k -= 2) * k * k + 2);

    },

  },

  Quartic: {

    In: (k: number) => {

      return k * k * k * k;

    },

    Out: (k: number) => {

      return 1 - (--k * k * k * k);

    },

    InOut: (k: number) => {
      k *= 2;
      if (k < 1) {
        return 0.5 * k * k * k * k;
      }

      return - 0.5 * ((k -= 2) * k * k * k - 2);

    },

  },

  Quintic: {

    In: (k: number) => {

      return k * k * k * k * k;

    },

    Out: (k: number) => {

      return --k * k * k * k * k + 1;

    },

    InOut: (k: number) => {
      k *= 2;
      if (k < 1) {
        return 0.5 * k * k * k * k * k;
      }

      return 0.5 * ((k -= 2) * k * k * k * k + 2);

    },

  },

  Sinusoidal: {

    In: (k: number) => {

      return 1 - Math.cos(k * Math.PI / 2);

    },

    Out: (k: number) => {

      return Math.sin(k * Math.PI / 2);

    },

    InOut: (k: number) => {

      return 0.5 * (1 - Math.cos(Math.PI * k));

    },

  },

  Exponential: {

    In: (k: number) => {

      return k === 0 ? 0 : Math.pow(1024, k - 1);

    },

    Out: (k: number) => {

      return k === 1 ? 1 : 1 - Math.pow(2, - 10 * k);

    },

    InOut: (k: number) => {

      if (k === 0) {
        return 0;
      }

      if (k === 1) {
        return 1;
      }
      k *= 2;
      if (k < 1) {
        return 0.5 * Math.pow(1024, k - 1);
      }

      return 0.5 * (- Math.pow(2, - 10 * (k - 1)) + 2);

    },

  },

  Circular: {

    In: (k: number) => {

      return 1 - Math.sqrt(1 - k * k);

    },

    Out: (k: number) => {

      return Math.sqrt(1 - (--k * k));

    },

    InOut: (k: number) => {
      k *= 2;
      if (k < 1) {
        return - 0.5 * (Math.sqrt(1 - k * k) - 1);
      }

      return 0.5 * (Math.sqrt(1 - (k -= 2) * k) + 1);

    },

  },

  Elastic: {

    In: (k: number) => {

      if (k === 0) {
        return 0;
      }

      if (k === 1) {
        return 1;
      }

      return -Math.pow(2, 10 * (k - 1)) * Math.sin((k - 1.1) * 5 * Math.PI);

    },

    Out: (k: number) => {

      if (k === 0) {
        return 0;
      }

      if (k === 1) {
        return 1;
      }

      return Math.pow(2, -10 * k) * Math.sin((k - 0.1) * 5 * Math.PI) + 1;

    },

    InOut: (k: number) => {

      if (k === 0) {
        return 0;
      }

      if (k === 1) {
        return 1;
      }

      k *= 2;

      if (k < 1) {
        return -0.5 * Math.pow(2, 10 * (k - 1)) * Math.sin((k - 1.1) * 5 * Math.PI);
      }

      return 0.5 * Math.pow(2, -10 * (k - 1)) * Math.sin((k - 1.1) * 5 * Math.PI) + 1;

    },

  },

  Back: {

    In: (k: number) => {

      let s = 1.70158;

      return k * k * ((s + 1) * k - s);

    },

    Out: (k: number) => {

      let s = 1.70158;

      return --k * k * ((s + 1) * k + s) + 1;

    },

    InOut: (k: number) => {

      let s = 1.70158 * 1.525;
      k *= 2;
      if (k < 1) {
        return 0.5 * (k * k * ((s + 1) * k - s));
      }

      return 0.5 * ((k -= 2) * k * ((s + 1) * k + s) + 2);

    },

  },

  Bounce: {

    In: (k: number) => {

      return 1 - JMEasing.Bounce.Out(1 - k);

    },

    Out: (k: number) => {

      if (k < (1 / 2.75)) {
        return 7.5625 * k * k;
      } else if (k < (2 / 2.75)) {
        return 7.5625 * (k -= (1.5 / 2.75)) * k + 0.75;
      } else if (k < (2.5 / 2.75)) {
        return 7.5625 * (k -= (2.25 / 2.75)) * k + 0.9375;
      } else {
        return 7.5625 * (k -= (2.625 / 2.75)) * k + 0.984375;
      }

    },

    InOut: (k: number) => {

      if (k < 0.5) {
        return JMEasing.Bounce.In(k * 2) * 0.5;
      }

      return JMEasing.Bounce.Out(k * 2 - 1) * 0.5 + 0.5;

    },

  },

};

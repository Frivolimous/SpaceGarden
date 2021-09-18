import * as _ from 'lodash';

export class JMEventListener<T = any> {
  public listeners: ((event: T) => void)[] = [];
  public once: ((event: T) => void)[] = [];

  public events: T[] = [];
  public active = false;

  constructor(private onlyLastListener?: boolean, private onlyLastEvent?: boolean) { }

  public addListener(output: (event: T) => void) {
    if (this.onlyLastListener) {
      this.listeners = [output];
    } else {
      this.listeners.push(output);
    }
  }

  public removeListener(output: (event: T) => void) {
    let i = this.listeners.indexOf(output);
    if (i >= 0) {
      this.listeners.splice(i, 1);
    }
  }

  public addOnce(output: (event: T) => void) {
    this.once.push(output);
  }

  public publish = (event?: T) => {
    this.events.push(event);
    if (!this.active) {
      requestAnimationFrame(this.process);
      this.active = true;
    }
  }

  public publishSync = (event?: T) => {
    let listeners = _.clone(this.listeners);
    listeners.forEach(output => output(event));
    while (this.once.length > 0) {
      this.once.shift()(event);
    }
  }

  public clear = () => {
    this.listeners = [];
    this.once = [];
    this.events = [];
    this.active = false;
  }

  private process = () => {
    this.active = false;

    if (this.onlyLastEvent) {
      this.events = [this.events.pop()];
    }

    while (this.events.length > 0) {
      let event = this.events.shift();

      let listeners = _.clone(this.listeners);
      listeners.forEach(output => output(event));

      while (this.once.length > 0) {
        this.once.shift()(event);
      }
    }
  }
}

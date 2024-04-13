import * as PIXI from 'pixi.js';
import _ from 'lodash';
import { JMTween, JMEasing } from '../../JMGE/JMTween';

const defaultConfig: IGauge = { width: 100, height: 30, bgColor: 0x333333, color: 0x888888 };

interface IGauge {
  width: number;
  height: number;
  bgColor: number;
  color: number;
}

export class SimpleGauge extends PIXI.Container {
  public percent = 1;
  public total: number;

  private back = new PIXI.Graphics();
  private front = new PIXI.Graphics();

  constructor(private config: IGauge) {
    super();

    this.config = _.defaults(config, defaultConfig);

    this.addChild(this.back, this.front);

    this.setWidth(this.config.width);
  }

  public setWidth(width?: number) {
    this.back.clear().beginFill(this.config.bgColor).drawRect(0, 0, width, this.config.height);
    this.setPercent(this.percent);
  }

  public setPercent(percent: number) {
    percent = Math.max(0, Math.min(1, percent));
    this.percent = percent;

    let width = Math.max(this.back.width * percent, this.config.height);

    this.front.clear().beginFill(this.config.color).drawRect(0, 0, width, this.config.height);

    // let oldWidth = Math.max(this.back.width * this.percent, this.config.height);
    // new JMTween({width: oldWidth}, 200).to({width}).start().easing(JMEasing.Quadratic.InOut).onUpdate((data) => {
    //   this.front.clear().beginFill(this.config.color).drawRect(0, 0, data.width, this.config.height);
    // }).onComplete(data => {
    //   this.front.clear().beginFill(this.config.color).drawRect(0, 0, data.width, this.config.height);
    // });
  }

  public setCount(count: number) {
    this.setFraction(count, this.total);
  }

  public setTotal(total: number) {
    this.total = total;
  }

  public setFraction(count: number, total: number) {
    this.setPercent(count / total);
  }

  public getWidth() {
    return this.back.width * this.scale.x;
  }

  public getHeight() {
    return this.back.height * this.scale.y;
  }
}

import * as PIXI from 'pixi.js';
// import { SoundData, SoundIndex } from '../../utils/SoundData';

export interface IClearButton {
  width: number;
  height: number;
  onClick: (e?: PIXI.FederatedPointerEvent) => void;
}

export class ClearButton extends PIXI.Container {
  constructor(private config: IClearButton) {
    super();

    let graphic = new PIXI.Graphics();
    this.addChild(graphic);
    graphic.beginFill(0, 0.01).drawRect(0, 0, config.width, config.height);

    this.eventMode = 'dynamic';
    this.cursor = 'pointer';
    this.addListener('pointerdown', () => {
      // SoundData.playSound(SoundIndex.CLICK);
      config.onClick();
    });
  }

  public getWidth() {
    return this.config.width;
  }

  public getHeight() {
    return this.config.height;
  }
}

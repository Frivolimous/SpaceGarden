import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { Button, IButton } from './Button';

const defaultConfig: Partial<IButton> = { width: 250, height: 60, rounding: 8, color: 0x77ccff, hoverScale: 0 };
export class LoadButton extends Button {
  constructor(config: IButton, deleteCallback?: () => void) {
    super(_.defaults(config, defaultConfig));
    if (deleteCallback) {
      let deleteB = new Button({width: 25, height: 25, rounding: 2, color: 0x442277, onClick: deleteCallback});
      this.addChild(deleteB);
      deleteB.position.set(220, 5);
    }
  }
}

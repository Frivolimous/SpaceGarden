import * as _ from 'lodash';
import * as PIXI from 'pixi.js';

import { TextureCache } from '../../services/TextureCache';

const GameSettings = {
  auto: true,
};

export class GameView extends PIXI.Container {
  // public onQueueEmpty = new JMEventListener<void>(false, false);
  // public onSpriteSelected = new JMEventListener<{sprite: SpriteModel, type: 'select' | 'unselect'}>(false, true);
  // public onActionComplete = new JMEventListener<IActionResult>();

  constructor() {
    super();

  }

  private onDown = (e: KeyboardEvent) => {
    if (e.key === ' ') {
    } else if (e.key === 'p') {
    } else if (e.key === 'q') {
      
    } else if (!isNaN(Number(e.key))) {
    } else if (e.key === 'l') {
    } else if (e.key === 'b') {
    }
  }
}

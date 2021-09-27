import * as PIXI from 'pixi.js';
import { GameEvents } from './GameEvents';

export const DEBUG_MODE = true;
export const GOD_MODE = false;

export class Debug {
  public static initialize(app: PIXI.Application) {
    if (DEBUG_MODE) {
      GameEvents.ACTION_LOG.addListener(e => {
        console.log('ACTION:', e.text);
      });
      GameEvents.APP_LOG.addListener(e => {
        console.log('APP:', e.type, ' : ', e.text);
      });
    }
  }
}

import * as PIXI from 'pixi.js';
import { GameEvents } from './GameEvents';

export const DEBUG_MODE = true;
export const GOD_MODE = true;

export class Debug {
  public static initialize(app: PIXI.Application) {
    if (DEBUG_MODE) {
      GameEvents.ACTIVITY_LOG.addListener(e => {
        // console.log('ACTION:', e.slug, ' : ', e.text || ' ');
      });
      GameEvents.APP_LOG.addListener(e => {
        console.log('APP:', e.type, ' : ', e.text);
      });
    }
  }
}

import { JMTicker } from '../JMGE/events/JMTicker';
import { JMRect } from '../JMGE/others/JMRect';
import { JMEventListener } from '../JMGE/events/JMEventListener';

export const GameEvents = {
  ticker: JMTicker,
  WINDOW_RESIZE: new JMEventListener<IResizeEvent>(),
  ACTION_LOG: new JMEventListener<IActionLog>(),
  APP_LOG: new JMEventListener<IAppLog>(),

  // SPRITE_ADDED: new JMEventListener<ISpriteAdded>(),
  // SPRITE_REMOVED: new JMEventListener<SpriteModel>(),
  // ANIMATE_ACTION: new JMEventListener<IAnimateAction>(true),
  // FIGHT_STATE: new JMEventListener<IFightState>(),
};

export interface IResizeEvent {
  outerBounds: JMRect;
  innerBounds: JMRect;
}

export interface IActionLog {
  action: string;
  data: any;
  text: string;
}

export interface IAppLog {
  type: AppEvent;
  data?: any;
  text: string;
}

export type AppEvent = 'INITIALIZE' | 'SAVE' | 'NAVIGATE';

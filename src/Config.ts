import _ from 'lodash';
import { GOD_MODE } from './services/_Debug';

export const Config = {
  INIT: {
    SCREEN_WIDTH: 800,
    SCREEN_HEIGHT: 800,
    RESOLUTION: 1,
    MOUSE_HOLD: 200,
    BORDER: false,
    GAME_DATA_VERSION: 29,
  },
  STAGE: {
    SCREEN_WIDTH: 800,
    SCREEN_HEIGHT: 800,
  },

  PHYSICS: {
    ELASTICITY: 0.004,
    TARGET_RATIO: 0.008,
    TARGET_MIN: 100,
    PULL_FRICTION: 0.9,
    EDGE_BOUNCE: 0.1,
    MAX_GRAB: 30,
    DAMP: 0.98,
    MIN_V: 0.0001,
    NEW_MIND: 40,
    NEW_FORCE: 0.2,
  },

  NODE: {
    FRUIT_THRESHOLD: 0.6,
    POWER_THRESHOLD: 0.25,
    GEN_FADE: 30,
    FRUIT_APPLY: 0.3,
    BUFF_FADE: 5,
    LAUNCH_PERCENT: 1,
    BLOB_AI: 0,
    SAVED_RESEARCH: 0,
    FRUIT_GROWTH: 0,
    HUB_BUTTONS: 0,
    AMP_AMOUNT: 2,
    AMP_FADE_BOOST: 3,
    BUFF_BOOST: 1.5,
    BUFF_DURATION: 1700,
    CAN_EXECUTE: 0,
  },

  CRAWLER: {
    BREED_AT: 1.5,
  },
};

if (GOD_MODE) {
  Config.NODE.LAUNCH_PERCENT = 0.05;
}

export const dConfigNode = _.clone(Config.NODE);
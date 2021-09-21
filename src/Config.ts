export const Config = {
  INIT: {
    SCREEN_WIDTH: 800,
    SCREEN_HEIGHT: 800,
    RESOLUTION: 1,
    MOUSE_HOLD: 200,
    BORDER: false,
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
    FRUIT_APPLY: 0.2,
  },

  SKILL_COST: [
    10,
    50,
    100,
    200,
    400,
    600,
    900,
    1200,
    1600,
    2000,
    2500,
    3000, // all 6 skills 1 + 1 + 2 + 2 + 3 + 3 = 12
    4000,
    5000,
    6500, //end game
    8000,
    10000,
    12000,
    15000,
    20000,
    30000,
    40000,
    50000,
    65000,
    80000,
    100000,
  ]
};

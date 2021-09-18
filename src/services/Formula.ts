import * as _ from 'lodash';
import { RandomSeed } from './RandomSeed';

export const Formula = {
  COMBAT_DISTANCE: 4,
  ENEMY_SPAWN_DISTANCE: 9,

  diminish(a: number, level: number): number {
    return 1 - Math.pow(1 - a, level);
  },
  addMult(a: number, b: number): number {
    return (1 - (1 - a) * (1 - b));
  },
  subMult(t: number, a: number): number {
    return (1 - (1 - t) / (1 - a));
  },
};

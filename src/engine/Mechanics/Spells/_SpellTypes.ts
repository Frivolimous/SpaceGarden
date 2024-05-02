import { BuffSpell } from './BuffSpell';
import { DeleteSpell } from './DeleteSpell';
import { TurboSpell } from './TurboSpell';
import { WeightSpell } from './WeightSpell';
import { BaseSpell } from './_BaseSpell';

export type SpellSlug = 'delete' | 'turbo' | 'buff' | 'weight' |
      'none';

export const SpellMap: {[key in SpellSlug]: typeof BaseSpell} = {
  'buff': BuffSpell,
  'delete': DeleteSpell,
  'turbo': TurboSpell,
  'weight': WeightSpell,
  'none': null,
};
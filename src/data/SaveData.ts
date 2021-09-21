import { NodeSlug } from "./NodeData";

export type CurrencySlug = 'gold' | 'tokens' | 'refresh' | 'suns' | 'souls';

export const CURRENT_VERSION = 22;

export interface IExtrinsicModel {
  achievements: boolean[];
  // flags: boolean[];
  // scores?: number[];

  currency: {[key in CurrencySlug]?: number};

  stageState?: string;
  skillsCurrent: string[];
  skillsNext: string[];
  skillTier: number;

  firstVersion?: number;
  logins?: number;

  skillTrees?: number[];

  nodes: NodeSlug[];

  options: {
    autoFill: boolean;
  };
}

export interface ISkillSave {
  skillId: string,
  level: number,
}

export const dExtrinsicModel: IExtrinsicModel = {
  achievements: [],
  currency: {
    gold: 0,
    tokens: 0,
    refresh: 0,
    suns: 0,
    souls: 0,
  },

  nodes: [
    'core',
    'stem',
    'generator',
    'grove',
    'lab',
    'seedling',
  ],

  options: {
    autoFill: false,
  },

  skillsCurrent: [],
  skillsNext: [],
  skillTier: 0,
};


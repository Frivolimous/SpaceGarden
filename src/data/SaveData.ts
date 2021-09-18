export type CurrencySlug = 'gold' | 'tokens' | 'refresh' | 'suns' | 'souls';

export interface IExtrinsicModel {
  achievements: boolean[];
  // flags: boolean[];
  // scores?: number[];

  currency: {[key in CurrencySlug]?: number};

  stageState?: string;
  skillsCurrent?: ISkillSave[];
  skillsNext?: ISkillSave[];

  firstVersion?: number;
  logins?: number;

  skillTrees?: number[];

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

  options: {
    autoFill: false,
  },
};

import { NodeBase, NodeSlug } from "./NodeData";

export const SkillData: ISkillData = {
  skills: [
    {
      slug: 'skill-1',
      title: 'Core Power',
      description: 'Increases core power generation by x1.5',
      // description: 'Increases core power generation by x{1-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'core', key: 'powerGen', valueType: 'multiplicative', value: 1.5},
      ],
    },
    {
      slug: 'skill-2',
      title: 'Core Links',
      description: 'Increases Core Links by +1',
      // description: 'Increases Core Links by +{1-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'core', key: 'maxLinks', valueType: 'additive', value: 1},
      ]
    },
    {
      slug: 'skill-3',
      title: 'Stem Efficiency',
      description: 'Increases Stem count by +4 and transfer rate by x3',
      // description: 'Increases Stem count by +{1-value} and transfer rate by x{2-value}',
      cost: 2,
      effects: [
        { effectType: 'node', slug: 'stem', key: 'maxCount', valueType: 'additive', value: 4},
        { effectType: 'node', slug: 'stem', key: 'powerClump', valueType: 'multiplicative', value: 3},
      ]
    },
    {
      slug: 'skill-6',
      title: 'Leafy Goodness',
      description: 'Stem Fruits reduce power drain of Stems by -0.05 each',
      // description: 'Leaves reduce power drain of Stems by {1-value-amount}',
      cost: 2,
      effects: [
        { effectType: 'node', slug: 'leaf', key: 'outletEffect', valueType: 'additive', value: {stat: 'powerGen', type: 'additive', amount: -NodeBase.powerDrain / 2}},
      ]
    },
    {
      slug: 'skill-4',
      title: 'Evolution Speed',
      description: 'Increases Lab Research Generation by x5 but power drain increased by x2',
      // description: 'Increases Lab Research Generation by x{1-value} but power drain increased by x{2-value}',
      cost: 3,
      effects: [
        { effectType: 'node', slug: 'lab', key: 'researchGen', valueType: 'multiplicative', value: 5},
        { effectType: 'node', slug: 'lab', key: 'powerGen', valueType: 'multiplicative', value: 2},
      ]
    },
    {
      slug: 'skill-5',
      title: 'Generator Count',
      description: 'Increases number of generators by +2',
      // description: 'Increases number of generators by +{1-value}',
      cost: 3,
      effects: [
        { effectType: 'node', slug: 'generator', key: 'maxCount', valueType: 'additive', value: 2},
      ]
    },
    {
      slug: 'skill-tier-1',
      title: 'Tier 2',
      description: '<p style="margin: 3px; font-style: italic; font-size: 11px;">Requires all Tier 1 skills to unlock</p> Permanently unlock all Tier 1 skills and increases Seedling Max Power by x10',
      cost: 3,
      skillRequirements: ['skill-1', 'skill-2', 'skill-3', 'skill-4', 'skill-5', 'skill-6'],
      effects: [
        {effectType: 'tier', valueType: 'replace', value: 1},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'multiplicative', value: 10},
      ]
    },
    {
      slug: 'skill-2-1',
      title: 'To be continued...',
      description: 'You beat the game!  More content coming soon!  Such as...',
      cost: 99,
      effects: [],
    },
    {
      slug: 'skill-2-2',
      title: 'Crawlers',
      description: 'Unlock the "Home" Node which spawns Crawlers.',
      cost: 99,
      effects: [],
    },
    {
      slug: 'skill-2-3',
      title: 'Crawler Smarts',
      description: 'Crawlers can bring Lab Fruits to the Seedling produce research',
      cost: 99,
      effects: [],
    },
    {
      slug: 'skill-2-4',
      title: 'Woodcutters',
      description: 'Crawlers can bring Grove Fruits to the Seedling to power it',
      cost: 99,
      effects: [],
    },
    {
      slug: 'skill-2-5',
      title: 'More Crawlers!',
      description: 'Crawlers can over-consume Home Fruits to spawn more crawlers',
      cost: 99,
      effects: [],
    },
    {
      slug: 'skill-2-6',
      title: 'Worship',
      description: 'Crawlers can dance on the Core to charge its fruits',
      cost: 99,
      effects: [],
    },
    {
      slug: 'skill-2-7',
      title: 'Take Off!',
      description: 'When launching your seedling, up to two crawlers come along',
      cost: 99,
      effects: [],
    },
    {
      slug: 'skill-tier-2',
      title: 'Tier 3',
      description: '<p style="margin: 3px; font-style: italic; font-size: 11px;">Requires all Tier 2 skills to unlock</p> Permanently unlock all Tier 2 skills but increases Seedling Power Drain to 0.5/s',
      skillRequirements: ['skill-2-1', 'skill-2-2', 'skill-2-3', 'skill-2-4', 'skill-2-5', 'skill-2-6'],
      cost: 99,
      effects: [
        {effectType: 'tier', valueType: 'replace', value: 1},
        {effectType: 'node', slug: 'seedling', key: 'powerDrain', valueType: 'additive', value: -0.1},
      ],
    },
  ],

  skillExchange: [
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
  ],

  skillTiers: [
    [],
    ['skill-1', 'skill-2', 'skill-3', 'skill-4', 'skill-5', 'skill-6', 'skill-tier-1'],
    ['skill-2-1', 'skill-2-2', 'skill-2-3', 'skill-2-4', 'skill-2-5', 'skill-2-6', 'skill-2-7', 'skill-tier-2'],
  ],
}

interface ISkillData {
  skills: SkillConfig[],
  skillExchange: number[],
  skillTiers: string[][],
}

export interface SkillConfig {
  slug: string,
  title: string,
  description: string,
  cost: number,
  effects: SkillEffect[],
  skillRequirements?: string[],
}

export interface SkillEffect {
  effectType: 'node' | 'tier',
  slug?: NodeSlug,
  key?: string,
  valueType: 'additive' | 'multiplicative' | 'replace',
  value: any,
}

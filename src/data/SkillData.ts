import { CommandType } from '../engine/Mechanics/CrawlerCommands/_CommandTypes';
import { AchievementSlug } from './ATSData';
import { CrawlerSlug } from './CrawlerData';
import { NodeBase, NodeSlug } from './NodeData';

export const SkillData: ISkillData = {
  skills: [
    {
      slug: 'skill-1',
      title: 'Build your Core',
      description: 'Increases core power generation by x1.5',
      // description: 'Increases core power generation by x{1-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'core', key: 'powerGen', valueType: 'multiplicative', value: 1.5},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'additive', value: 500},
      ],
    },
    {
      slug: 'skill-2',
      title: 'Second Path',
      description: 'Increases Core Links by +1 and transfer rate by x1.5',
      // description: 'Increases Core Links by +{1-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'core', key: 'maxLinks', valueType: 'additive', value: 1},
        { effectType: 'node', slug: 'core', key: 'powerClump', valueType: 'multiplicative', value: 1.5},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'additive', value: 500},
      ],
    },
    {
      slug: 'skill-3',
      title: 'Long Stemmed',
      description: 'Increases Stem count by +4 and transfer rate by x3',
      // description: 'Increases Stem count by +{1-value} and transfer rate by x{2-value}',
      cost: 2,
      effects: [
        { effectType: 'node', slug: 'stem', key: 'maxCount', valueType: 'additive', value: 4},
        { effectType: 'node', slug: 'stem', key: 'powerClump', valueType: 'multiplicative', value: 3},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'additive', value: 500},
      ],
    },
    {
      slug: 'skill-6',
      title: 'Leafy Goodness',
      description: 'Stems can produce fruits which reduce power drain of Stems down to 0',
      // description: 'Leaves reduce power drain of Stems by {1-value-amount}',
      cost: 2,
      effects: [
        { effectType: 'node', slug: 'stem', key: 'fruitType', valueType: 'replace', value: 'leaf'},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'additive', value: 500},
      ],
    },
    {
      slug: 'skill-4',
      title: 'Rapid Evolution',
      description: 'Increases Lab Research Generation by x5 but power drain increased by x1.5',
      // description: 'Increases Lab Research Generation by x{1-value} but power drain increased by x{2-value}',
      cost: 3,
      effects: [
        { effectType: 'node', slug: 'lab', key: 'researchGen', valueType: 'multiplicative', value: 5},
        { effectType: 'node', slug: 'lab', key: 'powerGen', valueType: 'multiplicative', value: 1.5},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'additive', value: 500},
      ],
    },
    {
      slug: 'skill-5',
      title: 'More Power',
      description: 'Increases number of generators by +2',
      // description: 'Increases number of generators by +{1-value}',
      cost: 3,
      effects: [
        { effectType: 'node', slug: 'generator', key: 'maxCount', valueType: 'additive', value: 2},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'additive', value: 500},
      ],
    },
    {
      slug: 'skill-tier-1',
      title: 'Tier 2',
      description: '<p class="skill-block-subtitle">Requires all Tier 1 skills to unlock</p> Permanently unlock all Tier 1 skills but increases Seedling Power Drain to 0.4/s',
      cost: 2,
      skillRequirements: ['skill-1', 'skill-2', 'skill-3', 'skill-4', 'skill-5', 'skill-6'],
      effects: [
        {effectType: 'tier', valueType: 'replace', value: 1},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.3},
      ],
    },
    {
      slug: 'skill-2-1',
      title: 'Crawlers',
      description: 'Unlock the "Home" Node which spawns Crawlers.',
      cost: 1,
      skillRequirements: ['skill-tier-1'],
      effects: [
        {effectType: 'buildable', valueType: 'additive', value: 'home'},
      ],
    },
    {
      slug: 'skill-2-2',
      title: 'Librarians',
      description: '<p class="skill-block-subtitle">Requires Crawlers to unlock</p>Crawlers can bring Lab Fruits to the Seedling produce research',
      cost: 2,
      skillRequirements: ['skill-2-1'],
      effects: [
        {effectType: 'crawler', slug: 'crawler', key: 'commands', valueType: 'additive', value: CommandType.RESEARCH},
        {effectType: 'crawler', slug: 'crawler', key: 'preferenceList', valueType: 'additive', value: CommandType.RESEARCH},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-2-3',
      title: 'Woodcutters',
      description: '<p class="skill-block-subtitle">Requires Crawlers to unlock</p>Crawlers can use Grove Fruits to power the Seedling or low power nodes',
      cost: 3,
      skillRequirements: ['skill-2-1'],
      effects: [
        {effectType: 'crawler', slug: 'crawler', key: 'commands', valueType: 'additive', value: CommandType.POWER},
        {effectType: 'crawler', slug: 'crawler', key: 'preferenceList', valueType: 'additive', value: CommandType.POWER},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-2-4',
      title: 'Handmaidens',
      description: '<p class="skill-block-subtitle">Requires Crawlers to unlock</p>Crawlers can over-consume Home Fruits to spawn more crawlers',
      cost: 3,
      skillRequirements: ['skill-2-1'],
      effects: [
        {effectType: 'crawler', slug: 'crawler', key: 'commands', valueType: 'additive', value: CommandType.BREED},
        {effectType: 'crawler', slug: 'crawler', key: 'preferenceList', valueType: 'additive', value: CommandType.BREED},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-2-5',
      title: 'Worshippers',
      description: '<p class="skill-block-subtitle">Requires Crawlers to unlock</p>Crawlers can dance on the Core to charge its fruits',
      cost: 4,
      skillRequirements: ['skill-2-1'],
      effects: [
        {effectType: 'crawler', slug: 'crawler', key: 'commands', valueType: 'additive', value: CommandType.DANCE},
        {effectType: 'crawler', slug: 'crawler', key: 'preferenceList', valueType: 'additive', value: CommandType.DANCE},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-2-6',
      title: 'Longevity',
      description: '<p class="skill-block-subtitle">Requires Crawlers to unlock</p>Crawlers lose 50% less health per second and move 20% faster.',
      cost: 4,
      skillRequirements: ['skill-2-1'],
      effects: [
        {effectType: 'crawler', slug: 'all', key: 'healthDrain', valueType: 'multiplicative', value: 0.75},
        {effectType: 'crawler', slug: 'all', key: 'speed', valueType: 'multiplicative', value: 1.2},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-tier-2',
      title: 'Tier 3',
      description: '<p class="skill-block-subtitle">Requires all Tier 2 skills to unlock</p> Permanently unlock Tier 2 but increases Seedling Max Power by x10 and Power Drain by +0.4/s',
      skillRequirements: ['skill-2-1', 'skill-2-2', 'skill-2-3', 'skill-2-4', 'skill-2-5', 'skill-2-6'],
      cost: 2,
      effects: [
        {effectType: 'tier', valueType: 'replace', value: 2},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'additive', value: 45000},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.4},
      ],
    },
    {
      slug: 'skill-3-1',
      title: 'Big Stem',
      description: 'Lets you place up to two Big Stem.',
      cost: 4,
      effects: [
        {effectType: 'buildable', valueType: 'additive', value: 'bigstem'},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-3-2',
      title: 'Generator Supreme',
      description: 'Doubles the power generated by your Generators and grants them +1 Connection.',
      cost: 4,
      effects: [
        {effectType: 'node', slug: 'generator', key: 'powerGen', valueType: 'multiplicative', value: 2},
        {effectType: 'node', slug: 'generator', key: 'maxLinks', valueType: 'additive', value: 1},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-3-3',
      title: 'Knowledge Osmosis',
      description: 'Triples the amount of research produced by Labs.',
      cost: 4,
      effects: [
        {effectType: 'node', slug: 'lab', key: 'researchGen', valueType: 'multiplicative', value: 3},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-3-4',
      title: 'Genetic Memory',
      description: 'Half of your earned Research Points are saved when you delete or launch your seedling.',
      cost: 4,
      effects: [
        {effectType: 'config', key: 'SAVED_RESEARCH', valueType: 'replace', value: 0.5},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-3-5',
      title: 'Crawler Shaman',
      description: 'Up to one Crawler appears as a Shaman, which has the ability to boost the movement speed of the rest of the crawlers.',
      cost: 4,
      effects: [
        {effectType: 'crawler-available', valueType: 'additive', value: 'shaman'},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-3-1',
      title: 'Big Stem',
      description: 'Lets you place up to two Big Stem.',
      cost: 4,
      effects: [
        {effectType: 'buildable', valueType: 'additive', value: 'bigstem'},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -0.02},
      ],
    },
    {
      slug: 'skill-tier-3',
      title: 'Tier 4',
      description: '<p class="skill-block-subtitle">Requires all Tier 3 skills to unlock</p> Permanently unlock Tier 3 but increases Seedling Max Power by x10 and Power Drain by +1/s',
      skillRequirements: ['skill-3-1', 'skill-3-2', 'skill-3-3', 'skill-3-4', 'skill-3-5', 'skill-3-6'],
      cost: 2,
      effects: [
        {effectType: 'tier', valueType: 'replace', value: 3},
        {effectType: 'node', slug: 'seedling', key: 'powerMax', valueType: 'additive', value: 450000},
        {effectType: 'node', slug: 'seedling', key: 'powerGen', valueType: 'additive', value: -1},
      ],
    },
    {
      slug: 'placeholder',
      title: 'You win!',
      description: 'You beat the game!  For now... more content will be coming soon™!',
      cost: 99,
      effects: [

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
    2000, // 10 SP
    2500,
    3000, // all 6 skills 1 + 1 + 2 + 2 + 3 + 3 = 12
    4000,
    5000, // T1 Upgrade
    6500,
    8000,
    10000,
    12000,
    15000, // T2 Upgrade probably goes here
    20000, // 20 SP
    30000,
    40000,
    50000,
    65000,
    80000, // 25 SP
  ],

  skillTiers: [
    [],
    ['skill-1', 'skill-2', 'skill-3', 'skill-4', 'skill-5', 'skill-6', 'skill-tier-1'],
    ['skill-2-1', 'skill-2-2', 'skill-2-3', 'skill-2-4', 'skill-2-5', 'skill-2-6', 'skill-tier-2'],
    ['skill-3-1', 'skill-3-2', 'skill-3-3', 'skill-3-4', 'skill-3-5', 'skill-3-6', 'skill-tier-3'],
    ['placeholder'],
  ],

  achievements: [
    {
      slug: AchievementSlug.PRESTIGE_10,
      title: 'Eventuality',
      description: 'Requirement: Launch Seedling 10 times<br>Reward: Seedling can be launched at 95% Power',
      effects: [
        {effectType: 'config', key: 'LAUNCH_PERCENT', valueType: 'replace', value: 0.95},
      ],
    },
    {
      slug: AchievementSlug.BLOB_15,
      title: 'Smart Blobs',
      description: 'Requirement: Have 15 Blobs at once<br>Reward: Blobs no longer path to dead ends',
      effects: [
        {effectType: 'config', key: 'BLOB_AI', valueType: 'replace', value: 1},
      ],
    },
    {
      slug: AchievementSlug.LAUNCH_DISTANCE_9,
      title: 'Longest Stem',
      description: 'Requirement: Launch your seedling from a distance of 9+ away from your Core<br>Reward: +1 Number of Stems',
      effects: [
        { effectType: 'node', slug: 'stem', key: 'maxCount', valueType: 'additive', value: 1},
      ],
    },
    {
      slug: AchievementSlug.CRAWLERS_15,
      title: 'Overpopulation',
      description: 'Requirement: Have 15 Crawlers at once<br>Reward: Crawlers gain 10% more health when eating',
      effects: [
        {effectType: 'crawler-command', slug: 'all', key: 'eatRatio', valueType: 'multiplicative', value: 1.1},
      ],
    },
    {
      slug: AchievementSlug.CRAWLERS_DIE_100,
      title: 'Massacre',
      description: 'Requirement: Kill 100 Crawlers in total<br>Reward: Crawlers move 10% faster',
      effects: [
        {effectType: 'crawler', slug: 'all', key: 'speed', valueType: 'multiplicative', value: 1.1},
      ],
    },
  ],
};

interface ISkillData {
  skills: ISkillConfig[];
  skillExchange: number[];
  skillTiers: string[][];
  achievements: IAchievement[];
}

export interface ISkillConfig {
  slug: string;
  title: string;
  description: string;
  cost: number;
  effects: ISkillEffect[];
  skillRequirements?: string[];
}

export interface IAchievement {
  slug: AchievementSlug;
  title: string;
  description: string;
  effects: ISkillEffect[];
}

export type ISkillEffect = ISkillEffectNode | ISkillEffectCrawler | ISkillEffectSimple;

export interface ISkillEffectNode {
  effectType: 'node';
  slug: NodeSlug;
  key: string;
  valueType: EffectValueType;
  value: any;
}

export interface ISkillEffectCrawler {
  effectType: 'crawler' | 'crawler-command';
  slug: CrawlerSlug | 'all';
  key: string;
  valueType: EffectValueType;
  value: any;
}

export interface ISkillEffectSimple {
  effectType: 'tier' | 'config' | 'buildable' | 'crawler-available';
  key?: string;
  valueType: EffectValueType;
  value: any;
}

export type EffectValueType = 'additive' | 'multiplicative' | 'replace';

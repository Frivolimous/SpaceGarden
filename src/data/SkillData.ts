import { NodeBase, NodeSlug } from "./NodeData";

export const SkillData: ISkillData = {
  skills: [
    {
      title: 'Core Power',
      description: 'Increases core power generation by x1.5',
      // description: 'Increases core power generation by x{1-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'core', key: 'powerGen', valueType: 'multiplicative', value: 1.5},
      ],
    },
    {
      title: 'Core Links',
      description: 'Increases Core Links by +1',
      // description: 'Increases Core Links by +{1-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'core', key: 'maxLinks', valueType: 'additive', value: 1},
      ]
    },
    {
      title: 'Stem Efficiency',
      description: 'Increases Stem count by +3 and transfer rate by x3',
      // description: 'Increases Stem count by +{1-value} and transfer rate by x{2-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'stem', key: 'maxCount', valueType: 'additive', value: 3},
        { effectType: 'node', slug: 'stem', key: 'powerClump', valueType: 'multiplicative', value: 3},
      ]
    },
    {
      title: 'Evolution Speed',
      description: 'Increases Lab Research Generation by x5 but power drain increased by x2',
      // description: 'Increases Lab Research Generation by x{1-value} but power drain increased by x{2-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'lab', key: 'researchGen', valueType: 'multiplicative', value: 5},
        { effectType: 'node', slug: 'lab', key: 'powerGen', valueType: 'multiplicative', value: 2},
      ]
    },
    {
      title: 'Generator Count',
      description: 'Increases number of generators by +2',
      // description: 'Increases number of generators by +{1-value}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'generator', key: 'maxCount', valueType: 'additive', value: 2},
      ]
    },
    {
      title: 'Leafy Goodness',
      description: 'Leaves reduce power drain of Stems by -0.05',
      // description: 'Leaves reduce power drain of Stems by {1-value-amount}',
      cost: 1,
      effects: [
        { effectType: 'node', slug: 'leaf', key: 'outletEffect', valueType: 'additive', value: {stat: 'powerGen', type: 'additive', amount: -NodeBase.powerDrain / 2}},
      ]
    },
  ],
}

interface ISkillData {
  skills: SkillConfig[]
}

export interface SkillConfig {
  title: string,
  description: string,
  cost: number,
  effects: SkillEffect[],
}

export interface SkillEffect {
  effectType: 'node',
  slug: NodeSlug,
  key: string,
  valueType: 'additive' | 'multiplicative' | 'replace',
  value: any,
}

import * as _ from 'lodash';
import { CommandType } from '../engine/Mechanics/CrawlerCommands/_BaseCommand';
import { ICrawler } from '../engine/Mechanics/Parts/CrawlerModel';
import { INodeConfig, NodeData, NodeSlug } from '../data/NodeData';
import { ISkillConfig, SkillData } from '../data/SkillData';

export class NodeManager {
  public skills: ISkillConfig[];

  public crawlerConfig: ICrawler;

  public buildableNodes: NodeSlug[] = [
    'core',
    'stem',
    'generator',
    'grove',
    'lab',
    'seedling',
  ];

  private data: INodeConfig[];

  constructor(data: INodeConfig[], skills: ISkillConfig[], private skillTier: number) {
    this.data = _.cloneDeep(data);
    this.skills = _.cloneDeep(skills);

    this.crawlerConfig = {
      health: 1,
      healthDrain: 0.0002,
      speed: 0.01,
      commands: [
        CommandType.WANDER,
        CommandType.IDLE,
        CommandType.EAT,
        CommandType.FRUSTRATED,
        CommandType.STARVING,
      ],
      preferenceList: [
        CommandType.WANDER,
      ],
    };
  }

  public destroy() {

  }

  public getNodeConfig(slug: NodeSlug): INodeConfig {
    let raw = this.data.find(config => config.slug === slug);

    return raw;
  }

  public extractTier(slugs: string[], currentTier: number): number {
    let tierSkills = this.getSkillsBySlugs(slugs).filter(skill => skill.effects.find(effect => effect.effectType === 'tier'));
    let max = Math.max(...tierSkills.map(skill => skill.effects.find(effect => effect.effectType === 'tier').value as number), currentTier);

    return max;
  }

  public getSkillAlways(tier: number): string[] {
    let m: string[] = [];
    for (let i = 0; i <= tier; i++) {
      m = m.concat(SkillData.skillTiers[i]);
    }

    return m;
  }

  public getSkillsBySlugs(slugs: string[]): ISkillConfig[] {
    return slugs.map(slug => this.skills.find(skill => skill.slug === slug));
  }

  public applySkills(slugs: string[]) {
    let skills = this.getSkillsBySlugs(slugs);
    let always = this.getSkillsBySlugs(this.getSkillAlways(this.skillTier));

    let allSkills = _.uniq(skills.concat(always));

    allSkills.forEach(this.applySkill);
  }

  public applySkill = (skill: ISkillConfig) => {
    skill.effects.forEach(effect => {
      if (effect.effectType === 'node') {
        let node = this.getNodeConfig(effect.slug);
        if (effect.key === 'outletEffect') {
          if (effect.valueType === 'additive') {
            node.outletEffects = node.outletEffects || [];
            node.outletEffects.push(_.clone(effect.value));
          } else if (effect.valueType === 'replace') {
            node.outletEffects = [_.clone(effect.value)];
          }
        } else {
          if (effect.valueType === 'additive') {
            (node as any)[effect.key] += effect.value;
          } else if (effect.valueType === 'multiplicative') {
            (node as any)[effect.key] *= effect.value;
          } else if (effect.valueType === 'replace') {
            (node as any)[effect.key] = effect.value;
          }
        }
      } else if (effect.effectType === 'crawler') {
        let config = this.crawlerConfig;
        if (effect.key === 'commands' || effect.key === 'preferenceList') {
          if (effect.valueType === 'additive') {
            (config as any)[effect.key].push(effect.value);
          }
        } else {
          if (effect.valueType === 'additive') {
            (config as any)[effect.key] += effect.value;
          } else if (effect.valueType === 'multiplicative') {
            (config as any)[effect.key] *= effect.value;
          } else if (effect.valueType === 'replace') {
            (config as any)[effect.key] = effect.value;
          }
        }
      } else if (effect.effectType === 'buildable') {
        if (effect.valueType === 'additive') {
          this.buildableNodes.push(effect.value);
        }
      }
    });
  }
}

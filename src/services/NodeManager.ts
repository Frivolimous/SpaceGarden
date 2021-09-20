import * as _ from 'lodash';
import { NodeConfig, NodeData, NodeSlug } from "../data/NodeData";
import { SkillConfig } from '../data/SkillData';

export class NodeManager {
  private data: NodeConfig[];
  public skills: SkillConfig[];

  constructor(data: NodeConfig[], skills: SkillConfig[]) {
    this.data = _.cloneDeep(data);
    this.skills = _.cloneDeep(skills);
  }
  public getNodeConfig(slug: NodeSlug): NodeConfig {
    let raw = this.data.find(config => config.slug === slug);

    return raw;
  }

  public applySkills(skills: SkillConfig[]) {
    skills.forEach(this.applySkill);
  }

  public applySkill = (skill: SkillConfig) => {
    skill.effects.forEach(effect => {
      if (effect.effectType === 'node') {
        let node = this.data.find(block => block.slug === effect.slug);
        if (effect.key === 'outletEffect') {
          if (effect.valueType === 'additive') {
            node.outletEffects = node.outletEffects || [];
            node.outletEffects.push(_.clone(effect.value));
          } else if (effect.valueType === 'replace') {
            node.outletEffects = [_.clone(effect.value)];
          }
        }
        if (effect.valueType === 'additive') {
          (node as any)[effect.key] += effect.value;
        } else if (effect.valueType === 'multiplicative') {
          (node as any)[effect.key] *= effect.value;
        } else if (effect.valueType === 'replace') {
          (node as any)[effect.key] = effect.value;
        }
      }
    });
  }
}

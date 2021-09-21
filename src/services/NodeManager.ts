import * as _ from 'lodash';
import { NodeConfig, NodeData, NodeSlug } from "../data/NodeData";
import { SkillConfig, SkillData } from '../data/SkillData';

export class NodeManager {
  public data: NodeConfig[];
  public skills: SkillConfig[];

  constructor(data: NodeConfig[], skills: SkillConfig[]) {
    this.data = _.cloneDeep(data);
    this.skills = _.cloneDeep(skills);
  }

  destroy() {
    
  }

  public getNodeConfig(slug: NodeSlug): NodeConfig {
    let raw = this.data.find(config => config.slug === slug);

    return raw;
  }

  public getSkillAlways(tier: number): string[] {
    let m: string[] = [];
    for (let i = 0; i <= tier; i++) {
      m = m.concat(SkillData.skillTiers[i]);
    }

    return m;
  }

  public getSkillsBySlugs(slugs: string[]): SkillConfig[] {
    return slugs.map(slug => this.skills.find(skill => skill.slug === slug));
  }
}

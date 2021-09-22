import * as _ from 'lodash';
import { INodeConfig, NodeData, NodeSlug } from '../data/NodeData';
import { ISkillConfig, SkillData } from '../data/SkillData';

export class NodeManager {
  public data: INodeConfig[];
  public skills: ISkillConfig[];

  constructor(data: INodeConfig[], skills: ISkillConfig[]) {
    this.data = _.cloneDeep(data);
    this.skills = _.cloneDeep(skills);
  }

  public destroy() {

  }

  public getNodeConfig(slug: NodeSlug): INodeConfig {
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

  public getSkillsBySlugs(slugs: string[]): ISkillConfig[] {
    return slugs.map(slug => this.skills.find(skill => skill.slug === slug));
  }
}

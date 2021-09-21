import * as _ from 'lodash';
import { NodeConfig, NodeData, NodeSlug } from "../data/NodeData";
import { SkillConfig } from '../data/SkillData';

export class NodeManager {
  public data: NodeConfig[];
  public skills: SkillConfig[];

  constructor(data: NodeConfig[], skills: SkillConfig[]) {
    this.data = _.cloneDeep(data);
    this.skills = _.cloneDeep(skills);
  }
  public getNodeConfig(slug: NodeSlug): NodeConfig {
    let raw = this.data.find(config => config.slug === slug);

    return raw;
  }
}

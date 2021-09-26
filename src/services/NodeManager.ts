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

  constructor(data: INodeConfig[], skills: ISkillConfig[]) {
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

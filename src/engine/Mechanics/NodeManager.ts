import * as _ from 'lodash';
import { INodeConfig, NodeData, NodeSlug } from '../../data/NodeData';
import { IAchievement, IHubConfig, ISkillConfig, ISkillEffect, ISkillEffectNode, SkillData } from '../../data/SkillData';
import { Config } from '../../Config';
import { CrawlerData, CrawlerSlug, ICrawlerConfig } from '../../data/CrawlerData';

export class NodeManager {
  public skills: ISkillConfig[];
  public hubSkills: IHubConfig[];

  public buildableNodes: NodeSlug[];

  public availableCrawlers: CrawlerSlug[];

  private data: INodeConfig[];
  private crawlers: ICrawlerConfig[];

  constructor(private skillTier: number) {
    this.buildableNodes = _.clone(NodeData.BaseBuildable);
    this.availableCrawlers = _.clone(CrawlerData.BaseAvailable);
    this.data = _.cloneDeep(NodeData.Nodes);
    this.skills = _.cloneDeep(SkillData.skills);
    this.hubSkills = _.cloneDeep(SkillData.hubs);
    this.crawlers = _.cloneDeep(CrawlerData.data);
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

  public getCrawlerConfig(slug: CrawlerSlug = 'crawler'): ICrawlerConfig {
    if (slug === 'chieftain') {
      return _.defaults(_.clone(this.crawlers.find(data => data.slug === 'chieftain')), this.getCrawlerConfig('crawler'));
    }
    return this.crawlers.find(data => data.slug === slug);
  }

  public applyAchievements(achievements: boolean[]) {
    achievements.forEach((state, slug) => {
      if (state) {
        let data = SkillData.achievements.find(a => a.slug === slug);
        this.applySkill(data);
      }
    });
  }

  public applyHubs(hubLevels: [string, number][]) {
    hubLevels.forEach(([slug, level]) => {
      let skill = this.hubSkills.find(el => el.slug === slug);
      for (let i = 0; i < level; i++) {
        this.applyNodeEffect(skill.effect);
      }
    });
  }

  public applySkills(slugs: string[]) {
    let skills = this.getSkillsBySlugs(slugs);
    let always = this.getSkillsBySlugs(this.getSkillAlways(this.skillTier));

    let allSkills = _.uniq(skills.concat(always));

    allSkills.forEach(this.applySkill);
  }

  applyNodeEffect(effect: ISkillEffectNode) {
    let config = this.getNodeConfig(effect.slug);
    if (effect.key === 'outletEffects') {
      this.finishArrayEffect(config, effect);
    } else {
      this.finishNumberEffect(config, effect);
    }
  }

  public applySkill = (skill: ISkillConfig | IAchievement) => {
    skill.effects.forEach(effect => {
      if (effect.effectType === 'node') {
        let config = this.getNodeConfig(effect.slug);
        if (effect.key === 'outletEffects') {
          this.finishArrayEffect(config, effect);
        } else {
          this.finishNumberEffect(config, effect);
        }
      } else if (effect.effectType === 'crawler') {
        if (effect.slug === 'all') {
          this.crawlers.forEach(config => {
            if (effect.key === 'commands' || effect.key === 'preferenceList') {
              this.finishArrayEffect(config, effect);
            } else {
              this.finishNumberEffect(config, effect);
            }
          });
        } else {
          let config = this.getCrawlerConfig(effect.slug);
          if (effect.key === 'commands' || effect.key === 'preferenceList') {
            this.finishArrayEffect(config, effect);
          } else {
            this.finishNumberEffect(config, effect);
          }
        }
      } else if (effect.effectType === 'crawler-command') {
        if (effect.slug === 'all') {
          this.crawlers.forEach(config => {
            if (config.slug !== 'chieftain') {
              this.finishNumberEffect(config.commandConfig, effect);
            }
          });
        } else {
          let config = this.getCrawlerConfig(effect.slug);
          this.finishNumberEffect(config.commandConfig, effect);
        }
      } else if (effect.effectType === 'buildable') {
        this.finishArrayEffect(this.buildableNodes, effect);
      } else if (effect.effectType === 'crawler-available') {
        this.finishArrayEffect(this.availableCrawlers, effect);
      } else if (effect.effectType === 'config') {
        this.finishNumberEffect(Config.NODE, effect);
      }
    });
  }

  private finishNumberEffect(config: any, effect: ISkillEffect) {
    if (effect.valueType === 'additive') {
      config[effect.key] += effect.value;
    } else if (effect.valueType === 'multiplicative') {
      config[effect.key] *= effect.value;
    } else if (effect.valueType === 'replace') {
      config[effect.key] = effect.value;
    }
  }

  private finishArrayEffect(config: any, effect: ISkillEffect) {
    if (effect.key) {
      if (!config[effect.key]) {
        config[effect.key] = [];
      }
      config = config[effect.key];
    }
    if (effect.valueType === 'additive') {
      config.push(effect.value);
    } else if (effect.valueType === 'replace') {
      config = [effect.value];
    }
  }
}

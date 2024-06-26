import _ from 'lodash';
import { GOD_MODE } from '../../services/_Debug';
import { NodeSlug } from '../../data/NodeData';
import { NodeManager } from './NodeManager';
import { PlantNode } from '../nodes/PlantNode';
import { GameController } from './GameController';
import { CrawlerModel } from './Parts/CrawlerModel';
import { IExtrinsicModel } from '../../data/SaveData';
import { AchievementSlug, ScoreType } from '../../data/ATSData';
import { GameEvents, IActivityLog } from '../../services/GameEvents';
import { SkillData } from '../../data/SkillData';
import { InfoPopup } from '../../components/domui/InfoPopup';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { CrawlerSlug } from '../../data/CrawlerData';
import { Facade } from '../..';
import { Colors } from '../../data/Colors';
import { SpellSlug } from './Spells/_SpellTypes';

export class GameKnowledge {
  public onAchievementUpdate = new JMEventListener<{slug: AchievementSlug, unlocked?: boolean, count?: string}>();
  public nodes: PlantNode[] = [];
  public normalNodes: PlantNode[] = [];
  public fruitNodes: PlantNode[] = [];
  public sortedNodes: {[key in NodeSlug]: PlantNode[]} = {
    'home': [],
    'lab': [],
    'generator': [],
    'grove': [],
    'stem': [],
    'bigstem': [],
    'hub': [],
    'core': [],
    'seedling': [],
    'enemy-core': [],
    'enemy-box': [],
    'food': [],
    'research': [],
    'battery': [],
    'gen': [],
    'burr': [],
    'wood': [],
    'big-evil': [],
    'small-evil': [],
    'leaf': [],
    'biglab': [],
    'wall': [],
    'amp': [],
    'volatile': [],
    'biggrove': [],
    'buffer': [],
  };

  public sortedCrawlers: {[key in CrawlerSlug]: CrawlerModel[]} = {
    crawler: [],
    chieftain: [],
    shaman: [],
  };

  public crawlerCount: number = 0;

  public totalGen: number = 0;
  public totalDrain: number = 0;

  public totalPower: number = 0;
  public totalMaxPower: number = 0;
  public seedlingPower: number = 0;
  public seedlingMaxPower: number = 0;

  private fpsCounter: number;
  private frames: number = 0;
  private fps: number = 0;

  private numBlobs: number = 0;
  private numRBlobs: number = 0;
  private numFBlobs: number = 0;
  private numBBlobs: number = 0;

  private highestHubResearch: number = 0;

  private extrinsic: IExtrinsicModel;

  constructor(private gameC: GameController, private manager: NodeManager) {
    gameC.onCrawlerAdded.addListener(this.crawlerAdded);
    gameC.onCrawlerRemoved.addListener(this.crawlerRemoved);
    gameC.onNodeAdded.addListener(this.nodeAdded);
    gameC.onNodeRemoved.addListener(this.nodeRemoved);
    gameC.onNodeClaimed.addListener(this.nodeClaimed);
    GameEvents.ACTIVITY_LOG.addListener(this.onActivityEvent);
    this.startFpsCounter();

    this.extrinsic = Facade.saveManager.getExtrinsic();
  }

  public destroy() {
    window.clearTimeout(this.fpsCounter);
    GameEvents.ACTIVITY_LOG.removeListener(this.onActivityEvent);
  }

  public initializeAchievements() {
    this.checkP10();
    this.checkBlob15();
    this.checkLaunch9Placement();
    this.checkCrawler15();
    this.checkCrawlerDie100();
    this.checkWeightSpell20();
    this.checkHubLevel(_.maxBy(this.extrinsic.hubLevels, el => el[1]));
  }

  public update() {
    this.seedlingMaxPower = 0;
    let gen = 0;
    let drain = 0;
    let power = 0;
    let maxPower = 0;
    this.normalNodes.forEach(node => {
      if (node.slug === 'seedling') {
        this.seedlingPower = node.power.powerCurrent;
        this.seedlingMaxPower = node.config.powerMax;
      } else {
        power += node.power.powerCurrent;
        maxPower += node.config.powerMax;
      }
      if (node.power.powerGen > 0) {
        gen += node.power.powerGen;
      } else {
        drain -= node.power.powerGen;
      }
    });

    this.totalPower = power;
    this.totalMaxPower = maxPower;

    this.frames++;

    this.totalGen = this.totalGen + (gen - this.totalGen) / 100;
    this.totalDrain = this.totalDrain + (drain - this.totalDrain) / 100;
  }

  public numFruitsPerNode(slug: NodeSlug) {
    return this.manager.getNodeConfig(slug).maxFruits;
  }

  public getCurrentResearch() {
    let seedling = this.sortedNodes.seedling[0];
    if (seedling) return seedling.power.researchCurrent;

    return 0;
  }

  public toString(): string {
    let powerPercent = this.totalPower / this.totalMaxPower;
    let powerColor = powerPercent > 1 ? Colors.overPowerGradient.getHexAt((powerPercent - 1) * 2) : Colors.powerGradient.getHexAt(powerPercent);
    let genColor = this.totalGen > this.totalDrain ? Colors.overPowerGradient.getHexAt(this.totalGen / this.totalDrain - 1) : Colors.powerGradient.getHexAt(this.totalGen / this.totalDrain);

    let m = `<div class='node-title'>Plant Overview</div>`;
    m += `<br>Power: <span style="color: ${powerColor}">${Math.round(this.totalPower)}</span> / ${Math.round(this.totalMaxPower)}`;
    if (this.seedlingMaxPower) {
      let seedlingColor = Colors.powerGradient.getHexAt(this.seedlingPower / this.seedlingMaxPower);
      m += `<br>Seedling Power: <span style="color: ${seedlingColor}">${Math.round(this.seedlingPower)}</span> / ${Math.round(this.seedlingMaxPower)}`;
    }
    m += `<br>Gen: <span style="color: ${genColor}">${(this.totalGen * 60).toFixed(0)}/s</span>, Drain: ${(this.totalDrain * 60).toFixed(0)}/s`;
    m += `<br><br>Crawlers: ${this.crawlerCount}`;
    m += `<br>Nodes: ${this.normalNodes.length}`;

    if (GOD_MODE) {
      m += '<br> --- <br> DEV STUFF <br><br>';
      m += `Research Blobs: ${this.numRBlobs}<br>Fruit Blobs: ${this.numFBlobs}<br>Fruit Blobs: ${this.numBBlobs}<br>`
      let keys = Object.keys(this.sortedNodes);
      keys.forEach(key => {
        let value = this.sortedNodes[key as NodeSlug].length;
        if (value !== 0) {
          m += `<br>${key}: ${value}`;
        }
      });
      m += `<br>FPS: ${this.fps}`;
      // if (this.crawlerCount > 0) {
      //   m += `<br>Claimed`;
      //   keys = Object.keys(this.sortedNodes);
      //   keys.forEach(key => {
      //     let value = this.sortedNodes[key as NodeSlug].filter(node => node.claimedBy).length;
      //     if (value !== 0) {
      //       m += `<br>${key}: ${value}`;
      //     }
      //   });
      // }
    }

    return m;
  }

  private crawlerAdded = (crawler: CrawlerModel) => {
    this.sortedCrawlers[crawler.slug].push(crawler);
    this.crawlerCount++;
    this.checkCrawler15();
  }

  private crawlerRemoved = (crawler: CrawlerModel) => {
    _.pull(this.sortedCrawlers[crawler.slug], crawler);
    this.crawlerCount--;
    this.extrinsic.scores[ScoreType.CRAWLERS_DEAD]++;
    this.checkCrawlerDie100();
  }

  private spellCast = (slug: SpellSlug) => {
    this.extrinsic.scores[ScoreType.WEIGHT_SPELLS_CAST]++;
    this.checkWeightSpell20();
  }

  private nodeAdded = (node: PlantNode) => {
    this.sortedNodes[node.slug].push(node);
    this.nodes.push(node);
    if (node.isFruit()) {
      this.fruitNodes.push(node);
    } else {
      this.normalNodes.push(node);
    }
    this.checkLaunch9Placement();
  }

  private nodeRemoved = (node: PlantNode) => {
    _.pull(this.sortedNodes[node.slug], node);
    _.pull(this.nodes, node);
    if (node.isFruit()) {
      _.pull(this.fruitNodes, node);
    } else {
      _.pull(this.normalNodes, node);
    }
    this.checkLaunch9Placement();
  }

  private nodeClaimed = (e: {claim: boolean, node: PlantNode, claimer: CrawlerModel}) => {
    // if (e.claim) {
    //   this.nodeClaims[e.node.slug]++;
    // } else {
    //   this.nodeClaims[e.node.slug]--;
    // }
  }

  private startFpsCounter() {
    this.fpsCounter = window.setTimeout(() => {
      this.fps = this.frames;
      this.frames = 0;
      this.startFpsCounter();
    }, 1000);
  }

  private onActivityEvent = (e: IActivityLog) => {
    switch (e.slug) {
      case 'PRESTIGE':
        this.extrinsic.scores[ScoreType.PRESTIGES] = (this.extrinsic.scores[ScoreType.PRESTIGES] + 1) || 1;
        this.checkP10();
        this.checkLaunch9Launch();
        this.checkTier3();
        break;
      case 'BLOB':
        if (e.data.add) {
          this.numBlobs++;
          if (e.data.type === 'research') {
            this.numRBlobs++;
          } else if (e.data.type === 'fruit') {
            this.numFBlobs++;
          } else {
            this.numBBlobs++;
          }
        } else {
          this.numBlobs--;
          if (e.data.type === 'research') {
            this.numRBlobs--;
          } else if (e.data.type === 'fruit') {
            this.numFBlobs--;
          } else {
            this.numBBlobs--;
          }
        }
        this.checkBlob15();
        break;
      case 'HUB':
        this.checkHubLevel(e.data);
        break
      case 'SPELL':
        this.spellCast(e.data);
    }
  }

  public achieveAchievement(slug: AchievementSlug) {
    if (!this.extrinsic.achievements[slug]) {
      this.extrinsic.achievements[slug] = true;
      let achievement = SkillData.achievements.find(data => data.slug === slug);
      this.manager.applySkill(achievement);
      new InfoPopup(`Achievement Unlocked: ${achievement.title}`);
      this.onAchievementUpdate.publish({slug, unlocked: true});
    }
  }

  private checkP10 = () => {
    if (!this.extrinsic.achievements[AchievementSlug.PRESTIGE_5]) {
      if (this.extrinsic.scores[ScoreType.PRESTIGES] >= 5) {
        this.achieveAchievement(AchievementSlug.PRESTIGE_5);
      } else {
        let count = `Current Launches: ${this.extrinsic.scores[ScoreType.PRESTIGES]} / 5`;
        this.onAchievementUpdate.publish({slug: AchievementSlug.PRESTIGE_5, count});
      }
    }
  }

  private checkBlob15 = () => {
    if (!this.extrinsic.achievements[AchievementSlug.BLOB_15]) {
      if (this.numBlobs >= 15) {
        this.achieveAchievement(AchievementSlug.BLOB_15);
      } else {
        let count = `Current Particles: ${this.numBlobs} / 15`;
        this.onAchievementUpdate.publish({slug: AchievementSlug.BLOB_15, count});
      }
    }
  }

  private checkHubLevel = (data: [string, number]) => {
    if (!data) return;
    
    if (!this.extrinsic.achievements[AchievementSlug.HUB_3]) {
      if (data[1] >= 3) {
        this.achieveAchievement(AchievementSlug.HUB_3);
      } else {
        this.highestHubResearch = Math.max(this.highestHubResearch, data[1])
        let count = `Highest Level: ${this.highestHubResearch}`;
        this.onAchievementUpdate.publish({slug: AchievementSlug.HUB_3, count});
      }
    }
  }

  private checkCrawler15 = () => {
    if (!this.extrinsic.achievements[AchievementSlug.CRAWLERS_15]) {
      if (this.crawlerCount >= 15) {
        this.achieveAchievement(AchievementSlug.CRAWLERS_15);
      } else {
        let count = `Current Crawlers: ${this.crawlerCount} / 15`;
        this.onAchievementUpdate.publish({slug: AchievementSlug.CRAWLERS_15, count});
      }
    }
  }

  private checkCrawlerDie100 = () => {
    if (!this.extrinsic.achievements[AchievementSlug.CRAWLERS_DIE_100]) {
      if (this.extrinsic.scores[ScoreType.CRAWLERS_DEAD] >= 100) {
        this.achieveAchievement(AchievementSlug.CRAWLERS_DIE_100);
      } else {
        let count = `Current Deaths: ${this.extrinsic.scores[ScoreType.CRAWLERS_DEAD]} / 100`;
        this.onAchievementUpdate.publish({slug: AchievementSlug.CRAWLERS_DIE_100, count});
      }
    }
  }

  private checkWeightSpell20 = () => {
    if (!this.extrinsic.achievements[AchievementSlug.CAST_WEIGHT_20]) {
      if (this.extrinsic.scores[ScoreType.WEIGHT_SPELLS_CAST] >= 20) {
        this.achieveAchievement(AchievementSlug.CAST_WEIGHT_20);
      } else {
        let count = `Focus Spells Cast: ${this.extrinsic.scores[ScoreType.WEIGHT_SPELLS_CAST]} / 20`;
        this.onAchievementUpdate.publish({slug: AchievementSlug.CAST_WEIGHT_20, count});
      }
    }
  }

  private checkLaunch9Placement = () => {
    if (!this.extrinsic.achievements[AchievementSlug.LAUNCH_DISTANCE_9]) {
      let seedling = this.gameC.nodes.find(node => node.slug === 'seedling');
      if (seedling) {
        this.onAchievementUpdate.publish({slug: AchievementSlug.LAUNCH_DISTANCE_9, count: `Current Distance: ${seedling.distanceCore} / 9`});
      } else {
        this.onAchievementUpdate.publish({slug: AchievementSlug.LAUNCH_DISTANCE_9, count: ' '});
      }
    }
  }

  private checkLaunch9Launch = () => {
    if (!this.extrinsic.achievements[AchievementSlug.LAUNCH_DISTANCE_9]) {
      let seedling = this.gameC.nodes.find(node => node.slug === 'seedling');
      if (seedling.distanceCore >= 9) {
        this.achieveAchievement(AchievementSlug.LAUNCH_DISTANCE_9);
      }
    }
  }

  private checkTier3 = () => {
    if (!this.extrinsic.achievements[AchievementSlug.TIER_3]) {
      let tier = this.extrinsic.skillTier;
      if (tier >= 2) {
        this.achieveAchievement(AchievementSlug.TIER_3);
      }
    }
  }
}

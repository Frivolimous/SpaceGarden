import _ from 'lodash';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { BaseCommand } from '../CrawlerCommands/_BaseCommand';
import { CrawlerView } from './CrawlerView';
import { AStarPath } from '../../../JMGE/others/AStar';
import { JMTween } from '../../../JMGE/JMTween';
import { GameKnowledge } from '../GameKnowledge';
import { JMEventListener } from '../../../JMGE/events/JMEventListener';
import { CrawlerSlug, ICrawlerConfig } from '../../../data/CrawlerData';
import { commandMap, CommandType } from '../CrawlerCommands/_CommandTypes';
import { TextureCache } from '../../../services/TextureCache';

export class CrawlerModel {
  public static generateUid() {
    CrawlerModel.cUid++;

    return this.cUid;
  }

  public static addUid(uid: number) {
    CrawlerModel.cUid = Math.max(uid, CrawlerModel.cUid);
  }

  public static resetUid() {
    CrawlerModel.cUid = 0;
  }

  private static cUid: number = 0;

  public onNodeClaimed = new JMEventListener<{claim: boolean, node: PlantNode, claimer: CrawlerModel}>();
  public claimedNode: PlantNode;

  public slug: CrawlerSlug = 'crawler';
  public uid: number;

  public type = 'crawler';

  public health: number = 1;
  public healthDrain: number;
  public view: CrawlerView;
  public speed: number;
  public cLoc: PlantNode;
  public currentCommand: BaseCommand;
  public preference: CommandType;

  public frustratedBy: string;

  private commandList: BaseCommand[] = [];

  constructor(public config: ICrawlerConfig, startingNode: PlantNode, private knowledge: GameKnowledge) {
    this.uid = CrawlerModel.generateUid();
    this.view = new CrawlerView(config.slug);
    this.cLoc = startingNode;

    this.slug = config.slug || this.slug;
    this.health = config.health;
    this.healthDrain = config.healthDrain;
    this.speed = config.speed;
    this.preference = config.preference || _.sample(config.preferenceList);
    config.commands.forEach(type => {
      this.commandList.push(new (commandMap[type])(this, config.commandConfig, knowledge));
    });

    this.setCommand(CommandType.IDLE);
  }

  public hasBuff(): boolean {
    return false;
  }

  public addBuff(count: number) {

  }

  public changeConfig(config: Partial<ICrawlerConfig>) {
    this.config = config;

    if (config.slug) {
      this.view.sprite.texture = TextureCache.getGraphicTexture(config.slug);
      this.slug = config.slug;
    }
    if (config.health) this.health = config.health;
    if (config.healthDrain) this.healthDrain = config.healthDrain;
    if (config.speed) this.speed = config.speed;
    if (config.preference) this.preference = config.preference;
    else if (config.preferenceList) this.preference = _.sample(config.preferenceList);

    if (config.commands) {
      this.commandList = [];
      config.commands.forEach(type => {
        this.commandList.push(new (commandMap[type])(this, config.commandConfig, this.knowledge));
      });

      this.setCommand(CommandType.IDLE);
    }
  }

  public destroy() {
    this.unclaimNode();
    console.log(`Crawler ${this.uid} destroyed`);
  }

  public isFruit = () => false;

  public selectNextCommand() {
    this.currentCommand = _.sortBy(this.commandList, command => command.genPriority())[0];
    this.currentCommand.initialize();
    this.colorTo(this.currentCommand.color);
  }

  public setCommand(type: CommandType = CommandType.NONE) {
    let command = this.commandList.find(data => data.type === type);
    if (command) {
      this.currentCommand = command;
      command.initialize();
      this.colorTo(command.color);
    } else {
      this.selectNextCommand();
    }
  }

  public colorTo(color: number) {
    new JMTween(this.view.sprite, 500).colorTo({tint: color}).start();
  }

  public update = () => {
    this.health -= this.healthDrain;
    this.currentCommand.update();
    if (this.currentCommand.isComplete) {
      this.selectNextCommand();
    }
  }

  public killMe = () => {
    this.health = -100;
  }

  public findPath(condition: (node: PlantNode) => boolean, start?: PlantNode): PlantNode[] {
    let pathing = new AStarPath(start || this.cLoc, condition);

    return pathing.path;
  }

  public claimNode = (node: PlantNode) => {
    if (node && !this.claimedNode && !node.claimedBy) {
      console.log(`${node.slug} ${node.uid} claimed by ${this.uid}`);
      this.claimedNode = node;
      this.claimedNode.claimedBy = this;
      this.onNodeClaimed.publish({claim: true, node, claimer: this});
    } else {
      console.log(`Claim unsuccessful. ${node ? node.slug + ' ' + node.uid.toString() : 'null'} not claimed by ${this.uid} (${!!this.claimedNode}, ${!!node.claimedBy})`);
    }
  }

  public unclaimNode = () => {
    if (this.claimedNode) {
      this.onNodeClaimed.publish({claim: false, node: this.claimedNode, claimer: this});
      if (this.claimedNode.outlets.length === 0 && this.claimedNode.fruits.length === 0) {
        this.claimedNode.flagDestroy = true;
      }
      console.log(`${this.claimedNode.slug + ' ' + this.claimedNode.uid.toString()} unclaimed by ${this.uid}`);
      this.claimedNode.claimedBy = null;
      this.claimedNode = null;
    }
  }

  public toString(): string {
    let m = `<div class='node-title'>${this.slug}</div>`;
    m += `Health: ${Math.floor(this.health * 100)}%`;
    m += `<br>Action: ${this.currentCommand ? CommandType[this.currentCommand.type] : 'NONE'}`;
    // if (this.currentCommand.type === CommandType.FRUSTRATED && this.frustratedBy) {
    //   m += ` by ${this.frustratedBy}`;
    // }
    if (this.preference) {
      m += `<br>Loves to ${CommandType[this.preference]}`;
    }
    return m;
  }
}

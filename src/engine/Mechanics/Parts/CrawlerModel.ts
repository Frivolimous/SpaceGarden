import _ from 'lodash';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { IdleCommand } from '../CrawlerCommands/IdleCommand';
import { EatCommand } from '../CrawlerCommands/EatCommand';
import { WanderCommand } from '../CrawlerCommands/WanderCommand';
import { BaseCommand, CommandType } from '../CrawlerCommands/_BaseCommand';
import { CrawlerView } from './CrawlerView';
import { AStarPath } from '../../../JMGE/others/AStar';
import { JMTween } from '../../../JMGE/JMTween';
import { DanceCommand } from '../CrawlerCommands/DanceCommand';
import { ResearchCommand } from '../CrawlerCommands/ResearchCommand';
import { PowerCommand } from '../CrawlerCommands/PowerCommand';
import { FrustratedCommand } from '../CrawlerCommands/FrustratedCommand';
import { BreedCommand } from '../CrawlerCommands/BreedCommand';
import { StarvingCommand } from '../CrawlerCommands/StarvingCommand';
import { GameKnowledge } from '../GameKnowledge';
import { JMEventListener } from '../../../JMGE/events/JMEventListener';

export class CrawlerModel {
  public static commandMap: {[key in CommandType]: typeof BaseCommand} = {
    [CommandType.NONE]: null,
    [CommandType.WANDER]: WanderCommand,
    [CommandType.IDLE]: IdleCommand,
    [CommandType.EAT]: EatCommand,
    [CommandType.DANCE]: DanceCommand,
    [CommandType.RESEARCH]: ResearchCommand,
    [CommandType.POWER]: PowerCommand,
    [CommandType.FRUSTRATED]: FrustratedCommand,
    [CommandType.STARVING]: StarvingCommand,
    [CommandType.BREED]: BreedCommand,
  };

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

  public slug: 'crawler' = 'crawler';
  public uid: number;

  public health: number = 1;
  public healthDrain: number;
  public view: CrawlerView;
  public speed: number;
  public cLoc: PlantNode;
  public currentCommand: BaseCommand;
  public preference: CommandType;

  public frustratedBy: string;

  private commandList: BaseCommand[] = [];

  constructor(public config: ICrawler, startingNode: PlantNode, private knowledge: GameKnowledge) {
    this.uid = CrawlerModel.generateUid();
    this.health = config.health;
    this.healthDrain = config.healthDrain;
    this.speed = config.speed;
    this.cLoc = startingNode;

    this.preference = config.preference || _.sample(config.preferenceList);
    this.view = new CrawlerView();

    config.commands.forEach(type => {
      this.commandList.push(new (CrawlerModel.commandMap[type])(this, config.commandConfig, knowledge));
    });

    this.setCommand(CommandType.IDLE);
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
    let m = `<div class='node-title'>Crawler</div>`;
    m += `Health: ${Math.floor(this.health * 100)}%`;
    m += `<br>Action: ${this.currentCommand ? CommandType[this.currentCommand.type] : 'NONE'}`;
    if (this.currentCommand.type === CommandType.FRUSTRATED && this.frustratedBy) {
      m += ` by ${this.frustratedBy}`;
    }
    if (this.preference) {
      m += `<br>Loves to ${CommandType[this.preference]}`;
    }
    return m;
  }
}

export interface ICrawler {
  health?: number;
  healthDrain?: number;
  breedThreshold?: number;
  speed?: number;
  commands?: CommandType[];
  preference?: CommandType;
  preferenceList?: CommandType[];
  commandConfig?: ICommandConfig;
}

export interface ICommandConfig {
  wanderRepeat: number;
  idleRepeat: number;
  frustratedRepeat: number;
  fruitSpeed: number;
  researchRatio: number;
  powerRatio: number;
  eatRatio: number;
  danceGen: number;
  danceTicks: number;
}

export const dCrawler: ICrawler = {
  health: 1,
  breedThreshold: 1.25,
  healthDrain: 0.0002,
  speed: 0.01,
  commands: [
    CommandType.WANDER,
    CommandType.IDLE,
    CommandType.EAT,
    // CommandType.DANCE,
    // CommandType.RESEARCH,
    // CommandType.POWER,
    CommandType.FRUSTRATED,
    CommandType.STARVING,
    // CommandType.BREED,
  ],
  preferenceList: [
    CommandType.WANDER,
    // CommandType.BREED,
    // CommandType.DANCE,
    // CommandType.RESEARCH,
    // CommandType.POWER,
  ],

  commandConfig: {
    wanderRepeat: 2,
    idleRepeat: 3,
    frustratedRepeat: 3,
    fruitSpeed: 0.95,
    researchRatio: 0.5,
    eatRatio: 0.0065,
    powerRatio: 2,
    danceGen: 0.25,
    danceTicks: 420,
  },
};

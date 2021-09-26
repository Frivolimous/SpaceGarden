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

  public slug: 'crawler' = 'crawler';

  public health: number = 1;
  public healthDrain: number;
  public view: CrawlerView;
  public speed: number;
  public cLoc: PlantNode;
  public currentCommand: BaseCommand;
  public preference: CommandType;
  public preferenceAmount = 0.5;

  private commandList: BaseCommand[] = [];

  constructor(private config: ICrawler, startingNode: PlantNode) {
    _.defaults(config, dCrawler);
    this.health = config.health;
    this.healthDrain = config.healthDrain;
    this.speed = config.speed;
    this.cLoc = startingNode;

    this.preference = config.preference || _.sample(config.preferenceList);
    this.view = new CrawlerView();

    config.commands.forEach(type => {
      this.commandList.push(new (CrawlerModel.commandMap[type])(this, config.commandConfig));
    });

    this.setCommand(CommandType.IDLE);
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

  public toString(): string {
    let m = `<div class='node-title'>Crawler</div>`;
    m += `Health: ${Math.floor(this.health * 100)}%`;
    m += `<br>Action: ${this.currentCommand ? CommandType[this.currentCommand.type] : 'NONE'}`;
    if (this.preference) {
      m += `<br>Loves to ${CommandType[this.preference]}`;
    }
    return m;
  }
}

export interface ICrawler {
  health?: number;
  healthDrain?: number;
  speed?: number;
  commands?: CommandType[];
  preference?: CommandType;
  preferenceList?: CommandType[];
  commandConfig?: ICommandConfig;
}

export interface ICommandConfig {
  breedHealthMin: number;
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

const dCrawler: ICrawler = {
  health: 1,
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
    breedHealthMin: 0.9,
    wanderRepeat: 3,
    idleRepeat: 3,
    frustratedRepeat: 3,
    fruitSpeed: 0.95,
    researchRatio: 1,
    powerRatio: 2,
    eatRatio: 0.0075,
    danceGen: 5,
    danceTicks: 500,
  },
};

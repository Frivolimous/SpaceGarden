import { CommandType } from '../engine/Mechanics/CrawlerCommands/_CommandTypes';
import { Colors } from './Colors';

export const CrawlerData: ICrawlerData = {
  data: [
    {
      slug: 'crawler',
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
        // CommandType.GET_EXECUTED,
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
    },
    {
      slug: 'chieftain',
      health: 1.5,
      healthDrain: 0.00015,
      speed: 0.015,
      maxCount: 1,
    },
    {
      slug: 'shaman',
      health: 1,
      maxCount: 1,
      breedThreshold: 10,
      healthDrain: 0.00013,
      speed: 0.007,
      commands: [
        CommandType.WANDER,
        CommandType.IDLE,
        CommandType.BUFF,
        CommandType.EAT,
        CommandType.FRUSTRATED,
        CommandType.STARVING,
        // CommandType.EXECUTE,
      ],
      preferenceList: [
        // CommandType.EXECUTE,
        CommandType.WANDER,
        CommandType.BUFF,
      ],

      commandConfig: {
        wanderRepeat: 1,
        idleRepeat: 1,
        frustratedRepeat: 3,
        fruitSpeed: 0.95,
        eatRatio: 0.0065,
        buffTime: 10,
        buffMult: 1.5,
      },
    },
  ],

  BaseAvailable: [
    'crawler',
    // 'shaman',
  ],
};

export interface ICrawlerData {
  data: ICrawlerConfig[];
  BaseAvailable: CrawlerSlug[];
}

export type CrawlerSlug = 'crawler' | 'chieftain' | 'shaman';

export interface ICrawlerConfig {
  slug?: CrawlerSlug;
  health?: number;
  healthDrain?: number;
  breedThreshold?: number;
  speed?: number;
  commands?: CommandType[];
  preference?: CommandType;
  preferenceList?: CommandType[];
  commandConfig?: ICommandConfig;
  maxCount?: number;
}

export interface ICommandConfig {
  wanderRepeat: number;
  idleRepeat: number;
  frustratedRepeat: number;
  fruitSpeed: number;
  eatRatio: number;
  researchRatio?: number;
  powerRatio?: number;
  danceGen?: number;
  danceTicks?: number;
  buffTime?: number;
  buffMult?: number;
}

export const CrawlerCommandColors: {[key in CommandType]: number} = {
  [CommandType.NONE]: Colors.Node.grey,
  [CommandType.WANDER]: Colors.Node.green,
  [CommandType.IDLE]: Colors.Node.darkgreen,
  [CommandType.EAT]: Colors.Node.blue,
  [CommandType.DANCE]: Colors.Node.lightyellow,
  [CommandType.RESEARCH]: Colors.Node.purple,
  [CommandType.POWER]: Colors.Node.yellow,
  [CommandType.FRUSTRATED]: Colors.Node.red,
  [CommandType.STARVING]: Colors.Node.darkblue,
  [CommandType.BREED]: Colors.Node.lightblue,
  [CommandType.BUFF]: Colors.Node.white,
  [CommandType.EXECUTE]: Colors.Node.grey,
  [CommandType.GET_EXECUTED]: Colors.Node.green,
}
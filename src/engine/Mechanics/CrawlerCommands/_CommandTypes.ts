import { IdleCommand } from './IdleCommand';
import { EatCommand } from './EatCommand';
import { WanderCommand } from './WanderCommand';
import { DanceCommand } from './DanceCommand';
import { ResearchCommand } from './ResearchCommand';
import { PowerCommand } from './PowerCommand';
import { FrustratedCommand } from './FrustratedCommand';
import { BreedCommand } from './BreedCommand';
import { StarvingCommand } from './StarvingCommand';
import { BaseCommand } from './_BaseCommand';
import { BuffCommand } from './BuffCommand';

export enum CommandType {
  NONE,
  WANDER,
  IDLE,
  EAT,
  DANCE,
  RESEARCH,
  POWER,
  FRUSTRATED,
  STARVING,
  BREED,
  BUFF,
}

export const commandMap: {[key in CommandType]: typeof BaseCommand} = {
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
  [CommandType.BUFF]: BuffCommand,
};

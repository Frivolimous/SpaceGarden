import { CrawlerModel } from '../Parts/CrawlerModel';
import { IdleCommand } from './IdleCommand';
import { WanderCommand } from './WanderCommand';

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
}

export class BaseCommand {
  public type: CommandType;
  public color: number;
  public isComplete: boolean = false;

  constructor(protected crawler: CrawlerModel) {
  }

  public initialize() {

  }

  public genPriority(): number {
    return 0;
  }

  public update() {

  }
}

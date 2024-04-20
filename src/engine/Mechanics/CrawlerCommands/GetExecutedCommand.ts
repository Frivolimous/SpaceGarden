import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { JMTween } from '../../../JMGE/JMTween';
import { PlantNode } from '../../nodes/PlantNode';
import { GameKnowledge } from '../GameKnowledge';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { ICommandConfig } from '../../../data/CrawlerData';
import { BaseCommand } from './_BaseCommand';
import { CommandType } from './_CommandTypes';

export class GetExecutedCommand extends BaseCommand {
  private state: 'walk' | 'tremble' | 'cancel';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.GET_EXECUTED;
    this.color = Colors.Node.red;
  }

  public initialize() {
    console.log('victim 1');
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(node => node === this.crawler.cLoc, this.waitHere, this.cancelPath, true);
  }

  public genPriority(): number {
    return 20;
  }

  public update() {
    console.log('victim 2');

    if (this.isComplete) return;
    if (this.state === 'walk') {
      this.updatePath(2);
    } else {
      this.crawler.view.vibrate(this.crawler.cLoc.view);
    }
  }

  public idleComplete = () => {
    this.startIdleLoop();
  }

  private waitHere = () => {
    this.state = 'tremble';
  }

  private cancelPath = () => {
    // this.isComplete = true;
    // this.crawler.setCommand(CommandType.STARVING);
  }
}

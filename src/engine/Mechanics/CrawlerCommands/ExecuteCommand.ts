import _ from 'lodash';
import { Colors } from '../../../data/Colors';
import { JMEasing, JMTween } from '../../../JMGE/JMTween';
import { PlantNode } from '../../nodes/PlantNode';
import { GameKnowledge } from '../GameKnowledge';
import { CrawlerModel } from '../Parts/CrawlerModel';
import { ICommandConfig } from '../../../data/CrawlerData';
import { BaseCommand } from './_BaseCommand';
import { CommandType } from './_CommandTypes';

export class ExecuteCommand extends BaseCommand {
  private state: 'execute' | 'walk' | 'return' | 'rest';

  private target: CrawlerModel;
  private hopping = false;

  private restTimer = 0;


  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);
    this.type = CommandType.EXECUTE;
    this.color = Colors.Node.blue;
  }

  public initialize() {
    this.isComplete = false;

    this.target = this.knowledge.sortedCrawlers.crawler.find(this.canExecute);
    this.state = 'walk'

    if (this.target) {
      this.prepVictim(this.target);
      this.startPath((node: PlantNode) => node === this.target.cLoc, this.executeTarget, this.cancelPath, false);
    } else {
      this.crawler.setCommand(CommandType.FRUSTRATED);
      this.crawler.frustratedBy = CommandType[this.type];
    }
  }

  public genPriority(): number {
    let numHere = this.knowledge.sortedCrawlers.crawler.filter(this.canExecute).length;
    
    if (numHere === 0) return 9;

    return 0;
    // return 1 - numHere * 0.1 - (this.crawler.preference === this.type ? 0.15 : 0);
  }

  public update() {
    if (this.isComplete) return;
    console.log(this.state);
    if (this.state === 'execute') {
      // this.standStill();
      if (!this.hopping) {
        this.target.killMe();
        this.state = 'return';
        this.startIdleReturn();
      }
    } else if (this.state === 'return') {
      this.updateIdle(this.returnComplete);
    } else if (this.state === 'rest') {
      this.restTimer -= 1;
      if (this.restTimer <= 0) {
        this.isComplete = true;
      }
    } else {
      this.updatePath();
    }
  }

  private prepVictim(target: CrawlerModel) {
    target.currentCommand.abortCommand();
    target.setCommand(CommandType.GET_EXECUTED);
  }

  public executeTarget = () => {
    this.state = 'execute';
    this.hop();
  }

  private hop = () => {
    this.hopping = true;

    let node = this.crawler.cLoc;
    let targetX = node.view.x + (- 3 / 4 + 6 / 4 * Math.random()) * node.view.radius;
    let targetY = node.view.y + (- 3 / 4 + 6 / 4 * Math.random()) * node.view.radius;

    let apexY = (node.view.y + targetY) / 2 - 15;
    new JMTween(this.crawler.view, 500).wait(500).to({y: apexY}).easing(JMEasing.Quadratic.Out).start()
    .chain(this.crawler.view, 100).to({y: targetY}).easing(JMEasing.Quadratic.In);

    new JMTween(this.crawler.view, 600).wait(500).to({x: targetX}).easing(JMEasing.Quadratic.InOut).start().onComplete(() => this.hopping = false);
  }

  private canExecute = (crawler: CrawlerModel) => {
    return (crawler.toExecute && crawler.health > 0 && (crawler.cLoc === this.crawler.cLoc || crawler.currentCommand.nextLoc === this.crawler.cLoc));
  }

  private cancelPath = () => {
    this.isComplete = true;
    this.crawler.setCommand(CommandType.FRUSTRATED);
    this.crawler.frustratedBy = CommandType[this.type];
  }

  private returnComplete = () => {
    // this.isComplete = true;
    this.restTimer = 2000 / 60;
    this.state = 'rest';
  }
}

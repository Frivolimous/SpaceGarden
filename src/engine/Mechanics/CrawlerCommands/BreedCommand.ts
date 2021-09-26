import _ from 'lodash';
import { Config } from '../../../Config';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { GameKnowledge } from '../GameKnowledge';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { BaseCommand, CommandType } from './_BaseCommand';

export class BreedCommand extends BaseCommand {
  private state: 'eat' | 'walk';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);
    this.type = CommandType.BREED;
    this.color = Colors.Node.lightblue;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.hasFood, this.eatHere, this.cancelPath, true);
  }

  public genPriority(): number {
    let numCrawlers = this.knowledge.crawlerCount;
    let numFood = this.knowledge.sortedNodes.food.filter(node => node.isHarvestable()).length;

    if (numFood > 0 && numCrawlers < numFood) {
      return 0.5 + numCrawlers / numFood * 0.5 - (this.crawler.preference === this.type ? 0.25 : 0);
    }

    return 20;
  }

  public update() {
    if (this.isComplete) return;
    if (this.state === 'eat') {
      this.standStill();
      this.dragFruit();
    } else {
      this.updatePath();
    }
  }

  private hasFood(node: PlantNode): boolean {
    return (node.power.fruitType === 'food' && node.hasHarvestableFruit());
  }

  private eatHere = () => {
    this.state = 'eat';
    let fruit = this.crawler.claimedNode || this.crawler.cLoc.harvestFruit();

    this.grabFruit(fruit, () => {
      this.deliverFruit(() => {
        this.crawler.health += fruit.power.powerCurrent * this.config.eatRatio;
        if (this.crawler.health < Config.CRAWLER.BREED_AT) {
          this.initialize();
        } else {
          this.isComplete = true;
        }
      });
    });
  }

  private cancelPath = () => {
    this.isComplete = true;
    this.crawler.setCommand(CommandType.FRUSTRATED);
    this.crawler.frustratedBy = CommandType[this.type];
  }
}

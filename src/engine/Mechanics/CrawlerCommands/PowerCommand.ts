import { BaseCommand, CommandType } from './_BaseCommand';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { GameKnowledge } from '../GameKnowledge';

export class PowerCommand extends BaseCommand {
  private state: 'walk' | 'harvest' | 'carry' | 'deliver';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.POWER;
    this.color = Colors.Node.yellow;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.hasPower, this.harvestHere, this.cancelPath, true);
  }

  // lowest is better
  public genPriority(): number {
    let numGen = this.knowledge.sortedNodes.gen.filter(node => node.isHarvestable()).length;

    if (numGen <= 0) {
      return 20;
    }

    let seedling = this.crawler.cLoc.findNode(node => node.slug === 'seedling');
    let closest = this.crawler.cLoc.findNode(node => node.power.powerPercent < 0.5);

    return Math.min(
      seedling ? 0.25 + 0.75 * seedling.power.powerPercent : 20,
      closest ? 0.5 + closest.power.powerPercent : 20)
       - (this.crawler.preference === this.type ? 0.25 : 0);
  }

  public update() {
    if (this.isComplete) return;
    this.dragFruit();
    if (this.state === 'harvest' || this.state === 'deliver') {
      this.standStill();
    } else {
      this.updatePath();
    }
  }

  private hasPower(node: PlantNode): boolean {
    return node.power.fruitType === 'gen' && node.hasHarvestableFruit();
  }

  private isDeliverable(node: PlantNode): boolean {
    return node.slug !== 'stem' && node.power.powerPercent < 0.5 || node.slug === 'seedling';
  }

  private harvestHere = () => {
    this.state = 'harvest';
    let fruit = this.crawler.claimedNode || this.crawler.cLoc.harvestFruit();

    this.grabFruit(fruit, () => {
      this.state = 'carry';
      this.startPath(this.isDeliverable, this.deliverHere, this.cancelPath);
    });
  }

  private cancelPath = (prepath: boolean) => {
    this.isComplete = true;
    if (this.fruit) {
      this.fruit.flagDestroy = true;
      this.fruit = null;
    }
    this.crawler.setCommand(CommandType.FRUSTRATED);
    this.crawler.frustratedBy = CommandType[this.type];
  }

  private deliverHere = () => {
    this.state = 'deliver';

    this.deliverFruit(fruit => {
      this.isComplete = true;
      this.crawler.cLoc.power.powerCurrent += (fruit.power.powerCurrent * this.config.powerRatio);
    });
  }
}

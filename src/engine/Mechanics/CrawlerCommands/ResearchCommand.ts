import { BaseCommand, CommandType } from './_BaseCommand';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';
import { GameKnowledge } from '../GameKnowledge';

export class ResearchCommand extends BaseCommand {

  private state: 'walk' | 'harvest' | 'carry' | 'deliver';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig, knowledge: GameKnowledge) {
    super(crawler, config, knowledge);

    this.type = CommandType.RESEARCH;
    this.color = Colors.Node.purple;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.hasResearch, this.harvestHere, this.cancelPath, true);
  }

  public genPriority(): number {
    let numResearch = this.knowledge.sortedNodes.research.filter(node => node.isHarvestable()).length;

    let numLabs = this.knowledge.sortedNodes.lab.length;
    let fruitPerLab = this.knowledge.numFruitsPerNode('lab');

    if (numResearch === 0 || this.knowledge.sortedNodes.seedling.length === 0) {
      return 20;
    }

    let researchRatio = Math.max(1, numResearch / (fruitPerLab * numLabs));

    return 1 - researchRatio * 0.5 - this.crawler.preference === this.type ? 0.25 : 0;
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

  private hasResearch(node: PlantNode): boolean {
    return node.power.fruitType === 'research' && node.hasHarvestableFruit();
  }

  private isSeed(node: PlantNode): boolean {
    return node.slug === 'seedling';
  }

  private harvestHere = () => {
    this.state = 'harvest';
    let fruit = this.crawler.claimedNode || this.crawler.cLoc.harvestFruit();

    this.grabFruit(fruit, () => {
      this.state = 'carry';
      this.startPath(this.isSeed, this.deliverHere, this.cancelPath);
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
      this.crawler.cLoc.receiveResearch(fruit.power.powerCurrent * this.config.researchRatio);
    });
  }
}

import { BaseCommand, CommandType } from './_BaseCommand';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../../engine/nodes/PlantNode';
import { CrawlerModel, ICommandConfig } from '../Parts/CrawlerModel';

export class ResearchCommand extends BaseCommand {

  private state: 'walk' | 'harvest' | 'carry' | 'deliver';

  constructor(crawler: CrawlerModel, protected config: ICommandConfig) {
    super(crawler, config);

    this.type = CommandType.RESEARCH;
    this.color = Colors.Node.purple;
  }

  public initialize() {
    this.isComplete = false;

    this.state = 'walk';
    this.startPath(this.hasResearch, this.harvestHere, this.cancelPath);
  }

  public genPriority(): number {
    if (!this.crawler.cLoc.findNode(this.hasResearch)) {
      return 10;
    }
    if (this.crawler.cLoc.findNode(node => node.slug === 'seedling')) {
      return 0.3 + Math.random() * 0.5 - (this.crawler.preference === this.type ? this.crawler.preferenceAmount : 0);
    } else {
      return 2;
    }
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
    let fruit = this.crawler.cLoc.harvestFruit();

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
  }

  private deliverHere = () => {
    this.state = 'deliver';

    this.deliverFruit(fruit => {
      this.isComplete = true;
      this.crawler.cLoc.receiveResearch(fruit.power.powerCurrent * this.config.researchRatio);
    });
  }
}

import { Config } from '../../../Config';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { TransferBlock } from './_TransferBlock';
import _ from 'lodash';

export class ResearchBlock extends TransferBlock {
  private blobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall'))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall' && (outlet.outlets.length >= 2 || outlet.config.slug === 'seedling')))),
  ];

  constructor(amount: number, fade: number = 0) {
    super('research', amount, fade);
  }

  public get color(): number{ 
    return this.amped ? Colors.Node.lightPurple : Colors.Node.purple;
  }

  public getAi(level: number) {
    return this.blobAI[level];
  }
  
  public finishTransfer(): boolean {
    if (this.target.slug === 'seedling') {
      this.target.receiveResearch(this.amount);
      return true;
    } else if (this.target.slug === 'hub' && this.target.power.canReceiveResearch) {
      this.target.receiveResearch(this.amount);
      return true;
    } else {
      this.checkAndAmplify();
      this.fade--;
      if (this.fade <= 0) return true;

      return false;
    }
  }
}
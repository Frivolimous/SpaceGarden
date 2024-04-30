import { Config } from '../../../Config';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { TransferBlock } from './_TransferBlock';
import _ from 'lodash';

export class BuffBlock extends TransferBlock {
  private blobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall'))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall' && (outlet.outlets.length >= 2 || outlet.canGetBuffed())))),
  ];

  constructor(amount: number, fade: number = 0) {
    super('buff', amount, fade);
  }

  public get color(): number{ 
    return this.amped ? Colors.Node.white : Colors.Node.grey;
  }

  public getAi(level: number) {
    return this.blobAI[level];
  }

  public finishTransfer(): boolean {
    if (!this.target.power.isBuffed && this.target.canGetBuffed()) {
      this.target.receiveBuff(this.amount);
      return true;
    // } else if (target.slug === 'hub' && target.power.canReceiveBuff) {
    //   target.receiveBuff(block.amount);
    //   return true;
    } else {
      this.checkAndAmplify();
      this.fade--;
      if (this.fade <= 0) return true;

      return false;
    }
  }
}
import { Config } from '../../../Config';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { TransferBlock } from './_TransferBlock';
import _ from 'lodash';

export class GrowBlock extends TransferBlock {
  private blobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall'))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall' && (outlet.outlets.length >= 2 || outlet.canSpawnFruit())))),
  ];

  constructor(amount: number, fade: number = 0) {
    super('grow', amount, fade);
  }

  public get color(): number{ 
    return Colors.Node.white;
  }

  public finishTransfer(): boolean {
    this.target.power.powerCurrent += this.amount;

    return true;
  }
}
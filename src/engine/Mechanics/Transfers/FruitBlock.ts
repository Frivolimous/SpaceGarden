import { Config } from '../../../Config';
import { Colors } from '../../../data/Colors';
import { PlantNode } from '../../nodes/PlantNode';
import { TransferBlock } from './_TransferBlock';
import _ from 'lodash';

export class FruitBlock extends TransferBlock {
  private blobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall'))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall' && (outlet.outlets.length >= 2 || outlet.canSpawnFruit())))),
  ];

  constructor(amount: number, fade: number = 0) {
    super('fruit', amount, fade);
  }

  public get color(): number{ 
    return this.amped ? Colors.Node.lightOrange : Colors.Node.orange;
  }

  public getAi(level: number) {
    return this.blobAI[level];
  }
  
  public finishTransfer(): boolean {
    if (this.target.canSpawnFruit() && Math.random() < Config.NODE.FRUIT_APPLY) {
      this.target.receiveFruitPower(this.amount);
      return true;
    } else if (this.target.slug === 'hub' && this.target.power.canReceiveFruit) {
      this.target.receiveFruitPower(this.amount);
      return true;
    } else {
      this.checkAndAmplify();
      this.fade--;
      if (this.fade <= 0) return true;

      return false;
    }
  }
}
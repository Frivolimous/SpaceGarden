import { Config } from '../../../Config';
import { Colors } from '../../../data/Colors';
import { IBlob } from '../../FDG/FDGLink';
import { PlantNode } from '../../nodes/PlantNode';
import _ from 'lodash';

export abstract class TransferBlock {
  public type: TransferType;
  public amount: number;
  public fade: number;
  public amped:boolean;

  public origin: PlantNode;
  public target: PlantNode;

  public defaultAI = (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall')));

  public get color(): number {
    return 0;
  }

  constructor(type: TransferType, amount: number, fade: number = 0) {
    this.type = type;
    this.amount = amount;
    this.fade = fade;
  }

  public setSource(origin: PlantNode, target: PlantNode) {
    this.origin = origin;
    this.target = target;
  }

  public getAi(level: number) {
    return this.defaultAI;
  }

  public executeStart() {

  }

  public executeComplete() {

  }

  public makeBlob(): IBlob {
    return {x: this.origin.view.x, y: this.origin.view.y, color: this.color, size: this.amped ? 3 : 2, fade: this.fade};

  }

  public finishTransfer() {
    return true;
  }

  public selectNextTarget(): PlantNode {
    let ai = this.getAi(Config.NODE.BLOB_AI);

    let target2 = ai(this.origin, this.target);
    if (!target2) target2 = _.sample(this.target.outlets.filter(outlet => (outlet.active && outlet.slug !== 'wall')));
    return target2;
  }

  protected checkAndAmplify() {
    if (this.target.slug === 'amp' && !this.amped) {
      this.fade += Config.NODE.AMP_FADE_BOOST;
      this.amount *= Config.NODE.AMP_AMOUNT;
      this.amped = true;
    }
  }
}

export type TransferType = 'grow' | 'fruit' | 'research' | 'buff';
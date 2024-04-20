import { Config } from '../../Config';
import { Colors } from '../../data/Colors';
import { IBlob } from '../FDG/FDGLink';
import { GameController } from '../Mechanics/GameController';
import { PlantNode } from '../nodes/PlantNode';
import _ from 'lodash';

export class TransferBlock {
  public type: TransferType;
  public amount: number;
  public fade: number;
  public removeOrigin: boolean;
  public amped:boolean;

  public origin: PlantNode;
  public target: PlantNode;

  private rBlobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall'))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall' && (outlet.outlets.length >= 2 || outlet.config.slug === 'seedling')))),
  ];

  private fBlobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall'))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall' && (outlet.outlets.length >= 2 || outlet.canSpawnFruit())))),
  ];
  private bBlobAI = [
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall'))),
    (origin: PlantNode, target: PlantNode) => _.sample(target.outlets.filter(outlet => (outlet.active && outlet !== origin && outlet.slug !== 'wall' && (outlet.outlets.length >= 2 || outlet.canGetBuffed())))),
  ];

  public get color(): number {
    switch(this.type) {
      case 'fruit': return this.amped ? Colors.Node.lightOrange : Colors.Node.orange;
      case 'research': return this.amped ? Colors.Node.lightPurple : Colors.Node.purple;
      case 'buff': return this.amped ? Colors.Node.grey : Colors.Node.white;
    }

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

  public executeStart() {

  }

  public executeComplete() {

  }

  public makeBlob(): IBlob {
    return {x: this.origin.view.x, y: this.origin.view.y, color: this.color, size: this.amped ? 3 : 2, fade: this.fade};

  }

  public finishTransferGrow() {
    this.target.power.powerCurrent += this.amount;
  }

  public finishTransferResearch(): boolean {
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

  public selectNextTarget(): PlantNode {
    let index = Config.NODE.BLOB_AI
    let ai = this.type === 'research' ? this.rBlobAI[index] :
      this.type === 'fruit' ? this.fBlobAI[index] :
      this.type === 'buff' ? this.bBlobAI[index] : null;

    let target2 = ai(this.origin, this.target);
    if (!target2) target2 = _.sample(this.target.outlets.filter(outlet => (outlet.active && outlet.slug !== 'wall')));
    return target2;
  }

  public finishTransferFruit(): boolean {
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

  public finishTransferBuff(): boolean {
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

  private checkAndAmplify() {
    if (this.target.slug === 'amp' && !this.amped) {
      this.fade += Config.NODE.AMP_FADE_BOOST;
      this.amount *= Config.NODE.AMP_AMOUNT;
      this.amped = true;
    }
  }
}

export type TransferType = 'grow' | 'fruit' | 'research' | 'buff';
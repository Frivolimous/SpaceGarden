import _ from 'lodash';
import { Config } from '../../Config';
import { INodeConfig, NodeSlug } from '../../data/NodeData';
import { PlantNode } from './PlantNode';

export class PlantNodePower {
  public fruitType: NodeSlug;
  public OVERCHARGE_PERCENT: number = 1.1;
  public fruitChain: number = 0;
  public maxFruits: number = 0;

  public powerCurrent: number = 0;
  public fruitCurrent: number = 0;
  public researchCurrent: number = 0;
  public _PowerGen: number = 0;

  public powerTick: number = 0;
  public fruitSpawn: number = 0;

  constructor(private config: INodeConfig, private data: PlantNode, private transferPower: TransferPowerFunction) {
    this.fruitType = config.fruitType;
    this.fruitChain = config.fruitChain;
    this.maxFruits = config.maxFruits;

    this._PowerGen = config.powerGen;
    this.powerTick = config.powerDelay;
  }

  public get powerPercent(): number {
    return this.powerCurrent / this.config.powerMax;
    // return Math.min(this.powerCurrent / this.config.powerMax, 1);
  }

  public set powerPercent(n: number) {
    this.powerCurrent = this.config.powerMax * n;
  }

  public get powerPercentOne(): number {
    return Math.min(this.powerCurrent / this.config.powerMax, 1);
  }

  public get powerPercentOver(): number {
    return Math.min(this.powerCurrent / this.config.powerMax, this.OVERCHARGE_PERCENT);
  }

  public get powerWeight(): number {
    return this.powerCurrent / this.config.powerMax / this.config.powerWeight;
  }

  public get powerGen() {
    if (this._PowerGen > 0) {
      return this._PowerGen * this.powerPercentOne;
    } else {
      return this._PowerGen * this.powerPercent;
    }
  }

  public get fruitGen() {
    return (this.config.fruitGen || 0) * this.powerPercentOne;
  }

  public get researchGen() {
    return (this.config.researchGen || 0) * this.powerPercentOne;
  }

  public onTick() {
    if (this.powerGen > 0) {
      if (this.powerCurrent < this.config.powerMax) {
        this.powerCurrent += this.powerGen;
      }
    } else {
      if (this.powerCurrent > 0) {
        this.powerCurrent += this.powerGen;
      }
    }

    this.powerTick--;
    if (this.powerTick <= 0) {
      this.powerTick = this.config.powerDelay + (-3 + 6 * Math.random());

      this.generateSpecial();
      this.powerFruits();
      // this.spawnFruits();
      this.attemptTransferPower();
    }
  }

  public generateSpecial() {
    if (this.config.researchGen > 0) {
      let target = _.sample(this.data.outlets.filter(outlet => outlet.active));

      if (target) {
        this.researchCurrent += this.researchGen;
        if (this.researchCurrent > Math.max(this.config.researchGen * 10, 1)) {
          let clump = this.researchCurrent;
          this.researchCurrent = 0;
          this.transferPower(this.data, target, {type: 'research', amount: clump, fade: Config.NODE.GEN_FADE});
        }
      }
    }

    if (this.config.fruitGen > 0) {
      let target = _.sample(this.data.outlets.filter(outlet => outlet.active));

      if (target) {
        this.fruitCurrent += this.fruitGen;
        if (this.fruitCurrent > Math.max(this.config.fruitGen * 10, 1)) {
          if (this.data.canSpawnFruit() && Math.random() < Config.NODE.FRUIT_APPLY) {
            this.data.receiveFruitPower(this.fruitCurrent);
          } else {
            let clump = this.fruitCurrent;
            this.transferPower(this.data, target, {type: 'fruit', amount: clump, fade: Config.NODE.GEN_FADE});
          }
          this.fruitCurrent = 0;
        }
      }
    }
  }

  public powerFruits() {
    if (this.data.fruits.length > 0 && this.powerPercent >= Config.NODE.FRUIT_THRESHOLD) {
      let target: PlantNode;
      this.data.fruits.forEach(fruit => {
        if (!target || fruit.power.powerPercentOver < target.power.powerPercentOver) {
          target = fruit;
        }
      });

      if (this.powerPercentOver > target.power.powerPercentOver) {
        let clump = Math.min(this.powerCurrent, this.config.fruitClump);
        this.transferPower(this.data, target, {type: 'grow', amount: clump, removeOrigin: clump === this.powerCurrent && (this.data.outlets.length + this.data.fruits.length === 1)});
        this.powerCurrent -= clump;
      }
    }
  }

  public attemptTransferPower() {
    if (this.config.powerClump > 0 && this.data.outlets.length > 0 && this.powerPercent > Config.NODE.POWER_THRESHOLD) {
      let target: PlantNode;

      this.data.outlets.forEach(outlet => {
        if (outlet.active) {
          if (outlet.power.powerPercentOver < this.powerPercentOver && (!target || outlet.power.powerWeight < target.power.powerWeight)) {
            target = outlet;
          }
        }
      });

      if (target) {
        let clump = Math.min(this.powerCurrent, this.config.powerClump);
        this.transferPower(this.data, target, {type: 'grow', amount: clump});
        this.powerCurrent -= clump;
      }
    }
  }
}

export type TransferPowerFunction = (origin: PlantNode, target: PlantNode, block: ITransferBlock) => void;

export interface ITransferBlock {
  type: 'grow' | 'fruit' | 'research';
  amount: number;
  fade?: number;
  removeOrigin?: boolean;
}

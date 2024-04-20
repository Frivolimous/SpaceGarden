import _ from 'lodash';
import { Config } from '../../Config';
import { INodeConfig, NodeSlug } from '../../data/NodeData';
import { PlantNode } from './PlantNode';
import { TransferBlock } from '../Transfers/_TransferBlock';

export class PlantNodePower {
  public fruitType: NodeSlug;
  public OVERCHARGE_PERCENT: number = 1;
  public fruitChain: number = 0;
  public maxFruits: number = 0;

  public powerCurrent: number = 0;
  public fruitCurrent: number = 0;
  public buffCurrent: number = 0;
  public powerClump: number = 0;
  public researchCurrent: number = 0;
  public isBuffed = false;
  
  // HUB ONLY
  public storedPowerCurrent: number = 0;
  public canReceiveResearch: boolean = true;
  public canReceiveFruit: boolean = true;
  public canStorePower: boolean = true;

  public _ResearchGen: number = 0;
  public _PowerGen: number = 0;

  public powerTick: number = 0;
  public fruitSpawn: number = 0;

  constructor(private config: INodeConfig, private data: PlantNode, private transferPower: TransferPowerFunction) {
    this.fruitType = config.fruitType;
    this.fruitChain = config.fruitChain;
    this.maxFruits = config.maxFruits;
    this.powerClump = config.powerClump

    this._PowerGen = config.powerGen;
    this._ResearchGen = config.researchGen;
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

  private get powerPercentOver(): number {
    return Math.min(this.powerCurrent / this.config.powerMax, this.OVERCHARGE_PERCENT);
  }

  public get powerWeight(): number {
    return this.powerCurrent / this.config.powerMax / this.config.powerWeight;
  }

  public get powerGen() {
    if (this._PowerGen > 0) {
      return this._PowerGen * this.powerPercentOne * (this.isBuffed ? Config.NODE.BUFF_BOOST : 1);
    } else {
      return this._PowerGen * this.powerPercent;
    }
  }

  public get fruitGen() {
    return (this.config.fruitGen || 0) * this.powerPercentOne * (this.isBuffed ? Config.NODE.BUFF_BOOST : 1);
  }

  public get buffGen() {
    return (this.config.buffGen || 0) * this.powerPercentOne * (this.isBuffed ? Config.NODE.BUFF_BOOST : 1);
  }

  public get researchGen() {
    return (this._ResearchGen || 0) * this.powerPercentOne * (this.isBuffed ? Config.NODE.BUFF_BOOST : 1);
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

    if (this.data.slug === 'hub' && this.canStorePower) {
      this.storedPowerCurrent -= this.powerGen;
      this.powerCurrent += this.powerGen;
    }

    if (this.powerCurrent < this.config.powerMax && this.data.isFruit()) {
      this.powerCurrent += Config.NODE.FRUIT_GROWTH;
    }

    this.powerTick--;
    if (this.powerTick <= 0) {
      this.powerTick += this.config.powerDelay * (0.9 + 0.2 * Math.random());

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
          let block = new TransferBlock('fruit', clump, Config.NODE.GEN_FADE);
          block.setSource(this.data, target);
          this.transferPower(block);
        }
      }
    }

    if (this.config.fruitGen > 0) {
      let target = _.sample(this.data.outlets.filter(outlet => outlet.active));

      if (target) {
        this.fruitCurrent += this.fruitGen;
        if (this.fruitCurrent > 1) {
          this.fruitCurrent--;
          if (this.data.canSpawnFruit() && Math.random() < Config.NODE.FRUIT_APPLY) {
            this.data.receiveFruitPower(1);
          } else {
            let block = new TransferBlock('fruit', 1, Config.NODE.GEN_FADE);
            block.setSource(this.data, target);
            this.transferPower(block);
          }
        }
      }
    }
    
    if (this.config.buffGen > 0) {
      let target = _.sample(this.data.outlets.filter(outlet => outlet.active));
      
      if (target) {
        this.buffCurrent+= this.buffGen;
        if (this.buffCurrent > 1) {
          this.buffCurrent--;
          let block = new TransferBlock('buff', 1, Config.NODE.BUFF_FADE);
          block.setSource(this.data, target);
          this.transferPower(block);
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
      
      if (this.powerPercentOver > target.power.powerPercentOver && target.power.powerPercentOver < target.power.OVERCHARGE_PERCENT) {
        let clump = Math.min(this.powerCurrent, this.config.fruitClump);
        let block = new TransferBlock('grow', clump);
        block.removeOrigin = clump === this.powerCurrent && (this.data.outlets.length + this.data.fruits.length === 1);
        block.setSource(this.data, target);
        this.transferPower(block);
        this.powerCurrent -= clump;
      }
    }
  }

  public attemptTransferPower() {
    if (this.powerClump > 0 && this.data.outlets.length > 0 && this.powerPercent > Config.NODE.POWER_THRESHOLD) {
      let target: PlantNode;

      this.data.outlets.forEach(outlet => {
        if (outlet.active) {
          if (outlet.power.powerPercentOver < this.powerPercentOver && (!target || outlet.power.powerWeight < target.power.powerWeight)) {
            target = outlet;
          }
        }
      });

      if (target && this.powerCurrent >= this.powerClump) {
        let clump = Math.min(this.powerCurrent, this.powerClump);
        let block = new TransferBlock('grow', clump);
        block.setSource(this.data, target);
        this.transferPower(block);
        this.powerCurrent -= clump;
      }
    }
  }
}

export type TransferPowerFunction = (block: TransferBlock) => void;


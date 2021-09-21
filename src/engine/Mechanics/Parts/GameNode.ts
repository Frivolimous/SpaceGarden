import * as _ from 'lodash';
import { Config } from '../../../Config';
import { Colors } from '../../../data/Colors';
import { NodeConfig, NodeSlug } from "../../../data/NodeData";
import { JMEventListener } from "../../../JMGE/events/JMEventListener";
import { NodeManager } from "../../../services/NodeManager";
import { TextureCache } from "../../../services/TextureCache";
import { FDGNode } from "../../FDG/FDGNode";

export class GameNode {
  private static cUid: number = 0;
  public static generateUid() {
    GameNode.cUid++;

    return this.cUid;
  }

  public static addUid(uid: number) {
    GameNode.cUid = Math.max(uid, GameNode.cUid);
  }

  public static resetUid() {
    GameNode.cUid = 0;
  }

  public uid: number;
  public onUpdate = new JMEventListener<GameNode>();
  public outlets: GameNode[] = [];
  public fruits: GameNode[] = [];

  active = true;

  fruitType: NodeSlug;
  fruitChain: number = 0;
  maxFruits: number = 0;

  powerCurrent: number = 0;
  fruitCurrent: number = 0;
  researchCurrent: number = 0;
  powerGen: number = 0;
  
  powerTick: number = 0;
  fruitSpawn: number = 0;

  constructor(public view: FDGNode, public config: NodeConfig, private transferPower: TransferPowerFunction) {
    this.uid = GameNode.generateUid();
    view.data = this;
    view.intensity = 1;

    this.fruitType = config.fruitType;
    this.fruitChain = config.fruitChain;
    this.maxFruits = config.maxFruits;

    this.powerGen = config.powerGen;
    this.powerTick = config.powerDelay;
  }

  public get powerPercent(): number {
    return Math.min(this.powerCurrent / this.config.powerMax, 1);
  }

  public set powerPercent(n: number) {
    this.powerCurrent = this.config.powerMax * n;
  }

  public get powerWeight(): number {
    return this.powerCurrent / this.config.powerMax * this.config.powerWeight;
  }

  public removeNode(node: GameNode) {
    if (node.config.type === 'fruit') {
      for (let i = 0; i < this.fruits.length; i++) {
        if (this.fruits[i] === node) {
          this.fruits.splice(i, 1);
          return;
        }
      }
    } else {
      for (let i = 0; i < this.outlets.length; i++) {
        if (this.outlets[i] === node) {
          this.outlets.splice(i, 1);
          return;
        }
      }
    }
  }

  public removeAllNodes() {
    this.outlets.forEach(node => {
      node.removeNode(this);
    });
    this.fruits.forEach(fruit => {
      fruit.removeNode(this);
    })
    this.outlets = [];
    this.fruits = [];
  }

  public checkConnections(testFunction: (node: GameNode) => boolean): boolean {
    let open: GameNode[] = [this];
    let closed: GameNode[] = [];

    while (open.length > 0) {
      let current = open.shift();
      if (testFunction(current)) return true;

      closed.push(current);

      current.outlets.forEach(node => {
        if (!open.includes(node) && !closed.includes(node)) {
          open.push(node);
        }
      });
    }

    return false;
  }

  public linkNode(target: GameNode, forceOutlet = false) {
    if (target.config.type === 'fruit' && !forceOutlet) {
      this.fruits.push(target);
      target.fruitType = target.fruitType || this.fruitType;
      target.fruitChain = this.fruitChain - 1;
      target.maxFruits = target.maxFruits || this.maxFruits;
    } else {
      this.outlets.push(target);

      if (target.config.type !== 'fruit' && this.config.outletEffects) {
        this.config.outletEffects.forEach(effect => {
          console.log('effect', effect, this, target);
          if (effect.type === 'additive') {
            console.log('additive', effect.amount, this.config.slug, target.config.slug);
            (target as any)[effect.stat] += effect.amount;
          } else {
            (target as any)[effect.stat] *= effect.amount;
          }
        });
      }
    }
  }

  public getPowerGen() {
    if (this.powerGen > 0) {
      return this.powerGen * this.powerPercent;
    } else {
      return this.powerGen * this.powerPercent;
    }
  }
  
  public getFruitGen() {
    return (this.config.fruitGen || 0) * this.powerPercent;
  }

  public getResearchGen() {
    return (this.config.researchGen || 0) * this.powerPercent;
  }

  public onTick = () => {
    if (!this.active) return;

    if (this.powerGen > 0) {
      if (this.powerCurrent < this.config.powerMax) {
        // console.log(this.config.powerMax, this.powerGen, this.powerCurrent)
        this.powerCurrent += this.getPowerGen();
      }
    } else {
      if (this.powerCurrent > 0) {
        this.powerCurrent += this.getPowerGen();
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

    if (this.view) this.view.intensity = this.powerPercent;
    this.onUpdate.publish(this);
  }

  public generateSpecial() {
    if (this.config.researchGen > 0) {
      let target = _.sample(this.outlets.filter(outlet => outlet.active));

      if (target) {
        this.researchCurrent += this.getResearchGen();
        if (this.researchCurrent > Math.max(this.config.researchGen * 10, 1)) {
          let clump = this.researchCurrent;
          this.researchCurrent = 0;
          this.transferPower(this, target, {type: 'research', amount: clump, fade: Config.NODE.GEN_FADE});
        }
      }
    }

    if (this.config.fruitGen > 0) {
      let target = _.sample(this.outlets.filter(outlet => outlet.active));

      if (target) {
        this.fruitCurrent += this.getFruitGen();
        if (this.fruitCurrent > Math.max(this.config.fruitGen * 10, 1)) {          
          if (this.canSpawnFruit() && Math.random() < Config.NODE.FRUIT_APPLY) {
            this.receiveFruitPower(this.fruitCurrent, Colors.Node.orange);
          } else {
            let clump = this.fruitCurrent;
            this.transferPower(this, target, {type: 'fruit', amount: clump, fade: Config.NODE.GEN_FADE});
          }
          this.fruitCurrent = 0;
        }
      }
    }
  }

  public powerFruits() {
    if (this.fruits.length > 0 && this.powerPercent >= Config.NODE.FRUIT_THRESHOLD) {
      let target: GameNode;
      for (let i = 0; i < this.fruits.length; i++) {
        if (!target || this.fruits[i].powerPercent < target.powerPercent) {
          target = this.fruits[i];
        }
      }

      if (this.powerPercent > target.powerPercent) {
        let clump = Math.min(this.powerCurrent, this.config.fruitClump);
        this.transferPower(this, target, {type: 'grow', amount: clump, removeOrigin: clump === this.powerCurrent && (this.outlets.length + this.fruits.length === 1)});
        this.powerCurrent -= clump;
      }
    }
  }

  public attemptTransferPower() {
    if (this.config.powerClump > 0 && this.outlets.length > 0 && this.powerPercent > Config.NODE.POWER_THRESHOLD) {
      let target: GameNode;

      this.outlets.forEach(outlet => {
        if (outlet.active) {
          if (outlet.powerPercent < this.powerPercent && (!target || outlet.powerWeight < target.powerWeight)) {
            target = outlet;
          }
        }
      });

      if (target) {
        let clump = Math.min(this.powerCurrent, this.config.powerClump);
        this.transferPower(this, target, {type: 'grow', amount: clump});
        this.powerCurrent -= clump;
      }
    }
  }

  public canSpawnFruit(): boolean {
    if (this.fruitChain <= 0) return false;
    if (this.fruits.length < this.config.maxFruits) return true;

    if (this.fruits.length > 0) {
      return _.some(this.fruits, fruit => fruit.canSpawnFruit());
    }

    return false;
  }
  
  public receiveFruitPower = (amount: number, color: number) => {
    console.log("RECEIVED!", this.config.slug, this.canSpawnFruit(), this.fruits.length, this.config.maxFruits)
    if (this.fruits.length < this.config.maxFruits) {
      this.fruitSpawn += amount;
      this.view.pulse(color);
    } else {
      let fruit = this.fruits.find(fruit => fruit.canSpawnFruit());
      if (fruit) {
        fruit.receiveFruitPower(amount, color);
      }
    }
  }

  public receiveResearch = (amount: number) => {
    this.view.pulse(Colors.Node.purple);
    this.researchCurrent += amount;
  }

  public toString(): string {
    let m = `<div class="node-title">${this.config.slug.charAt(0).toUpperCase() + this.config.slug.slice(1)}</div>
        Power: ${Math.round(this.powerCurrent)} / ${this.config.powerMax}`;
    if (!this.active) {
      m += `<p style="color: #eedd33;">[ Drag to your network in order to connect the new node ]</p>`
    } else {
      if (this.config.slug === 'seedling') {
        m += `<br>Research Points: ${Math.round(this.researchCurrent)}`;
      }
      if (this.getPowerGen() > 0) {
        m += `<br>Power Gen: ${this.getPowerGen().toFixed(2)}/s (transfer: ${(this.config.powerClump / this.config.powerDelay).toFixed(2)}/s)`;
      } else {
        m += `<br>Power Drain: ${-this.getPowerGen().toFixed(2)}/s (transfer: ${(this.config.powerClump / this.config.powerDelay).toFixed(2)}/s)`;
      }
      if (this.getResearchGen() > 0) {
        m += `<br>Research Gen: ${this.getResearchGen().toFixed(2)}`;
      }
      if (this.getFruitGen() > 0) {
        m += `<br>Fruit Gen: ${this.getFruitGen().toFixed(2)}`;
      }
      if (this.config.maxLinks > 1) {
        m += `<br>Connections: ${this.outlets.length} / ${this.config.maxLinks}`;
      }
      // if (this.maxFruits > 0) {
      //   m += `<br>Fruits: ${this.fruits.length} / ${this.maxFruits}`;
      // }
    }
    return m;
  }
}

export type TransferPowerFunction = (origin: GameNode, target: GameNode, block: TransferBlock) => void;

export interface NodeSave {
  uid: number;
  slug: NodeSlug;
  powerCurrent: number;
  researchCurrent: number;
  outlets: number[];
  x: number,
  y: number,
}

export interface TransferBlock {
  type: 'grow' | 'fruit' | 'research';
  amount: number;
  fade?: number;
  removeOrigin?: boolean;
}

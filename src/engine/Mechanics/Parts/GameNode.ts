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

  type: 'normal' | 'fruit' = 'normal';

  active = true;

  fruitType: NodeSlug;
  fruitChain: number = 0;
  maxFruits: number = 0;

  powerCurrent: number = 0;
  powerTick: number = 0;
  fruitSpawn: number = 0;

  constructor(public view: FDGNode, public config: NodeConfig, private transferPower: TransferPowerFunction) {
    this.uid = GameNode.generateUid();
    view.data = this;
    view.intensity = 1;

    this.fruitType = config.fruitType;
    this.fruitChain = config.fruitChain;
    this.maxFruits = config.maxFruits;
  }

  public get powerPercent(): number {
    return this.powerCurrent / this.config.powerMax;
  }

  public set powerPercent(n: number) {
    this.powerCurrent = this.config.powerMax * n;
  }

  public get powerWeight(): number {
    return this.powerCurrent / this.config.powerMax;
    // return 1 - (1 - this.powerCurrent / this.config.powerMax) * this.config.powerWeight;
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
    } else {
      this.outlets.push(target);
    }
  }

  public getPowerGen() {
    if (this.config.powerGen > 0) {
      return this.config.powerGen * this.powerPercent;
    } else {
      return this.config.powerGen * this.powerPercent;
    }
  }

  public onTick = () => {
    if (!this.active) return;

    if (this.config.powerDelay > -1) {
      if (this.config.powerGen > 0) {
        if (this.powerCurrent < this.config.powerMax) {
          // console.log(this.config.powerMax, this.config.powerGen, this.powerCurrent)
          this.powerCurrent += this.getPowerGen();
        }
      } else {
        if (this.powerCurrent > 0) {
          this.powerCurrent += this.getPowerGen();
        }
      }

      this.powerTick++;
      if (this.powerTick >= this.config.powerDelay) {
        this.powerTick = 0;

        this.powerFruits();
        this.spawnFruits();
        this.attemptTransferPower();
      }
    }

    if (this.view) this.view.intensity = Math.min(1, this.powerPercent);
    this.onUpdate.publish(this);
  }

  public powerFruits() {
    if (this.fruits.length > 0 && this.powerPercent >= 0.9) {
      let target: GameNode;
      for (let i = 0; i < this.fruits.length; i++) {
        if (!target || this.fruits[i].powerPercent < target.powerPercent) {
          target = this.fruits[i];
        }
      }

      if (this.powerPercent > target.powerPercent) {
        let clump = Math.min(this.powerCurrent, this.config.fruitClump);
        this.transferPower(this, target, clump, clump === this.powerCurrent && (this.outlets.length + this.fruits.length === 1));
        this.powerCurrent -= clump;
      }
    }
  }

  public attemptTransferPower() {
    if (this.config.powerClump > 0 && this.outlets.length > 0 && this.powerPercent > 0.25) {
      let target: GameNode;

      for (let i = 0; i < this.outlets.length; i++) {
        if (!this.outlets[i].active) continue;
        if (!target || this.outlets[i].powerPercent < target.powerPercent) {
          target = this.outlets[i];
        }
      }

      if (target && this.powerWeight > target.powerWeight) {
        let clump = Math.min(this.powerCurrent, this.config.powerClump);
        this.transferPower(this, target, clump);
        this.powerCurrent -= clump;
      }
    }
  }

  public spawnFruits() {
    if (this.fruitChain > 0 && this.powerPercent >= 0.8 && this.fruits.length < this.maxFruits) {
      this.fruitSpawn += this.config.fruitClump * 2;
      this.powerCurrent -= this.config.fruitClump;
    }
  }

  public toString(): string {
    let m = `<div class="node-title">${this.config.name.charAt(0).toUpperCase() + this.config.name.slice(1)}</div>
        Power: ${Math.round(this.powerCurrent)} / ${this.config.powerMax}<br>`;
    if (this.getPowerGen() > 0) {
      m += `Gen: ${this.getPowerGen().toFixed(2)} (transfer: ${(this.config.powerClump / this.config.powerDelay).toFixed(2)})<br>`;
    } else {
      m += `Drain: ${-this.getPowerGen().toFixed(2)} (transfer: ${(this.config.powerClump / this.config.powerDelay).toFixed(2)})<br>`;
    }
    m += `Connections: ${this.outlets.length} / ${this.config.maxLinks}`;
    if (this.maxFruits > 0) {
      m += `<br>Fruits: ${this.fruits.length} / ${this.maxFruits}`;
    }
    return m;
  }

  public static importSave(save: NodeSave, transferPower: TransferPowerFunction): GameNode {
    let config = NodeManager.getNodeConfig(save.slug);

    let texture = TextureCache.getNodeGraphicTexture(config.shape, config.radius);

    let m = new GameNode(new FDGNode(texture, config), config, transferPower);
    m.powerCurrent = save.powerCurrent;
    
    m.uid = save.uid;
    
    m.view.position.set(save.x, save.y);

    GameNode.addUid(save.uid);
    return m;
  }
}

export type TransferPowerFunction = (origin: GameNode, target: GameNode, amount: number, andRemove?: boolean) => void;

export interface NodeSave {
  uid: number;
  slug: NodeSlug;
  powerCurrent: number;
  outlets: number[];
  x: number,
  y: number,
}

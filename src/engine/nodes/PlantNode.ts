import _ from 'lodash';
import { TextureCache } from '../../services/TextureCache';
import { INodeConfig, NodeSlug } from '../../data/NodeData';
import { PlantNodePhysics } from './PlantNodePhysics';
import { PlantNodePower, TransferPowerFunction } from './PlantNodePower';
import { PlantNodeView } from './PlantNodeView';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { Colors } from '../../data/Colors';

export class PlantNode {
  public static generateUid() {
    PlantNode.cUid++;

    return this.cUid;
  }

  public static addUid(uid: number) {
    PlantNode.cUid = Math.max(uid, PlantNode.cUid);
  }

  public static resetUid() {
    PlantNode.cUid = 0;
  }

  private static cUid: number = 0;

  public uid: number;
  public outlets: PlantNode[] = [];
  public fruits: PlantNode[] = [];

  public active = true;

  public view: PlantNodeView;
  public physics: PlantNodePhysics;
  public power: PlantNodePower;

  constructor(private config: INodeConfig, transferPower: TransferPowerFunction) {
    _.defaults(config, dNodeConfig);

    this.uid = PlantNode.generateUid();

    let texture = TextureCache.getNodeGraphicTexture(config.shape, config.radius);
    this.view = new PlantNodeView(texture, config.color, config.radius);
    this.physics = new PlantNodePhysics(config, this.view);
    this.power = new PlantNodePower(config, this, transferPower);
  }

  get slug(): NodeSlug {
    return this.config.slug;
  }

  set ghostMode(b: boolean) {
    if (b) {
      this.view.alpha = 0.5;
      this.physics.hasMass = false;
    } else {
      this.view.alpha = 1;
      this.physics.hasMass = true;
    }
  }

  get maxOutlets(): number {
    return this.config.maxLinks;
  }

  public destroy() {
    this.view.destroy();
  }

  public isFruit(): boolean {
    return this.config.type === 'fruit';
  }

  public canSpawnFruit(): boolean {
    if (this.power.fruitChain <= 0) return false;
    if (this.fruits.length < this.config.maxFruits) return true;

    if (this.fruits.length > 0) {
      return _.some(this.fruits, fruit => fruit.canSpawnFruit());
    }

    return false;
  }

  public isConnectedToCore(): boolean {
    return this.checkConnections(node => node.config.slug === 'core');
  }

  public removeNode(node: PlantNode) {
    if (node.isFruit()) {
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
    });
    this.outlets = [];
    this.fruits = [];
  }

  public checkConnections(testFunction: (node: PlantNode) => boolean): boolean {
    let open: PlantNode[] = [this];
    let closed: PlantNode[] = [];

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

  public setFruitStats(fruitType: NodeSlug, maxFruits: number, fruitChain: number) {
    this.power.fruitType = this.power.fruitType || fruitType;
    this.power.maxFruits = this.power.maxFruits || maxFruits;
    this.power.fruitChain = fruitChain;
  }

  public linkNode(target: PlantNode, forceOutlet = false) {
    if (target.isFruit() && !forceOutlet) {
      this.fruits.push(target);
      target.setFruitStats(this.power.fruitType, this.power.maxFruits, this.power.fruitChain - 1);

    } else {
      this.outlets.push(target);

      if (!target.isFruit() && this.config.outletEffects) {
        this.config.outletEffects.forEach(effect => {
          if (effect.type === 'additive') {
            (target as any)[effect.stat] += effect.amount;
          } else {
            (target as any)[effect.stat] *= effect.amount;
          }
        });
      }
    }
  }

  public receiveFruitPower = (amount: number) => {
    if (this.fruits.length < this.config.maxFruits) {
      this.power.fruitSpawn += amount;
      this.view.pulse(Colors.Node.orange);
    } else {
      let fruit = this.fruits.find(fruit2 => fruit2.canSpawnFruit());
      if (fruit) {
        fruit.receiveFruitPower(amount);
      }
    }
  }

  public receiveResearch = (amount: number) => {
    this.view.pulse(Colors.Node.purple);
    this.power.researchCurrent += amount;
  }

  public tickPower() {
    if (this.active) {
      this.power.onTick();
      this.view.setIntensity(this.power.powerPercent);
    }
  }

  public tickPhysics() {
    this.physics.moveBody();
    this.view.adjustIntensity();

  }

  public toString(): string {
    let m = `<div class='node-title'>${this.config.slug.charAt(0).toUpperCase() + this.config.slug.slice(1)}</div>
        Power: ${Math.round(this.power.powerCurrent)} / ${this.config.powerMax}`;
    if (!this.active) {
      m += `<p style='color: #eedd33;'>[ Drag to your network in order to connect the new node ]</p>`;
    } else {
      if (this.config.slug === 'seedling') {
        m += `<br>Research Points: ${Math.round(this.power.researchCurrent)}`;
      }
      if (this.power.powerGen > 0) {
        m += `<br>Power Gen: ${this.power.powerGen.toFixed(2)}/s (transfer: ${(this.config.powerClump / this.config.powerDelay).toFixed(2)}/s)`;
      } else {
        m += `<br>Power Drain: ${-this.power.powerGen.toFixed(2)}/s (transfer: ${(this.config.powerClump / this.config.powerDelay).toFixed(2)}/s)`;
      }
      // m += `<br>Weight: ${Math.round(this.powerWeight * 100)} / ${Math.round(this.powerPercent * 100)}`;

      if (this.power.researchGen > 0) {
        m += `<br>Research Gen: ${this.power.researchGen.toFixed(2)}`;
      }
      if (this.power.fruitGen > 0) {
        m += `<br>Fruit Gen: ${this.power.fruitGen.toFixed(2)}`;
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

  public showConnectionCount(show: boolean) {
    if (show) {
      this.view.showConnectionCount(this.outlets.length, this.config.maxLinks);
    } else {
      this.view.showConnectionCount(0, 0);
    }
  }
}

const dNodeConfig: Partial<INodeConfig> = {
  color: 0xffffff,
  radius: 10,
  mass: 1,
  force: 1,
};

export interface INodeSave {
  uid: number;
  slug: NodeSlug;
  powerCurrent: number;
  researchCurrent: number;
  outlets: number[];
  x: number;
  y: number;
}

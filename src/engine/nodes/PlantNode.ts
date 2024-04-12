import _ from 'lodash';
import { TextureCache } from '../../services/TextureCache';
import { INodeConfig, NodeSlug } from '../../data/NodeData';
import { PlantNodePhysics } from './PlantNodePhysics';
import { PlantNodePower, TransferPowerFunction } from './PlantNodePower';
import { PlantNodeView } from './PlantNodeView';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { Colors } from '../../data/Colors';
import { CrawlerModel } from '../Mechanics/Parts/CrawlerModel';
import { ColorGradient } from '../../JMGE/others/Colors';

export class PlantNode {
  public static powerGradient = new ColorGradient(0xcc0000, 0xffffff);
  public static overPowerGradient = new ColorGradient(0xffffff, 0xffff00);

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
  public type: 'normal' | 'fruit';
  public outlets: PlantNode[] = [];
  public fruits: PlantNode[] = [];

  public active = true;
  public exists = true;

  public view: PlantNodeView;
  public physics: PlantNodePhysics;
  public power: PlantNodePower;

  public flagUnlink = false;
  public flagDestroy = false;
  public flagCallOnRemove = true;
  public claimedBy: CrawlerModel;

  // public distanceSeedling: number = 0;
  public distanceCore: number = 0;

  constructor(public config: INodeConfig, transferPower: TransferPowerFunction) {
    _.defaults(config, dNodeConfig);

    this.uid = PlantNode.generateUid();
    this.type = config.type;

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
    this.exists = false;
    this.view.destroy();
    console.log(`${this.slug} ${this.uid} destroyed`);
  }

  public isFruit(): boolean {
    return this.type === 'fruit';
  }

  public canSpawnFruit(): boolean {
    if (!this.power.fruitType) return false;
    if (this.power.fruitChain <= 0) return false;
    if (this.fruits.length < this.config.maxFruits) return true;

    if (this.fruits.length > 0) {
      return _.some(this.fruits, fruit => fruit.canSpawnFruit());
    }

    return false;
  }

  public canClaim(): boolean {
    return !this.claimedBy && this.exists && !this.flagDestroy;
  }

  public isConnectedToCore(): boolean {
    return this.findNode(node => node.config.slug === 'core') !== null;
  }

  public findCore() {
    return this.findNode(node => node.config.slug === 'core');
  }

  public removeNode(node: PlantNode) {
    if (node.isFruit()) {
      for (let i = 0; i < this.fruits.length; i++) {
        if (this.fruits[i] === node) {
          this.fruits.splice(i, 1);
          if (!this.isFruit() && node.config.outletEffects) {
            node.config.outletEffects.forEach(effect => {
              if (effect.type === 'additive') {
                (this.power as any)[effect.stat] -= effect.amount;
              } else {
                (this.power as any)[effect.stat] /= effect.amount;
              }
            });
          }
          return;
        }
      }
    } else {
      for (let i = 0; i < this.outlets.length; i++) {
        if (this.outlets[i] === node) {
          this.outlets.splice(i, 1);
          if (!this.isFruit() && node.config.outletEffects) {
            node.config.outletEffects.forEach(effect => {
              if (effect.type === 'additive') {
                (this.power as any)[effect.stat] -= effect.amount;
              } else {
                (this.power as any)[effect.stat] /= effect.amount;
              }
            });
          }
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

  public findNode(testFunction: (node: PlantNode) => boolean): PlantNode {
    let open: PlantNode[] = [this];
    let closed: PlantNode[] = [];

    while (open.length > 0) {
      let current = open.shift();
      if (testFunction(current)) return current;

      closed.push(current);

      current.outlets.forEach(node => {
        if (!open.includes(node) && !closed.includes(node)) {
          open.push(node);
        }
      });
    }

    return null;
  }

  public distanceTo(testFunction: (node: PlantNode) => boolean): number {
    let open: {node: PlantNode, distance: number}[] = [{node: this, distance: 0}];
    let closed: {node: PlantNode, distance: number}[] = [];

    while (open.length > 0) {
      let current = open.shift();
      if (testFunction(current.node)) return current.distance;

      closed.push(current);

      current.node.outlets.forEach(node => {
        if (!_.some(open, data => data.node === node) && !_.some(closed, data => data.node === node)) {
          open.push({node, distance: current.distance + 1});
        }
      });
    }

    return -1;
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
            (target.power as any)[effect.stat] += effect.amount;
          } else {
            (target.power as any)[effect.stat] *= effect.amount;
          }
        });
      }
    }
  }

  public receiveFruitPower = (amount: number) => {
    if (this.slug === 'hub') {
      this.view.pulse(Colors.Node.orange);
      this.power.fruitCurrent += amount;
    } else {
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
  }

  public receiveResearch = (amount: number) => {
    this.view.pulse(Colors.Node.purple);
    this.power.researchCurrent += amount;
  }

  public isHarvestable = (): boolean => {
    return this.power.powerPercent > 0.6 && !this.claimedBy;
  }

  public hasHarvestableFruit(): boolean {
    return _.some(this.fruits, fruit => (fruit.isHarvestable() || fruit.hasHarvestableFruit()));
  }

  public harvestFruit(): PlantNode {
    let fruit: PlantNode;
    fruit = _.sample(_.filter(this.fruits, fruit2 => fruit2.isHarvestable()));

    if (!fruit) return null;

    return fruit.harvestFruit() || fruit;
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
    let powerColor = this.power.powerPercent > 1 ? PlantNode.overPowerGradient.getHexAt((this.power.powerPercent - 1) * 2) : PlantNode.powerGradient.getHexAt(this.power.powerPercent);
    let nameColor = this.config.color.toString(16);
    while (nameColor.length < 6) nameColor = '0' + nameColor;
    let m = `<div class='node-title' style='color: #${nameColor}'>${this.config.slug.charAt(0).toUpperCase() + this.config.slug.slice(1)}</div>
        Power: <span style="color: ${powerColor}">${Math.round(this.view._Intensity * this.config.powerMax)}</span> / ${this.config.powerMax}`;
    if (!this.active) {
      m += `<p style='color: #eedd33;'>[ Drag to your network in order to connect the new node ]</p>`;
    } else {
      if (this.config.slug === 'seedling') {
        m += `<br>Research Points: ${Math.round(this.power.researchCurrent)}`;
      }
      if (this.power.powerGen > 0) {
        m += `<br>Power Gen: ${(this.power.powerGen * 60).toFixed(0)}/s (transfer: ${(this.power.powerClump / this.config.powerDelay * 60).toFixed(0)}/s)`;
      } else if (this.power.powerGen < 0) {
        m += `<br>Power Drain: ${(-this.power.powerGen * 60).toFixed(0)}/s`;
        if (this.config.maxLinks > 1) m += ` (transfer: ${(this.power.powerClump / this.config.powerDelay * 60).toFixed(0)}/s)`;
      } else if (this.config.maxLinks > 1){
        m += `<br>Transfer: ${(this.power.powerClump / this.config.powerDelay * 60).toFixed(0)}/s`
      }
      
      // m += `<br>Weight: ${Math.round(this.powerWeight * 100)} / ${Math.round(this.powerPercent * 100)}`;

      if (this.power.researchGen > 0) {
        m += `<br>Research Gen: ${(this.power.researchGen / this.config.powerDelay * 60 * 60).toFixed(0)}/min`;
      }
      if (this.power.fruitGen > 0) {
        m += `<br>Fruit Gen: ${(this.power.fruitGen / this.config.powerDelay * 60 * 60).toFixed(0)}/min`;
      }
      if (this.config.maxLinks > 1) {
        m += `<br>Connections: ${this.outlets.length} / ${this.config.maxLinks}`;
      }
      // m += `<br>Distance to Core: ${this.distanceCore}`;
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
  researchCurrent?: number;
  fruitCurrent?: number;
  storedPowerCurrent?: number;
  receiveResearch?: boolean;
  receiveFruit?: boolean;
  receivePower?: boolean;
  outlets: number[];
  x: number;
  y: number;
}

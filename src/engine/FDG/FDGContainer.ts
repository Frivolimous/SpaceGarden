import * as PIXI from 'pixi.js';
import * as _ from 'lodash';

import { FDGLink } from './FDGLink';
import { FDGNode } from './FDGNode';
import { JMRect } from '../../JMGE/others/JMRect';
import { NodeSlug } from '../../data/NodeData';
import { JMEventListener } from '../../JMGE/events/JMEventListener';
import { Config } from '../../Config';

export class FDGContainer extends PIXI.Graphics {
  public onNodeAdded = new JMEventListener<FDGNode>();
  public onNodeRemoved = new JMEventListener<FDGNode>();

  private nodes: FDGNode[] = [];
  public links: FDGLink[] = [];
  private pulls: IPull[] = [];

  constructor(private borders: JMRect) {
    super();
  }

  public addPull(pull: IPull) {
    this.pulls.push(pull);
  }

  public removePull(pull: IPull) {
    _.pull(this.pulls, pull);
  }

  public addNode(node: FDGNode) {
    this.nodes.push(node);
    this.addChild(node);
    this.onNodeAdded.publish(node);
  }

  public removeNode(node: FDGNode) {
    let i = this.nodes.indexOf(node);

    if (i < 0) return;

    let outlets = _.clone(node.data.outlets);
    let fruits = _.clone(node.data.fruits);
    this.removeAllLinksFor(node);

    this.nodes.splice(i, 1);

    node.destroy();
    this.onNodeRemoved.publishSync(node);

    outlets.forEach(n => {
      if (!n.view.isConnectedToCore()) {
        this.removeNode(n.view);
      }
    });

    fruits.forEach(n => this.removeNode(n.view));
  }

  public removeAllLinksFor(node: FDGNode, excludeFruit?: boolean) {
    for (let i = this.links.length - 1; i >= 0; i--) {
      if (this.links[i].hasNode(node)) {
        if (excludeFruit && this.links[i].other(node).config.type === 'fruit') continue;

        this.links[i].origin.removeNode(this.links[i].target);
        this.links[i].target.removeNode(this.links[i].origin);
        this.links.splice(i, 1);
      }
    };
  }

  public getLink(origin: FDGNode, target: FDGNode): FDGLink {
    return this.links.find(link => link.hasNode(origin) && link.hasNode(target));
  }

  public linkNodes(origin: FDGNode, target: FDGNode): FDGLink {
    origin.linkNode(target);
    target.linkNode(origin, true);

    let link = new FDGLink(origin, target);

    this.links.push(link);

    return link;
  }

  public removeLink(origin: FDGNode, target: FDGNode) {
    for (let i = this.links.length; i >= 0; i--) {
      if (this.links[i].hasNode(origin, target)) {
        origin.removeNode(target);
        target.removeNode(origin);
        this.links.splice(i, 1);
        return;
      }
    }
  }


  public getClosestObject(config: { x: number, y: number, distance?: number, maxLinks?: boolean, filter?: FDGNode, notType?: NodeSlug, notFruit?: boolean }) {
    let m: FDGNode;

    let distance = config.distance ? config.distance * config.distance : Config.PHYSICS.MAX_GRAB * Config.PHYSICS.MAX_GRAB;

    for (let i = 0; i < this.nodes.length; i++) {
      let node = this.nodes[i];

      if (config.maxLinks && node.data.outlets.length >= node.config.maxLinks) continue;
      if (config.filter && config.filter === node) continue;
      if (config.notType && node.config.slug === config.notType) continue;
      if (config.notFruit && node.config.type === 'fruit') continue;
      // if (par.hasTag!=null && par.hasTag!==this.nodes[i].tag) continue;
      // if (par.notHasTag!=null && par.notHasTag===this.nodes[i].tag) continue;

      let x = node.x - config.x;
      let y = node.y - config.y;

      let distance2 = x * x + y * y;

      if (distance2 < distance) {
        distance = distance2;
        m = node;
      }
    }

    return m;
  }

  public showConnectionCount(show: boolean = true) {
    this.nodes.forEach(node => node.config.type !== 'fruit' ? node.showConnectionCount(show) : null);
  }

  // ==== RUNNING ==== \\

  public onTick(ticks: number) {
    this.applyForces();
    this.drawLinks();
  }

  public drawLinks = () => {
    this.clear();
    this.links.forEach(link => {
      link.drawTo(this);
    });
  }

  public applyForces = () => {
    this.links.forEach(link => {
      if (!link.active) return;

      link.onTick.publish();

      let dX = link.origin.x - link.target.x;
      let dY = link.origin.y - link.target.y;
      let dist2 = dX * dX + dY * dY;
      let dist = Math.sqrt(dist2);

      if (dist < link.length) return;

      let mult = Config.PHYSICS.ELASTICITY * (link.length - dist) / dist;

      link.origin.v.x += mult * dX * link.origin.iMass;
      link.origin.v.y += mult * dY * link.origin.iMass;
      link.target.v.x -= mult * dX * link.target.iMass;
      link.target.v.y -= mult * dY * link.target.iMass;
    });

    this.pulls.forEach(pull => {
      pull.node.v.x *= Config.PHYSICS.PULL_FRICTION;
      pull.node.v.y *= Config.PHYSICS.PULL_FRICTION;

      let dX = pull.node.x - pull.x;
      let dY = pull.node.y - pull.y;
      let dist2 = dX * dX + dY * dY;

      if (dist2 > Config.PHYSICS.TARGET_MIN) {
        if (!pull.minD || dist2 > pull.minD * pull.minD) {
          let mult = -Config.PHYSICS.TARGET_RATIO;

          if (pull.force) mult *= pull.force;

          pull.node.v.x += mult * dX * pull.node.iMass;
          pull.node.v.y += mult * dY * pull.node.iMass;
        }
      }
    });

    for (let i = this.nodes.length - 1; i >= 0; i--) {
      let node = this.nodes[i];

      for (let j = i - 1; j >= 0; j--) {
        let other = this.nodes[j];

        let dX = node.x - other.x;
        let dY = node.y - other.y;

        if (dX > 0) dX = Math.max(2, dX);
        else dX = Math.min(-2, dX);

        if (dY > 0) dY = Math.max(2, dY);
        else dY = Math.min(-2, dY);

        let dist2 = dX * dX + dY * dY;
        let mult = node.config.force * other.config.force / dist2 / Math.sqrt(dist2);

        node.v.x += mult * dX * node.iMass;
        node.v.y += mult * dY * node.iMass;
        other.v.x -= mult * dX * other.iMass;
        other.v.y -= mult * dY * other.iMass;
      }

      if (node.x < this.borders.left) node.v.x += Config.PHYSICS.EDGE_BOUNCE * (-node.x + this.borders.left);
      if (node.x > this.borders.right) node.v.x += Config.PHYSICS.EDGE_BOUNCE * (-node.x + this.borders.right);
      if (node.y < this.borders.top) node.v.y += Config.PHYSICS.EDGE_BOUNCE * (-node.y + this.borders.top);
      if (node.y > this.borders.bottom) node.v.y += Config.PHYSICS.EDGE_BOUNCE * (-node.y + this.borders.bottom);

      node.tick();
    }
  }
}

export interface IPull {
  x: number;
  y: number;
  node: FDGNode;
  minD?: number;
  force?: number;

  onRelease?: () => void;
  onMove?: (position: { x: number, y: number }) => void;
}

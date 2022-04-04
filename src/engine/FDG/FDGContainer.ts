import * as PIXI from 'pixi.js';
import * as _ from 'lodash';

import { FDGLink } from './FDGLink';
import { JMRect } from '../../JMGE/others/JMRect';
import { NodeSlug } from '../../data/NodeData';
import { Config } from '../../Config';
import { CrawlerView } from '../Mechanics/Parts/CrawlerView';
import { PlantNode } from '../nodes/PlantNode';

export class FDGContainer extends PIXI.Graphics {
  public links: FDGLink[] = [];

  private nodeLayer = new PIXI.Container();
  private crawlerLayer = new PIXI.Container();
  private nodes: PlantNode[] = [];
  private pulls: IPull[] = [];

  constructor(private borders: JMRect) {
    super();
    this.addChild(this.nodeLayer, this.crawlerLayer);
  }

  public addPull(pull: IPull) {
    this.pulls.push(pull);
  }

  public removePull(pull: IPull) {
    _.pull(this.pulls, pull);
  }

  public addNode(node: PlantNode) {
    this.nodes.push(node);
    this.nodeLayer.addChild(node.view);
  }

  public removeNode(node: PlantNode) {
    let i = this.nodes.indexOf(node);

    if (i >= 0) {
      this.nodeLayer.removeChild(node.view);
      this.nodes.splice(i, 1);
    }
  }

  public addCrawler(crawler: CrawlerView) {
    this.crawlerLayer.addChild(crawler);
  }

  public removeCrawler(crawler: CrawlerView) {
    this.crawlerLayer.removeChild(crawler);
  }

  public addLink(origin: PlantNode, target: PlantNode): FDGLink {
    let link = new FDGLink(origin, target);

    this.links.push(link);

    return link;
  }

  public removeLink(origin: PlantNode, target: PlantNode) {
    let index = this.links.findIndex(link => link.hasNode(origin, target));
    if (index >= 0) {
      this.links.splice(index, 1);
    }
  }

  public removeAllLinksFor(node: PlantNode, excludeFruit?: boolean) {
    for (let i = this.links.length - 1; i >= 0; i--) {
      if (this.links[i].hasNode(node)) {
        if (excludeFruit && this.links[i].other(node).isFruit()) continue;
        this.links.splice(i, 1);
      }
    }
  }

  public getLink(origin: PlantNode, target: PlantNode): FDGLink {
    return this.links.find(link => link.hasNode(origin) && link.hasNode(target));
  }

  public getClosestObject(config: { x: number, y: number, distance?: number, maxLinks?: boolean, filter?: PlantNode, notType?: NodeSlug, notFruit?: boolean }) {
    let m: PlantNode;

    let distance = config.distance ? config.distance * config.distance : Config.PHYSICS.MAX_GRAB * Config.PHYSICS.MAX_GRAB;

    for (let i = 0; i < this.nodes.length; i++) {
      let node = this.nodes[i];

      if (config.maxLinks && node.outlets.length >= node.maxOutlets) continue;
      if (config.filter && config.filter === node) continue;
      if (config.notType && node.slug === config.notType) continue;
      if (config.notFruit && node.isFruit()) continue;
      // if (par.hasTag!=null && par.hasTag!==this.nodes[i].tag) continue;
      // if (par.notHasTag!=null && par.notHasTag===this.nodes[i].tag) continue;

      let x = node.view.x - config.x;
      let y = node.view.y - config.y;

      let distance2 = x * x + y * y;

      if (distance2 < distance) {
        distance = distance2;
        m = node;
      }
    }

    return m;
  }

  public showConnectionCount(show: boolean = true) {
    this.nodes.forEach(node => !node.isFruit() ? node.showConnectionCount(show) : null);
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

      let dX = link.origin.view.x - link.target.view.x;
      let dY = link.origin.view.y - link.target.view.y;
      let dist = Math.sqrt(dX * dX + dY * dY);

      if (dist < link.length) return;

      let mult = Config.PHYSICS.ELASTICITY * (link.length - dist) / dist;

      link.origin.physics.vX += mult * dX * link.origin.physics.iMass;
      link.origin.physics.vY += mult * dY * link.origin.physics.iMass;
      link.target.physics.vX -= mult * dX * link.target.physics.iMass;
      link.target.physics.vY -= mult * dY * link.target.physics.iMass;
    });

    this.pulls.forEach(pull => {
      pull.node.physics.vX *= Config.PHYSICS.PULL_FRICTION;
      pull.node.physics.vY *= Config.PHYSICS.PULL_FRICTION;

      let dX = pull.node.view.x - pull.x;
      let dY = pull.node.view.y - pull.y;
      let dist2 = dX * dX + dY * dY;

      if (dist2 > Config.PHYSICS.TARGET_MIN) {
        if (!pull.minD || dist2 > pull.minD * pull.minD) {
          let mult = -Config.PHYSICS.TARGET_RATIO;

          if (pull.force) mult *= pull.force;

          pull.node.physics.vX += mult * dX * pull.node.physics.iMass;
          pull.node.physics.vY += mult * dY * pull.node.physics.iMass;
        }
      }
    });

    for (let i = this.nodes.length - 1; i >= 0; i--) {
      let node = this.nodes[i];

      for (let j = i - 1; j >= 0; j--) {
        let other = this.nodes[j];

        let dX = node.view.x - other.view.x;
        let dY = node.view.y - other.view.y;

        if (dX > 0) dX = Math.max(2, dX);
        else dX = Math.min(-2, dX);

        if (dY > 0) dY = Math.max(2, dY);
        else dY = Math.min(-2, dY);

        let dist2 = dX * dX + dY * dY;
        let mult = node.physics.force * other.physics.force / dist2 / Math.sqrt(dist2);

        node.physics.vX += mult * dX * node.physics.iMass;
        node.physics.vY += mult * dY * node.physics.iMass;
        other.physics.vX -= mult * dX * other.physics.iMass;
        other.physics.vY -= mult * dY * other.physics.iMass;
      }

      if (node.view.x < this.borders.left) node.physics.vX += Config.PHYSICS.EDGE_BOUNCE * (-node.view.x + this.borders.left)
        else if (node.view.x > this.borders.right) node.physics.vX += Config.PHYSICS.EDGE_BOUNCE * (-node.view.x + this.borders.right);
      if (node.view.y < this.borders.top) node.physics.vY += Config.PHYSICS.EDGE_BOUNCE * (-node.view.y + this.borders.top)
        else if (node.view.y > this.borders.bottom) node.physics.vY += Config.PHYSICS.EDGE_BOUNCE * (-node.view.y + this.borders.bottom);

      node.tickPhysics();
    }
  }
}

export interface IPull {
  x: number;
  y: number;
  node: PlantNode;
  minD?: number;
  force?: number;

  onRelease?: () => void;
  onMove?: (position: { x: number, y: number }) => void;
}

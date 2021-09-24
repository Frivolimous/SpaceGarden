import _ from 'lodash';
import { PlantNode } from 'src/engine/nodes/PlantNode';
import { CrawlerView } from './CrawlerView';

export enum AIType {
  NONE,
  WANDER,
  IDLE,
  GO_CENTER,
}

export class CrawlerModel {
  public view: CrawlerView;

  public magnitude: number = 0;
  public angle: number = 0;
  public speed: number = 0.01;

  public cLoc: PlantNode;
  public nextLoc: PlantNode;

  public aiType: AIType = AIType.IDLE;
  public aiExtra = 0;
  public aiExtra1 = 0;

  constructor(startingNode: PlantNode) {
    this.cLoc = startingNode;
    this.view = new CrawlerView();
  }

  public randomizeAi() {
    if (Math.random() < 0.5) {
      this.setAi(AIType.IDLE);
    } else {
      this.setAi(AIType.WANDER);
    }
  }

  public setAi(command: AIType = AIType.NONE) {
    if (command === AIType.NONE) {
      this.randomizeAi();
      return;
    }
    this.aiType = command;
    switch (command) {
      case AIType.WANDER:
        if (this.cLoc.outlets.length === 0) {
          this.setAi(AIType.IDLE);
        } else {
          this.nextLoc = _.sample(this.cLoc.outlets);
          this.magnitude = 0;
          this.speed = Math.abs(this.speed);
        }
        break;
      case AIType.IDLE:
        this.angle = -Math.PI + 2 * Math.PI * Math.random();
        this.aiExtra = 0.3 + Math.random() * 0.7;
        this.aiExtra1 = -Math.PI / 4 + Math.PI / 2 * Math.random();
        if (this.aiExtra > this.magnitude) {
          this.speed = Math.abs(this.speed);
        } else {
          this.speed = -Math.abs(this.speed);
        }
        break;
      case AIType.GO_CENTER:
        this.speed = Math.abs(this.speed);
        break;
    }
  }

  public update() {
    let x: number;
    let y: number;

    if (this.aiType === AIType.WANDER) {
      x = this.cLoc.view.x + (this.nextLoc.view.x - this.cLoc.view.x) * this.magnitude;
      y = this.cLoc.view.y + (this.nextLoc.view.y - this.cLoc.view.y) * this.magnitude;
    } else {
      x = this.cLoc.view.x + this.magnitude * this.cLoc.view.radius * Math.cos(this.angle);
      y = this.cLoc.view.y + this.magnitude * this.cLoc.view.radius * Math.sin(this.angle);
    }

    this.view.x = x;
    this.view.y = y;
  }
}

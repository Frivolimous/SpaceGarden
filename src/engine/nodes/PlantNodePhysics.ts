import { Config } from '../../Config';
import { INodeConfig } from '../../data/NodeData';
import { PlantNodeView } from './PlantNodeView';

export class PlantNodePhysics {
  public vX: number;
  public vY: number;

  public _iMass = 0;
  public vR = 0.005;

  public fixed = false;
  public hasMass = true;

  constructor(private config: INodeConfig, private view: PlantNodeView) {
    this._iMass = 1 / config.mass;
  }

  get force(): number {
    return this.config.force;
  }

  get iMass(): number {
    return this.hasMass ? this._iMass : 5;
  }

  public moveBody = () => {
    this.rotate();

    if (this.fixed) return;

    this.vX *= Config.PHYSICS.DAMP;
    this.vY *= Config.PHYSICS.DAMP;

    if (Math.abs(this.vX) > Config.PHYSICS.MIN_V || Math.abs(this.vY) > Config.PHYSICS.MIN_V) {
      this.view.x += this.vX;
      this.view.y += this.vY;
    } else {
      this.vX = 0;
      this.vY = 0;
    }
  }

  public rotate() {
    this.view.sprite.rotation += this.vR;
    if (this.view.sprite.rotation > Math.PI) this.view.sprite.rotation -= Math.PI * 2;
    if (this.view.sprite.rotation < -Math.PI) this.view.sprite.rotation += Math.PI * 2;
  }

}

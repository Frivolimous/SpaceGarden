import './dragonbones';
import * as PIXI from 'pixi.js';

import { IPixiArmatureDisplay, ILoadedBody, ILoadedCosmetic } from './DragonInterfaces';
declare var dragonBones: any;

export class DragonSpriteBasic {
  public display: IPixiArmatureDisplay;

  private facingLeft = false;
  private actions: any = [];
  private cAction = 0;

  constructor(body: ILoadedBody, runAnimation: boolean = true) {
    dragonBones.PixiFactory.factory.parseDragonBonesData(body.skeleton);
    dragonBones.PixiFactory.factory.parseTextureAtlasData(body.texture_json, body.texture_png);
    this.display = dragonBones.PixiFactory.factory.buildArmatureDisplay(body.skeleton.armature[0].name, body.skeleton.name);
    this.actions = body.skeleton.armature[0].animation;
    if (runAnimation) {
      this.display.animation.play(body.skeleton.armature[0].defaultActions[0].gotoAndPlay);
    }
  }

  public nextAnim() {
    this.cAction = (this.cAction + 1) % this.actions.length;
    this.display.animation.play(this.actions[this.cAction].name);
  }

  public turnLeft() {
    if (!this.facingLeft) {
      this.display.width = -this.display.width;
      this.facingLeft = true;
    }
  }

  public turnRight() {
    if (this.facingLeft) {
      this.display.width = -this.display.width;
      this.facingLeft = false;
    }
  }
}

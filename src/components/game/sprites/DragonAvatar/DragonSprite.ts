import './dragonbones';
import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
// import * as TWEEN from '@tweenjs/tween.js';

import { IPixiArmatureDisplay, ILoadedSticker, ILoadedOutfit, ILoadedBody, isLoadedOutfit, isLoadedColor, ILoadedCosmetic, isLoadedBody } from './DragonInterfaces';
// import { CosmeticCategory } from 'src/app/data/collections/heads-up-lobby';
// import { parseColor } from '../ColorUtilities';
declare var dragonBones: any;

export class DragonSprite {
  public display: IPixiArmatureDisplay;

  private outfits: { data: ILoadedOutfit, parts: ILoadedSticker[] }[] = [];
  private stickers: { data: ILoadedSticker, src: PIXI.Sprite }[] = [];
  private cosmeticByCategory: any = {};

  private partNames: string[] = [];
  private partMap: {[key: string]: PIXI.Sprite} = {};

  private skinColor = 0xffffff;
  private hairColor = 0x52B539;

  private facingLeft = false;

  constructor(body: ILoadedBody, cosmetics: ILoadedCosmetic[], runAnimation: boolean = true) {
    cosmetics = cosmetics.filter(el => !isLoadedBody(el));
    // cosmetics = _.filter(cosmetics, {type: CosmeticCategory.BODY});
    dragonBones.PixiFactory.factory.parseDragonBonesData(body.skeleton);
    dragonBones.PixiFactory.factory.parseTextureAtlasData(body.texture_json, body.texture_png);
    this.display = dragonBones.PixiFactory.factory.buildArmatureDisplay(body.skeleton.armature[0].name, body.skeleton.name);
    if (runAnimation) {
      this.display.animation.play(body.skeleton.armature[0].defaultActions[0].gotoAndPlay);
    }

    let slots = this.display.armature._slots;
    let children = this.display.children;

    _.forEach(slots, (slot) => {
      let name: string = slots[children.indexOf(slot.display)].name;

      this.partNames.push(name);
      this.partMap[name] = slot.display;
    });

    // cosmetics.forEach(cosmetic => this.addCosmetic(cosmetic));
  }

  // addCosmetic = (cosmetic: ILoadedCosmetic) => {
  //   if (isLoadedBody(cosmetic)) {
  //     return;
  //   }
  //   if (cosmetic.category && this.cosmeticByCategory[cosmetic.category] === cosmetic.slug) {
  //     return;
  //   }
  //   if (cosmetic.category === CosmeticCategory.BODY) {
  //     return;
  //   }
  //   if (isLoadedOutfit(cosmetic)) {
  //     this.finishAddOutfit(cosmetic);
  //   } else {
  //     if (isLoadedColor(cosmetic)) {
  //       if (cosmetic.category === CosmeticCategory.SKIN) {
  //         this.changeSkinColor(cosmetic.tint);
  //       } else if (cosmetic.category === CosmeticCategory.HAIR_COLOR) {
  //         this.changeHairColor(cosmetic.tint);
  //       }
  //     } else {
  //       this.finishAddSticker(cosmetic);
  //     }
  //   }
  // }

  // removeCosmetic = (slug?: string) => {
  //   // _.pull(this.data.parts, slug);
  //   let outfit = _.find(this.outfits, data => data.data.slug === slug);
  //   if (outfit) {
  //     this.clearOutfit(outfit);
  //     _.pull(this.outfits, outfit);
  //     return;
  //   }

  //   let sticker = _.find(this.stickers, data => data.data.slug === slug);
  //   if (sticker) {
  //     sticker.src.parent.removeChild(sticker.src);
  //     _.pull(this.stickers, sticker);
  //   }
  // }

  // changeSkinColor = (color: number) => {
  //   this.skinColor = color;

  //   for (let i = 0; i < this.partNames.length; i++) {
  //     if (this.partNames[i] !== 'Face') {
  //       this.partMap[this.partNames[i]].tint = color;
  //     }
  //   }

  //   for (let i = 0; i < this.stickers.length; i++) {
  //     if (this.stickers[i].data.tint === 'skin') {
  //       this.stickers[i].src.tint = color;
  //     }
  //   }
  // }

  // changeHairColor = (color: number) => {
  //   this.hairColor = color;

  //   for (let i = 0; i < this.stickers.length; i++) {
  //     if (this.stickers[i].data.tint === 'hair') {
  //       this.stickers[i].src.tint = color;
  //     }
  //   }
  // }

  // private clearOutfit(outfit: { data: ILoadedOutfit }) {
  //   outfit.data.partMap.forEach(part => {
  //     let index = _.findIndex(this.stickers, sticker => sticker.data === part);
  //     this.stickers[index].src.destroy();
  //     this.stickers.splice(index, 1);
  //   });
  // }

  // private finishAddOutfit(cosmetic: ILoadedOutfit) {
  //   let textureJSON = cosmetic.texture_json;
  //   let partMap = cosmetic.partMap;

  //   let textures: { [key: string]: PIXI.Texture } = {};

  //   let baseTexture = cosmetic.baseTexture;
  //   textureJSON.SubTexture.forEach(config => {
  //     let bounds = new PIXI.Rectangle(config.x, config.y, config.width, config.height);
  //     let texture = new PIXI.Texture(baseTexture, bounds);
  //     textures[config.name] = texture;
  //   });

  //   let data = { data: cosmetic, parts: [] };
  //   this.outfits.push(data);

  //   if (cosmetic.category) {
  //     if (this.cosmeticByCategory[cosmetic.category]) {
  //       this.removeCosmetic(this.cosmeticByCategory[cosmetic.category]);
  //     }
  //     this.cosmeticByCategory[cosmetic.category] = cosmetic.slug;
  //   }

  //   partMap.forEach(cosmetic2 => this.finishAddSticker(cosmetic2));
  // }

  // private finishAddSticker(cosmetic: ILoadedSticker) {
  //   let part = this.partMap[cosmetic.bodypart];
  //   let sticker = new PIXI.Sprite(cosmetic.texture);
  //   this.stickers.push({ data: cosmetic, src: sticker });
  //   if (cosmetic.forceUnder) {
  //     part.addChildAt(sticker, 0);
  //   } else {
  //     part.addChild(sticker);
  //   }

  //   if (cosmetic.tint === 'skin') {
  //     sticker.tint = this.skinColor;
  //   } else if (cosmetic.tint === 'hair') {
  //     sticker.tint = this.hairColor;
  //   } else {
  //     sticker.tint = parseColor(cosmetic.tint);
  //   }

  //   sticker.position.set(cosmetic.x, cosmetic.y);
  //   sticker.rotation = cosmetic.rotation;

  //   // let bounds = sticker.getBounds();
  //   // let offX = - bounds.width * 0.1;
  //   // let offY = - bounds.height * 0.1;
  //   // let tween = new TWEEN.Tween(sticker.scale).to({x: 1.1, y: 1.1}, 150).start().chain(new TWEEN.Tween(sticker.scale).to({x: 1, y: 1}, 150));
  //   // tween = new TWEEN.Tween(sticker.position).to({x: offX, y: offY}, 150).start().chain(new TWEEN.Tween(sticker.position).to({x: cosmetic.x, y: cosmetic.y}, 150));

  //   if (cosmetic.category) {
  //     if (this.cosmeticByCategory[cosmetic.category]) {
  //       this.removeCosmetic(this.cosmeticByCategory[cosmetic.category]);
  //     }
  //     this.cosmeticByCategory[cosmetic.category] = cosmetic.slug;
  //   }
  // }

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

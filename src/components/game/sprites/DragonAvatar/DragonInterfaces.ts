import * as PIXI from 'pixi.js';

export interface IUserAvatarCosmeticsEquipped {
  uid: string;
  updatedIn?: string;
  elements: {[category: string]: string};
}

export interface ILoadedCosmeticsEquipped {
  uid: string;
  body: ILoadedBody;
  cosmetics: ILoadedCosmetic[];
}

export interface IPixiArmatureDisplay extends PIXI.Sprite {
  armature: {
    _slots: any[],
  };
  animation: {
    animationNames: string[]
    fadeIn: (slug: string) => void
    play: (animation: string, repeat?: number) => void
    stop: (animation?: string) => void
    reset: () => void,
  };
  addDBEventListener: (type: string, listener: (e: any) => void, target?: any) => void;
  removeDBEventListener: (type: string, listener: (e: any) => void, target?: any) => void;
}

export interface ILoadedBody {
  slug: string;
  type: 'body';
  skeleton: any;
  category: 'body';
  texture_json: any;
  texture_png: PIXI.Texture;
}

export interface ILoadedOutfit {
  slug: string;
  type: 'outfit';
  baseTexture: PIXI.BaseTexture;
  category: string;

  texture_json: any;
  partMap: ILoadedSticker[];
}

export interface ILoadedSticker {
  slug: string;
  type: 'sticker' | 'part';
  texture: PIXI.Texture;
  category: string;

  bodypart: string;
  forceUnder: boolean;
  x: number;
  y: number;
  rotation: number;
  tint: string | number;
}

export interface ILoadedColor {
  slug: string;
  category: string;
  type: 'color';
  tint: number;
}

export type ILoadedCosmetic = ILoadedBody | ILoadedOutfit | ILoadedSticker | ILoadedColor;

export function isLoadedOutfit(part: ILoadedCosmetic): part is ILoadedOutfit {
  return (part.type === 'outfit');
}
export function isLoadedSticker(part: ILoadedCosmetic): part is ILoadedSticker {
  return (part.type === 'sticker');
}
export function isLoadedBody(part: ILoadedCosmetic): part is ILoadedBody {
  return (part.type === 'body');
}
export function isLoadedColor(part: ILoadedCosmetic): part is ILoadedColor {
  return (part.type === 'color');
}

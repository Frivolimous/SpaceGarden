import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { ILoadedBody, ILoadedCosmetic, ILoadedOutfit, ILoadedSticker } from '../components/game/sprites/DragonAvatar/DragonInterfaces';
import { TextureData } from '../data/TextureData';

export const AssetLoader = new class AssetLoaderC {
  public static bodyDefs: { [key: string]: ILoadedBody | ILoaderQueue } = {};
  public static partDefs: { [key: string]: ILoadedSticker | ILoadedOutfit | ILoaderQueue } = {};

  constructor() { }

  public getBody(slug: string, callback: (data: ILoadedBody) => void) {
    let def = AssetLoaderC.bodyDefs[slug];
    if (def) {
      if (isLoaderQueue(def)) {
        def.queue.push({ slug, callback });
      } else {
        callback(def);
      }
      return;
    } else {
      AssetLoaderC.bodyDefs[slug] = { type: 'queue', queue: [] };
    }
    let chunk = (TextureData.bodies as any)[slug];
    let slugs = [chunk.skeleton, chunk.texture_json, chunk.texture_png];
    getSharedResource({slugs, callback: loaded => {
        let skeleton = loaded.res[slugs[0]].data;
        let texture_json = loaded.res[slugs[1]].data;
        let texture_png = loaded.res[slugs[2]].texture;

        let data: ILoadedBody = {
          slug,
          type: 'body',
          category: 'body',
          skeleton,
          texture_json,
          texture_png,
        };

        let queue = AssetLoaderC.bodyDefs[slug] as ILoaderQueue;
        AssetLoaderC.bodyDefs[slug] = data;
        callback(data);
        queue.queue.forEach(req => req.callback(data));
      },
    });
  }
}();

interface IPixiLoaderReturn {
  data?: any;
  newLoad: boolean;
  res: any;
}

export interface ILoaderQueue {
  type: 'queue';
  queue: { slug: string, callback: (loaded: any) => void }[];
}

interface IPixiLoaderPacket {
  slugs: string[];
  callback: (loaded: IPixiLoaderReturn) => void;
}

let loaderQueue: IPixiLoaderPacket[] = [];

export function isLoaderQueue(part: ILoaderQueue |ILoadedCosmetic): part is ILoaderQueue {
  return (part.type === 'queue');
}

let getSharedResource = (packet: IPixiLoaderPacket) => {
  if (PIXI.Loader.shared.loading) {
    loaderQueue.push(packet);
  } else {
    let unloaded = _.filter(packet.slugs, (slug => !PIXI.Loader.shared.resources[slug]));
    if (unloaded.length === 0) {

      packet.callback({ res: PIXI.Loader.shared.resources, newLoad: false });

      if (loaderQueue.length > 0) {
        getSharedResource(loaderQueue.shift());
      }
    } else if (!PIXI.Loader.shared.loading) {
      unloaded.forEach(slug => PIXI.Loader.shared.add(slug, slug));
      PIXI.Loader.shared.load((loader, res) => {
        packet.callback({ res, newLoad: true });
        if (loaderQueue.length > 0) {
          getSharedResource(loaderQueue.shift());
        }
      });
    }
  }
};

import * as _ from 'lodash';
import { IExtrinsicModel, dExtrinsicModel, CURRENT_VERSION } from '../data/SaveData';

var SAVE_LOC: 'virtual' | 'local' | 'online' = 'local';
const DOC_NAME = 'SG-Extrinsic';
const VER_NAME = 'SG-Version';

export const virtualSave: {version: number, extrinsic: IExtrinsicModel } = {
  version: 8,
  extrinsic: dExtrinsicModel,
};

function versionControl(version: number, extrinsic: any): IExtrinsicModel {
  // adjust the save between versions

  if (version < CURRENT_VERSION) {
    extrinsic = _.cloneDeep(dExtrinsicModel);
  }
  return extrinsic;
}

export class SaveManager {
  public static async init(): Promise<null> {
    console.log('init!');
    if (SAVE_LOC === 'local') {
      try {
        let extrinsicStr = window.localStorage.getItem(DOC_NAME);
      } catch (e) {
        SAVE_LOC = 'virtual';
      }
    }
    return new Promise<null>((resolve) => {
      SaveManager.loadExtrinsic().then(extrinsic => {
        console.log('ext!', extrinsic);
        if (extrinsic) {
          console.log('has ext');
          SaveManager.loadVersion().then(version => {
            console.log('version loaded', version);
            if (version < CURRENT_VERSION) {
              console.log('old V');
              extrinsic = versionControl(version, extrinsic);
              SaveManager.saveVersion(CURRENT_VERSION);
              SaveManager.saveExtrinsic(extrinsic);
            }
            SaveManager.extrinsic = extrinsic;
            resolve(null);
          });
        } else {
          console.log('reset ext');
          SaveManager.confirmReset();
          SaveManager.saveVersion(CURRENT_VERSION);
          SaveManager.saveExtrinsic(this.getExtrinsic());
          resolve(null);
        }
      });
    });
  }

  public static resetData(): () => void {
    // returns the confirmation function
    return SaveManager.confirmReset;
  }

  public static getExtrinsic(): IExtrinsicModel {
    if (SaveManager.extrinsic) {
      return SaveManager.extrinsic;
    }
  }

  public static async saveCurrent(): Promise<null> {
    return new Promise(resolve => {
      let processes = 1;
      SaveManager.saveExtrinsic().then(() => {
        processes--;
        if (processes === 0) {
          resolve(null);
        }
      });
    });
  }

  public static async saveExtrinsic(extrinsic?: IExtrinsicModel): Promise<IExtrinsicModel> {
    return new Promise((resolve) => {
      extrinsic = extrinsic || SaveManager.extrinsic;

      switch (SAVE_LOC) {
        case 'virtual': virtualSave.extrinsic = extrinsic; break;
        case 'local':
          if (typeof Storage !== undefined) {
            window.localStorage.setItem(DOC_NAME, JSON.stringify(extrinsic));
          } else {
            console.log('NO STORAGE!');
          }
          break;
        case 'online': break;
      }

      resolve(extrinsic);
    });
  }

  public static null = () => {
    return new Promise((resolve) => {
      switch (SAVE_LOC) {
        case 'virtual':
        case 'local':
        case 'online':
      }
    });
  }

  private static extrinsic: IExtrinsicModel;

  private static confirmReset = () => {
    SaveManager.extrinsic = _.cloneDeep(dExtrinsicModel);
  }

  private static async loadExtrinsic(): Promise<IExtrinsicModel> {
    let extrinsic: IExtrinsicModel;
    return new Promise((resolve) => {
      switch (SAVE_LOC) {
        case 'virtual': extrinsic = virtualSave.extrinsic; break;
        case 'local':
          if (typeof Storage !== undefined) {
            let extrinsicStr = window.localStorage.getItem(DOC_NAME);
            if (extrinsicStr !== 'undefined') {
              extrinsic = JSON.parse(extrinsicStr);
            }
          } else {
            console.log('NO STORAGE!');
          }
          break;
        case 'online': break;
      }
      resolve(extrinsic);
    });
  }

  // == Version Controls == //

  private static loadVersion(): Promise<number> {
    return new Promise((resolve) => {
      let version;
      switch (SAVE_LOC) {
        case 'virtual': version = virtualSave.version; break;
        case 'local':
          if (typeof Storage !== undefined) {
            version = Number(window.localStorage.getItem(VER_NAME));
          } else {
            console.log('NO STORAGE!');
            resolve(0);
          }
          break;
        case 'online': break;
      }

      resolve(version);
    });
  }

  private static saveVersion(version: number) {
    switch (SAVE_LOC) {
      case 'virtual': virtualSave.version = version; break;
      case 'local':
        if (typeof Storage !== undefined) {
          window.localStorage.setItem(VER_NAME, String(version));
        } else {
          console.log('NO STORAGE!');
        }
        break;
      case 'online': break;
    }
  }
}

(window as any).checkSaves = () => console.log(SaveManager.getExtrinsic());

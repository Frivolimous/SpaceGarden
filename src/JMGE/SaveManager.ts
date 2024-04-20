import _ from 'lodash';
import { JMEventListener } from './events/JMEventListener';

export class SaveManager<SaveFormat> {
  public saveEvent = new JMEventListener<ISaveEvent>();
  private virtualSave: {version: number, extrinsic: SaveFormat};

  constructor(private config: ISaveManagerConfig, private dSave: SaveFormat, private versionChanges: {version: number, callback: (extrinsic: SaveFormat) => SaveFormat}[] = []) {
    this.virtualSave = {version: config.CurrentVersion, extrinsic: dSave};
  }

  public async init(): Promise<null> {
    if (this.config.SaveLoc === 'local') {
      try {
        let extrinsicStr = window.localStorage.getItem(this.config.DocName);
      } catch (e) {
        this.config.SaveLoc = 'virtual';
      }
    }
    return new Promise<null>((resolve) => {
      this.loadExtrinsic().then(extrinsic => {
        if (extrinsic) {
          this.loadVersion().then(version => {
            this.saveEvent.publish({text: `Data Version: ${version}`});
            if (version < this.config.CurrentVersion) {
              this.saveEvent.publish({text: 'Version Update'});
              extrinsic = this.versionControl(version, extrinsic);
              this.saveVersion(this.config.CurrentVersion);
              this.saveExtrinsic(extrinsic);
            }
            this.extrinsic = extrinsic;
            resolve(null);
          });
        } else {
          this.saveEvent.publish({text: 'Save Data Reset'});
          this.confirmReset();
          this.saveVersion(this.config.CurrentVersion);
          this.saveExtrinsic(this.getExtrinsic());
          resolve(null);
        }
      });
    });
  }

  public versionControl(version: number, extrinsic: SaveFormat): SaveFormat {
    this.versionChanges.forEach(el => {
      if (version < el.version) {
        extrinsic = el.callback(extrinsic);
      }
    });

    return extrinsic;
  }

  public resetData = (): () => void => {
    // returns the confirmation function
    return this.confirmReset;
  }

  public getExtrinsic(): SaveFormat {
    if (this.extrinsic) {
      return this.extrinsic;
    }
  }

  public async saveCurrent(): Promise<null> {
    return new Promise(resolve => {
      let processes = 1;
      this.saveExtrinsic().then(() => {
        processes--;
        if (processes === 0) {
          resolve(null);
        }
      });
    });
  }

  public async saveExtrinsic(extrinsic?: SaveFormat, andSet?: boolean): Promise<SaveFormat> {
    return new Promise((resolve) => {
      extrinsic = extrinsic || this.extrinsic;
      if (andSet) {
        this.extrinsic = extrinsic;
      }

      switch (this.config.SaveLoc) {
        case 'virtual': this.virtualSave.extrinsic = extrinsic; break;
        case 'local':
          if (typeof Storage !== undefined) {
            window.localStorage.setItem(this.config.DocName, JSON.stringify(extrinsic));
          } else {
            console.log('NO STORAGE!');
          }
          break;
        case 'online': break;
      }

      resolve(extrinsic);
    });
  }

  public null = () => {
    return new Promise((resolve) => {
      switch (this.config.SaveLoc) {
        case 'virtual':
        case 'local':
        case 'online':
      }
    });
  }

  private extrinsic: SaveFormat;

  private confirmReset = () => {
    this.extrinsic = _.cloneDeep(this.dSave);
    this.saveExtrinsic();
  }

  private async loadExtrinsic(): Promise<SaveFormat> {
    let extrinsic: SaveFormat;
    return new Promise((resolve) => {
      switch (this.config.SaveLoc) {
        case 'virtual': extrinsic = this.virtualSave.extrinsic; break;
        case 'local':
          if (typeof Storage !== undefined) {
            let extrinsicStr = window.localStorage.getItem(this.config.DocName);
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

  private loadVersion(): Promise<number> {
    return new Promise((resolve) => {
      let version;
      switch (this.config.SaveLoc) {
        case 'virtual': version = this.virtualSave.version; break;
        case 'local':
          if (typeof Storage !== undefined) {
            version = Number(window.localStorage.getItem(this.config.VerName));
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

  private saveVersion(version: number) {
    switch (this.config.SaveLoc) {
      case 'virtual': this.virtualSave.version = version; break;
      case 'local':
        if (typeof Storage !== undefined) {
          window.localStorage.setItem(this.config.VerName, String(version));
        } else {
          console.log('NO STORAGE!');
        }
        break;
      case 'online': break;
    }
  }
}

export interface ISaveManagerConfig {
  CurrentVersion: number;
  DocName: string;
  VerName: string;
  SaveLoc: 'virtual' | 'local' | 'online';
}

export interface ISaveEvent {
  data?: any;
  text: any;
}

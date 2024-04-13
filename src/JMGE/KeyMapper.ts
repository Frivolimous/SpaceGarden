export class KeyMapper {
  public enabled = true;

  private holding: string[] = [];

  constructor(private keysDown?: IKeyMap[], private keysUp?: IKeyMap[]) {
    if (keysDown) this.makeLower(keysDown);
    if (keysUp) this.makeLower(keysUp);

    window.addEventListener('keydown', this.onKeyDown);
    window.addEventListener('keyup', this.onKeyUp);
  }

  public destroy() {
    window.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keyup', this.onKeyUp);
  }

  public setKeys(keysDown: IKeyMap[], keysUp?: IKeyMap[]) {
    this.makeLower(keysDown);
    if (keysUp) this.makeLower(keysUp);

    this.keysDown = keysDown;
    this.keysUp = keysUp || this.keysUp;
  }

  public addKeys(keysDown: IKeyMap[]) {
    this.makeLower(keysDown);
    this.keysDown = this.keysDown.concat(keysDown);
  }

  private makeLower(map: IKeyMap[]) {
    map.forEach(data => {
      data.key = data.key.toLowerCase();
      if (data.altKey) data.altKey = data.altKey.toLowerCase();
    });
  }

  private onKeyDown = (e: KeyboardEvent) => {
    if (!this.enabled || this.keysDown === null) return;

    let key = e.key.toLowerCase();
    let ctrl = e.ctrlKey;

    for (let i = 0; i < this.keysDown.length; i++) {
      let currentKey = this.keysDown[i];
      if (currentKey.key === key || (currentKey.altKey !== null && currentKey.altKey === key)) {
        if (currentKey.withCtrl && !ctrl) continue;
        if (currentKey.noCtrl && ctrl) continue;

        if (currentKey.noHold) {
          if (this.holding.includes(key)) return;

          this.holding.push(key);
        }
        currentKey.function();
        return;
      }
    }
  }

  private onKeyUp = (e: KeyboardEvent) => {
    if (!this.enabled) return;
    let key = e.key.toLowerCase();

    let holding = this.holding.indexOf(key);
    if (holding > -1) {
      this.holding.splice(holding, 1);
    }

    if (this.keysUp === null) return;

    for (let i = 0; i < this.keysUp.length; i++) {
      let currentKey = this.keysUp[i];
      if (currentKey.key === key || (currentKey.altKey !== null && currentKey.altKey === key)) {
        currentKey.function();
        return;
      }
    }
  }
}

export interface IKeyMap {
  key: string;
  altKey?: string;
  noHold?: boolean;
  withCtrl?: boolean;
  noCtrl?: boolean;
  function: () => void;
}

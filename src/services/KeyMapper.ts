export class KeyMapper {
  public enabled = true;
  public holding: string[] = [];

  constructor(private keysDown?: KeyMap[], private keysUp?: KeyMap[]) {
    if (keysDown) this.makeLower(keysDown);
    if (keysUp) this.makeLower(keysUp);

    window.addEventListener('keydown', this.onKeyDown);
    window.addEventListener('keyup', this.onKeyUp);
  }

  public destroy() {
    window.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keyup', this.onKeyUp);
  }

  public setKeys(keysDown: KeyMap[], keysUp?: KeyMap[]) {
    this.makeLower(keysDown);
    if (keysUp) this.makeLower(keysUp);

    this.keysDown = keysDown;
    this.keysUp = keysUp || this.keysUp;
  }

  private makeLower(map: KeyMap[]) {
    map.forEach(data => {
      data.key = data.key.toLowerCase();
      if (data.altKey) data.altKey = data.altKey.toLowerCase();
    });
  }

  private onKeyDown = (e: KeyboardEvent) => {
    if (!this.enabled || this.keysDown === null) return;

    let key = e.key.toLowerCase();

    for (let i = 0; i < this.keysDown.length; i++) {
      let currentKey = this.keysDown[i];
      if (currentKey.key === key || (currentKey.altKey !== null && currentKey.altKey === key)) {
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

export type KeyMap = {
  key: string,
  altKey?: string,
  noHold?: boolean,
  function: () => void;
}
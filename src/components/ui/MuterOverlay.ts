import * as PIXI from 'pixi.js';

export class MuterOverlay extends PIXI.Graphics {
  private pause: PIXI.Sprite;
  private play: PIXI.Sprite;
  private sound: PIXI.Sprite;
  private noSound: PIXI.Sprite;

  private paused = false;
  private muted = false;

  private options: {muted: boolean};

  constructor(private showPause?: boolean, hasBackground = true) {
    super();
    // if (hasBackground) {
    //   this.beginFill(0x666666);
    //   this.lineStyle(2);
    //   this.drawRect(0, 0, 100, 50);
    // }

    // let extrinsic = SaveData.getExtrinsic();
    // this.options = extrinsic.data.options;
    // if (!this.options) {
    //   extrinsic.data.options = {muted: false};
    //   this.options = extrinsic.data.options;
    //   SaveData.saveExtrinsic();
    // }

    // if (showPause) {
    //   this.pause = new PIXI.Sprite(TextureData.pause);
    //   this.play = new PIXI.Sprite(TextureData.play);
    //   this.pause.scale.set(0.3);
    //   this.play.scale.set(0.3);
    //   this.pause.position.set(0, 5);
    //   this.play.position.set(0, 5);
    //   this.addChild(this.pause, this.play);
    //   let pauseB = new ClearButton({width: 30, height: 50, onClick: () => GameEvents.REQUEST_PAUSE_GAME.publish({paused: !this.paused})});
    //   this.addChild(pauseB);
    //   this.togglePause({paused: false});
    //   GameEvents.REQUEST_PAUSE_GAME.addListener(this.togglePause);
    // }

    // this.sound = new PIXI.Sprite(TextureData.sound);
    // this.noSound = new PIXI.Sprite(TextureData.noSound);
    // this.sound.scale.set(0.3);
    // this.noSound.scale.set(0.3);
    // this.sound.position.set(45, 5);
    // this.noSound.position.set(45, 5);
    // this.addChild(this.sound, this.noSound);
    // this.toggleSound(this.options.muted);

    // let soundB = new ClearButton({width: 30, height: 50, onClick: () => this.toggleSound()});
    // soundB.x = 45;
    // this.addChild(soundB);
  }

  public destroy() {
    // if (this.showPause) {
    //   GameEvents.REQUEST_PAUSE_GAME.removeListener(this.togglePause);
    // }
  }

  public reset() {
    // this.toggleSound(this.options.muted);
    // if (this.paused) {
    //   this.togglePause({paused: false});
    // }
  }

  public getWidth() {
    return 100;
  }

  public getHeight() {
    return 50;
  }

  // private togglePause = (e: IPauseEvent) => {
  //   if (e.paused || e.paused === false) {
  //     this.paused = e.paused;
  //   } else {
  //     this.paused = !this.paused;
  //   }

  //   if (this.paused) {
  //     this.pause.visible = false;
  //     this.play.visible = true;
  //   } else {
  //     this.pause.visible = true;
  //     this.play.visible = false;
  //   }
  // }

  // private toggleSound = (b?: boolean) => {
  //   // SoundData.runTest();
  //   // console.log(SoundData);
  //   if (b || b === false) {
  //     this.muted = b;
  //   } else {
  //     this.muted = !this.muted;
  //   }

  //   this.options.muted = this.muted;
  //   SoundData.setMute(this.muted);
  //   SaveData.saveExtrinsic();

  //   if (this.muted) {
  //     this.sound.visible = false;
  //     this.noSound.visible = true;
  //   } else {
  //     this.sound.visible = true;
  //     this.noSound.visible = false;
  //   }
  // }
}

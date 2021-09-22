import * as PIXI from 'pixi.js';
import { BaseUI } from './_BaseUI';
import { MuterOverlay } from '../components/ui/MuterOverlay';
import { SaveManager } from '../services/SaveManager';
import { Button } from '../components/ui/Button';
import { Fonts } from '../data/Fonts';
import { IResizeEvent } from '../services/GameEvents';
import { GameUI } from './GameUI';
import { StringManager } from '../services/StringManager';
import { OptionModal } from '../components/ui/modals/OptionModal';

export class MenuUI extends BaseUI {
  public muter: MuterOverlay;

  private title: PIXI.Text;

  private startB: Button;
  private resetB: Button;
  private langB: Button;

  constructor() {
    super({bgColor: 0x777777});
    this.title = new PIXI.Text(StringManager.data.GAME_TITLE, { fontSize: 30, fontFamily: Fonts.UI, fill: 0x3333ff });
    this.addChild(this.title);

    this.startB = new Button({ width: 100, height: 30, label: StringManager.data.BUTTON.ADVENTURE, onClick: this.startGame });
    this.startB.position.set(150, 200);
    this.addChild(this.startB);
    this.resetB = new Button({ width: 100, height: 30, label: StringManager.data.BUTTON.RESET, onClick: this.resetGame, color: 0xff3333 });
    this.resetB.position.set(150, 400);
    this.addChild(this.resetB);
    this.langB = new Button({ width: 100, height: 30, label: StringManager.data.BUTTON.LANGUAGE, onClick: this.changeLanguage });
    this.langB.position.set(150, 380);
    this.addChild(this.startB, this.langB);
    this.langB.visible = false;

    this.muter = new MuterOverlay();
    this.addChild(this.muter);
  }

  public navIn = () => {
    this.muter.reset();

    let extrinsic = SaveManager.getExtrinsic();
  }

  public positionElements = (e: IResizeEvent) => {
    this.title.x = (e.innerBounds.width - this.title.width) / 2;
    this.title.y = 50;
    this.muter.x = e.outerBounds.right - this.muter.getWidth();
    this.muter.y = e.outerBounds.bottom - this.muter.getHeight();
    this.langB.addLabel(StringManager.getCurrentLanguage());
    this.langB.x = e.outerBounds.right - this.langB.width - 20;
    this.langB.y = 20;
  }

  private startGame = () => {
    this.navForward(new GameUI());
  }

  private resetGame = () => {
    let dialogue = new OptionModal('Are you sure you want to delete all your data and start over?', [{label: 'Yes', color: 0x00ff00, onClick: SaveManager.resetData()}, {label: 'No', color: 0xff0000}]);
    this.addDialogueWindow(dialogue);
  }

  private changeLanguage = () => {
    this.langB.addLabel(StringManager.changeLanguage());
    this.title.text = StringManager.data.GAME_TITLE;
    this.startB.addLabel(StringManager.data.BUTTON.ADVENTURE);
  }
}

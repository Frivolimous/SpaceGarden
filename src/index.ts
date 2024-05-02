import * as PIXI from 'pixi.js';
import _ from 'lodash';
import { MenuUI } from './pages/MenuUI';
import { Config } from './Config';
import { SaveManager } from './JMGE/SaveManager';
import { TooltipReader } from './components/tooltip/TooltipReader';
import { JMRect } from './JMGE/others/JMRect';
import { Fonts } from './data/Fonts';
import { TextureCache } from './services/TextureCache';
import { GameEvents, IResizeEvent } from './services/GameEvents';
import { Debug } from './services/_Debug';
import { BaseUI, IFadeTiming, dFadeTiming } from './pages/_BaseUI';
import { ScreenCover } from './JMGE/effects/ScreenCover';
import { FontLoader } from './services/FontLoader';
import { IExtrinsicModel, dExtrinsicModel } from './data/SaveData';

export let interactionMode: 'desktop'|'mobile' = 'desktop';
const versionUpdates: {version: number, callback: (save: IExtrinsicModel) => IExtrinsicModel}[] = [
  // <28, reset
  // 28 -> 29, add another SCORE
  {version: 28, callback: () => _.cloneDeep(dExtrinsicModel)},
  {version: 29, callback: (save) => {save.scores[3] = 0; return save}},
];

export let Facade = new class FacadeInner {
  private static exists = false;
  public app: PIXI.Application;
  public stageBorders: JMRect;
  public innerBorders: JMRect;
  public screen: PIXI.Container;
  public border: PIXI.Graphics;
  public saveManager: SaveManager<IExtrinsicModel>;
  
  private element: HTMLCanvasElement;
  private previousResize: IResizeEvent;
  private currentPage: BaseUI;

  constructor() {
    // console.warn = (a: any) => {};
    if (FacadeInner.exists) throw new Error('Cannot instatiate more than one Facade Singleton.');
    FacadeInner.exists = true;
    try {
      document.createEvent('TouchEvent');
      interactionMode = 'mobile';
    } catch (e) {

    }

    // Setup PIXI
    this.element = document.getElementById('game-canvas') as HTMLCanvasElement;

    this.app = new PIXI.Application({
      backgroundColor: 0x770000,
      antialias: true,
      resolution: Config.INIT.RESOLUTION,
      width: this.element.offsetWidth,
      height: this.element.offsetHeight,
    });
    this.element.append(this.app.view as any);

    this.app.stage.scale.x = 1 / Config.INIT.RESOLUTION;
    this.app.stage.scale.y = 1 / Config.INIT.RESOLUTION;

    this.screen = new PIXI.Container();
    this.screen.eventMode = 'dynamic';
    this.app.stage.addChild(this.screen);
    if (Config.INIT.BORDER) {
      this.border = new PIXI.Graphics();
      this.border.lineStyle(3, 0xff00ff).drawRect(0, 0, Config.INIT.SCREEN_WIDTH, Config.INIT.SCREEN_HEIGHT);
      this.app.stage.addChild(this.border);
    }

    this.stageBorders = new JMRect();
    this.innerBorders = new JMRect(0, 0, Config.STAGE.SCREEN_WIDTH, Config.STAGE.SCREEN_HEIGHT);

    // Initialize Libraries
    new TooltipReader(this.screen, this.stageBorders, {});
    TextureCache.initialize(this.app);
    this.saveManager = new SaveManager({CurrentVersion: Config.INIT.GAME_DATA_VERSION, DocName: 'SG-Extrinsic', VerName: 'SG-Version', SaveLoc: 'local'}, dExtrinsicModel, versionUpdates);

    // Resize Event (for full screen mode / scaling)
    let finishResize = _.debounce(this.finishResize, 500);
    window.addEventListener('resize', finishResize);

    let fonts: string[] = _.map(Fonts);

    // load fonts then preloader!
    GameEvents.APP_LOG.publish({type: 'INITIALIZE', text: 'Primary Setup'});
    // window.requestAnimationFrame(() => FontLoader.load(fonts).then(this.init));
    window.requestAnimationFrame(() => this.init());
  }

  public init = () => {
    // this will happen after 'preloader'
    Debug.initialize(this.app);

    GameEvents.APP_LOG.publish({type: 'INITIALIZE', text: 'Post-Loader'});
    this.saveManager.init().then(() => {
      GameEvents.APP_LOG.publish({type: 'INITIALIZE', text: 'Save Manager Initialized'});

      let menu = new MenuUI();

      this.currentPage = menu;
      this.screen.addChild(menu);
      menu.navIn();

      window.requestAnimationFrame(() => this.finishResize());
    });
  }

  public setCurrentPage(nextPage: BaseUI, fadeTiming?: IFadeTiming, andDestroy?: boolean) {
    fadeTiming = _.defaults(fadeTiming || {}, dFadeTiming);

    let screen = new ScreenCover(this.previousResize.outerBounds, fadeTiming.color).onFadeComplete(() => {
      this.currentPage.navOut();
      this.saveManager.saveCurrent().then(() => {
        this.screen.removeChild(this.currentPage);
        if (andDestroy) this.currentPage.destroy();

        this.currentPage = nextPage;
        this.screen.addChildAt(nextPage, 0);
        nextPage.navIn();
        if (this.previousResize) {
          nextPage.onResize(this.previousResize);
        }
        let screen2 = new ScreenCover(this.previousResize.outerBounds, fadeTiming.color).fadeOut(fadeTiming.fadeOut);
        nextPage.addChild(screen2);
      });
    }).fadeIn(fadeTiming.fadeIn, fadeTiming.delay, fadeTiming.delayBlank);
    this.screen.addChild(screen);
  }

  private finishResize = () => {
    // resize event
    // let viewWidth = this.element.offsetWidth;
    // let viewHeight = this.element.offsetHeight;
    let ratio = this.element.offsetWidth / this.element.offsetHeight;
    let viewHeight = Math.min(this.element.offsetHeight, 2000);
    let viewWidth = viewHeight * ratio;
    this.app.view.style.width = '100%';
    this.app.renderer.resize(viewWidth, viewHeight);
    let innerWidth = Config.STAGE.SCREEN_WIDTH;
    let innerHeight = Config.STAGE.SCREEN_HEIGHT;
    let scale = Math.min(viewWidth / innerWidth, viewHeight / innerHeight);
    this.screen.scale.set(scale);
    this.screen.x = (viewWidth - innerWidth * scale) / 2;
    this.screen.y = (viewHeight - innerHeight * scale) / 2;
    this.stageBorders.set(0 - this.screen.x / scale, 0 - this.screen.y / scale, viewWidth / scale, viewHeight / scale);

    // to show border (for sizing / spacing tests)
    if (this.border) {
      this.border.clear();
      this.border.lineStyle(10, 0xff00ff);
      this.border.drawShape(this.stageBorders);
      this.border.lineStyle(3, 0x00ffff);
      this.border.drawShape(this.innerBorders);
      this.border.scale.set(scale);
      this.border.position.set(this.screen.x, this.screen.y);
    }

    this.previousResize = {outerBounds: this.stageBorders, innerBounds: this.innerBorders};

    GameEvents.WINDOW_RESIZE.publish(this.previousResize);
  }
}();

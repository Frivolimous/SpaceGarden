import * as _ from 'lodash';
import * as PIXI from 'pixi.js';
import { BaseUI } from './_BaseUI';
import { Fonts } from '../data/Fonts';
import { GameEvents, IResizeEvent } from '../services/GameEvents';
import { Direction, ScrollingContainer } from '../components/ScrollingContainer';
import { KeyMapper } from '../services/KeyMapper';
import { JMTicker } from '../JMGE/events/JMTicker';
import { FDGContainer } from '../engine/FDG/FDGContainer';
import { MouseController } from '../services/MouseController';
import { BottomBar } from '../components/BottomBar';
import { NodeManager } from '../engine/Mechanics/NodeManager';
import { FDGLink } from '../engine/FDG/FDGLink';
import { GameController } from '../engine/Mechanics/GameController';
import { Sidebar } from '../components/domui/Sidebar';
import { INodeConfig, NodeData } from '../data/NodeData';
import { IExtrinsicModel, TierSaves } from '../data/SaveData';
import { SaveManager } from '../services/SaveManager';
import { Config, dConfigNode } from '../Config';
import { InfoPopup } from '../components/domui/InfoPopup';
import { ISkillConfig, SkillData } from '../data/SkillData';
import { SkillPanel } from '../components/domui/SkillPanel';
import { StringManager } from '../services/StringManager';
import { GOD_MODE } from '../services/_Debug';
import { INodeSave, PlantNode } from '../engine/nodes/PlantNode';
import { AchievementPanel } from '../components/domui/AchievementPanel';

export class GameUI extends BaseUI {
  public gameC: GameController;

  private canvas: ScrollingContainer;
  private container: FDGContainer;
  private nodeManager: NodeManager;
  private sidebar: Sidebar;
  private bottomBar: BottomBar;

  private keymapper: KeyMapper;
  private mouseC: MouseController;

  private gameSpeed: number = 1;
  private turboSpeed: number = 3;
  private running = true;
  private exists = true;

  private extrinsic: IExtrinsicModel;

  constructor(level?: INodeSave[]) {
    super({bgColor: 0x777777});

    // --- initialize components --- \\

    Config.NODE = _.clone(dConfigNode);

    this.extrinsic = SaveManager.getExtrinsic();

    this.nodeManager = new NodeManager(this.extrinsic.skillTier);
    this.canvas = new ScrollingContainer(1500, 1000);
    this.container = new FDGContainer(this.canvas.innerBounds);
    this.mouseC = new MouseController(this.canvas, this.container);
    this.gameC = new GameController(this.container, this.nodeManager, this.extrinsic.scores);

    this.nodeManager.applySkills(this.extrinsic.skillsCurrent);
    this.nodeManager.applyAchievements(this.extrinsic.achievements);

    let always = this.nodeManager.getSkillAlways(this.extrinsic.skillTier);

    let nextSkillPanel = new SkillPanel(this.nodeManager.skills, this.extrinsic.skillsNext, always, this.extrinsic.skillTier);
    let currentSkillPanel: SkillPanel;

    if (this.extrinsic.skillsCurrent.length + always.length > 0) {
      currentSkillPanel = new SkillPanel(this.nodeManager.skills, this.extrinsic.skillsCurrent, always, this.extrinsic.skillTier, true);
    }

    let achieveElement = new AchievementPanel(this.extrinsic.achievements, SkillData.achievements);

    this.sidebar = new Sidebar(currentSkillPanel, nextSkillPanel, achieveElement);
    this.keymapper = new KeyMapper();
    this.bottomBar = new BottomBar(100, 100, this.nodeManager.buildableNodes.map(slug => this.nodeManager.getNodeConfig(slug)));

    // --- add to stage --- \\
    this.canvas.addChild(this.container);
    this.addChild(this.canvas);
    this.addChild(this.bottomBar);

    // --- weave components --- \\

    this.mouseC.onMove.addListener(this.onMouseMove);
    this.gameC.onNodeAdded.addListener(this.sidebar.addNodeElement);
    this.gameC.onNodeAdded.addListener(this.bottomBar.nodeAdded);
    this.gameC.onNodeRemoved.addListener(this.bottomBar.nodeRemoved);
    this.gameC.onNodeRemoved.addListener(this.sidebar.removeNodeElement);
    this.gameC.onCrawlerAdded.addListener(this.sidebar.addNodeElement);
    this.gameC.onCrawlerRemoved.addListener(this.sidebar.removeNodeElement);
    this.bottomBar.onProceedButton.addListener(this.nextStage);
    this.bottomBar.onCreateButton.addListener(this.createNewNode);
    this.bottomBar.onDeleteButton.addListener(this.deleteNextClicked);
    this.bottomBar.onTurboButton.addListener(this.toggleTurboMode);
    this.gameC.knowledge.onAchievementUpdate.addListener(this.sidebar.updateAchievement);

    this.keymapper.enabled = false;
    this.keymapper.setKeys([
      {key: 'ArrowLeft', altKey: 'a', function: () => this.canvas.startPan(Direction.LEFT)},
      {key: 'ArrowRight', altKey: 'd', function: () => this.canvas.startPan(Direction.RIGHT)},
      {key: 'ArrowUp', altKey: 'w', function: () => this.canvas.startPan(Direction.UP)},
      {key: 'ArrowDown', altKey: 's', function: () => this.canvas.startPan(Direction.DOWN)},
      {key: ' ', function: () => this.running = !this.running},
      {key: '=', altKey: '+', function: () => this.canvas.zoomBy(1.2), noHold: true},
      {key: '-', altKey: '_', function: () => this.canvas.zoomBy(1 / 1.2), noHold: true},
    ],
    [
      {key: 'ArrowLeft', altKey: 'a', function: () => this.canvas.endPan(Direction.LEFT)},
      {key: 'ArrowRight', altKey: 'd', function: () => this.canvas.endPan(Direction.RIGHT)},
      {key: 'ArrowUp', altKey: 'w', function: () => this.canvas.endPan(Direction.UP)},
      {key: 'ArrowDown', altKey: 's', function: () => this.canvas.endPan(Direction.DOWN)},
    ]);

    if (GOD_MODE) {
      this.keymapper.addKeys([
        {key: '[', function: () => this.gameSpeed = Math.max(this.gameSpeed - 1, 1)},
        {key: ']', function: () => this.gameSpeed++},
        {key: 'p', function: () => this.resetGame()},
        {key: '`', function: () => this.logSave()},
        {key: '1', noCtrl: true, function: () => this.loadSave(TierSaves[1])},
        {key: '2', noCtrl: true, function: () => this.loadSave(TierSaves[2])},
        {key: '0', noCtrl: true, function: () => this.loadSave(TierSaves[0])},
        {key: '1', withCtrl: true, function: () => this.loadSave(TierSaves[11])},
        {key: '2', withCtrl: true, function: () => this.loadSave(TierSaves[12])},
        {key: '0', withCtrl: true, function: () => this.loadSave(TierSaves[10])},
        {key: 'z', function: () => this.gameC.addCrawler(null, this.gameC.nodes[0])},
        {key: 'r', function: this.cheatResearch},
        {key: 'o', function: () => GameEvents.ACTIVITY_LOG.publish({slug: 'PRESTIGE'})},
        // {key: 'k', function: this.toggleKnowledge},
      ]);
    }

    // --- initialize game state --- \\

    if (level) {
      if (level.length === 0) {
        this.newGame();
      } else {
        this.gameC.loadSaves(level, []);
      }
    } else if (this.extrinsic.stageState) {
      this.gameC.loadSaves(this.extrinsic.stageState, this.extrinsic.crawlers);
    } else this.newGame();

    this.gameC.knowledge.initializeAchievements();
    this.saveGameTimeout();
    GameEvents.APP_LOG.publish({type: 'NAVIGATE', text: 'GAME INSTANCE CREATED'});
    (window as any).exportSave = this.logSave;
    (window as any).importSave = this.loadSave;
  }

  public destroy() {
    super.destroy();
    this.exists = false;

    this.canvas.destroy();
    this.container.destroy();
    this.gameC.destroy();
    this.nodeManager.destroy();
    this.sidebar.destroy();
    this.bottomBar.destroy();
    this.keymapper.destroy();
    this.mouseC.destroy();

    GameEvents.APP_LOG.publish({type: 'NAVIGATE', text: 'GAME INSTANCE DESTROYED'});
  }

  public navIn = () => {
    JMTicker.add(this.onTick);
    this.keymapper.enabled = true;
    this.sidebar.navIn();
    GameEvents.APP_LOG.publish({type: 'NAVIGATE', text: 'GAME NAV IN'});

  }

  public navOut = () => {
    JMTicker.remove(this.onTick);
    this.exists = false;
    this.keymapper.enabled = false;
    this.sidebar.navOut();
    GameEvents.APP_LOG.publish({type: 'NAVIGATE', text: 'GAME NAV OUT'});
  }

  public saveGameTimeout = () => {
    if (!this.exists) return;
    new InfoPopup(StringManager.data.UI_SAVE);
    GameEvents.APP_LOG.publish({type: 'SAVE', text: 'SAVE GAME PERIODIC'});

    this.saveGame();
    window.setTimeout(this.saveGameTimeout, 30000);
  }

  public saveGame = () => {
    this.extrinsic.stageState = this.gameC.saveNodes();
    this.extrinsic.crawlers = this.gameC.saveCrawlers();
    SaveManager.saveExtrinsic();
  }

  public newGame() {
    let node = this.gameC.addNewNode(this.nodeManager.getNodeConfig('core'));
    node.view.position.set(600, 300);
    node.power.powerPercent = 0.5;
    node.view.setIntensity(0.5, true);
    // this.saveGame();
  }

  public resetGame = () => {
    PlantNode.resetUid();
    this.navAndDestroy(new GameUI([]));
  }

  public nextStage = () => {
    GameEvents.ACTIVITY_LOG.publishSync({slug: 'PRESTIGE'});

    this.extrinsic.skillTier = this.nodeManager.extractTier(this.extrinsic.skillsNext, this.extrinsic.skillTier);
    this.extrinsic.skillsCurrent = this.extrinsic.skillsNext;
    this.extrinsic.skillsNext = [];
    this.resetGame();
  }

  public onTick = () => {
    this.canvas.onTick();

    if (!this.running) return;

    this.container.onTick(this.gameSpeed);
    this.gameC.onTick(this.gameSpeed);
    this.gameC.knowledge.update();

    this.sidebar.updateNodes();

    let seedling = this.gameC.nodes.find(node => node.slug === 'seedling');

    if (seedling) {
      this.bottomBar.updateSeedling(seedling);
    }

    this.sidebar.updateKnowledge(this.gameC.knowledge);
  }

  public positionElements = (e: IResizeEvent) => {
    this.canvas.position.set(e.outerBounds.x, e.outerBounds.y);
    this.canvas.outerBounds = e.outerBounds;

    this.bottomBar.resize(e.outerBounds.width);
    this.bottomBar.position.set(e.outerBounds.x, e.outerBounds.bottom - this.bottomBar.barHeight);
  }

  public toggleTurboMode = (b: boolean) => {
    this.gameSpeed = b ? this.turboSpeed : 1;
  }

  public onMouseMove = (position: {x: number, y: number}) => {
    if (position.x < this.previousResize.outerBounds.right) {
      let node = this.container.getClosestObject({x: position.x, y: position.y, notFruit: true});
      this.sidebar.highlightNode(node);
    }
  }

  public createNewNode = (e: {config: INodeConfig, e: PIXI.InteractionEvent, onComplete: () => void}) => {
    if (!e) {
      this.mouseC.clearNextClickEvent();
      this.container.showConnectionCount(false);
      return;
    }

    let link: FDGLink;

    this.container.showConnectionCount();

    let dragCreate = false;
    let timeout = window.setTimeout(() => {
      dragCreate = true;
      this.mouseC.clearNextClickEvent();

      let position = e.e.data.getLocalPosition(this.container);
      let node = this.gameC.addNewNode(e.config);
      node.ghostMode = true;
      node.active = false;
      node.view.position.set(position.x, position.y);

      let initialNearest = this.container.getClosestObject({x: position.x, y: position.y, filter: node, maxLinks: true, notFruit: true});
      if (initialNearest) {
        node.ghostMode = false;
        link = this.gameC.linkNodes(initialNearest, node);
        link.active = false;
      }

      this.mouseC.startDrag({x: position.x, y: position.y - 100, minD: Config.PHYSICS.NEW_MIND, force: Config.PHYSICS.NEW_FORCE, node,
        onRelease: () => {
          this.container.showConnectionCount(false);
          if (node.outlets.length > 0) {
            node.ghostMode = false;
            node.active = true;
            node.view.setIntensity(node.power.powerPercent, true);
            if (link) link.active = true;
          } else {
            this.gameC.removeNode(node);
          }
          e.onComplete();
        },
        onMove: (position2: {x: number, y: number}) => {
          this.gameC.disconnectNode(node);
          node.ghostMode = true;
          let nearest = this.container.getClosestObject({x: position2.x, y: position2.y, filter: node, maxLinks: true, notFruit: true});
          if (nearest) {
            node.ghostMode = false;
            link = this.gameC.linkNodes(nearest, node);
            link.active = false;
          }
        },
      });
    }, 150);

    this.mouseC.onUp.addOnce(() => {
      if (!dragCreate) {
        window.clearTimeout(timeout);
      }
    });

    this.mouseC.setNextClickEvent({onDown: position => {
      let node = this.gameC.addNewNode(e.config);
      node.ghostMode = true;
      node.active = false;
      node.view.position.set(position.x, position.y);

      let initialNearest = this.container.getClosestObject({x: position.x, y: position.y, filter: node, maxLinks: true, notFruit: true});
      if (initialNearest) {
        node.ghostMode = false;
        link = this.gameC.linkNodes(initialNearest, node);
        link.active = false;
      }

      this.mouseC.startDrag({x: position.x, y: position.y, minD: Config.PHYSICS.NEW_MIND, force: Config.PHYSICS.NEW_FORCE, node,
        onRelease: () => {
          this.container.showConnectionCount(false);
          if (node.outlets.length > 0) {
            node.ghostMode = false;
            node.active = true;
            node.view.setIntensity(node.power.powerPercent, true);
            if (link) link.active = true;
          } else {
            this.gameC.removeNode(node);
          }
          e.onComplete();
        },
        onMove: (position2: {x: number, y: number}) => {
          this.gameC.disconnectNode(node);
          node.ghostMode = true;
          let nearest = this.container.getClosestObject({x: position2.x, y: position2.y, filter: node, maxLinks: true, notFruit: true});
          if (nearest) {
            node.ghostMode = false;
            link = this.gameC.linkNodes(nearest, node);
            link.active = false;
          }
        },
      });
    }});
  }

  public deleteNextClicked = (e: { onComplete: () => void }) => {
    if (e) {
      this.container.showConnectionCount();
      this.mouseC.setNextClickEvent({ onDown: position => {
        let nodeToDelete = this.container.getClosestObject({ x: position.x, y: position.y, notType: 'core', notFruit: true });
        if (nodeToDelete) {
          nodeToDelete.flagDestroy = true;
        }

        e.onComplete && e.onComplete();
      } });
    } else {
      this.mouseC.clearNextClickEvent();
      this.container.showConnectionCount(false);
    }
  }

  private logSave = () => {
    console.log(JSON.stringify(this.extrinsic));
  }

  private loadSave = (json: string) => {
    let extrinsic = JSON.parse(json);
    SaveManager.saveExtrinsic(extrinsic, true).then(() => {
      PlantNode.resetUid();
      this.navAndDestroy(new GameUI());
    });
  }

  private cheatResearch = () => {
    let seedling = this.gameC.nodes.find(node => node.slug === 'seedling');

    if (seedling) {
      seedling.receiveResearch(1000);
    }
  }
}

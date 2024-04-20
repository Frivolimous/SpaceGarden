import _, { update } from 'lodash';
import * as PIXI from 'pixi.js';
import { BaseUI } from './_BaseUI';
import { GameEvents, IResizeEvent } from '../services/GameEvents';
import { Direction, ScrollingContainer } from '../components/ScrollingContainer';
import { KeyMapper } from '../JMGE/KeyMapper';
import { JMTicker } from '../JMGE/events/JMTicker';
import { FDGContainer } from '../engine/FDG/FDGContainer';
import { MouseController } from '../services/MouseController';
import { BottomBar } from '../components/BottomBar';
import { NodeManager } from '../engine/Mechanics/NodeManager';
import { FDGLink } from '../engine/FDG/FDGLink';
import { GameController } from '../engine/Mechanics/GameController';
import { Sidebar } from '../components/domui/Sidebar';
import { INodeConfig } from '../data/NodeData';
import { IExtrinsicModel, TierSaves } from '../data/SaveData';
import { Config, dConfigNode } from '../Config';
import { InfoPopup } from '../components/domui/InfoPopup';
import { HubCostType, IHubConfig, SkillData } from '../data/SkillData';
import { SkillPanel } from '../components/domui/SkillPanel';
import { StringManager } from '../services/StringManager';
import { GOD_MODE } from '../services/_Debug';
import { INodeSave, PlantNode } from '../engine/nodes/PlantNode';
import { AchievementPanel } from '../components/domui/AchievementPanel';
import { AchievementSlug, ScoreType } from '../data/ATSData';
import { HubPanel } from '../components/domui/HubPanel';
import { Timer } from '../components/domui/Timer';
import { Facade } from '..';
import { SimpleModal } from '../components/ui/modals/SimpleModal';
import { PointingArrow } from '../JMGE/effects/PointingArrow';
import { JMTween } from '../JMGE/JMTween';

export class GameUI extends BaseUI {
  public gameC: GameController;

  private timer: Timer;
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

  private pointingArrow: PointingArrow;

  constructor(level?: INodeSave[]) {
    super({bgColor: 0x777777});

    // --- initialize components --- \\

    Config.NODE = _.clone(dConfigNode);

    this.extrinsic = Facade.saveManager.getExtrinsic();

    this.nodeManager = new NodeManager(this.extrinsic.skillTier);
    this.canvas = new ScrollingContainer(1500, 1000);
    this.container = new FDGContainer(this.canvas.innerBounds);
    this.mouseC = new MouseController(this.canvas, this.container);
    this.gameC = new GameController(this.container, this.nodeManager, this.extrinsic.scores);

    this.nodeManager.applySkills(this.extrinsic.skillsCurrent);
    this.nodeManager.applyAchievements(this.extrinsic.achievements);
    this.nodeManager.applyHubs(this.extrinsic.hubLevels);

    let always = this.nodeManager.getSkillAlways(this.extrinsic.skillTier);

    let nextSkillPanel = new SkillPanel(this.nodeManager.skills, this.extrinsic.skillsNext, always, this.extrinsic.skillTier);
    let hubPanel = new HubPanel(this.nodeManager.hubSkills, this.extrinsic.hubLevels, this.increaseHubLevel, this.toggleHubCollection);
    hubPanel.toggleToggleButtons(false);
    let currentSkillPanel: SkillPanel;

    if (this.extrinsic.skillsCurrent.length + always.length > 0) {
      currentSkillPanel = new SkillPanel(this.nodeManager.skills, this.extrinsic.skillsCurrent, always, this.extrinsic.skillTier, true);
    }

    let achieveElement = new AchievementPanel(this.extrinsic.achievements, SkillData.achievements);

    this.sidebar = new Sidebar(currentSkillPanel, nextSkillPanel, hubPanel, achieveElement);
    this.keymapper = new KeyMapper();
    this.bottomBar = new BottomBar(100, 100, this.nodeManager.buildableNodes.map(slug => this.nodeManager.getNodeConfig(slug)));

    this.timer = new Timer();

    // --- add to stage --- \\
    this.canvas.addChild(this.container);
    this.addChild(this.canvas);
    this.addChild(this.bottomBar);

    // --- weave components --- \\

    this.mouseC.onMove.addListener(this.onMouseMove);
    this.gameC.onNodeAdded.addListener(this.sidebar.addNodeElement);
    this.gameC.onNodeAdded.addListener(this.bottomBar.nodeAdded);
    this.gameC.onNodeRemoved.addListener(this.nodeRemoved);
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
        {key: '3', noCtrl: true, function: () => this.loadSave(TierSaves[3])},
        {key: '4', noCtrl: true, function: () => this.loadSave(TierSaves[4])},
        {key: '0', noCtrl: true, function: () => this.loadSave(TierSaves[0])},
        {key: '1', withCtrl: true, function: () => this.loadSave(TierSaves[11])},
        {key: '2', withCtrl: true, function: () => this.loadSave(TierSaves[12])},
        {key: '0', withCtrl: true, function: () => this.loadSave(TierSaves[10])},
        {key: 'z', function: () => this.gameC.addCrawler(null, this.gameC.nodes[0])},
        {key: 'r', function: this.cheatResearch},
        {key: 'o', function: () => GameEvents.ACTIVITY_LOG.publish({slug: 'PRESTIGE'})},
        {key: 'c', function: () => this.nodeManager.hubSkills.forEach(el => el.costs = el.costs.map(el => 5))},
        {key: 'z', function: () => this.gameC.knowledge.achieveAchievement(AchievementSlug.HUB_3)},
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
    this.timer.destroy();

    GameEvents.APP_LOG.publish({type: 'NAVIGATE', text: 'GAME INSTANCE DESTROYED'});
  }

  public navIn = () => {
    JMTicker.add(this.onTick);
    this.keymapper.enabled = true;
    this.sidebar.navIn();
    GameEvents.APP_LOG.publish({type: 'NAVIGATE', text: 'GAME NAV IN'});
    if (this.extrinsic.tutorialStep < 6) {
      JMTicker.add(this.checkTutorial);
    }
  }

  public navOut = () => {
    JMTicker.remove(this.onTick);
    // JMTween.clearTweens();
    this.exists = false;
    this.keymapper.enabled = false;
    this.sidebar.navOut();
    GameEvents.APP_LOG.publish({type: 'NAVIGATE', text: 'GAME NAV OUT'});
    if (this.extrinsic.tutorialStep < 6) {
      JMTicker.remove(this.checkTutorial);
    }
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
    Facade.saveManager.saveExtrinsic();
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

    this.extrinsic.scores[ScoreType.RESEARCH_SAVED] = this.gameC.knowledge.getCurrentResearch() * Config.NODE.SAVED_RESEARCH;
    this.extrinsic.skillTier = this.nodeManager.extractTier(this.extrinsic.skillsNext, this.extrinsic.skillTier);
    this.extrinsic.skillsCurrent = this.extrinsic.skillsNext;
    this.extrinsic.skillsNext = [];
    this.extrinsic.prestigeTime = 0;
    this.resetGame();
  }

  public onTick = (delta: number) => {
    this.canvas.onTick();

    if (!this.running) return;

    this.updateTimer(delta);
    this.container.onTick(this.gameSpeed);
    this.gameC.onTick(this.gameSpeed);
    this.gameC.knowledge.update();

    this.sidebar.updateNodes();

    let seedling = this.gameC.knowledge.sortedNodes.seedling[0];
    let core = this.gameC.knowledge.sortedNodes.core[0];

    if (core) this.canvas.lockCamera(core.view);

    if (seedling) {
      this.bottomBar.updateSeedling(seedling);
    }

    this.sidebar.updateKnowledge(this.gameC.knowledge);
    }

  public checkTutorial = () => {
    if (!this.extrinsic.tutorialStep) {
      this.extrinsic.tutorialStep = 1;
      let modal = new SimpleModal('Wecome to your Space Garden.  This is a slow paced game, where your goal is to grow a magical space plant and evolve its seedling.', () => this.extrinsic.tutorialStep = 2);
      this.addDialogueWindow(modal, 500);
      this.bottomBar.buttons.forEach(button => button.disabled = true);

    } else if (this.extrinsic.tutorialStep === 2) {
      this.extrinsic.tutorialStep = 3;
      this.pointingArrow = new PointingArrow();
      this.pointingArrow.x = 25;
      this.pointingArrow.visible = false;
      let modal = new SimpleModal('To start with, try adding a Stem to your Core by dragging it from the button here.', null);
      modal.onAppearComplete = () => this.pointingArrow.visible = true;
      this.addDialogueWindow(modal, 500);
      modal.position.set(this.previousResize.outerBounds.x + 400 / 2 + 25, this.bottomBar.y - 200);

      let stemB = this.bottomBar.getButton('stem');
      stemB.disabled = false;
      stemB.addChild(this.pointingArrow);

    } else if (this.extrinsic.tutorialStep === 3) {
      let stem = this.gameC.nodes.find(el => el.slug === 'stem');
      if (stem) {
        this.extrinsic.tutorialStep = 4;
        this.pointingArrow && this.pointingArrow.destroy();
        this.pointingArrow = null;
        (this.dialogueWindow as SimpleModal).closeModal();
      }
    } else if (this.extrinsic.tutorialStep === 4) {
      let stem = this.gameC.nodes.find(el => el.slug === 'stem');
      if (!stem) {
        this.extrinsic.tutorialStep = 2;
      } else {
        if (stem.active) {
          this.extrinsic.tutorialStep = 5;
        }
      }
    } else if (this.extrinsic.tutorialStep === 5) {
      this.bottomBar.buttons.forEach(button => button.disabled = false);
      let coreB = this.bottomBar.getButton('core');
      coreB.disabled = true;
      this.extrinsic.tutorialStep = 6;
      let modal = new SimpleModal('Keep building your plant to discover all the mysteries that the Space Garden holds!', () => this.extrinsic.tutorialStep = 6);
      this.addDialogueWindow(modal, 500);
      JMTicker.remove(this.checkTutorial);
    }
  }

  public updateTimer(delta: number) {
    this.extrinsic.totalTime += this.gameSpeed * delta;
    this.extrinsic.prestigeTime += this.gameSpeed * delta;
    this.timer.setTime(this.extrinsic.prestigeTime);
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

  public createNewNode = (e: {config: INodeConfig, e: PIXI.FederatedPointerEvent, onComplete: () => void}) => {
    if (!e) {
      this.mouseC.clearNextClickEvent();
      this.container.showConnectionCount(false);
      return;
    }

    this.container.showConnectionCount();

    let dragCreate = false;
    let timeout = window.setTimeout(() => {
      dragCreate = true;
      this.mouseC.clearNextClickEvent();

      this.finishCreateNewNode(e.e.getLocalPosition(this.container), e.config, e.onComplete);
    }, 150);

    this.mouseC.onUp.addOnce(() => {
      if (!dragCreate) {
        window.clearTimeout(timeout);
      }
    });

    this.mouseC.setNextClickEvent({onDown: position => {
      this.finishCreateNewNode(position, e.config, e.onComplete);
    }});
  }

  finishCreateNewNode(position: {x: number, y: number}, config: INodeConfig, onComplete: () => void) {
    let link: FDGLink;

    let node = this.gameC.addNewNode(config);
    node.ghostMode = true;
    node.active = false;
    node.view.position.set(position.x, position.y);
    if (node.slug === 'seedling') {
      node.power.researchCurrent = this.extrinsic.scores[ScoreType.RESEARCH_SAVED];
    }

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
        onComplete();
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
  }

  public deleteNextClicked = (e: { onComplete: () => void }) => {
    if (e) {
      this.container.showConnectionCount();
      this.mouseC.setNextClickEvent({ onDown: position => {
        let nodeToDelete = this.container.getClosestObject({ x: position.x, y: position.y, notType: 'core', notFruit: true });
        if (nodeToDelete) {
          nodeToDelete.flagDestroy = true;
          nodeToDelete.flagExplode = true;
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
    Facade.saveManager.saveExtrinsic(extrinsic, true).then(() => {
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

  private nodeRemoved = (node: PlantNode) => {
    this.bottomBar.nodeRemoved(node);
    this.sidebar.removeNodeElement(node);
    if (node.active && node.slug === 'seedling') {
      this.extrinsic.scores[ScoreType.RESEARCH_SAVED] = node.power.researchCurrent * Config.NODE.SAVED_RESEARCH;
    }
  }

  increaseHubLevel = (skill: IHubConfig) => {
    let hub = this.gameC.nodes.find(el => el.slug === 'hub');

    if (hub) {
      let levelPair = this.extrinsic.hubLevels.find(el => el[0] === skill.slug);
      let level = levelPair ? levelPair[1] : 0;

      let cost = skill.costs[level];
      if (skill.costType === 'research') {
        hub.power.researchCurrent -= cost;
      } else if (skill.costType === 'fruit') {
        hub.power.fruitCurrent -= cost;
      } else if (skill.costType === 'buff') {
        hub.power.buffCurrent -= cost;
      } else {
        hub.power.storedPowerCurrent -= cost;
      }

      this.nodeManager.applyNodeEffect(skill.effect);

      if (skill.effect.key === 'maxCount') {
        let node = this.nodeManager.getNodeConfig(skill.effect.slug);
        this.bottomBar.refreshNodeButton(node);
      } else if (skill.effect.key === 'powerGen') {
        this.gameC.nodes.forEach(node => {
          if (node.slug === skill.effect.slug) {
            node.power._PowerGen += skill.effect.value;
          }
        });
      } else if (skill.effect.key === 'researchGen') {
        this.gameC.nodes.forEach(node => {
          if (node.slug === skill.effect.slug) {
            node.power._ResearchGen *= skill.effect.value;
          }
        })
      }

      if (!levelPair) {
        levelPair = [skill.slug, 0];
        this.extrinsic.hubLevels.push(levelPair);
      }

      levelPair[1]++;

      GameEvents.ACTIVITY_LOG.publish({ slug: 'HUB', data: levelPair, text: `Level Up Hub Ability: ${levelPair[0]} to level ${levelPair[1]}`});
    }
  }

  toggleHubCollection = (type: HubCostType, state: boolean) => {
    let hub = this.gameC.nodes.find(el => el.slug === 'hub');

    if (hub) {
      switch(type) {
        case 'research': hub.power.canReceiveResearch = state; break;
        case 'fruit': hub.power.canReceiveFruit = state; break;
        case 'power': hub.power.canStorePower = state; break;
      }
    }
  }
}

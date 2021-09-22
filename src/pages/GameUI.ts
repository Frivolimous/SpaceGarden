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
import { NodeManager } from '../services/NodeManager';
import { FDGLink } from '../engine/FDG/FDGLink';
import { GameController } from '../engine/Mechanics/GameController';
import { Sidebar } from '../components/domui/Sidebar';
import { GameNode } from '../engine/Mechanics/Parts/GameNode';
import { INodeConfig, NodeData } from '../data/NodeData';
import { FDGNode } from '../engine/FDG/FDGNode';
import { IExtrinsicModel } from '../data/SaveData';
import { SaveManager } from '../services/SaveManager';
import { Config } from '../Config';
import { InfoPopup } from '../components/domui/InfoPopup';
import { ISkillConfig, SkillData } from '../data/SkillData';
import { SkillPanel } from '../components/domui/SkillPanel';

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

  constructor(level?: string) {
    super({bgColor: 0x777777});

    this.extrinsic = SaveManager.getExtrinsic();

    this.nodeManager = new NodeManager(NodeData.Nodes, SkillData.skills);

    let skills = this.nodeManager.getSkillsBySlugs(this.extrinsic.skillsCurrent);
    skills.filter(skill => skill.effects.find(effect => effect.effectType === 'tier')).forEach(this.applySkillTier);

    console.log('s', skills);
    let always = this.nodeManager.getSkillAlways(this.extrinsic.skillTier);
    let allSkills = _.uniq(skills.concat(this.nodeManager.getSkillsBySlugs(always)));
    allSkills.forEach(this.applySkill);
    console.log(allSkills);

    this.canvas = new ScrollingContainer(1500, 1000);
    this.container = new FDGContainer(this.canvas.innerBounds);
    this.mouseC = new MouseController(this.canvas, this.container);
    this.gameC = new GameController(this.container, this.nodeManager);

    let nextSkillPanel = new SkillPanel(this.nodeManager.skills, this.extrinsic.skillsNext, always, this.extrinsic.skillTier);
    let currentSkillPanel: SkillPanel;

    if (this.extrinsic.skillsCurrent.length + always.length > 0) {
      currentSkillPanel = new SkillPanel(this.nodeManager.skills, this.extrinsic.skillsCurrent, always, this.extrinsic.skillTier, true);
    }

    this.sidebar = new Sidebar(currentSkillPanel, nextSkillPanel);
    this.keymapper = new KeyMapper();
    this.bottomBar = new BottomBar(100, 100, this.extrinsic.nodes.map(slug => this.nodeManager.getNodeConfig(slug)));

    this.canvas.addChild(this.container);
    this.addChild(this.canvas);
    this.addChild(this.bottomBar);

    this.mouseC.onMove.addListener(this.onMouseMove);
    this.container.onNodeAdded.addListener(this.sidebar.addNodeElement);
    this.container.onNodeAdded.addListener(this.bottomBar.nodeAdded);
    this.container.onNodeRemoved.addListener(this.bottomBar.nodeRemoved);
    this.container.onNodeRemoved.addListener(this.gameC.removeNodeByView);
    this.container.onNodeRemoved.addListener(this.sidebar.removeNodeElement);
    this.bottomBar.onProceedButton.addListener(this.nextStage);
    this.bottomBar.onCreateButton.addListener(this.createNewNode);
    this.bottomBar.onDeleteButton.addListener(this.mouseC.deleteNextClicked);
    this.bottomBar.onTurboButton.addListener(this.toggleTurboMode);

    this.keymapper.enabled = false;
    this.keymapper.setKeys([
      {key: 'ArrowLeft', altKey: 'a', function: () => this.canvas.startPan(Direction.LEFT)},
      {key: 'ArrowRight', altKey: 'd', function: () => this.canvas.startPan(Direction.RIGHT)},
      {key: 'ArrowUp', altKey: 'w', function: () => this.canvas.startPan(Direction.UP)},
      {key: 'ArrowDown', altKey: 's', function: () => this.canvas.startPan(Direction.DOWN)},
      {key: ' ', function: () => this.running = !this.running},
      {key: '=', altKey: '+', function: () => this.canvas.zoomBy(1.2), noHold: true},
      {key: '-', altKey: '_', function: () => this.canvas.zoomBy(1 / 1.2), noHold: true},
      {key: '[', function: () => this.gameSpeed = Math.max(this.gameSpeed - 1, 1)},
      {key: ']', function: () => this.gameSpeed++},
    ],
    [
      {key: 'ArrowLeft', altKey: 'a', function: () => this.canvas.endPan(Direction.LEFT)},
      {key: 'ArrowRight', altKey: 'd', function: () => this.canvas.endPan(Direction.RIGHT)},
      {key: 'ArrowUp', altKey: 'w', function: () => this.canvas.endPan(Direction.UP)},
      {key: 'ArrowDown', altKey: 's', function: () => this.canvas.endPan(Direction.DOWN)},
      {key: 'p', function: () => this.resetGame()},
    ]);

    if (level) {
      if (level === 'empty') {
        this.newGame();
      } else {
        this.gameC.loadSaves(JSON.parse(level));
      }
    } else if (this.extrinsic.stageState) {
      this.gameC.loadSaves(JSON.parse(this.extrinsic.stageState));
    } else this.newGame();

    this.saveGameTimeout();
  }

  public destroy() {
    super.destroy();

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
  }

  public navOut = () => {
    JMTicker.remove(this.onTick);
    this.keymapper.enabled = false;
    GameEvents.APP_LOG.publish({type: 'NAVIGATE', text: 'GAME NAV OUT'});
  }

  public saveGameTimeout = () => {
    if (!this.exists) return;
    new InfoPopup('Game Saved!');

    this.saveGame();
    window.setTimeout(this.saveGameTimeout, 30000);
  }

  public saveGame = () => {
    let state = JSON.stringify(this.gameC.saveNodes());
    this.extrinsic.stageState = state;
    SaveManager.saveExtrinsic();
  }

  public newGame() {
    let node = this.gameC.addNewNode(this.nodeManager.getNodeConfig('core'));
    node.position.set(600, 300);
    node.data.powerPercent = 0.5;
  }

  public resetGame = () => {
    GameNode.resetUid();
    this.navAndDestroy(new GameUI('empty'));
  }

  public nextStage = () => {
    this.extrinsic.skillsCurrent = this.extrinsic.skillsNext;
    this.extrinsic.skillsNext = [];
    this.resetGame();
  }

  public onTick = () => {
    this.canvas.onTick();

    if (!this.running) return;

    this.gameC.onTick(this.gameSpeed);
    this.container.onTick(this.gameSpeed);
    this.sidebar.updateNodes();

    let seedling = this.gameC.nodes.find(node => node.config.slug === 'seedling');

    if (seedling) {
      this.bottomBar.updateSeedling(seedling.view);
    }
  }

  public positionElements = (e: IResizeEvent) => {
    this.canvas.position.set(e.outerBounds.x, e.outerBounds.y);
    this.canvas.outerBounds = e.outerBounds;

    this.bottomBar.resize(e.outerBounds.width);
    this.bottomBar.position.set(e.outerBounds.x, e.outerBounds.bottom - this.bottomBar.barHeight);
  }

  public applySkillTier = (skill: ISkillConfig) => {
    skill.effects.forEach(effect => {
      if (effect.effectType === 'tier') {
        if (effect.valueType === 'additive') {
          this.extrinsic.skillTier += effect.value;
        } else if (effect.valueType === 'replace') {
          this.extrinsic.skillTier = Math.max(this.extrinsic.skillTier, effect.value);
        }
      }
    });
  }

  public applySkill = (skill: ISkillConfig) => {
    skill.effects.forEach(effect => {
      if (effect.effectType === 'node') {
        let node = this.nodeManager.data.find(block => block.slug === effect.slug);
        if (effect.key === 'outletEffect') {
          if (effect.valueType === 'additive') {
            node.outletEffects = node.outletEffects || [];
            node.outletEffects.push(_.clone(effect.value));
          } else if (effect.valueType === 'replace') {
            node.outletEffects = [_.clone(effect.value)];
          }
        }
        if (effect.valueType === 'additive') {
          (node as any)[effect.key] += effect.value;
        } else if (effect.valueType === 'multiplicative') {
          (node as any)[effect.key] *= effect.value;
        } else if (effect.valueType === 'replace') {
          (node as any)[effect.key] = effect.value;
        }
      }
    });
  }

  public toggleTurboMode = (b: boolean) => {
    this.gameSpeed = b ? this.turboSpeed : 1;
  }

  public onMouseMove = (position: {x: number, y: number}) => {
    let node = this.container.getClosestObject({x: position.x, y: position.y, notFruit: true});
    this.sidebar.highlightNode(node);
  }

  public createNewNode = (e: {config: INodeConfig, e: PIXI.interaction.InteractionEvent}) => {
    let position = e.e.data.getLocalPosition(this.container);
    let node = this.gameC.addNewNode(e.config);
    let link: FDGLink;

    node.ghostMode = true;
    node.data.active = false;
    node.position.set(position.x, position.y);

    this.container.showConnectionCount();

    this.mouseC.startDrag({x: position.x, y: position.y - 100, minD: Config.PHYSICS.NEW_MIND, force: Config.PHYSICS.NEW_FORCE, node,
      onRelease: () => {
        this.container.showConnectionCount(false);
        if (node.data.outlets.length > 0) {
          node.ghostMode = false;
          node.data.active = true;
          if (link) link.active = true;
        } else {
          this.container.removeNode(node);
        }
      },
      onMove: (position2: {x: number, y: number}) => {
        this.container.removeAllLinksFor(node, true);
        node.ghostMode = true;
        let nearest = this.container.getClosestObject({x: position2.x, y: position2.y, filter: node, maxLinks: true, notFruit: true});
        if (nearest) {
          node.ghostMode = false;
          link = this.container.linkNodes(nearest, node);
          link.active = false;
        }

        // let nearest2 = this.container.getClosestObject({x: position.x, y: position.y, filter: node, maxLinks: true, notFruit: true});
        // if (nearest2) {
        //   if (!nearest) {
        //     node.ghostMode = false;
        //     link = this.container.linkNodes(nearest2, node);
        //     link.active = false;
        //     nearest = nearest2;
        //   } else if (nearest2 !== nearest) {
        //     this.container.removeAllLinksFor(node, true);
        //     link = this.container.linkNodes(nearest2, node);
        //     link.active = false;
        //     nearest = nearest2;
        //   }
        // } else {
        //   if (nearest) {
        //     node.ghostMode = true;
        //     this.container.removeAllLinksFor(node, true);
        //     nearest = null;
        //   }
        // }
      },
    });
  }
}

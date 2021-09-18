import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { SaveManager } from './SaveManager';
import { JMEventListener } from '../JMGE/events/JMEventListener';
import { TutorialPopup } from '../components/ui/TutorialPopup';
import { IExtrinsicModel } from '../data/SaveData';

export interface IAchievement {
  id: number;
  emitter: JMEventListener;
  condition: (extrinsic: IExtrinsicModel, e: any) => boolean;
  title: string;
  caption: string;
  listener?: (e: any) => void;
  active?: boolean;
  prev?: number;
}

export interface ITutorial {
  id: number;
  emitter: JMEventListener;
  condition: (extrinsic: IExtrinsicModel, e: any) => boolean;
  title: string;
  caption: string;
  listener?: (e: any) => void;
  active?: boolean;
}

export interface IScore {
  id: number;
  type: '++'|'=';
  prop: string;
  emitter: JMEventListener;
  condition: (extrinsic: IExtrinsicModel, e: any) => boolean;
  listener?: (e: any) => void;
}

export interface IATSManager {
  Achievements: IAchievement[];
  Tutorials: ITutorial[];
  Scores: IScore[];
  achievementPopup: any;
  tutorialPopup: any;
  canvas: PIXI.Container;
}

export class ATSManager { // Achievement, Tutorial, Score
  private extrinsic: IExtrinsicModel;

  private currentTutorial: TutorialPopup;

  constructor(private config: IATSManager) {
    this.extrinsic = SaveManager.getExtrinsic();

    // config.Achievements.forEach(achievement => {
    //   if (!this.extrinsic.data.badges[achievement.id]) {
    //     let listener = (e: any) => this.testAchievement(achievement, e);
    //     achievement.listener = listener;
    //     achievement.emitter.addListener(listener);
    //     achievement.active = true;
    //   }
    // });

    // config.Tutorials.forEach(tutorial => {
    //   if (!this.extrinsic.data.tutorials[tutorial.id]) {
    //     let listener = (e: any) => this.testTutorial(tutorial, e);
    //     tutorial.listener = listener;
    //     tutorial.emitter.addListener(listener);
    //     tutorial.active = true;
    //   }
    // });

    // config.Scores.forEach(score => {
    //   let listener = (e: any) => this.testScore(score, e);
    //   score.listener = listener;
    //   score.emitter.addListener(listener);
    // });

    // GameEvents.REQUEST_PAUSE_GAME.addListener(this.onPause);
  }

  // private testAchievement = (achievement: IAchievement, e: any) => {
  //   if (achievement.condition(this.extrinsic, e)) {
  //     let popup = new this.config.achievementPopup(achievement.title, achievement.caption);
  //     this.config.canvas.addChild(popup);
  //     this.extrinsic.data.badges[achievement.id] = true;
  //     SaveManager.saveExtrinsic();
  //     achievement.emitter.removeListener(achievement.listener);
  //     achievement.active = false;
  //   }
  // }

  // private testTutorial = (tutorial: ITutorial, e: any) => {
  //   if (tutorial.condition(this.extrinsic, e)) {
  //     // GameEvents.REQUEST_PAUSE_GAME.publish({paused: true});
  //     let popup = new this.config.tutorialPopup(tutorial.title, tutorial.caption);
  //     this.currentTutorial = popup;
  //     this.config.canvas.addChild(popup);
  //     this.extrinsic.data.tutorials[tutorial.id] = true;
  //     SaveManager.saveExtrinsic();
  //     tutorial.emitter.removeListener(tutorial.listener);
  //     tutorial.active = false;
  //   }
  // }

  // private testScore = (score: IScore, e: any) => {
  //   if (score.condition(this.extrinsic, e)) {
  //     switch (score.type) {
  //       case '++': this.incrementExtrinsicValue(score.prop, 1); break;
  //       case '=': this.setExtrinsicValue(score.prop, e); break;
  //     }
  //   }
  // }

  private setExtrinsicValue(prop: string, value: any) {
    let cProp: any = this.extrinsic;
    let propArray = _.split(prop, '.');
    while (propArray.length > 1) {
      let key = propArray.shift();
      cProp = cProp[key];
    }
    cProp[propArray.shift()] = value;

    SaveManager.saveExtrinsic();
  }

  private incrementExtrinsicValue(prop: string, value: number) {
    let cProp: any = this.extrinsic;
    let propArray = _.split(prop, '.');
    while (propArray.length > 1) {
      let key = propArray.shift();
      cProp = cProp[key];
    }
    cProp[propArray.shift()] += value;

    SaveManager.saveExtrinsic();
  }

  // private onPause = (e: IPauseEvent) => {
  //   if (!e.paused) {
  //     if (this.currentTutorial) {
  //       this.currentTutorial.destroy();
  //       this.currentTutorial = null;
  //     }
  //   }
  // }
}

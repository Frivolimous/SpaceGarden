// import * as _ from 'lodash';
// import * as PIXI from 'pixi.js';
// import { SkillPage } from './SkillPage';
// import { DataConverter } from '../../services/DataConverter';
// import { SkillPageMap, ISkill, ISkillPageMap } from '../../data/SkillData';
// import { Button } from '../ui/Button';

// export class SkillTree extends PIXI.Container {
//   private pages: SkillPage[] = [];
//   private currentPage: number;
//   private indicators: PIXI.Graphics[] = [];

//   constructor() {
//     super();

//     this.loadPages([], SkillPageMap, (skill: ISkill) => {
//       if (skill.level < 10) {
//         return DataConverter.getSkill(skill.slug, skill.level + 1);
//       } else {
//         return skill;
//       }
//     });

//     let rightArrow = this.makeArrowButton(() => {
//       this.removeChild(this.pages[this.currentPage]);
//       this.currentPage = (this.currentPage + 1) % this.pages.length;
//       this.addChild(this.pages[this.currentPage]);
//       this.indicators.forEach((g, i) => g.tint = (i === this.currentPage ? 0x333333 : 0xffffff));
//     }, false);

//     let leftArrow = this.makeArrowButton(() => {
//       this.removeChild(this.pages[this.currentPage]);
//       this.currentPage --;
//       if (this.currentPage < 0) {
//         this.currentPage += this.pages.length;
//       }
//       this.addChild(this.pages[this.currentPage]);
//       this.indicators.forEach((g, i) => g.tint = (i === this.currentPage ? 0x333333 : 0xffffff));
//     }, true);

//     this.addChild(leftArrow, rightArrow);

//     leftArrow.position.set(0, 0);
//     rightArrow.position.set(180, 0);
//   }

//   public loadPages(skills: ISkill[], pages: ISkillPageMap[], callback: (skill: ISkill, passive?: boolean) => ISkill) {
//     this.pages.forEach(page => page.destroy());
//     this.pages = [];
//     while (this.indicators.length > 0) {
//       this.indicators.shift().destroy();
//     }

//     let pageSettings = { width: 200, height: 200, bgColor: 0x333333, lineColor: 0xffffff };

//     this.pages = _.map(pages, page => {
//       let m = new SkillPage(skills, page, pageSettings, callback);
//       m.position.set(0, 30);
//       return m;
//     });
//     this.addChild(this.pages[0]);
//     this.currentPage = 0;
//     for (let i = 0; i < this.pages.length; i++) {
//       let indicator = this.makeIndicatorIcon();
//       this.addChild(indicator);
//       indicator.position.set(100 + 5 - (15 * this.pages.length / 2) + i * 15, -10);
//       this.indicators.push(indicator);
//     }
//     if (this.indicators.length > 0) {
//       this.indicators[0].tint = 0x333333;
//     }
//   }

//   public setMaxLevel(n: number) {
//     this.pages.forEach(page => page.setMaxLevel(n));
//   }

//   public getWidth() {
//     return 200;
//   }

//   public getHeight() {
//     return 200;
//   }

//   public makeArrowButton(callback: () => void, left?: boolean) {
//     let size = 20;
//     let button = new Button({ width: size, height: size, onClick: callback, color: 0x333399 });
//     if (left) {
//       button.startCustomDraw().moveTo(size, 0).lineTo(0, size / 2).lineTo(size, size).lineTo(size, 0);
//     } else {
//       button.startCustomDraw().moveTo(0, 0).lineTo(size, size / 2).lineTo(0, size).lineTo(0, 0);
//     }

//     return button;
//   }

//   public makeIndicatorIcon() {
//     let graphic = new PIXI.Graphics();
//     graphic.beginFill(0xf1f1f1).lineStyle(2, 0x333333).drawCircle(0, 0, 5);
//     return graphic;
//   }
// }

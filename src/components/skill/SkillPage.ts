// import * as _ from 'lodash';
// import * as PIXI from 'pixi.js';
// import { SkillIcon } from './SkillIcon';
// import { ISkill, ISkillPageMap, SkillPrerequisiteMap } from '../../data/SkillData';
// import { DataConverter } from '../../services/DataConverter';
// import { Fonts } from '../../data/Fonts';
// import { StringManager } from '../../services/StringManager';
// export interface ISkillPage {
//   width: number;
//   height: number;
//   bgColor: number;
//   lineColor: number;
// }

// const across: number = 5;
// const padding: number = 5;

// export class SkillPage extends PIXI.Container {
//   private icons: SkillIcon[] = [];
//   private passiveIcon: SkillIcon;
//   private background = new PIXI.Graphics();
//   private title: PIXI.Text;

//   constructor(sources: ISkill[], pageSettings: ISkillPageMap, private settings: ISkillPage, private callback?: (skill: ISkill, passive?: boolean) => ISkill) {
//     super();

//     this.icons = _.map(pageSettings.skills, set => new SkillIcon(_.find(sources, {slug: set.slug}) || DataConverter.getSkill(set.slug, 0), { position: set.position }, (skill => {
//       skill = callback(skill);
//       if (skill.level === 1) {
//         let pres = _.filter(SkillPrerequisiteMap, v => v[1] === skill.slug);
//         pres.forEach(pre => {
//           let cIcon = _.find(this.icons, icon => icon.source.slug === pre[0]);
//           if (cIcon && cIcon.source.level === 0) {
//             cIcon.darken();
//           }
//         });
//       }
//       if (skill.level > 0) {
//         let passiveSkill = callback(this.passiveIcon.source, true);
//         this.passiveIcon.updateSource(passiveSkill);
//       }

//       return skill;
//     })));
//     this.passiveIcon = new SkillIcon(_.find(sources, {slug: pageSettings.passive}) || DataConverter.getSkill(pageSettings.passive, 0), { position: -9 });
//     this.addChild(this.background);

//     this.background.beginFill(settings. bgColor).drawRoundedRect(-padding, -padding, settings.width + padding * 2, settings.height + padding * 2, 4);
//     this.icons.forEach(icon => {
//       this.addChild(icon);
//       this.positionSkill(icon);
//     });
//     this.addChild(this.passiveIcon);
//     this.passiveIcon.position.set(0, this.settings.height + 10);
//     // this.positionSkill(this.passiveIcon);

//     pageSettings.skills.forEach(set => {
//       let pre = _.find(SkillPrerequisiteMap, v => v[0] === set.slug);
//       if (pre) {
//         let cIcon = _.find(this.icons, icon => icon.source.slug === set.slug);
//         let pIcon = _.find(this.icons, icon => icon.source.slug === pre[1]);
//         this.drawLines(cIcon, pIcon);
//         if (pIcon.source.level === 0) {
//           cIcon.blacken();
//         }
//       }
//     });

//     this.title = new PIXI.Text(StringManager.data.SKILL_TREE[pageSettings.slug], {fontSize: 20, fontFamily: Fonts.UI});
//     this.addChild(this.title);
//     this.title.position.set((settings.width - this.title.width) / 2, -this.title.height - padding * 2);
//   }

//   public setMaxLevel(n: number) {
//     this.icons.forEach(icon => icon.maxLevel = n);
//   }

//   private positionSkill(skill: SkillIcon) {
//     let position = skill.getPosition();
//     let tWidth = (this.settings.width - skill.getWidth() - padding * 2) / (across - 1);
//     let tHeight = (this.settings.height - skill.getHeight() - padding * 2) / (across - 1);
//     let x = padding + Math.abs(position % across) * tWidth;
//     let y = padding + Math.floor(position / across) * tHeight;
//     skill.position.set(x, y);
//   }

//   private drawLines(icon1: SkillIcon, icon2: SkillIcon) {
//     this.background.endFill().lineStyle(2, this.settings.lineColor);
//     this.background.moveTo(icon1.x + icon1.getWidth() / 2, icon1.y + icon1.getHeight() / 2);
//     this.background.lineTo(icon2.x + icon2.getWidth() / 2, icon2.y + icon2.getHeight() / 2);
//   }
// }

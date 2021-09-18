// import * as PIXI from 'pixi.js';
// import * as _ from 'lodash';
// import { ISkill } from '../../data/SkillData';
// import { TooltipReader } from '../tooltip/TooltipReader';
// import { Fonts } from '../../data/Fonts';
// import { StringManager } from '../../services/StringManager';

// export interface ISkillIcon {
//   width?: number;
//   height?: number;
//   position?: number;
// }

// const dSkillIcon: ISkillIcon = {
//   width: 50,
//   height: 50,
// };

// export class SkillIcon extends PIXI.Container {
//   public maxLevel: number = 10;
//   private text: PIXI.Text;
//   private numbers: PIXI.Text;
//   private background = new PIXI.Graphics();
//   private overlay = new PIXI.Graphics();

//   constructor(public source: ISkill, private settings?: ISkillIcon, private callback?: (skill: ISkill) => ISkill) {
//     super();
//     this.interactive = true;
//     this.buttonMode = true;
//     this.settings = _.defaults(settings, dSkillIcon);

//     let color: number = 0xffffcc;
//     this.background.beginFill(color).lineStyle(3).drawRoundedRect(0, 0, this.settings.width, this.settings.height, 2);
//     this.addChild(this.background);
//     this.overlay.beginFill(0xffffff).lineStyle(3).drawRoundedRect(0, 0, this.settings.width, this.settings.height, 2);
//     this.addChild(this.overlay);
//     this.overlay.visible = false;

//     this.addListener('pointerdown', this.pointerDown);

//     TooltipReader.addTooltip(this, () => ({ title: source.name, description: StringManager.makeSkillDescription(this.source) }));

//     this.text = new PIXI.Text(source.name, { fontFamily: Fonts.UI, fontSize: 25, wordWrap: true, wordWrapWidth: 100 });
//     this.addChild(this.text);
//     this.text.width = this.getWidth() - 10;
//     this.text.scale.y = this.text.scale.x;
//     this.text.position.set(5, (this.getHeight() - this.text.height) / 2);

//     this.numbers = new PIXI.Text('00', { fontFamily: Fonts.UI, fontSize: 10, stroke: 0xcccccc, strokeThickness: 1});
//     this.addChild(this.numbers);

//     this.redraw();
//   }

//   public redraw() {
//     if (this.source.level === 0) {
//       if (!this.overlay.visible) {
//         this.darken();
//       }
//     } else if (this.source.level >= this.maxLevel) {
//       this.overlay.tint = 0xffffff;
//       this.overlay.visible = true;
//       this.overlay.alpha = 0.3;
//     } else {
//       this.overlay.visible = false;
//     }

//     this.numbers.text = String(this.source.level);
//     if (this.source.level < 10) {
//       this.numbers.text = '0' + this.numbers.text;
//     }
//     this.numbers.position.set(this.getWidth() - this.numbers.width - 1, this.getHeight() - this.numbers.height - 1);

//     TooltipReader.resetTooltip(this);
//   }

//   public blacken() {
//     this.overlay.tint = 0;
//     this.overlay.visible = true;
//     this.overlay.alpha = 0.7;
//   }

//   public darken() {
//     this.overlay.tint = 0;
//     this.overlay.visible = true;
//     this.overlay.alpha = 0.4;
//   }

//   public getPosition() {
//     return this.settings.position;
//   }

//   public getWidth() {
//     return this.settings.width;
//   }

//   public getHeight() {
//     return this.settings.height;
//   }

//   public updateSource = (source: ISkill) => {
//     this.source = source;
//     this.redraw();
//   }

//   private pointerDown = (e: PIXI.interaction.InteractionEvent) => {
//     if (this.callback) {
//       this.updateSource(this.callback(this.source));
//     }
//   }
// }

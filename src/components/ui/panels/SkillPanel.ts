// import * as PIXI from 'pixi.js';
// import * as _ from 'lodash';

// import { BasePanel } from './_BasePanel';
// import { SpriteModel } from '../../../engine/sprites/SpriteModel';
// import { Fonts } from '../../../data/Fonts';
// import { SkillPageMap, SkillSlug } from '../../../data/SkillData';
// import { SkillTree } from '../../skill/SkillTree';
// import { StatModel } from '../../../engine/stats/StatModel';
// import { Button } from '../Button';
// import { Formula } from '../../../services/Formula';

// export class SkillPanel extends BasePanel {
//   private tree: SkillTree;
//   private skillpoints: PIXI.Text;
//   private respecB: Button;
//   private respecT: PIXI.Text;
//   private onRepec: (value: number, onSuccess: () => void) => void;

//   private source: StatModel;

//   private rerespec: boolean;

//   constructor(bounds: PIXI.Rectangle = new PIXI.Rectangle(525, 150, 275, 650), color: number = 0xf1f1f1) {
//     super(bounds, color);

//     this.tree = new SkillTree();
//     this.addChild(this.tree);

//     this.skillpoints = new PIXI.Text('Skillpoints: ', {fontSize: 15, fontFamily: Fonts.UI});
//     this.addChild(this.skillpoints);

//     this.tree.position.set((this.getWidth() - this.tree.getWidth()) / 2, 50);
//     this.skillpoints.position.set(130, 295);
//   }

//   public addRespec(onRespec: (value: number, onSuccess: () => void) => void) {
//     this.onRepec = onRespec;
//     this.respecB = new Button({label: 'Respec', width: 50, height: 30, onClick: this.respecSkills});
//     this.respecT = new PIXI.Text('1000g', {fontFamily: Fonts.UI, fontSize: 18});
//     this.addChild(this.respecB, this.respecT);

//     this.respecB.position.set(130, 330);
//     this.respecT.position.set(190, 330);
//   }

//   public addSource = (sprite: SpriteModel | StatModel) => {
//     if (sprite instanceof SpriteModel) {
//       sprite = sprite.stats;
//     }
//     this.source = sprite;
//     this.tree.loadPages(sprite.skills, _.filter(SkillPageMap, page => _.some(this.source.skillTrees, slug => slug === page.slug)), (skill, passive) => {
//       if (passive) {
//         skill = this.source.skills.find(data => data.slug === skill.slug);
//         if (skill) {
//           return skill;
//         }
//       } else {
//         skill = this.source.tryLevelSkill(skill);
//         this.setSkillpoints();
//         return skill;
//       }
//     });

//     if (sprite.talent.slug === SkillSlug.NOBLE) {
//       this.tree.setMaxLevel(7);
//     }

//     this.setSkillpoints();
//   }

//   public respecSkills = () => {
//     if (this.source.getTotalSkillLevel() > 0) {
//       this.onRepec(this.getRespecCost(), () => {
//         this.source.respecSkills();
//         this.addSource(this.source);
//         this.setSkillpoints();
//         this.rerespec = true;
//       });
//     }
//   }

//   public update = (sprite: SpriteModel | StatModel) => {
//     if (sprite instanceof SpriteModel) {
//       sprite = sprite.stats;
//     }
//     this.setSkillpoints();
//   }

//   public setSkillpoints() {
//     this.skillpoints.text = 'Skillpoints: ' + this.source.skillpoints;
//     if (this.respecT) {
//       this.respecT.text = this.getRespecCost() + 'g';
//     }
//   }

//   public getRespecCost() {
//     if (this.rerespec || this.source.talent.slug === SkillSlug.ORDINARY) {
//       return 0;
//     } else {
//       return Formula.getRespecValue(this.source.getTotalSkillLevel());
//     }
//   }
// }

import { StringManager } from '../../services/StringManager';
import { ISkillConfig, SkillData } from '../../data/SkillData';
import { SkillBar } from './SkillBar';
import { Formula } from '../../services/Formula';
import { JMEventListener } from 'src/JMGE/events/JMEventListener';

export class HubPanel {
  public skillsSpent: number = 0;
  public hasSkillToLevel: boolean = false;

  private element: HTMLDivElement;
  private tierContainer: HTMLDivElement;
  private contentElement: HTMLDivElement;
  private skillbar: SkillBar;
  private skillpointElement: HTMLDivElement;
  private skillMap: { element: HTMLButtonElement, skill: ISkillConfig }[] = [];
  private negative: HTMLDivElement;

  private skillLevels: number = 0;

  constructor() {
    this.element = document.createElement('div');
    this.element.classList.add('skill-panel');
    document.body.appendChild(this.element);
    let top = document.createElement('div');
    top.classList.add('top');
    this.element.appendChild(top);
    top.innerHTML = `
    <div class="skill-title">Evolution Hub</div>
    <div class="skill-subtitle">Improvements made here will only affect the current plant.</div>`;
    
    this.contentElement = document.createElement('div');
    this.contentElement.classList.add('skill-content');
    top.appendChild(this.contentElement);
    // skills.forEach((skill) => {
    //   let block = this.createSkillBlock(skill);
    //   this.contentElement.appendChild(block);
    // });

    let button = document.createElement('button');
    button.classList.add('close-button');
    this.element.appendChild(button);
    button.innerHTML = 'X';
    button.addEventListener('click', () => this.hidden = true);

    let skillElement = document.createElement('div');

    this.skillpointElement = document.createElement('div');
    this.skillpointElement.classList.add('skill-skillpoint');
    skillElement.appendChild(this.skillpointElement);
    this.skillpointElement.innerHTML = `5 Skillpoints`;

    this.negative = document.createElement('div');
    this.negative.classList.add('skill-negative');
    skillElement.appendChild(this.negative);

    this.skillbar = new SkillBar();
    skillElement.appendChild(this.skillbar.element);
    this.element.appendChild(skillElement);

    this.hidden = true;
  }

  public get skillpoints(): number {
    return this.skillLevels - this.skillsSpent;
  }

  public get hidden(): boolean {
    return this.element.style.display === 'none';
  }

  public set hidden(b: boolean) {
    if (b) {
      this.element.style.display = 'none';
    } else {
      this.element.style.removeProperty('display');
    }
  }

  public destroy = () => {
    document.body.removeChild(this.element);
  }

  public openTierPage = (pageIndex: number) => {
    let tierList = SkillData.skillTiers[pageIndex];
    this.skillMap.forEach(data => {
      if (tierList.includes(data.skill.slug)) {
        data.element.hidden = false;
      } else {
        data.element.hidden = true;
      }
    });
    if (this.negative) this.negative.innerHTML = (StringManager.data as any)['NEGATIVE_TIER_' + pageIndex];
  }

  public clear() {
    // while (this.leveled.length > 0) this.leveled.shift();
    // this.skillsSpent = 0;
    // this.skillMap.forEach(data => data.element.disabled = false);
  }

  public updateCurrencies = (research: number, fruit: number, power: number) => {
    this.skillpointElement.innerHTML = `${Math.round(research)} Research, ${Math.round(fruit)} Fruits, ${Math.round(power)} Power.`
  }

  // public updateSkillpoints = (research: number) => {
  //   let oldpoints = this.skillpoints;
  //   this.skillLevels = Formula.getNextSkillLevel(research);
  //   let nextCost = Formula.getSkillCost(this.skillLevels);
  //   this.skillbar.updateText(research, nextCost);
  //   this.skillpointElement.innerHTML = `${Math.round(this.skillpoints)} ${StringManager.data.UI_SKILLTREE_SKILLPOINTS}`;
  //   if (oldpoints !== this.skillpoints) {
  //     this.updateHighlights();
  //   }
  // }

  private updateHighlights() {
    let sp = this.skillpoints;
    this.hasSkillToLevel = false;
    this.skillMap.forEach((data, i) => {
      if (data.skill.cost <= sp && !data.element.disabled && !data.element.classList.contains('greyed')) {
        data.element.classList.add('highlight');
        this.hasSkillToLevel = true;
      } else {
        data.element.classList.remove('highlight');
      }
    });
  }

  private createSkillBlock(skill: ISkillConfig): HTMLElement {
    return null;
    // let element = document.createElement('button');
    // element.classList.add('skill-block');
    // element.innerHTML = `<div class="skill-block-title">${skill.title}</div>
    // <div class="skill-block-content">${skill.description}</div>
    // <div class="skill-block-cost">${skill.cost} Point${skill.cost === 1 ? '' : 's'}</div>`;

    // if (this.always.includes(skill.slug)) {
    //   element.disabled = true;
    // }

    // if (this.leveled.includes(skill.slug)) {
    //   element.disabled = true;
    //   this.skillsSpent += skill.cost;
    // }

    // if (this.disabled) {
    //   element.classList.add('greyed');
    // } else {
    //   element.addEventListener('click', () => {
    //     if (skill.cost <= this.skillpoints && !element.classList.contains('greyed')) {
    //       this.skillsSpent += skill.cost;
    //       this.leveled.push(skill.slug);
    //       element.disabled = true;
    //       this.skillMap.forEach(this.checkRequirements);
    //       this.updateHighlights();
    //     }
    //   });
    //   this.checkRequirements({element, skill});
    // }

    // this.skillMap.push({element, skill});

    // return element;
  }

  private checkRequirements = (data: { element: HTMLButtonElement, skill: ISkillConfig }) => {
    // if (data.skill.skillRequirements) {
    //   let allGood = true;
    //   data.skill.skillRequirements.forEach(req => {
    //     if (!this.leveled.includes(req) && !this.always.includes(req)) {
    //       allGood = false;
    //     }
    //   });

    //   if (allGood) {
    //     if (data.element.classList.contains('greyed')) {
    //       data.element.classList.remove('greyed');
    //     }
    //   } else {
    //     data.element.classList.add('greyed');
    //   }
    // }
  }
}

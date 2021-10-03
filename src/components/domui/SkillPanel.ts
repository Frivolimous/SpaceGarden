import { StringManager } from '../../services/StringManager';
import { ISkillConfig, SkillData } from '../../data/SkillData';
import { SkillBar } from './SkillBar';
import { Formula } from '../../services/Formula';

export class SkillPanel {
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

  constructor(skills: ISkillConfig[], private leveled: string[], private always: string[], tier: number, private disabled?: boolean) {
    this.element = document.createElement('div');
    this.element.classList.add('skill-panel');
    document.body.appendChild(this.element);
    if (disabled) {
      this.element.innerHTML = `
      <div class="skill-title">${StringManager.data.UI_SKILLTREE_TITLE}</div>
      <div class="skill-subtitle">${StringManager.data.UI_SKILLTREE_SUBTITLE_INACTIVE}</div>`;
    } else {
      this.element.innerHTML = `
      <div class="skill-title">${StringManager.data.UI_SKILLTREE_TITLE}</div>
      <div class="skill-subtitle">${StringManager.data.UI_SKILLTREE_SUBTITLE_ACTIVE}</div>`;
    }
    if (tier > 0) {
      this.tierContainer = document.createElement('div');
      this.tierContainer.classList.add('tier-content');
      this.element.appendChild(this.tierContainer);
    }

    this.contentElement = document.createElement('div');
    this.contentElement.classList.add('skill-content');
    this.element.appendChild(this.contentElement);
    skills.forEach((skill) => {
      let block = this.createSkillBlock(skill);
      this.contentElement.appendChild(block);
    });

    let button = document.createElement('button');
    button.classList.add('close-button');
    this.element.appendChild(button);
    button.innerHTML = 'X';
    button.addEventListener('click', () => this.hidden = true);

    if (!disabled) {
      this.skillpointElement = document.createElement('div');
      this.skillpointElement.classList.add('skill-skillpoint');
      this.element.appendChild(this.skillpointElement);
      this.skillpointElement.innerHTML = `5 Skillpoints`;

      this.negative = document.createElement('div');
      this.negative.classList.add('skill-negative');
      this.element.appendChild(this.negative);

      this.skillbar = new SkillBar();
      this.element.appendChild(this.skillbar.element);
    }

    this.hidden = true;

    let buttons: HTMLButtonElement[] = [];
    if (tier > 0) {
      for (let i = 1; i <= tier + 1; i++) {
        let j = i;
        let tierButton = document.createElement('button');
        tierButton.classList.add('tier-button');
        tierButton.innerHTML = `${StringManager.data.UI_SKILLTREE_TIER} ${i}`;
        buttons.push(tierButton);
        this.tierContainer.appendChild(tierButton);
        tierButton.addEventListener('click', () => {
          buttons.forEach(b => b.disabled = b === tierButton);
          this.openTierPage(j);
        });
      }
      buttons[tier].disabled = true;
      this.openTierPage(tier + 1);
    } else {
      this.openTierPage(1);
    }
  }

  public get skillpoints(): number {
    return this.skillLevels - this.skillsSpent;
  }

  public get hidden(): boolean {
    return this.element.style.display === 'none';
  }

  public set hidden(b: boolean) {
    this.element.style.display = b ? 'none' : 'flex';
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
    while (this.leveled.length > 0) this.leveled.shift();
    this.skillsSpent = 0;
    this.skillMap.forEach(data => data.element.disabled = false);
  }

  public updateSkillpoints = (research: number) => {
    if (this.disabled) return;
    let oldpoints = this.skillpoints;
    this.skillLevels = Formula.getNextSkillLevel(research);
    let nextCost = Formula.getSkillCost(this.skillLevels);
    this.skillbar.updateText(research, nextCost);
    this.skillpointElement.innerHTML = `${Math.round(this.skillpoints)} ${StringManager.data.UI_SKILLTREE_SKILLPOINTS}`;
    if (oldpoints !== this.skillpoints) {
      this.updateHighlights();
    }
  }

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
    let element = document.createElement('button');
    element.classList.add('skill-block');
    element.innerHTML = `<div class="skill-block-title">${skill.title}</div>
    <div class="skill-block-content">${skill.description}</div>
    <div class="skill-block-cost">${skill.cost} Point${skill.cost === 1 ? '' : 's'}</div>`;

    if (this.always.includes(skill.slug)) {
      element.disabled = true;
    }

    if (this.leveled.includes(skill.slug)) {
      element.disabled = true;
      this.skillsSpent += skill.cost;
    }

    if (this.disabled) {
      element.classList.add('greyed');
    } else {
      element.addEventListener('click', () => {
        if (skill.cost <= this.skillpoints && !element.classList.contains('greyed')) {
          this.skillsSpent += skill.cost;
          this.leveled.push(skill.slug);
          element.disabled = true;
          this.skillMap.forEach(this.checkRequirements);
          this.updateHighlights();
        }
      });
      this.checkRequirements({element, skill});
    }

    this.skillMap.push({element, skill});

    return element;
  }

  private checkRequirements = (data: { element: HTMLButtonElement, skill: ISkillConfig }) => {
    if (data.skill.skillRequirements) {
      let allGood = true;
      data.skill.skillRequirements.forEach(req => {
        if (!this.leveled.includes(req) && !this.always.includes(req)) {
          allGood = false;
        }
      });

      if (allGood) {
        if (data.element.classList.contains('greyed')) {
          data.element.classList.remove('greyed');
        }
      } else {
        data.element.classList.add('greyed');
      }
    }
  }
}

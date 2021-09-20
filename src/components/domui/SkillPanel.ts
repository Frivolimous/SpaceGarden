import { Config } from "../../Config";
import { SkillConfig } from "../../data/SkillData";
import { SkillBar } from "./SkillBar";

export class SkillPanel {
  private element: HTMLDivElement;
  private contentElement: HTMLDivElement;
  private skillbar: SkillBar;
  private skillpointElement: HTMLDivElement;

  private skillLevels: number = 0;
  private skillsSpent: number = 0;

  constructor(private skills: SkillConfig[], private leveled: number[], private disabled?: boolean) {
    this.element = document.createElement('div');
    this.element.classList.add('skill-panel');
    document.body.appendChild(this.element);
    if (disabled) {
      this.element.innerHTML = `
      <div class="skill-title">Skill Tree</div>
      <div class="skill-subtitle">Currently active skills</div>`;
    } else {
      this.element.innerHTML = `
      <div class="skill-title">Skill Tree</div>
      <div class="skill-subtitle">Your skills will be applied only on the next plant</div>`;
    }
    this.contentElement = document.createElement('div');
    this.contentElement.classList.add('skill-content');
    this.element.appendChild(this.contentElement);
    skills.forEach((skill, i) => {
      let block = this.createSkillBlock(skill, i);
      this.contentElement.appendChild(block);
    })

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
  
      this.skillbar = new SkillBar;
      this.element.appendChild(this.skillbar.element);
    }
  }

  public get skillpoints(): number {
    return this.skillLevels - this.skillsSpent;
  }

  public get hidden(): boolean {
    return this.element.hidden;
  }

  public set hidden(b: boolean) {
    this.element.hidden = b;
  }

  public destroy = () => {
    document.body.removeChild(this.element);
  }

  public updateSkillpoints = (skillpoints: number) => {
    if (this.disabled) return;
    this.skillLevels = Config.SKILL_COST.findIndex(cost => cost > skillpoints);
    let nextCost = Config.SKILL_COST[this.skillLevels];
    this.skillbar.updateText(skillpoints, nextCost);
    this.skillpointElement.innerHTML = `${Math.round(this.skillpoints)} Skillpoints`;
  }

  private createSkillBlock(skill: SkillConfig, i: number): HTMLElement {
    let element = document.createElement('button');
    element.classList.add('skill-block');
    element.innerHTML = `<div class="skill-block-title">${skill.title}</div>
    <div class="skill-block-content">${skill.description}</div>
    <div class="skill-block-cost">${skill.cost} Point${skill.cost === 1 ? '' : 's'}</div>`;
    if (this.leveled.includes(i)) {
      element.disabled = true;
      this.skillsSpent += skill.cost;
    }

    if (this.disabled) {
      element.classList.add('greyed');
    } else {
      element.addEventListener('click', () => {
        console.log('click', skill.cost, this.skillLevels, this.skillsSpent);
        if (skill.cost <= this.skillpoints) {
          this.skillsSpent += skill.cost;
          this.leveled.push(i);
          element.disabled = true;
        } 
      });
    }
    return element;
  }
}
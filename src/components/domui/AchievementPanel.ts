import { StringManager } from '../../services/StringManager';
import { IAchievement } from '../../data/SkillData';
import { SkillBar } from './SkillBar';
import { Formula } from '../../services/Formula';
import { JMEventListener } from 'src/JMGE/events/JMEventListener';
import { AchievementSlug } from '../../data/ATSData';

export class AchievementPanel {
  public element: HTMLDivElement;
  private contentElement: HTMLDivElement;
  private achievements: {slug: AchievementSlug, element: HTMLDivElement}[] = [];

  constructor(states: boolean[], private rawAchievements: IAchievement[]) {
    this.element = document.createElement('div');
    this.element.classList.add('achievement-panel');
    this.element.innerHTML = '<div class="achievement-title">Achievements</div>';
    // document.body.appendChild(this.element);

    this.contentElement = document.createElement('div');
    this.contentElement.classList.add('achievement-content');
    // <div class="node-content">${content}</div>
    this.element.appendChild(this.contentElement);

    rawAchievements.forEach(data => {
      let block = this.createAchievementBlock(data);
      this.contentElement.appendChild(block);

      if (states[data.slug]) {
        this.toggleAchievement(data.slug);
      }
    });

    // let button = document.createElement('button');
    // button.classList.add('close-button');
    // this.element.appendChild(button);
    // button.innerHTML = 'X';
    // button.addEventListener('click', () => this.hidden = true);
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

  // public destroy = () => {
  //   document.body.removeChild(this.element);
  // }

  public toggleAchievement(slug: AchievementSlug, state: boolean = true) {
    let data = this.achievements.find(block => block.slug === slug);

    if (state) {
      data.element.classList.add('highlight');
      let countElement = data.element.getElementsByClassName('achievement-block-cost')[0];
      countElement.innerHTML = '';
      let titleElement = data.element.getElementsByClassName('achievement-block-title')[0];
      titleElement.innerHTML += ' <span style="font-size:0.6em; color:#550;">[ACHIEVED]</span>';
    } else {
      data.element.classList.remove('highlight');
    }
  }

  public updateAchievementCount(slug: AchievementSlug, count: string) {
    let data = this.achievements.find(block => block.slug === slug);

    let countElement = data.element.getElementsByClassName('achievement-block-cost')[0];
    countElement.innerHTML = count;
  }

  private createAchievementBlock(data: IAchievement): HTMLElement {
    let element = document.createElement('div');
    element.classList.add('achievement-block');
    element.innerHTML = `<div class="achievement-block-title">${data.title}</div>
    <div class="achievement-block-content">${data.description}</div>
    <div class="achievement-block-cost"></div>`;

    this.achievements.push({slug: data.slug, element});

    return element;
  }

  // private updateHighlights() {
  //   let sp = this.skillpoints;
  //   this.hasSkillToLevel = false;
  //   this.skillMap.forEach((data, i) => {
  //     if (data.skill.cost <= sp && !data.element.disabled && !data.element.classList.contains('greyed')) {
  //       data.element.classList.add('highlight');
  //       this.hasSkillToLevel = true;
  //     } else {
  //       data.element.classList.remove('highlight');
  //     }
  //   });
  // }
}

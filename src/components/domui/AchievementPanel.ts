import { IAchievement } from '../../data/SkillData';
import { AchievementSlug } from '../../data/ATSData';
import { SidebarElement } from './SidebarElement';
import { DomManager } from '../../JMGE/DomManager';

export class AchievementPanel extends SidebarElement {
  private achievements: {slug: AchievementSlug, element: HTMLDivElement}[] = [];

  constructor(states: boolean[], private rawAchievements: IAchievement[]) {
    super(null, 'achievement-panel', 'achievement-content');
    let title = DomManager.makeDiv('achievement-title');
    title.innerText = 'Achievements';
    this.element.prepend(title);

    rawAchievements.forEach(data => {
      this.createAchievementBlock(data);

      if (states[data.slug]) {
        this.toggleAchievement(data.slug);
      }
    });
  }

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
    let element = DomManager.makeDiv('achievement-block', this.content);
    element.innerHTML = `<div class="achievement-block-title">${data.title}</div>
    <div class="achievement-block-content">${data.description}</div>
    <div class="achievement-block-cost"></div>`;

    this.achievements.push({slug: data.slug, element});

    return element;
  }
}

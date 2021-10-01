import { StringManager } from '../../services/StringManager';

export class SkillBar {
  public element: HTMLDivElement;

  public current: number = 0;
  public nextLevel: number = 100;

  constructor() {
    this.element = document.createElement('div');
    this.element.classList.add('skillbar');
    this.updateText(0, 100);
  }

  public updateText(current: number, next: number) {
    this.current = current;
    this.nextLevel = next;
    // this.element.innerHTML = `${StringManager.data.UI_SKILLTREE_RESEARCH}: ${Math.round(current)} / ${next}`;
    this.element.innerHTML = `${StringManager.data.UI_SKILLTREE_RESEARCH}: ${Math.round(current)} / ${next}
                              <p style="font-size: 16px; margin-block-start: 0px; font-weight: normal;">Next Skillpoint at ${next} Research</p>`;
  }
}

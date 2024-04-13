import { DomManager } from '../../JMGE/DomManager';
import { StringManager } from '../../services/StringManager';

export class SkillBar {
  public element: HTMLDivElement;
  public innerElement: HTMLDivElement;
  public textElement: HTMLDivElement;

  public current: number = 0;
  public nextLevel: number = 100;

  constructor() {
    this.element = DomManager.makeDiv('skillbar');
    this.innerElement = DomManager.makeDiv('innerbar', this.element);
    this.textElement = DomManager.makeDiv('sub-text', this.element);

    this.updateText(0, 100);
  }

  public updateText(current: number, next: number, prev: number = 0) {
    this.current = (current - prev);
    this.nextLevel = (next - prev);
    this.innerElement.style.width = `${Math.round(this.current / this.nextLevel * 100)}%`;
    this.textElement.innerHTML = `${StringManager.data.UI_SKILLTREE_RESEARCH}: ${Math.round(this.current)} / ${this.nextLevel}`;
  }
}

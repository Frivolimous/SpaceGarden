import { StringManager } from '../../services/StringManager';

export class SkillBar {
  public element: HTMLDivElement;
  public innerElement: HTMLDivElement;
  public textElement: HTMLDivElement;

  public current: number = 0;
  public nextLevel: number = 100;

  constructor() {
    this.element = document.createElement('div');
    this.innerElement = document.createElement('div');
    this.textElement = document.createElement('div');
    this.textElement.style.position = 'relative';
    this.textElement.style.top = '-1.5em';

    this.element.classList.add('skillbar');

    let color = '#A380F0';

    this.element.style.width = '30em';
    this.element.style.height = '2em';
    this.element.style.border = 'black solid'
    this.innerElement.style.background = color;
    // this.innerElement.style.border = `color sol`
    this.innerElement.style.height = '100%';
    this.element.appendChild(this.innerElement);
    this.element.appendChild(this.textElement);
    this.updateText(0, 100);
  }

  public updateText(current: number, next: number, prev: number = 0) {
    this.current = (current - prev);
    this.nextLevel = (next - prev);
    this.innerElement.style.width = `${Math.round(this.current / this.nextLevel * 100)}%`;
    this.textElement.innerHTML = `${StringManager.data.UI_SKILLTREE_RESEARCH}: ${Math.round(this.current)} / ${this.nextLevel}`;
  }
}

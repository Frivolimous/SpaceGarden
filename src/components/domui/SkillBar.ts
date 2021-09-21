export class SkillBar {
  public element: HTMLDivElement;

  public current: number = 0;
  public nextLevel: number = 100;

  constructor() {
    this.element = document.createElement('div');
    this.element.classList.add('skillbar');
    this.updateText(0, 100);
  }

  updateText(current: number, next: number) {
    this.current = current;
    this.nextLevel = next;
    this.element.innerHTML = `Research: ${Math.round(current)} / ${next}`;
  }
}
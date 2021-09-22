export class InfoPopup {
  private element: HTMLDivElement;

  constructor(content: string) {
    this.element = document.createElement('div');
    this.element.classList.add('info-popup');
    this.element.innerHTML = content;
    document.body.appendChild(this.element);
    window.setTimeout(this.destroy, 1500);
  }

  public destroy = () => {
    document.body.removeChild(this.element);
  }
}

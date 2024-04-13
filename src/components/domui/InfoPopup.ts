import { DomManager } from '../../JMGE/DomManager';

export class InfoPopup {
  private element: HTMLDivElement;

  constructor(content: string) {
    this.element = DomManager.makeDiv('info-popup', document.body);

    this.element.innerHTML = content;
    window.setTimeout(this.destroy, 1500);
  }

  public destroy = () => {
    document.body.removeChild(this.element);
  }
}

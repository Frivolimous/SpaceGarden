import { DomManager } from '../../JMGE/DomManager';

export class SidebarButton {
  public element: HTMLButtonElement;
  public content: HTMLDivElement;

  private notification: HTMLDivElement;

  constructor(content: string, className: string, onClick: () => void) {
    this.element = DomManager.makeButton(content, className, onClick);
  }

  public get isHidden() {
    return this.element.style.display === 'none';
  }

  public set isHidden(b: boolean) {
    if (b) {
      this.element.style.display = 'none';
    } else {
      this.element.style.removeProperty('display');
    }
  }

  public get disabled() {
    return this.element.disabled;
  }

  public set disabled(b: boolean) {
    this.element.disabled = b;
  }

  public notify(b?: boolean) {
    if (!this.notification) {
      this.notification = DomManager.makeDiv('notification',this.element);
    }

    this.notification.hidden = !b;
  }
}

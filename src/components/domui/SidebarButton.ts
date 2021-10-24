export class SidebarButton {
  public element: HTMLButtonElement;
  public content: HTMLDivElement;

  private notification: HTMLDivElement;

  constructor(content: string, className: string, onClick: () => void) {
    this.element = document.createElement('button');
    this.element.classList.add(className);
    this.element.innerHTML = content;
    this.element.addEventListener('click', onClick);
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
      this.notification = document.createElement('div');
      this.notification.classList.add('notification');
      this.element.appendChild(this.notification);
    }

    this.notification.hidden = !b;
  }
}

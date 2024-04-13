import { DomManager } from '../../JMGE/DomManager';

export class Timer {
  private element: HTMLDivElement;

  constructor() {
    this.element = DomManager.makeDiv('timer', document.body);
  }

  public setTime(time: number) {
    let seconds = Math.floor(time / 1000);
    let minutes = Math.floor(seconds / 60);
    seconds -= minutes * 60;
    let hours = Math.floor(minutes / 60);
    minutes -= hours * 60;
    this.element.innerHTML = `${hours}:${minutes < 10 ? 0 : ""}${minutes}:${seconds < 10 ? 0 : ""}${seconds}`;
  }

  public destroy = () => {
    document.body.removeChild(this.element);
  }
}

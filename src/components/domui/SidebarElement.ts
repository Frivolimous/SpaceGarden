import { DomManager } from '../../JMGE/DomManager';
import { CrawlerModel } from '../../engine/Mechanics/Parts/CrawlerModel';
import { PlantNode } from '../../engine/nodes/PlantNode';
import { SidebarButton } from './SidebarButton';

export class SidebarElement {
  public element: HTMLDivElement;
  public content: HTMLDivElement;
  public button: SidebarButton;

  public order: string;

  constructor(public source?: PlantNode | CrawlerModel, mainClass: string = 'node-block', contentClass: string = 'node-content') {
    this.element = DomManager.makeDiv(mainClass);
    this.content = DomManager.makeDiv(contentClass, this.element);
    this.content.innerHTML = source ? source.toString() : '';

    if (source) this.setOrder(source.slug);
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

  public highlight(b: boolean, andNode?: boolean) {
    if (b) {
      this.element.classList.add('highlight');
      if (this.source && andNode) {
        this.source.view.highlight = true;
      }
    } else {
      this.element.classList.remove('highlight');
      if (this.source) {
        this.source.view.highlight = false;
      }
    }
  }

  public addButton(content: string, onClick: () => void) {
    this.button = new SidebarButton(content, 'skill-button', onClick);
    this.element.appendChild(this.button.element);
  }

  public removeButton() {
    if (this.button) {
      this.element.removeChild(this.button.element);
      this.button = null;
    }
  }

  public updateFromSource() {
    this.content.innerHTML = this.source.toString();
  }

  private setOrder(slug: string) {
    let order: string;
    switch (slug) {
      case 'core': case 'chieftain': order = '1'; break;
      case 'seedling': case 'shaman': order = '2'; break;
      case 'hub': order = '3'; break;
      default: order = '5'; break;
    }

    this.order = order;
    this.element.style.order = order;
  }
}

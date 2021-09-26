import { CrawlerModel } from '../../engine/Mechanics/Parts/CrawlerModel';
import { PlantNode } from '../../engine/nodes/PlantNode';
import { StringManager } from '../../services/StringManager';
import { SkillPanel } from './SkillPanel';

type SidebarElement = PlantNode | CrawlerModel;

export class Sidebar {
  private static aid: number = 0;

  private static genAid(): string {
    Sidebar.aid++;

    return `id-${Sidebar.aid}`;
  }

  public knowledgeElement: HTMLDivElement;

  private nodeMap: { element: HTMLDivElement, node: SidebarElement, atStart?: boolean }[] = [];
  private currentHighlight: { element: HTMLDivElement, node: SidebarElement};
  private hideStemButton: HTMLButtonElement;
  private showCrawlersButton: HTMLButtonElement;

  private container: HTMLDivElement;
  private notification: HTMLElement;

  private _AreStemsHidden: boolean;
  private _IsCrawlerMode: boolean;

  constructor(private currentSkillPanel: SkillPanel, private nextSkillPanel: SkillPanel) {
    this.container = document.getElementById('node-container') as HTMLDivElement;

    this.hideStemButton = document.createElement('button');
    this.hideStemButton.classList.add('skill-button');
    this.hideStemButton.innerHTML = StringManager.data.UI_SIDEBAR_HIDE_STEMS;
    this.hideStemButton.style.position = 'absolute';
    this.hideStemButton.style.right = '5px';
    this.hideStemButton.style.top = '0px';
    this.hideStemButton.addEventListener('click', () => {
      this.areStemsHidden = !this.areStemsHidden;
      if (this.areStemsHidden) {
        this.hideStemButton.innerHTML = StringManager.data.UI_SIDEBAR_SHOW_STEMS;
      } else {
        this.hideStemButton.innerHTML = StringManager.data.UI_SIDEBAR_HIDE_STEMS;
      }
    });
  }

  private get areStemsHidden(): boolean {
    return this._AreStemsHidden;
  }

  private set areStemsHidden(b: boolean) {
    this._AreStemsHidden = b;
    if (b) {
      this.nodeMap.forEach(data => {
        if (data.node.slug === 'stem') {
          data.element.style.display = 'none';
        }
      });
    } else {
      this.nodeMap.forEach(data => {
        data.element.style.removeProperty('display');
      });
    }
  }

  private get isCrawlerMode(): boolean {
    return this._IsCrawlerMode;
  }

  private set isCrawlerMode(b: boolean) {
    this._IsCrawlerMode = b;
    if (b) {
      this.nodeMap.forEach(data => {
        if (data.node.slug === 'crawler') {
          data.element.style.removeProperty('display');
        } else {
          data.element.style.display = 'none';
        }
      });
    } else {
      this.nodeMap.forEach(data => {
        if (data.node.slug === 'crawler') {
          data.element.style.display = 'none';
        } else {
          data.element.style.removeProperty('display');
        }
      });
    }
  }

  public destroy() {
    this.currentSkillPanel && this.currentSkillPanel.destroy();
    this.nextSkillPanel.destroy();
  }

  public navIn() {
    this.container.innerHTML = '';
    this.container.appendChild(this.hideStemButton);
    this.showCrawlersButton && this.container.appendChild(this.showCrawlersButton);

    this.nodeMap.forEach(data => {
      if (data.atStart) {
        this.container.prepend(data.element);
      } else {
        this.container.appendChild(data.element);
      }
    });
  }

  public navOut() {
    this.container.innerHTML = '';
  }

  public addKnowledgeElement(content: string) {
    if (this.knowledgeElement) return;

    let element = this.addElement(content, true);

    this.knowledgeElement = element;
  }

  public removeKnowledgeElement() {
    if (this.knowledgeElement) {
      this.container.removeChild(this.knowledgeElement);
      this.knowledgeElement = null;
    }
  }

  public updateKnowledge(content: string) {
    if (this.knowledgeElement) {
      let contentElement = this.knowledgeElement.querySelector('.node-content');
      contentElement.innerHTML = content;
    }
  }

  public addNodeElement = (node: SidebarElement) => {
    if (node.slug === 'crawler') this.addShowCrawlersButton();
    if (!node.isFruit()) {
      let atStart = (node.slug === 'seedling' || node.slug === 'crawler');
      let element = this.addElement(node.toString(), atStart);
      element.addEventListener('pointerover', () => {
        this.highlightNode(node, true);
      });

      element.addEventListener('pointerout', () => {
        this.highlightNode(null);
      });

      this.nodeMap.push({ element, node, atStart });

      if (node.slug === 'seedling') {
        let button = document.createElement('button');
        button.classList.add('skill-button');
        button.innerHTML = StringManager.data.UI_SIDEBAR_SKILLTREE_NEXT;
        button.addEventListener('click', () => {
          this.nextSkillPanel.hidden = false;
        });
        element.appendChild(button);

        this.notification = document.createElement('div');
        this.notification.classList.add('notification');
        element.appendChild(this.notification);
        this.notification.hidden = true;
      } else if (node.slug === 'stem') {
        if (this.areStemsHidden) {
          element.style.display = 'none';
        }
      }
      if (this.isCrawlerMode) {
        if (node.slug !== 'crawler') {
          element.style.display = 'none';
        }
      } else {
        if (node.slug === 'crawler') {
          element.style.display = 'none';
        }
      }

      if (node.slug === 'core' && this.currentSkillPanel) {
        let button = document.createElement('button');
        button.classList.add('skill-button');
        button.innerHTML = StringManager.data.UI_SIDEBAR_SKILLTREE_CURRENT;
        button.addEventListener('click', () => {
          this.currentSkillPanel.hidden = false;
        });
        element.appendChild(button);
      }
    }
  }

  public removeNodeElement = (node: SidebarElement) => {
    if (!node.isFruit()) {
      if (node.slug === 'seedling') {
        this.nextSkillPanel.clear();
      }
      let index = this.nodeMap.findIndex(map => map.node === node);
      this.container.removeChild(this.nodeMap[index].element);
      this.nodeMap.splice(index, 1);
    }
  }

  public updateNodes() {
    this.nodeMap.forEach(data => {
      let contentElement = data.element.querySelector('.node-content');
      contentElement.innerHTML = data.node.toString();
      if (data.node.slug === 'seedling') {
        this.nextSkillPanel.updateSkillpoints(data.node.power.researchCurrent);
        this.notification.hidden = (this.nextSkillPanel.skillpoints === 0 || !this.nextSkillPanel.hidden);
      }
    });
  }

  public highlightNode(node: SidebarElement, andHighlightNode?: boolean) {
    if (this.currentHighlight) {
      this.currentHighlight.element.classList.remove('highlight');
      this.currentHighlight.node.view.highlight = false;
      this.currentHighlight = null;
    }
    if (node) {
      let map = this.nodeMap.find(data => data.node === node);
      if (map) {
        map.element.classList.add('highlight');
        if (andHighlightNode) {
          node.view.highlight = true;
        }
        this.currentHighlight = map;
      }
    }
  }

  private addShowCrawlersButton() {
    if (this.showCrawlersButton) return;

    this.showCrawlersButton = document.createElement('button');
    this.showCrawlersButton.classList.add('skill-button');
    this.showCrawlersButton.innerHTML = StringManager.data.UI_SIDEBAR_CRAWLER_MODE;
    this.showCrawlersButton.style.position = 'absolute';
    this.showCrawlersButton.style.left = '5px';
    this.showCrawlersButton.style.top = '0px';
    this.showCrawlersButton.addEventListener('click', () => {
      this.isCrawlerMode = !this.isCrawlerMode;
      if (this.isCrawlerMode) {
        this.showCrawlersButton.innerHTML = StringManager.data.UI_SIDEBAR_NODE_MODE;
      } else {
        this.showCrawlersButton.innerHTML = StringManager.data.UI_SIDEBAR_CRAWLER_MODE;
      }
    });
    this.container.appendChild(this.showCrawlersButton);
  }

  private addElement(content: string, start: boolean): HTMLDivElement {
    let element = document.createElement('div');
    element.innerHTML = `<div class="node-content">${content}</div>`;
    element.classList.add('node-block');
    element.id = Sidebar.genAid();
    if (start) {
      this.container.prepend(element);
    } else {
      this.container.appendChild(element);
    }
    return element;
  }
}

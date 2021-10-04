import { GameKnowledge } from 'src/engine/Mechanics/GameKnowledge';
import { CrawlerModel } from '../../engine/Mechanics/Parts/CrawlerModel';
import { PlantNode } from '../../engine/nodes/PlantNode';
import { StringManager } from '../../services/StringManager';
import { SkillPanel } from './SkillPanel';

type SidebarElement = PlantNode | CrawlerModel;
type SidebarTab = 'node' | 'crawler' | 'knowledge' | 'none';

export class Sidebar {
  private static aid: number = 0;

  private static genAid(): string {
    Sidebar.aid++;

    return `id-${Sidebar.aid}`;
  }

  private knowledgeElement: HTMLDivElement;
  private knowledgeContent: HTMLDivElement;

  private nodeMap: { element: HTMLDivElement, node: SidebarElement, atStart?: boolean }[] = [];
  private currentHighlight: { element: HTMLDivElement, node: SidebarElement};
  private hideStemButton: HTMLButtonElement;

  private knowledgeButton: HTMLButtonElement;
  private nodeButton: HTMLButtonElement;
  private crawlerButton: HTMLButtonElement;

  private sidebar: HTMLDivElement;
  private tabContainer: HTMLDivElement;
  private container: HTMLDivElement;
  private notification: HTMLElement;

  private _AreStemsHidden: boolean = false;
  private currentTab: SidebarTab = 'node';

  constructor(private currentSkillPanel: SkillPanel, private nextSkillPanel: SkillPanel) {
    this.sidebar = document.getElementById('sidebar') as HTMLDivElement;
    this.sidebar.innerHTML = `
    <div class="tab-container" id="tab-container"></div>
    <hr width=80% color=black>
      <div class="node-container" id="node-container"></div>
    `;
    this.tabContainer = document.getElementById('tab-container') as HTMLDivElement;
    this.container = document.getElementById('node-container') as HTMLDivElement;

    this.knowledgeButton = this.makeButton('Overview', 'tab-button', () => this.setCurrentTab('knowledge'));
    this.tabContainer.appendChild(this.knowledgeButton);
    this.nodeButton = this.makeButton('Nodes', 'tab-button', () => this.setCurrentTab('node'));
    this.tabContainer.appendChild(this.nodeButton);
    this.crawlerButton = this.makeButton('Crawlers', 'tab-button', () => this.setCurrentTab('crawler'));
    this.tabContainer.appendChild(this.crawlerButton);
    this.hideElement(this.crawlerButton);

    this.addKnowledgeElement('');

    // this.hideStemButton = this.makeButton(StringManager.data.UI_SIDEBAR_HIDE_STEMS, 'skill-button', () => {
    //   this.areStemsHidden = !this.areStemsHidden;
    //   if (this.areStemsHidden) {
    //     this.hideStemButton.innerHTML = StringManager.data.UI_SIDEBAR_SHOW_STEMS;
    //   } else {
    //     this.hideStemButton.innerHTML = StringManager.data.UI_SIDEBAR_HIDE_STEMS;
    //   }
    // });
    // this.hideStemButton.style.position = 'absolute';
    // this.hideStemButton.style.right = '5px';
    // this.hideStemButton.style.top = '0px';
  }

  private get areStemsHidden(): boolean {
    return this._AreStemsHidden;
  }

  private set areStemsHidden(b: boolean) {
    this._AreStemsHidden = b;
    this.setCurrentTab(this.currentTab);
  }

  public destroy() {
    this.currentSkillPanel && this.currentSkillPanel.destroy();
    this.nextSkillPanel.destroy();
  }

  public navIn() {
    this.container.innerHTML = '';

    this.nodeMap.forEach(data => {
      if (data.atStart) {
        this.container.prepend(data.element);
      } else {
        this.container.appendChild(data.element);
      }
    });
    this.container.prepend(this.knowledgeElement);
    this.setCurrentTab('node');
  }

  public navOut() {
    this.container.innerHTML = '';
    this.knowledgeElement = null;
  }

  public setCurrentTab(tab: SidebarTab) {
    console.log('tab', tab);
    this.currentTab = tab;

    if (tab === 'crawler') {
      this.crawlerButton.disabled = true;
      this.nodeButton.disabled = false;
      this.knowledgeButton.disabled = false;
      this.nodeMap.forEach(data => this.hideElement(data.element, data.node.slug !== 'crawler'));
      this.hideElement(this.knowledgeElement);
    } else if (tab === 'node') {
      this.crawlerButton.disabled = false;
      this.nodeButton.disabled = true;
      this.knowledgeButton.disabled = false;
      this.nodeMap.forEach(data => this.hideElement(data.element, data.node.slug === 'crawler' || (this.areStemsHidden && data.node.slug === 'stem')));
      this.hideElement(this.knowledgeElement);
    } else if (tab === 'knowledge') {
      this.crawlerButton.disabled = false;
      this.nodeButton.disabled = false;
      this.knowledgeButton.disabled = true;
      this.nodeMap.forEach(data => this.hideElement(data.element, true));
      this.hideElement(this.knowledgeElement, false);
    } else if (tab === 'none') {
      this.nodeMap.forEach(data => this.hideElement(data.element, true));
      this.hideElement(this.knowledgeElement);
    }
  }

  public updateKnowledge(knowledge: GameKnowledge) {
    if (this.currentTab === 'knowledge') {
      this.knowledgeContent.innerHTML = knowledge.toString();
    }
  }

  public addNodeElement = (node: SidebarElement) => {
    if (node.slug === 'crawler' && this.isElementHidden(this.crawlerButton)) {
      this.hideElement(this.crawlerButton, false);
      this.setCurrentTab('crawler');
    }
    if (!node.isFruit()) {
      let atStart = (node.slug === 'seedling');
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
        button.appendChild(this.notification);
        this.notification.hidden = true;
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

      this.setCurrentTab(this.currentTab);
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
        this.notification.hidden = !this.nextSkillPanel.hidden || !this.nextSkillPanel.hasSkillToLevel;
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

  private addKnowledgeElement(content: string) {
    if (this.knowledgeElement) return;

    this.knowledgeElement = this.addElement(content, true);
    this.knowledgeContent = this.knowledgeElement.querySelector('.node-content');
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

  private makeButton(innerHTML: string, className: string, onClick: () => void): HTMLButtonElement {
    let button = document.createElement('button');
    button.classList.add(className);
    button.innerHTML = innerHTML;
    button.addEventListener('click', onClick);

    return button;
  }

  private hideElement(element: HTMLElement, hidden: boolean = true) {
    if (hidden) {
      element.style.display = 'none';
    } else {
      element.style.removeProperty('display');
    }
  }

  private isElementHidden(element: HTMLElement): boolean {
    return element.style.display === 'none';
  }
}

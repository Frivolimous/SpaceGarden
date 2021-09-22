import { FDGNode } from '../../engine/FDG/FDGNode';
import { GameNode } from '../../engine/Mechanics/Parts/GameNode';
import { SkillPanel } from './SkillPanel';

export class Sidebar {
  public static genAid(): string {
    Sidebar.aid++;

    return `id-${Sidebar.aid}`;
  }

  private static aid: number = 0;

  public nodeMap: { element: HTMLDivElement, node: GameNode }[] = [];
  public currentHighlight: HTMLDivElement;

  public container: HTMLDivElement;
  // currentSkillPanel: SkillPanel;
  // nextSkillPanel: SkillPanel;
  public notification: HTMLElement;

  public _AreStemsHidden: boolean;

  constructor(private currentSkillPanel: SkillPanel, private nextSkillPanel: SkillPanel) {
    this.container = document.getElementById('node-container') as HTMLDivElement;
    this.container.innerHTML = '';
    let hideStemButton = document.createElement('button');
    hideStemButton.classList.add('skill-button');
    hideStemButton.innerHTML = 'Hide Stems';
    hideStemButton.style.position = 'absolute';
    hideStemButton.style.right = '5px';
    hideStemButton.style.top = '0px';
    hideStemButton.addEventListener('click', () => {
      this.areStemsHidden = !this.areStemsHidden;
      if (this.areStemsHidden) {
        hideStemButton.innerHTML = 'Show Stems';
      } else {
        hideStemButton.innerHTML = 'Hide Stems';
      }
    });
    this.container.appendChild(hideStemButton);
  }

  public get areStemsHidden(): boolean {
    return this._AreStemsHidden;
  }

  public set areStemsHidden(b: boolean) {
    this._AreStemsHidden = b;
    if (b) {
      this.nodeMap.forEach(data => {
        if (data.node.config.slug === 'stem') {
          data.element.style.display = 'none';
        }
      });
    } else {
      this.nodeMap.forEach(data => {
        data.element.style.removeProperty('display');
      });
    }
  }

  public addNodeElement = (view: FDGNode) => {
    if (view.config.type !== 'fruit') {
      let node = view.data;
      let element = this.addElement(node.toString(), view.config.slug === 'seedling');
      this.nodeMap.push({ element, node });

      if (view.config.slug === 'seedling') {
        let button = document.createElement('button');
        button.classList.add('skill-button');
        button.innerHTML = 'Next Plant';
        button.addEventListener('click', () => {
          this.nextSkillPanel.hidden = false;
        });
        element.appendChild(button);

        this.notification = document.createElement('div');
        this.notification.classList.add('notification');
        element.appendChild(this.notification);
        this.notification.hidden = true;
      } else if (view.config.slug === 'stem') {
        if (this.areStemsHidden) {
          element.style.display = 'none';
        }
      }

      if (view.config.slug === 'core' && this.currentSkillPanel) {
        let button = document.createElement('button');
        button.classList.add('skill-button');
        button.innerHTML = 'Current Skills';
        button.addEventListener('click', () => {
          this.currentSkillPanel.hidden = false;
        });
        element.appendChild(button);
      }
    }
  }

  public removeNodeElement = (view: FDGNode) => {
    if (view.config.type !== 'fruit') {
      if (view.config.slug === 'seedling') {
        this.nextSkillPanel.clear();
      }
      let node = view.data;
      let index = this.nodeMap.findIndex(map => map.node === node);
      this.container.removeChild(this.nodeMap[index].element);
      this.nodeMap.splice(index, 1);
    }
  }

  public updateNodes() {
    this.nodeMap.forEach(data => {
      let contentElement = data.element.querySelector('.node-content');
      contentElement.innerHTML = data.node.toString();
      if (data.node.config.slug === 'seedling') {
        this.nextSkillPanel.updateSkillpoints(data.node.researchCurrent);
        this.notification.hidden = (this.nextSkillPanel.skillpoints === 0 || !this.nextSkillPanel.hidden);
      }
    });
  }

  public addElement(content: string, start: boolean): HTMLDivElement {
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

  public destroy() {
    this.container.innerHTML = '';
    this.currentSkillPanel.destroy();
    this.nextSkillPanel.destroy();
  }

  public highlightNode(node: FDGNode) {
    if (this.currentHighlight) {
      this.currentHighlight.classList.remove('highlight');
      this.currentHighlight = null;
    }
    if (node) {
      let map = this.nodeMap.find(data => data.node.view === node);
      map.element.classList.add('highlight');
      this.currentHighlight = map.element;
    }
  }
}

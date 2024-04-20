import { Config } from '../../Config';
import { AchievementSlug } from '../../data/ATSData';
import { GameKnowledge } from '../../engine/Mechanics/GameKnowledge';
import { CrawlerModel } from '../../engine/Mechanics/Parts/CrawlerModel';
import { PlantNode } from '../../engine/nodes/PlantNode';
import { StringManager } from '../../services/StringManager';
import { AchievementPanel } from './AchievementPanel';
import { HubPanel } from './HubPanel';
import { SidebarButton } from './SidebarButton';
import { SidebarElement } from './SidebarElement';
import { SkillPanel } from './SkillPanel';

type SidebarSource = PlantNode | CrawlerModel;
type SidebarTab = 'node' | 'crawler' | 'knowledge' | 'none';

export class Sidebar {
  private knowledgeElement: SidebarElement;

  private nodeMap: SidebarElement[] = [];
  private seedlingElement: SidebarElement;
  private hubElement: SidebarElement;
  private currentHighlight: SidebarElement;
  private hideStemButton: SidebarButton;

  private knowledgeButton: SidebarButton;
  private nodeButton: SidebarButton;
  private crawlerButton: SidebarButton;

  private sidebar: HTMLDivElement;
  private tabContainer: HTMLDivElement;
  private container: HTMLDivElement;

  private _AreStemsHidden: boolean = false;
  private currentTab: SidebarTab = 'node';

  constructor(private currentSkillPanel: SkillPanel, private nextSkillPanel: SkillPanel, private hubPanel: HubPanel, private achieveElement: AchievementPanel) {
    this.sidebar = document.getElementById('sidebar') as HTMLDivElement;
    this.sidebar.innerHTML = `
    <div class="tab-container" id="tab-container"></div>
    <hr width=80% color=black>
      <div class="node-container" id="node-container"></div>
    `;
    this.tabContainer = document.getElementById('tab-container') as HTMLDivElement;
    this.container = document.getElementById('node-container') as HTMLDivElement;

    this.knowledgeButton = new SidebarButton('Overview', 'tab-button', () => this.setCurrentTab('knowledge'));
    this.tabContainer.appendChild(this.knowledgeButton.element);
    this.nodeButton = new SidebarButton('Nodes', 'tab-button', () => this.setCurrentTab('node'));
    this.tabContainer.appendChild(this.nodeButton.element);
    this.crawlerButton = new SidebarButton('Crawlers', 'tab-button', () => this.setCurrentTab('crawler'));
    this.tabContainer.appendChild(this.crawlerButton.element);
    this.crawlerButton.isHidden = true;
    this.knowledgeElement = new SidebarElement();

    // this.hideStemButton = new SidebarButton(StringManager.data.UI_SIDEBAR_HIDE_STEMS, 'skill-button', () => {
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
    this.hubPanel && this.hubPanel.destroy();
  }

  public navIn() {
    this.container.innerHTML = '';

    this.nodeMap.forEach(data => {
      this.container.appendChild(data.element);
    });
    this.container.prepend(this.knowledgeElement.element);
    this.container.appendChild(this.achieveElement.element);
    this.setCurrentTab('node');
  }

  public navOut() {
    this.container.innerHTML = '';
    this.knowledgeElement = null;
  }

  public setCurrentTab(tab: SidebarTab) {
    this.currentTab = tab;

    if (tab === 'crawler') {
      this.crawlerButton.disabled = true;
      this.nodeButton.disabled = false;
      this.knowledgeButton.disabled = false;
      this.nodeMap.forEach(data => data.isHidden = data.source.type !== 'crawler');
      this.knowledgeElement.isHidden = true;
      this.achieveElement.isHidden = true;
    } else if (tab === 'node') {
      this.crawlerButton.disabled = false;
      this.nodeButton.disabled = true;
      this.knowledgeButton.disabled = false;
      this.nodeMap.forEach(data => data.isHidden = data.source.type === 'crawler' || (this.areStemsHidden && data.source.slug === 'stem'));
      this.knowledgeElement.isHidden = true;
      this.achieveElement.isHidden = true;
    } else if (tab === 'knowledge') {
      this.crawlerButton.disabled = false;
      this.nodeButton.disabled = false;
      this.knowledgeButton.disabled = true;
      this.nodeMap.forEach(data => data.isHidden = true);
      this.knowledgeElement.isHidden = false;
      this.achieveElement.isHidden = false;
    } else if (tab === 'none') {
      this.nodeMap.forEach(data => data.isHidden = true);
      this.knowledgeElement.isHidden = true;
      this.achieveElement.isHidden = true;
    }
  }

  public updateKnowledge(knowledge: GameKnowledge) {
    if (this.currentTab === 'knowledge') {
      this.knowledgeElement.content.innerHTML = knowledge.toString();
    }
  }

  public updateAchievement = (e: {slug: AchievementSlug, unlocked?: boolean, count?: string}) => {
    if (e.unlocked) {
      this.achieveElement.toggleAchievement(e.slug, true);
    } else {
      this.achieveElement.updateAchievementCount(e.slug, e.count);
    }
  }

  public addNodeElement = (node: SidebarSource) => {
    if (node.isFruit()) return;

    let element = new SidebarElement(node);
    this.container.appendChild(element.element);

    element.element.addEventListener('pointerover', () => {
      this.highlightNode(node, true);
    });

    element.element.addEventListener('pointerout', () => {
      this.highlightNode(null);
    });

    this.nodeMap.push(element);

    if (node.slug === 'seedling') {
      element.addButton(StringManager.data.UI_SIDEBAR_SKILLTREE_NEXT, () => {
        this.nextSkillPanel.hidden = !this.nextSkillPanel.hidden;
        this.currentSkillPanel && (this.currentSkillPanel.hidden = true);
        this.hubPanel && (this.hubPanel.hidden = true);
      });
      this.seedlingElement = element;
    } else if (node.slug === 'core' && this.currentSkillPanel) {
      element.addButton(StringManager.data.UI_SIDEBAR_SKILLTREE_CURRENT, () => {
        this.currentSkillPanel.hidden = !this.currentSkillPanel.hidden;
        this.nextSkillPanel.hidden = true;
        this.hubPanel.hidden = true;
      });
    } else if (node.slug === 'hub') {
      element.addButton(StringManager.data.UI_SIDEBAR_HUB, () => {
        this.hubPanel.hidden = !this.hubPanel.hidden;
        this.currentSkillPanel.hidden = true;
        this.nextSkillPanel.hidden = true;
        this.hubPanel.toggleCollection('research', node.power.canReceiveResearch);
        this.hubPanel.toggleCollection('fruit', node.power.canReceiveFruit);
        this.hubPanel.toggleCollection('power', node.power.canStorePower);
      });
      this.hubElement = element;
    } else if (node.slug === 'crawler' || node.slug === 'chieftain' || node.slug === 'shaman') {
      // element.addButton('Execute', () => {
      //   node.toExecute = !node.toExecute;
      //   element.button.text = node.toExecute ? 'Let it Live' : 'Execute';
      // });
    }

    if (node.type === 'crawler' && this.crawlerButton.isHidden) {
      this.crawlerButton.isHidden = false;
      this.setCurrentTab('crawler');
    }

    this.setCurrentTab(this.currentTab);
  }

  public removeNodeElement = (node: SidebarSource) => {
    if (!node.isFruit()) {
      if (node.slug === 'seedling') {
        this.nextSkillPanel.clear();
        this.seedlingElement = null;
      } else if (node.slug === 'hub') {
        this.hubElement = null;
        this.hubPanel.hidden = true;
        this.hubPanel.clear();
      }
      let index = this.nodeMap.findIndex(data => data.source === node);
      this.container.removeChild(this.nodeMap[index].element);
      this.nodeMap.splice(index, 1);
    }
  }

  public updateNodes() {
    this.nodeMap.forEach(data => data.updateFromSource());

    if (this.seedlingElement) {
      this.nextSkillPanel.updateSkillpoints((this.seedlingElement.source as PlantNode).power.researchCurrent);
      this.seedlingElement.button.notify(this.nextSkillPanel.hidden && this.nextSkillPanel.hasSkillToLevel);
    }

    if (this.hubElement) {
      let plantElement = this.hubElement.source as PlantNode;
      this.hubPanel.updateCurrencies(plantElement.power.researchCurrent, plantElement.power.fruitCurrent, plantElement.power.storedPowerCurrent, plantElement.power.buffCurrent);
      this.hubElement.button.notify(this.hubPanel.hidden && this.hubPanel.hasSkillToLevel);
      this.hubPanel.toggleToggleButtons(Boolean(Config.NODE.HUB_BUTTONS));
    }
  }

  public highlightNode(node: SidebarSource, andHighlightNode?: boolean) {
    if (this.currentHighlight) {
      this.currentHighlight.highlight(false);
      this.currentHighlight = null;
    }
    if (node) {
      let data = this.nodeMap.find(data2 => data2.source === node);
      if (data) {
        data.highlight(true, andHighlightNode);
        this.currentHighlight = data;
      }
    }
  }
}

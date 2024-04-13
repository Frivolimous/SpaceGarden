import { DomManager } from '../../JMGE/DomManager';
import { HubCostType, IHubConfig } from '../../data/SkillData';

export class HubPanel {
  public skillsSpent: number = 0;
  public hasSkillToLevel: boolean = false;

  private element: HTMLDivElement;
  private contentElement: HTMLDivElement;
  private skillpointElement: HTMLDivElement;
  private skillMap: { element: HTMLButtonElement, skill: IHubConfig }[] = [];

  private research: number = 0;
  private fruit: number = 0;
  private power: number = 0;

  private toggleCollectionButtons: HTMLButtonElement[] = [];

  constructor(private skills: IHubConfig[], private leveled: [string, number][], private onHubLevel: (hub: IHubConfig) => void, private onToggleCollection: (type: HubCostType, state: boolean) => void) {
    this.element = DomManager.makeDiv('skill-panel', document.body);
    let top = DomManager.makeDiv('top', this.element);

    top.innerHTML = `
    <div class="skill-title">Evolution Hub</div>
    <div class="skill-subtitle">Improvements made here will only affect the current plant.</div>`;
    
    this.contentElement = DomManager.makeDiv('skill-content', top);

    skills.forEach((skill) => {
      let block = this.createSkillBlock(skill);
      this.contentElement.appendChild(block);
    });

    DomManager.makeButton('X', 'close-button', e => this.hidden = true, this.element);
    let skillElement = DomManager.makeDiv(null, this.element);
    this.skillpointElement = DomManager.makeDiv('skill-skillpoint', skillElement);
    this.skillpointElement.innerHTML = `5 Skillpoints`;

    let buttonContainer = DomManager.makeDiv(null, this.element);

    this.toggleCollectionButtons.push(DomManager.makeButton('Research Collection', 'skill-button', () => this.toggleCollection('research'), buttonContainer))
    this.toggleCollectionButtons.push(DomManager.makeButton('Fruit Collection', 'skill-button', () => this.toggleCollection('fruit'), buttonContainer))
    this.toggleCollectionButtons.push(DomManager.makeButton('Power Collection', 'skill-button', () => this.toggleCollection('power'), buttonContainer))

    this.hidden = true;
  }

  public get hidden(): boolean {
    return this.element.style.display === 'none';
  }

  public set hidden(b: boolean) {
    if (b) {
      this.element.style.display = 'none';
    } else {
      this.element.style.removeProperty('display');
    }
  }

  public destroy = () => {
    document.body.removeChild(this.element);
  }

  public clear() {
  }

  public updateCurrencies = (research: number, fruit: number, power: number) => {
    this.research = research;
    this.fruit = fruit;
    this.power = power;

    this.skillpointElement.innerHTML = `${Math.round(research)} Research, ${Math.round(fruit)} Fruits, ${Math.round(power)} Power.`;
    this.updateHighlights();
  }

  private updateHighlights() {
    this.hasSkillToLevel = false;

    this.skillMap.forEach((data, i) => {
      let levelPair = this.leveled.find(el => el[0] === data.skill.slug);
      let level = levelPair ? levelPair[1] : 0;

      let currentAmount = data.skill.costType === 'research' ? this.research : data.skill.costType === 'fruit' ? this.fruit : this.power;

      if (data.skill.costs[level] <= currentAmount) {
        data.element.classList.add('highlight');
        this.hasSkillToLevel = true;
      } else {
        data.element.classList.remove('highlight');
      }
    });
  }

  private createSkillBlock(skill: IHubConfig): HTMLElement {
    let element = document.createElement('button');
    element.classList.add('skill-block');
    this.refreshButton(element, skill);
  
    element.addEventListener('click', () => {
      let currentAmount = skill.costType === 'research' ? this.research : skill.costType === 'fruit' ? this.fruit : this.power;
      let levelPair = this.leveled.find(el => el[0] === skill.slug);
      let level = levelPair ? levelPair[1] : 0;

      if (skill.costs[level] <= currentAmount) {
        this.onHubLevel(skill);
        this.refreshButton(element, skill);
      }
    });

    this.skillMap.push({element, skill});

    return element;
  }

  private refreshButton(element: HTMLButtonElement, skill: IHubConfig) {
    let levelPair = this.leveled.find(el => el[0] === skill.slug);
    let level = levelPair ? levelPair[1] : 0;

    element.innerHTML = '';
    let skillName = DomManager.makeDiv('skill-block-title', element);
    skillName.innerHTML = `${skill.effect.slug} ${skill.effect.key}: ${skill.effect.valueType === 'multiplicative' ? 'x' : '+'}${skill.effect.value}`;
    let levelElement = DomManager.makeDiv('skill-block-content', element);

    if (level >= skill.costs.length) {
      levelElement.innerHTML = 'Max Level';
      element.disabled = true;
    } else {
      levelElement.innerHTML = `Level ${level}`;
      let costElement = DomManager.makeDiv('skill-block-cost', element);
      costElement.innerHTML = `${skill.costs[level]} ${skill.costType}`;
    }
  }

  public toggleToggleButtons(state: boolean) {
    this.toggleCollectionButtons.forEach(button => {
      button.hidden = !state;
    })
  }

  public toggleCollection(type: HubCostType, state ?: boolean) {
    let index = type === 'research' ? 0 : type === 'fruit' ? 1 : 2;

    if (state === undefined) {
      state = Boolean(this.toggleCollectionButtons[index].value);
    }

    this.toggleCollectionButtons[index].value = state ? "" : "TRUE";

    if (state) this.toggleCollectionButtons[index].classList.remove('falsed');
    else this.toggleCollectionButtons[index].classList.add('falsed');

    this.onToggleCollection(type, !this.toggleCollectionButtons[index].value);
  }
}

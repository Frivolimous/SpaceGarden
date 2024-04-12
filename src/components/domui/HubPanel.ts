import { IHubConfig } from '../../data/SkillData';

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

  constructor(private skills: IHubConfig[], private leveled: [string, number][], private onHubLevel: (hub: IHubConfig) => void, private onToggleCollection: (type: 'research' | 'fruit' | 'power', state: boolean) => void) {
    this.element = document.createElement('div');
    this.element.classList.add('skill-panel');
    document.body.appendChild(this.element);
    let top = document.createElement('div');
    top.classList.add('top');
    this.element.appendChild(top);
    top.innerHTML = `
    <div class="skill-title">Evolution Hub</div>
    <div class="skill-subtitle">Improvements made here will only affect the current plant.</div>`;
    
    this.contentElement = document.createElement('div');
    this.contentElement.classList.add('skill-content');
    top.appendChild(this.contentElement);
    skills.forEach((skill) => {
      let block = this.createSkillBlock(skill);
      this.contentElement.appendChild(block);
    });

    let button = document.createElement('button');
    button.classList.add('close-button');
    this.element.appendChild(button);
    button.innerHTML = 'X';
    button.addEventListener('click', () => this.hidden = true);

    let skillElement = document.createElement('div');

    this.skillpointElement = document.createElement('div');
    this.skillpointElement.classList.add('skill-skillpoint');
    skillElement.appendChild(this.skillpointElement);
    this.skillpointElement.innerHTML = `5 Skillpoints`;

    this.element.appendChild(skillElement);

    let buttonContainer = document.createElement('div');

    this.element.appendChild(buttonContainer);
    for (let i = 0; i < 3; i++) {
      let b2 = document.createElement('button');
      b2.classList.add('skill-button');
      this.toggleCollectionButtons.push(b2);
      buttonContainer.appendChild(b2);
    }
    this.toggleCollectionButtons[0].innerHTML = 'Research Collection';
    this.toggleCollectionButtons[0].addEventListener('click', () => this.toggleCollection('research'));
    this.toggleCollectionButtons[1].innerHTML = 'Fruit Collection';
    this.toggleCollectionButtons[1].addEventListener('click', () => this.toggleCollection('fruit'));
    this.toggleCollectionButtons[2].innerHTML = 'Power Collection';
    this.toggleCollectionButtons[2].addEventListener('click', () => this.toggleCollection('power'));

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
    // while (this.leveled.length > 0) this.leveled.shift();
    // this.skillsSpent = 0;
    // this.skillMap.forEach(data => data.element.disabled = false);
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

    if (level >= skill.costs.length) {
      element.innerHTML = `<div class="skill-block-title">${skill.effect.slug} ${skill.effect.key}: ${skill.effect.valueType === 'multiplicative' ? 'x' : '+'}${skill.effect.value}</div>
      <div class="skill-block-content">Max Level</div>`;
      element.disabled = true;
    } else {
      element.innerHTML = `<div class="skill-block-title">${skill.effect.slug} ${skill.effect.key}: ${skill.effect.valueType === 'multiplicative' ? 'x' : '+'}${skill.effect.value}</div>
      <div class="skill-block-content">Level ${level}</div>
      <div class="skill-block-cost">${skill.costs[level]} ${skill.costType}</div>`;
    }
  }

  public toggleCollection(type: 'research' | 'fruit' | 'power', state ?: boolean) {
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

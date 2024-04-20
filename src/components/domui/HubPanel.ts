import { DomManager } from '../../JMGE/DomManager';
import { Colors } from '../../data/Colors';
import { HubCostType, IHubConfig } from '../../data/SkillData';
import { Formula } from '../../services/Formula';

export class HubPanel {
  public hasSkillToLevel: boolean = false;

  private element: HTMLDivElement;
  private contentElements: HTMLDivElement[] = [];
  private currencyElements: HTMLDivElement[] = [];
  private toggleCollectionButtons: HTMLButtonElement[] = [];

  private skillMap: { element: HTMLButtonElement, skill: IHubConfig }[] = [];

  private research: number = 0;
  private fruit: number = 0;
  private power: number = 0;


  constructor(private skills: IHubConfig[], private leveled: [string, number][], private onHubLevel: (hub: IHubConfig) => void, private onToggleCollection: (type: HubCostType, state: boolean) => void) {
    this.element = DomManager.makeDiv('skill-panel', document.body);
    let top = DomManager.makeDiv('top', this.element);
    DomManager.makeButton('X', 'close-button', e => this.hidden = true, this.element);
    top.style.height = '95%';
    top.style.maxHeight = '95%';

    top.innerHTML = `
    <div class="skill-title">Evolution Hub</div>
    <div class="skill-subtitle">Improvements made here will only affect the current plant.</div>`;

    let horizontal = DomManager.makeDiv(null, top);
    horizontal.style.display = 'flex';
    horizontal.style.height = '100%';

    let columns: HTMLDivElement[] = [];
    for (let i = 0; i < 3; i++) {
      columns.push(DomManager.makeDiv(null, horizontal));
      columns[i].style.display = 'flex';
      columns[i].style.flexDirection = 'column';
      columns[i].style.alignItems = 'center';
      this.contentElements.push(DomManager.makeDiv('skill-content', columns[i]));
      this.contentElements[i].style.flexFlow = 'column';
      // this.contentElements[i].style.height = '49em';
      this.contentElements[i].style.overflowX = 'hidden';
      this.contentElements[i].style.flexGrow = '99';
      this.contentElements[i].style.justifyContent = 'flex-start';
      this.currencyElements.push(DomManager.makeDiv('skill-skillpoint', columns[i]));
    }

    this.contentElements[0].style.background = Formula.colorToHex(Colors.Node.lightPurple);
    this.contentElements[1].style.background = Formula.colorToHex(Colors.Node.lightOrange);
    this.contentElements[2].style.background = Formula.colorToHex(Colors.Node.yellow);

    skills.forEach((skill) => {
      this.createSkillBlock(skill);
    });

    this.toggleCollectionButtons.push(DomManager.makeButton('Evolution Collection', 'skill-button', () => this.toggleCollection('research'), columns[0]))
    this.toggleCollectionButtons.push(DomManager.makeButton('Fruit Collection', 'skill-button', () => this.toggleCollection('fruit'), columns[1]))
    this.toggleCollectionButtons.push(DomManager.makeButton('Power Collection', 'skill-button', () => this.toggleCollection('power'), columns[2]))

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

  public updateCurrencies = (research: number, fruit: number, power: number, buff: number) => {
    this.research = research;
    this.fruit = fruit;
    this.power = power;

    this.currencyElements[0].innerHTML = `${Math.round(research)} Evolution`;
    this.currencyElements[1].innerHTML = `${Math.round(fruit)} Fruits`;
    this.currencyElements[2].innerHTML = `${Math.round(power)} Power`;

    // this.skillpointElement.innerHTML = `${Math.round(research)} Evolution, ${Math.round(fruit)} Fruits, ${Math.round(power)} Power.`;
    this.updateHighlights();
  }

  private updateHighlights() {
    this.hasSkillToLevel = false;

    this.skillMap.forEach((data, i) => {
      let levelPair = this.leveled.find(el => el[0] === data.skill.slug);
      let level = levelPair ? levelPair[1] : 0;

      let currentAmount = this.getCurrency(data.skill.costType);

      if (data.skill.costs[level] <= currentAmount) {
        data.element.classList.add('highlight');
        this.hasSkillToLevel = true;
      } else {
        data.element.classList.remove('highlight');
      }
    });
  }

  private createSkillBlock(skill: IHubConfig): HTMLElement {
    let column: number = skill.costType === 'research' ? 0 : skill.costType === 'fruit' ? 1 : 2;
    let element = DomManager.makeButton(null,'skill-block', () => this.levelUp(element, skill), this.contentElements[column]);

    this.refreshButton(element, skill);
    this.skillMap.push({element, skill});

    return element;
  }

  private levelUp (element: HTMLButtonElement, skill: IHubConfig) {
    let currentAmount = this.getCurrency(skill.costType);
    let levelPair = this.leveled.find(el => el[0] === skill.slug);
    let level = levelPair ? levelPair[1] : 0;

    if (skill.costs[level] <= currentAmount) {
      this.onHubLevel(skill);
      this.refreshButton(element, skill);
    }
  }

  private getCurrency(costType: HubCostType) {
    return costType === 'research' ? this.research : costType === 'fruit' ? this.fruit : this.power;
  }

  private refreshButton(element: HTMLButtonElement, skill: IHubConfig) {
    let levelPair = this.leveled.find(el => el[0] === skill.slug);
    let level = levelPair ? levelPair[1] : 0;

    element.innerHTML = '';
    let skillName = DomManager.makeDiv('skill-block-title', element);
    skillName.innerHTML = `${skill.label}: ${skill.effect.valueType === 'multiplicative' ? 'x' : '+'}${skill.effect.value}`;
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

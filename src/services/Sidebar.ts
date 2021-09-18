import { FDGNode } from "../engine/FDG/FDGNode";
import { GameNode } from "../engine/Mechanics/Parts/GameNode";

export class Sidebar {
  private static aid: number = 0;
  public static genAid(): string {
    Sidebar.aid++;

    return `id-${Sidebar.aid}`;
  }

  nodeMap: { id: string, node: GameNode }[] = [];

  container: HTMLDivElement;

  constructor() {
    this.container = document.getElementById('node-container') as HTMLDivElement;
    this.container.innerHTML = '';
  }

  addNodeElement = (view: FDGNode) => {
    if (view.config.type !== 'fruit') {
      let node = view.data
      let id = this.addElement(node.toString(), view.config.name === 'seedling');
      this.nodeMap.push({ id, node });
    }
  }

  removeNodeElement = (view: FDGNode) => {
    if (view.config.type !== 'fruit') {
      let node = view.data;
      let index = this.nodeMap.findIndex(map => map.node === node);
      this.removeElementById(this.nodeMap[index].id);
      this.nodeMap.splice(index, 1);
    }
  }

  updateNodes() {
    this.nodeMap.forEach(data => this.updateElement(data.id, data.node.toString()));
  }

  addElement(content: string, start: boolean): string {
    let element = document.createElement("div");
    element.innerHTML = content;
    element.classList.add('node-block');
    element.id = Sidebar.genAid();
    console.log(element.id);
    if (start) {
      this.container.prepend(element);
    } else {
      this.container.appendChild(element);
    }
    return element.id;
  }

  updateElement(id: string, content: string) {
    let element = document.getElementById(id);
    if (element) {
      element.innerHTML = content;
    }
  }

  removeElementById(id: string) {
    let element = document.getElementById(id);
    this.container.removeChild(element);
  }

  destroy() {
    this.container.innerHTML = '';
  }
}
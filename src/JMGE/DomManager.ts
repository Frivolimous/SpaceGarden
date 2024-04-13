export class DomManager {

  public static makeDiv(className?: string, parent?: HTMLElement) {
    let div = document.createElement('div');
    if (className) {
      div.classList.add(className);
    }
    if (parent) {
      parent.appendChild(div);
    }
    return div;
  }

  public static makeButton(innerHTML?: string, className?: string, onClick?: (e: MouseEvent) => void, parent?: HTMLElement) {
    let button = document.createElement('button');
    if (innerHTML) {
      button.innerHTML = innerHTML;
    }
    if (className) {
      button.classList.add(className);
    }
    if (parent) {
      parent.appendChild(button);
    }
    if (onClick) {
      button.addEventListener('click', onClick);
    }
    return button;
  }
}

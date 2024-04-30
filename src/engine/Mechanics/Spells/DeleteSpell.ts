import { IClickEvent } from '../../../services/MouseController';
import { BaseSpell, SpellSlug } from './_BaseSpell';

export class DeleteSpell extends BaseSpell{
  slug: SpellSlug = 'delete';
  cooldown: number = 5000;

  clickEvent: IClickEvent = {
    onDown: position => {
      let nodeToDelete = this.gameUI.container.getClosestObject({ x: position.x, y: position.y, notType: 'core', notFruit: true });
      if (nodeToDelete) {
        nodeToDelete.flagDestroy = true;
        nodeToDelete.flagExplode = true;
        this.onComplete(true);
      } else {
        this.onComplete(false);
      }
    }
  }

  activate = () => {
    this.gameUI.container.showConnectionCount();
  }

  cancelActivation = () => {
    this.gameUI.container.showConnectionCount(false);
  }
}

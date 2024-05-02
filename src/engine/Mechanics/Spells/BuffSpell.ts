import { Config } from '../../../Config';
import { IClickEvent } from '../../../services/MouseController';
import { BaseSpell } from './_BaseSpell';
import { SpellSlug } from './_SpellTypes';

export class BuffSpell extends BaseSpell{
  slug: SpellSlug = 'buff';
  durMult = 1;
  cooldown: number = Config.NODE.BUFF_DURATION * 1000 / 60 * this.durMult * 1.5;
  
  clickEvent: IClickEvent = {
    onDown: position => {
      let nodeToBuff = this.gameUI.container.getClosestObject({ x: position.x, y: position.y, notFruit: true, customFilter: (node) => node.canGetBuffed() });
      if (nodeToBuff) {
        nodeToBuff.receiveBuff(this.durMult);
        this.onComplete(true);
      } else {
        this.onComplete(false);
      }
    }
  }

  activate = () => {
    this.gameUI.container.highlightConditional(node => node.canGetBuffed());
  }

  cancelActivation = () => {
    this.gameUI.container.removeHighlights();
  }
}
import { JMTween } from '../../../JMGE/JMTween';
import { IClickEvent } from '../../../services/MouseController';
import { BaseSpell } from './_BaseSpell';
import * as PIXI from 'pixi.js';
import { SpellSlug } from './_SpellTypes';

export class WeightSpell extends BaseSpell{
  slug: SpellSlug = 'weight';
  cooldown: number = 10000;

  // weightMult = -1;
  weightDuration = 10000;
  
  clickEvent: IClickEvent = {
    onDown: position => {
      let node = this.gameUI.container.getClosestObject({ x: position.x, y: position.y, notFruit: true });
      if (node) {
        node.power.hasPowerPriority = true;
        let dot = new PIXI.Graphics();
        dot.beginFill(0xffff77, 0.5);
        dot.drawCircle(0, 0, node.config.radius * 1.3);
        node.view.addChildAt(dot, 0);
        new JMTween({percent: 0}, this.weightDuration).to({percent: 1}).onComplete(() => {
          node.view.removeChild(dot);
          node.power.hasPowerPriority = false;
        }).start();
        this.onComplete(true);
      } else {
        this.onComplete(false);
      }
    }
  }

  activate = () => {
    this.gameUI.container.highlightConditional(node => !node.isFruit());
  }

  cancelActivation = () => {
    this.gameUI.container.removeHighlights();
  }
}
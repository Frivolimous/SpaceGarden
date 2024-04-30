import { JMTween } from '../../../JMGE/JMTween';
import { IClickEvent } from '../../../services/MouseController';
import { BaseSpell, SpellSlug } from './_BaseSpell';
import * as PIXI from 'pixi.js';

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
        // let dot = new PIXI.Graphics();
        // dot.beginFill(0x00ffff);
        // dot.drawCircle(0, 0, 10);
        // node.view.addChild(dot);
        new JMTween({percent: 0}, this.weightDuration).to({percent: 1}).onComplete(() => {
          // node.view.removeChild(dot);
          node.power.hasPowerPriority = false;
        }).start();
        this.onComplete(true);
      } else {
        this.onComplete(false);
      }
    }
  }

  activate = () => {
    this.gameUI.container.highlightConditional(node => true);
  }

  cancelActivation = () => {
    this.gameUI.container.removeHighlights();
  }
}
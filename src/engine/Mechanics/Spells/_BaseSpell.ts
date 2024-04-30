import { GameUI } from '../../../pages/GameUI';
import { IClickEvent } from '../../../services/MouseController';

export abstract class BaseSpell {
  slug: SpellSlug = 'none';
  cooldown: number = 10;

  onComplete: (successful: boolean) => void = () => {};

  constructor(protected gameUI: GameUI) {

  }

  clickEvent: IClickEvent = {
    onDown: position => {
      console.log('spell is cast');

      this.onComplete(true);
    }
  }

  activate = () => {
    
  }

  cancelActivation = () => {
    
  }
}

export type SpellSlug = 'delete' | 'turbo' | 'buff' | 'weight' |
      'none';
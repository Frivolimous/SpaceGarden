import { InfoPopup } from '../../../components/domui/InfoPopup';
import { BaseSpell, SpellSlug } from './_BaseSpell';

export class TurboSpell extends BaseSpell{
  slug: SpellSlug = 'turbo';
  isTurbo: boolean = false;
  cooldown: number = 2000;

  turboSpeed = 3

  activate = () => {
    this.isTurbo = !this.isTurbo;

    this.gameUI.gameSpeed = this.isTurbo ? this.turboSpeed : 1;

    this.onComplete(true);

    new InfoPopup('Game Speed: ' + this.gameUI.gameSpeed);
  }
}
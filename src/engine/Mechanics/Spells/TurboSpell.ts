import { InfoPopup } from '../../../components/domui/InfoPopup';
import { BaseSpell } from './_BaseSpell';
import { SpellSlug } from './_SpellTypes';

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
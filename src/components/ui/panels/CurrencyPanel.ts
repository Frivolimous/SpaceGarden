import { BasePanel } from './_BasePanel';
import * as PIXI from 'pixi.js';

import { IExtrinsicModel } from '../../../data/SaveData';
import { Fonts } from '../../../data/Fonts';
import { TooltipReader } from '../../tooltip/TooltipReader';

export class CurrencyPanel extends BasePanel {
  private goldText: PIXI.Text;
  // private tokenText: PIXI.Text;

  constructor() {
    super(new PIXI.Rectangle(0, 0, 300, 50), 0x999999);

    this.goldText = new PIXI.Text('', {fill: 0, fontFamily: Fonts.UI, fontSize: 18});
    // this.tokenText = new PIXI.Text('', {fill: 0, fontFamily: Fonts.UI, fontSize: 18});
    this.goldText.position.set(5, 5);
    this.addChild(this.goldText);
    // TooltipReader.addTooltip(this.zoneGauge, () => ({ title: 'Enemies Killed', description: String(this.zoneGauge.count) + ' / ' + String(this.zoneGauge.total) }));
  }

  public update = (extrinsic: IExtrinsicModel) => {
    this.goldText.text = 'Gold: ' + String(extrinsic.currency.gold) + '  Tokens: ' + String(extrinsic.currency.tokens);
  }
}

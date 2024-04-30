import * as PIXI from 'pixi.js';
import { INodeConfig, NodeData, NodeSlug } from '../data/NodeData';
import { JMEventListener } from '../JMGE/events/JMEventListener';
import { StringManager } from '../services/StringManager';
import { TooltipReader } from './tooltip/TooltipReader';
import { Button } from './ui/Button';
import { NodeButton } from './ui/NodeButton';
import { ToggleButton } from './ui/ToggleButton';
import { PlantNode } from '../engine/nodes/PlantNode';
import { Config } from '../Config';
import _ from 'lodash';
import { BaseSpell } from '../engine/Mechanics/Spells/_BaseSpell';
import { MouseController } from '../services/MouseController';

export class BottomBar extends PIXI.Container {
  public onCreateButton = new JMEventListener<{ config: INodeConfig, e: PIXI.FederatedPointerEvent, onComplete: () => void }>();
  public onProceedButton = new JMEventListener<null>();

  private graphic = new PIXI.Graphics();
  public nodeButtons: NodeButton[] = [];
  public spellButtons: ToggleButton[] = [];
  private proceedButton: Button;

  constructor(barWidth: number, public barHeight: number, private mouseC: MouseController) {
    super();

    this.addChild(this.graphic);

    this.graphic.beginFill(0xf1f1f1, 0.7)
      .drawRect(0, 0, barWidth, barHeight);

    this.proceedButton = new Button({ label: StringManager.data.BUTTON_PROCEED, onClick: this.onProceedButton.publish });
    TooltipReader.addTooltip(this.proceedButton, {title: StringManager.data.TOOLTIP_PROCEED_TITLE, description: StringManager.data.TOOLTIP_PROCEED_DESC});
    this.proceedButton.visible = false;
    this.addChild(this.proceedButton);

    this.resize(barWidth);
  }

  public setNodeButtons(configs: INodeConfig[]) {
    configs = _.sortBy(configs, config => NodeData.NodeOrder.indexOf(config.slug));

    configs.forEach(config => {
      let button = new NodeButton({label: (StringManager.data as any)[`NODE_NAME_SHORT_${config.slug}`], width: 50, height: 50, maxNodes: config.maxCount, color: config.color, onDown: e => {
        button.selected = !button.selected;
        if (button.selected) {
          this.nodeButtons.forEach(otherButton => {
            if (otherButton !== button) {
              otherButton.selected = false;
            }
          });
          this.spellButtons.forEach(spell => spell.selected = false);

          this.onCreateButton.publish({config, e, onComplete: () => button.selected = false});
        } else {
          this.onCreateButton.publish(null);
        }
      }});
      TooltipReader.addTooltip(button, {title: (StringManager.data as any)[`NODE_NAME_LONG_${config.slug}`], description: (StringManager.data as any)[`NODE_DESC_${config.slug}`]});
      this.addChild(button);
      this.nodeButtons.push(button);
    });
  }

  public setSpells(spells: BaseSpell[]) {
    spells.forEach(spell => {
      let spellButton = new ToggleButton({
        label: (StringManager.data as any)['SPELL_NAME_SHORT_' + spell.slug], width: 50, height: 50, rounding: 25, color: 0x77ccff, selectedColor: 0xffcc77, onToggle: (b: boolean) => {
          if (b) {
            this.nodeButtons.forEach(button => button.selected = false);
            this.spellButtons.forEach(button => ((button !== spellButton) && (button.selected = false)));
            spell.activate();
            this.mouseC.setNextClickEvent(spell.clickEvent);
          } else {
            spell.cancelActivation();
            this.mouseC.clearNextClickEvent();
          }
        },
      });
      spell.onComplete = (success) => {
        spellButton.selected = false;
        if (success) spellButton.disableTimer(spell.cooldown);
      };
      TooltipReader.addTooltip(spellButton, {title: (StringManager.data as any)['SPELL_NAME_LONG_' + spell.slug], description: (StringManager.data as any)['SPELL_DESC_' + spell.slug]});

      this.addChild(spellButton);

      this.spellButtons.push(spellButton);
    });
  }

  public refreshNodeButton(config: INodeConfig) {
    let nodeButton = this.nodeButtons.find(el => el.getLabel() === config.slug);

    if (nodeButton) {
      nodeButton.maxNodes = config.maxCount;
      nodeButton.disabled = nodeButton.count >= nodeButton.maxNodes;
    }
  }

  public resize(barWidth: number) {
    this.graphic.clear().beginFill(0xf1f1f1, 0.7)
      .drawRect(0, 0, barWidth, this.barHeight);

    this.nodeButtons.forEach((button, i) => {
      button.position.set(25 + i * 60, 25);
    });

    this.spellButtons.forEach((button, i) => button.position.set(barWidth - 50 - 10 - (60 * i), -50 - 10))
    this.proceedButton.position.set(barWidth - this.proceedButton.getWidth() - 5, 25);
  }

  public nodeAdded = (node: PlantNode) => {
    let name = (StringManager.data as any)[`NODE_NAME_SHORT_${node.slug}`];
    let button = this.nodeButtons.find(b => b.getLabel() === name);

    if (button && button.maxNodes > 0 && button.maxNodes !== Infinity) {
      button.count++;

      if (button.count >= button.maxNodes) {
        button.disabled = true;
      }
    }

    if (node.slug === 'seedling') {
      this.updateSeedling(node);
    }
  }

  public nodeRemoved = (node: PlantNode) => {
    let name = (StringManager.data as any)[`NODE_NAME_SHORT_${node.slug}`];
    let button = this.nodeButtons.find(b => b.getLabel() === name);

    if (button && button.maxNodes > 0) {
      button.count--;

      button.disabled = false;
    }

    if (node.slug === 'seedling') {
      this.updateSeedling(null);
    }
  }

  public updateSeedling(seedling?: PlantNode) {
    if (seedling) {
      if (this.proceedButton.visible === false) {
        this.proceedButton.visible = true;
        this.proceedButton.highlight(true, 2.5);
      }
      let percent = seedling.power.powerPercent;
      this.proceedButton.addLabel(`${StringManager.data.BUTTON_PROCEED} (${Math.floor(percent * 100)}%)`);
      if (this.proceedButton.disabled) {
        if (percent >= Config.NODE.LAUNCH_PERCENT) {
          this.proceedButton.disabled = false;
          this.proceedButton.highlight(true, 2.5);
        }
      } else {
        if (percent < Config.NODE.LAUNCH_PERCENT) {
          this.proceedButton.disabled = true;
        }
      }
    } else {
      this.proceedButton.visible = false;
      this.proceedButton.disabled = true;
    }
  }

  public getButton(slug: NodeSlug) {
    let label = (StringManager.data as any)[`NODE_NAME_SHORT_${slug}`];
    return this.nodeButtons.find(el => el.getLabel() === label);
  }
}

import * as PIXI from 'pixi.js';
import { GOD_MODE } from '../services/_Debug';
import { INodeConfig } from '../data/NodeData';
import { JMEventListener } from '../JMGE/events/JMEventListener';
import { StringManager } from '../services/StringManager';
import { TooltipReader } from './tooltip/TooltipReader';
import { Button } from './ui/Button';
import { NodeButton } from './ui/NodeButton';
import { ToggleButton } from './ui/ToggleButton';
import { PlantNode } from '../engine/nodes/PlantNode';
import { Config } from '../Config';

export class BottomBar extends PIXI.Container {
  public onCreateButton = new JMEventListener<{ config: INodeConfig, e: PIXI.InteractionEvent, onComplete: () => void }>();
  public onDeleteButton = new JMEventListener<{ onComplete: () => void }>();
  public onProceedButton = new JMEventListener<null>();
  public onTurboButton = new JMEventListener<boolean>();

  private graphic = new PIXI.Graphics();
  private buttons: NodeButton[] = [];
  private deleteButton: ToggleButton;
  private turboButton: ToggleButton;
  private proceedButton: Button;

  constructor(barWidth: number, public barHeight: number, configs: INodeConfig[]) {
    super();

    this.addChild(this.graphic);

    this.graphic.beginFill(0xf1f1f1, 0.7)
      .drawRect(0, 0, barWidth, barHeight);

    configs.forEach(config => {
      let button = new NodeButton({label: config.slug, width: 50, height: 50, maxNodes: config.maxCount, color: config.color, onDown: e => {
        button.selected = !button.selected;
        if (button.selected) {
          this.buttons.forEach(otherButton => {
            if (otherButton !== button) {
              otherButton.selected = false;
            }
          });
          this.deleteButton.selected = false;
          this.onCreateButton.publish({config, e, onComplete: () => button.selected = false});
        } else {
          this.onCreateButton.publish(null);
        }
      }});
      TooltipReader.addTooltip(button, {title: (StringManager.data as any)[`TOOLTIP_${config.slug}_TITLE`], description: (StringManager.data as any)[`TOOLTIP_${config.slug}_DESC`]});
      this.addChild(button);
      this.buttons.push(button);
    });

    this.deleteButton = new ToggleButton({
      label: StringManager.data.BUTTON_DELETE, width: 50, height: 50, color: 0x77ccff, selectedColor: 0xffcc77, onToggle: (b: boolean) => {
        if (b) {
          this.buttons.forEach(button => button.selected = false);
          this.onDeleteButton.publish({
            onComplete: () => {
              this.deleteButton.selected = false;
            },
          });
        } else {
          this.onDeleteButton.publish();
        }
      },
    });
    TooltipReader.addTooltip(this.deleteButton, {title: StringManager.data.TOOLTIP_DELETE_TITLE, description: StringManager.data.TOOLTIP_DELETE_DESC});
    this.turboButton = new ToggleButton({ label: StringManager.data.BUTTON_TURBO, width: 100, height: 50, color: 0x77ccff, selectedColor: 0xffcc77, onToggle: this.onTurboButton.publish });
    TooltipReader.addTooltip(this.turboButton, {title: StringManager.data.TOOLTIP_TURBO_TITLE, description: StringManager.data.TOOLTIP_TURBO_DESC});
    this.turboButton.visible = GOD_MODE;
    this.proceedButton = new Button({ label: StringManager.data.BUTTON_PROCEED, onClick: this.onProceedButton.publish });
    TooltipReader.addTooltip(this.proceedButton, {title: StringManager.data.TOOLTIP_PROCEED_TITLE, description: StringManager.data.TOOLTIP_PROCEED_DESC});
    this.proceedButton.visible = false;

    this.addChild(this.deleteButton);
    this.addChild(this.turboButton);
    this.addChild(this.proceedButton);

    this.resize(barWidth);
  }

  public resize(barWidth: number) {
    this.graphic.clear().beginFill(0xf1f1f1, 0.7)
      .drawRect(0, 0, barWidth, this.barHeight);

    this.buttons.forEach((button, i) => {
      button.position.set(25 + i * 60, 25);
    });

    this.deleteButton.position.set(this.buttons[this.buttons.length - 1].x + 60, 25);
    this.proceedButton.position.set(barWidth - this.proceedButton.getWidth() - 5, 25);
    this.turboButton.position.set(this.proceedButton.x - this.turboButton.getWidth() - 10, 25);
  }

  public nodeAdded = (node: PlantNode) => {
    let button = this.buttons.find(b => b.getLabel() === node.slug);

    if (button && button.maxNodes > 0) {
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
    let button = this.buttons.find(b => b.getLabel() === node.slug);

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
}

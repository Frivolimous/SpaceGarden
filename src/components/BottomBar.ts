import * as PIXI from 'pixi.js';
import { NodeConfig } from '../data/NodeData';
import { FDGNode } from '../engine/FDG/FDGNode';
import { JMEventListener } from '../JMGE/events/JMEventListener';
import { StringManager } from '../services/StringManager';
import { TooltipReader } from './tooltip/TooltipReader';
import { Button } from './ui/Button';
import { NodeButton } from './ui/NodeButton';
import { ToggleButton } from './ui/ToggleButton';

const TURBO = true;

export class BottomBar extends PIXI.Container {
  public onCreateButton = new JMEventListener<{ config: NodeConfig, e: PIXI.interaction.InteractionEvent }>();
  public onDeleteButton = new JMEventListener<{ onComplete: () => void }>();
  public onProceedButton = new JMEventListener<null>();
  public onTurboButton = new JMEventListener<boolean>();

  graphic = new PIXI.Graphics();
  buttons: NodeButton[] = [];
  deleteButton: ToggleButton;
  turboButton: ToggleButton;
  proceedButton: Button;

  constructor(barWidth: number, public barHeight: number, configs: NodeConfig[]) {
    super();

    this.addChild(this.graphic);

    this.graphic.beginFill(0xf1f1f1, 0.7)
      .drawRect(0, 0, barWidth, barHeight);

    configs.forEach(config => {
      let button = new NodeButton({label: config.slug, width: 50, height: 50, maxNodes: config.maxCount, color: config.color, onDown: e => this.onCreateButton.publish({config, e})});
      TooltipReader.addTooltip(button, {title: (StringManager.data.NODE_TOOLTIPS as any)[config.slug].TITLE, description: (StringManager.data.NODE_TOOLTIPS as any)[config.slug].DESCRIPTION})
      this.addChild(button);
      this.buttons.push(button);
    });

    this.deleteButton = new ToggleButton({
      label: 'Delete', width: 50, height: 50, color: 0x77ccff, selectedColor: 0xffcc77, onToggle: (b: boolean) => {
        if (b) {
          this.onDeleteButton.publish({
            onComplete: () => {
              this.deleteButton.selected = false;
            }
          });
        } else {
          this.onDeleteButton.publish();
        }
      }
    });
    TooltipReader.addTooltip(this.deleteButton, {title: 'Delete Node', description: 'Select a single node and it will be instantly removed from the network.'});
    this.turboButton = new ToggleButton({ label: 'Turbo', width: 100, height: 50, color: 0x77ccff, selectedColor: 0xffcc77, onToggle: this.onTurboButton.publish });
    TooltipReader.addTooltip(this.turboButton, {title: 'Dev Feature', description: 'You should not see this!  If you do, it\'s because the dev forgot to disable it before publishing.  Please report this!'});
    this.turboButton.visible = TURBO;
    this.proceedButton = new Button({ label: 'Launch Seedling', onClick: this.onProceedButton.publish });
    TooltipReader.addTooltip(this.proceedButton, {title: 'Launch Seedling', description: 'Launch your seedling with all of its research, starting over to grow your next plant network.'});
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

  public nodeAdded = (node: FDGNode) => {
    let button = this.buttons.find(b => b.getLabel() === node.config.slug);

    if (button && button.maxNodes > 0) {
      button.count++;

      if (button.count >= button.maxNodes) {
        button.disabled = true;
      }
    }

    if (node.config.slug === 'seedling') {
      this.updateSeedling(node);
    }
  }

  public nodeRemoved = (node: FDGNode) => {
    let button = this.buttons.find(b => b.getLabel() === node.config.slug);

    if (button && button.maxNodes > 0) {
      button.count--;

      button.disabled = false;
    }

    if (node.config.slug === 'seedling') {
      this.updateSeedling(null);
    }
  }

  public updateSeedling(node?: FDGNode) {
    if (node) {
      if (this.proceedButton.visible === false) {
        this.proceedButton.visible = true;
        this.proceedButton.highlight(true, 2.5);
      }
      let percent = node.data.powerPercent;
      this.proceedButton.addLabel(`Launch Seedling (${Math.floor(percent * 100)}%)`);
      if (this.proceedButton.disabled) {
        if (percent >= 1) {
          this.proceedButton.disabled = false;
          this.proceedButton.highlight(true, 2.5);
        }
      } else {
        if (percent < 1) {
          this.proceedButton.disabled = true;
        }
      }
    } else {
      this.proceedButton.visible = false;
      this.proceedButton.disabled = true;
    }
  }
}

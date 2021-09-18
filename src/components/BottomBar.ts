import * as PIXI from 'pixi.js';
import { NodeConfig } from '../data/NodeData';
import { FDGNode } from '../engine/FDG/FDGNode';
import { JMEventListener } from '../JMGE/events/JMEventListener';
import { Button } from './ui/Button';
import { NodeButton } from './ui/NodeButton';
import { ToggleButton } from './ui/ToggleButton';

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
      let button = new NodeButton({label: config.name, width: 50, height: 50, maxNodes: config.maxCount, color: config.color, onDown: e => this.onCreateButton.publish({config, e})});
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
    this.turboButton = new ToggleButton({ label: 'Turbo', width: 100, height: 50, color: 0x77ccff, selectedColor: 0xffcc77, onToggle: this.onTurboButton.publish });
    this.proceedButton = new Button({ label: 'Launch Seedling', onClick: this.onProceedButton.publish });
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
    let button = this.buttons.find(b => b.getLabel() === node.config.name);

    if (button && button.maxNodes > 0) {
      button.count++;

      if (button.count >= button.maxNodes) {
        button.disabled = true;
      }
    }

    if (node.config.name === 'seedling') {
      this.updateSeedling(node);
    }
  }

  public nodeRemoved = (node: FDGNode) => {
    let button = this.buttons.find(b => b.getLabel() === node.config.name);

    if (button && button.maxNodes > 0) {
      button.count--;

      button.disabled = false;
    }

    if (node.config.name === 'seedling') {
      this.updateSeedling(null);
    }
  }

  public updateSeedling(node?: FDGNode) {
    if (node) {
      this.proceedButton.visible = true;
      let percent = node.data.powerPercent;
      this.proceedButton.addLabel(`Launch Seedling (${Math.floor(percent * 100)}%)`);
      this.proceedButton.disabled = percent <= 1 ;
    } else {
      this.proceedButton.visible = false;
    }
  }
}

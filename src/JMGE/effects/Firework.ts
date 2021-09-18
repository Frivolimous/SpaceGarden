import * as PIXI from 'pixi.js';
import * as _ from 'lodash';
import { JMTicker } from '../events/JMTicker';
import { TextureCache } from '../../services/TextureCache';

const DExplosion: IExplosion = {
  x: 0, y: 0, count: 20,
  angle_min: 0, angle_max: Math.PI * 2,
  mag_min: 1, mag_max: 3,
  fade: 0.06,
  size_min: 5, size_max: 9,
  tint: 0xcccccc,
};

export class Firework {
  public static TEXTURE: PIXI.Texture;

  public static makeExplosion(stage: PIXI.Container, config: IExplosion) {
    _.defaults(config, DExplosion);
    if (!Firework.initialized) {
      Firework.initialize();
    }

    for (let i = 0; i < config.count; i++) {
      let display = new PIXI.Sprite(Firework.TEXTURE);
      stage.addChild(display);

      let size = _.random(config.size_min, config.size_max);
      display.width = size;
      display.scale.y = display.scale.x;
      display.alpha = _.random(1, 2, true);
      display.position.set(config.x, config.y);
      display.tint = config.tint;

      let mag = _.random(config.mag_min, config.mag_max);
      let angle = _.random(config.angle_min, config.angle_max);

      let particle: IParticle = {
        display,
        local: false,
        delay: 0,
        vX: mag * Math.cos(angle),
        vY: mag * Math.sin(angle),
        fade: config.fade,
      };
      Firework.particles.push(particle);
    }
  }

  private static particles: IParticle[] = [];
  private static initialized: boolean = false;

  private static initialize() {
    if (!Firework.initialized) {
      Firework.TEXTURE = TextureCache.getGraphicTexture('firework');
      JMTicker.add(Firework.onTick);
      Firework.initialized = true;
    }
  }

  private static onTick = () => {
    for (let i = 0; i < Firework.particles.length; i++) {
      Firework.updateParticle(Firework.particles[i]);
      // Firework.particles[i].update();
      if (Firework.particles[i].display.alpha < 0.1) {
        Firework.particles[i].display.destroy();
        Firework.particles.splice(i, 1);
        i -= 1;
      }
    }
    // if (this.particles.length>0) console.log(this.particles[0].x+" "+this.particles[0].y);
  }

  private static updateParticle(particle: IParticle) {
    particle.display.x += particle.vX;
    particle.display.y += particle.vY;
    particle.display.alpha -= particle.fade;
  }
}

interface IExplosion {
  x: number;
  y: number;
  count?: number;
  angle_min?: number;
  angle_max?: number;
  mag_min?: number;
  mag_max?: number;
  fade?: number;
  size_min?: number;
  size_max?: number;
  tint?: number;
}
interface IParticle {
  display: PIXI.Sprite;
  local: boolean;
  delay: number;
  vX: number;
  vY: number;
  fade: number;
}

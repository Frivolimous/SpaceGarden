export const JMTicker = {
  speedFactor: 1,
  prevTime: -1,
  tickEvents: [] as ((ms?: number) => void)[],

  framerate: 1000 / 60,
  frames: 0,

  active: false,

  add: (output: (ms?: number) => void) => {
    JMTicker.tickEvents.push(output);
    if (!JMTicker.active) {
      JMTicker.active = true;
      requestAnimationFrame(JMTicker.onTick);
    }
  },

  addOnce: (output: (ms?: number) => void, delay: number = 0) => {
    if (delay > 0) {
      let m = () => {
        delay--;
        if (delay === 0) {
          JMTicker.remove(m);
          output();
        }
      };
      JMTicker.tickEvents.push(m);
    } else {
      let m = () => {
        JMTicker.remove(m);
        output();
      };
      JMTicker.tickEvents.push(m);
    }
  },

  remove: (output: (ms?: number) => void) => {
    let i = JMTicker.tickEvents.indexOf(output);
    if (i >= 0) {
      JMTicker.tickEvents.splice(i, 1);
    }
  },

  clear : () => {
    JMTicker.tickEvents = [];
  },

  onTick: (time: number) => {
    let ms: number = JMTicker.prevTime < 0 ? 0 : (time - JMTicker.prevTime);
    JMTicker.prevTime = time;
    JMTicker.frames += ms;

    if (JMTicker.frames >= JMTicker.framerate) {
      JMTicker.frames -= JMTicker.framerate;

      if (JMTicker.tickEvents.length === 0) {
        JMTicker.active = false;
        JMTicker.prevTime = -1;
        return;
      } else {
        JMTicker.tickEvents.forEach(output => output(ms));
        if (JMTicker.speedFactor > 1) {
          for (let i = 0; i < JMTicker.speedFactor - 1; i++) {
            JMTicker.tickEvents.forEach(output => output(ms));
          }
        }
      }
    }

    requestAnimationFrame(JMTicker.onTick);
  },
};

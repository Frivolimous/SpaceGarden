import * as WebFont from 'webfontloader';

export class FontLoader {
  public static load(fonts: string[]): Promise<void> {
    // specifically loads GOOGLE fonts through WebFont with a simple interface.
    // Can extend this to cover other platforms but why bother?
    return new Promise(resolve => {
      WebFont.load({
        google: {
          families: fonts,
        },
        active: () => resolve(),
        inactive: () => resolve(),
      });
    });
  }
}

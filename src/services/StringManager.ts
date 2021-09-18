import * as _ from 'lodash';

import { EnglishStringData, GibberishStringData } from '../data/StringData';

type langType = 'English' | 'Gibberish';
let languages: langType[] = ['English', 'Gibberish'];
let cLanguage: langType = 'English';

let changeLanguage = (): langType => {
  let i = (languages.indexOf(cLanguage) + 1) % languages.length;
  cLanguage = languages[i];

  switch (cLanguage) {
    case 'English': StringManager = EnglishStringManager; break;
    case 'Gibberish': StringManager = GibberishStringManager; break;
  }
  return cLanguage;
};

export const EnglishStringManager = {
  data: EnglishStringData,

  changeLanguage: (): langType => {
    return changeLanguage();
  },

  getCurrentLanguage: (): langType => {
    return cLanguage;
  },
}

export const GibberishStringManager = _.cloneDeep(EnglishStringManager);

function gibberize(data: any) {
  _.forIn(data, ((value, key) => {
    if (_.isString(value)) {
      data[key] = _.shuffle(value.split('')).join('');
      // console.log('gib:', data.key, value);
    } else {
      data[key] = gibberize(value);
    }
  }));
  return data;
}

gibberize(GibberishStringManager.data);

export let StringManager = EnglishStringManager;

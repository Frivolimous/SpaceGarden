export const EnglishStringData = {
  GAME_TITLE: 'Space Garden',
  BUTTON: {
    MENU: 'Menu',
    HOME: 'Home',
    NEW_CHAR: 'New Character',
    LOAD_CHAR: 'Load Character',
    STATISTICS: 'Statistics',
    STORE: 'Store',
    GOLD_STORE: 'Gold Store',
    BLACK_MARKET: 'Black Market',
    STASH: 'Stash',
    NEXT_TAB: 'Next Tab',
    TO_TOWN: 'To Town',
    CONTINUE: 'Continue',
    YES: 'Yes',
    NO: 'No',
    CONFIRM: 'Confirm',
    CANCEL: 'Cancel',
    BACK: 'Back',
    FIGHT: 'Fight!',
    ADVENTURE: 'Adventure',
    CREDITS: 'Credits',
    LANGUAGE: 'Language',
    STATS_TAB: 'Stats',
    ACTION_TAB: 'Actions',
    STASH_TAB: 'Stash',
    INVENTORY_TAB: 'Inventory',
    SKILL_TAB: 'Skills',
    COSMETIC_TAB: 'Cosmetics',
  },

  MENU_TEXT: {
    CREDITS:  `
      Programmer: Jeremy Moshe
      Artist: ???
      Music: Stefen
      Sound Effects: Jeremy Moshe

      Special Thanks:
      Sofia Moshe
      Damon Kentridge
      Kurosai
      and YOU!
    `,
    DEAD_MODAL: 'You have died!',
    LEVEL_COMPLETE: 'You finished the level!',
    COMPLETE_PUCHASE: 'Proceed with purchase?',
    NOT_ENOUGH: 'Not enough ',
    LAST_CHAR: 'You cannot delete your last character',
    DELETE_CHAR: 'Delete this character?',
    NAME: 'Name',
    REFRESH_STORE_TOKEN: 'Use 1 Power Token to refresh the store slots?',
  },

  CURRENCY: {
    gold: 'gold',
    tokens: 'tokens',
    refresh: 'refresh',
    suns: 'suns',
    souls: 'souls',
  },

  TAG_DESC: {
    Incanted: 'Only uses half spell power',
    Double: 'Attacks twice',
    Relic: 'Only one Relic of any type can ever be equipped at a time',
    Trade: 'Can be sold for money... no other use',
    Heavy: 'Gains bonus damage from Strength',
    Finesse: 'Gains bonus damage from Dexterity',
    Cryptic: 'Gains bonus damage from Cunning',
    Mystic: 'Gains bonus damage from Magic',
    Ranged: 'Can only be used midrange or far',
    Thrown: 'Can be used in melee or midrange',
  },

  EFFECT_DESC: {
    lifesteal: 'Heals based on a percent of base damage',
    walk: 'Advance through the realm',
    rushed: 'Reduced stats',
    approach: 'Advance towards the enemy',
    backwards: 'Move away from the enemy',
    knockback: 'Pushes your opponent away from you',
    doubleshot: 'Chance to attack again',
    town: 'Go back to town during the next safe action',
    gassed: 'Takes damage over time',
    spikey: 'Deals damage whenever struck by a melee weapon',
//     export type EffectSlug = 'lifesteal' | 'walk' | 'rushed' | 'approach' | 'clearCritInit' | 'clearParryInit' | 'backwards' | 'knockback' |
//   'doubleshot' | 'magicStrike' | 'proc' | 'dazzled' | 'spikey' |
//   'ordinary' | 'deft' | 'holy' | 'noble' | 'wild' |
//   BuffSlug |
//   EffectSlugExt;
// export type EffectSlugExt = 'Bonus Physical';

// 'rushed' | 'burning' | 'crippled' | 'town' | 'useless' | 'leapbuff' | 'critInit' | 'parryInit' | 'aim' | 'markInit' |
//   'poisoned' | 'stunned' | 'confused' | 'weakened' | 'vulnerable' | 'empowered' | 'hastened' | 'enchanted' |
//   'amplified' | 'turtle' | 'celerity' | 'purity' | 'gassed' | 'blinded' | 'cursed' |
//   'afraid' | 'berserk' | 'strengthen' |
//   'deft';
  },
};

export let GibberishStringData = EnglishStringData;

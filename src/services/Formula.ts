import { SkillData } from '../data/SkillData';

export class Formula {
  public static getNextSkillLevel(research: number): number {
    let i = 0;
    while (true) {
      let iCost = Formula.getSkillCost(i);

      if (iCost > research) return i;
      i++;
    }
    // let nextSkill = SkillData.skillExchange.findIndex(cost => cost > research);

    // return nextSkill;
  }

  public static getSkillCost(skillpoints: number): number {
    if (skillpoints > SkillData.skillExchange.length - 1) {
      let diff = skillpoints - SkillData.skillExchange.length;

      let mult = Math.ceil ((diff + 1) / 9);
      let index = SkillData.skillExchange.length - 9 + diff % 9;

      return SkillData.skillExchange[index] * Math.pow(10, mult);
    }
    return SkillData.skillExchange[skillpoints];
  }

  public static colorToHex(n: number) {
    let hex = n.toString(16);
    while(hex.length < 6) hex = '0' + hex;

    return '#' + hex;
  }
}

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
}

(window as any).getSkills = () => {
  console.log('level for 0 is ', Formula.getNextSkillLevel(0));
  console.log('level for 5 is ', Formula.getNextSkillLevel(5));
  console.log('level for 10 is ', Formula.getNextSkillLevel(10));
  console.log('level for 20 is ', Formula.getNextSkillLevel(20));
  console.log('level for 50 is ', Formula.getNextSkillLevel(50));
  console.log('level for 51 is ', Formula.getNextSkillLevel(51));
  console.log('level for 99 is ', Formula.getNextSkillLevel(99));
  console.log('level for 100 is ', Formula.getNextSkillLevel(100));
  console.log('level for 101 is ', Formula.getNextSkillLevel(101));
  console.log('skillcost 0 is ', Formula.getSkillCost(0));
  console.log('skillcost 1 is ', Formula.getSkillCost(1));
  console.log('skillcost 2 is ', Formula.getSkillCost(2));
};

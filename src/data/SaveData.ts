import { CommandType } from '../engine/Mechanics/CrawlerCommands/_BaseCommand';
import { INodeSave } from '../engine/nodes/PlantNode';
import { NodeSlug } from './NodeData';

export type CurrencySlug = 'gold' | 'tokens' | 'refresh' | 'suns' | 'souls';

export const CURRENT_VERSION = 22;

export interface IExtrinsicModel {
  achievements: boolean[];
  // flags: boolean[];
  // scores?: number[];

  currency: {[key in CurrencySlug]?: number};

  stageState?: INodeSave[];
  skillsCurrent: string[];
  skillsNext: string[];
  skillTier: number;

  firstVersion?: number;
  logins?: number;

  skillTrees?: number[];

  crawlers: ICrawlerSave[];

  options: {
    autoFill: boolean;
  };
}

export interface ICrawlerSave {
  preference: CommandType;
  health: number;
  location: number;
}

export const dExtrinsicModel: IExtrinsicModel = {
  achievements: [],
  currency: {
    gold: 0,
    tokens: 0,
    refresh: 0,
    suns: 0,
    souls: 0,
  },

  crawlers: [],

  options: {
    autoFill: false,
  },

  skillsCurrent: [],
  skillsNext: [],
  skillTier: 0,
};

export const TierSaves: string[] = [
  '{"achievements":[],"currency":{"gold":0,"tokens":0,"refresh":0,"suns":0,"souls":0},"options":{"autoFill":false},"skillsCurrent":[],"skillsNext":[],"skillTier":0,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":281,"researchCurrent":0,"outlets":[2],"x":621,"y":309},{"uid":2,"slug":"stem","powerCurrent":91,"researchCurrent":0,"outlets":[4,8],"x":549,"y":299},{"uid":4,"slug":"stem","powerCurrent":95,"researchCurrent":0,"outlets":[5,6],"x":511,"y":314},{"uid":5,"slug":"stem","powerCurrent":84,"researchCurrent":0,"outlets":[7],"x":477,"y":307},{"uid":6,"slug":"generator","powerCurrent":79,"researchCurrent":0,"outlets":[],"x":483,"y":347},{"uid":7,"slug":"generator","powerCurrent":78,"researchCurrent":0,"outlets":[],"x":437,"y":293},{"uid":8,"slug":"seedling","powerCurrent":80,"researchCurrent":0,"outlets":[],"x":520,"y":237}]}',
  '{"achievements":[],"currency":{"gold":0,"tokens":0,"refresh":0,"suns":0,"souls":0},"options":{"autoFill":false},"skillsCurrent":[],"skillsNext":[],"skillTier":1,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":300,"researchCurrent":0,"outlets":[8,9,10,17],"x":589,"y":269},{"uid":8,"slug":"battery","powerCurrent":25,"researchCurrent":0,"outlets":[24],"x":625,"y":279},{"uid":9,"slug":"stem","powerCurrent":51,"researchCurrent":0,"outlets":[13,16,21,25],"x":582,"y":341},{"uid":10,"slug":"stem","powerCurrent":80,"researchCurrent":0,"outlets":[15,20,42,79],"x":601,"y":194},{"uid":13,"slug":"generator","powerCurrent":58,"researchCurrent":0,"outlets":[26,36,37],"x":616,"y":364},{"uid":15,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":615,"y":187},{"uid":16,"slug":"stem","powerCurrent":80,"researchCurrent":0,"outlets":[19,31,33,43],"x":567,"y":379},{"uid":17,"slug":"battery","powerCurrent":64,"researchCurrent":0,"outlets":[35],"x":552,"y":272},{"uid":19,"slug":"generator","powerCurrent":66,"researchCurrent":0,"outlets":[23,27,39],"x":591,"y":411},{"uid":20,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":616,"y":200},{"uid":21,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":576,"y":355},{"uid":23,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":611,"y":417},{"uid":24,"slug":"battery","powerCurrent":0,"researchCurrent":0,"outlets":[34],"x":636,"y":282},{"uid":25,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":567,"y":346},{"uid":26,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":636,"y":365},{"uid":27,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":606,"y":426},{"uid":31,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":553,"y":374},{"uid":33,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":554,"y":386},{"uid":34,"slug":"battery","powerCurrent":20,"researchCurrent":0,"outlets":[],"x":647,"y":284},{"uid":35,"slug":"battery","powerCurrent":0,"researchCurrent":0,"outlets":[38],"x":541,"y":273},{"uid":36,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":634,"y":374},{"uid":37,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":627,"y":381},{"uid":38,"slug":"battery","powerCurrent":20,"researchCurrent":0,"outlets":[],"x":530,"y":273},{"uid":39,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":596,"y":431},{"uid":42,"slug":"seedling","powerCurrent":4484,"researchCurrent":481.65477692913043,"outlets":[],"x":604,"y":119},{"uid":43,"slug":"stem","powerCurrent":78,"researchCurrent":0,"outlets":[45,46,50,52],"x":546,"y":411},{"uid":45,"slug":"stem","powerCurrent":48,"researchCurrent":0,"outlets":[47,51,53,78],"x":553,"y":445},{"uid":46,"slug":"stem","powerCurrent":50,"researchCurrent":0,"outlets":[57,58,59,77],"x":514,"y":426},{"uid":47,"slug":"generator","powerCurrent":75,"researchCurrent":0,"outlets":[54,56,60],"x":572,"y":481},{"uid":50,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":533,"y":404},{"uid":51,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":567,"y":451},{"uid":52,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":537,"y":423},{"uid":53,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":540,"y":454},{"uid":54,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":590,"y":491},{"uid":56,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":581,"y":499},{"uid":57,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":512,"y":441},{"uid":58,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":499,"y":427},{"uid":59,"slug":"stem","powerCurrent":93,"researchCurrent":0,"outlets":[62,63,68,76],"x":487,"y":447},{"uid":60,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":569,"y":501},{"uid":62,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":489,"y":462},{"uid":63,"slug":"stem","powerCurrent":104,"researchCurrent":0,"outlets":[67,70,73,75],"x":466,"y":473},{"uid":67,"slug":"lab","powerCurrent":187,"researchCurrent":0,"outlets":[71],"x":431,"y":489},{"uid":68,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":471,"y":446},{"uid":70,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":466,"y":489},{"uid":71,"slug":"research","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":413,"y":498},{"uid":73,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":451,"y":473},{"uid":75,"slug":"lab","powerCurrent":180,"researchCurrent":0,"outlets":[],"x":461,"y":512},{"uid":76,"slug":"lab","powerCurrent":143,"researchCurrent":0,"outlets":[],"x":448,"y":440},{"uid":77,"slug":"lab","powerCurrent":169,"researchCurrent":0,"outlets":[],"x":481,"y":406},{"uid":78,"slug":"lab","powerCurrent":148,"researchCurrent":0,"outlets":[],"x":533,"y":479},{"uid":79,"slug":"lab","powerCurrent":182,"researchCurrent":0,"outlets":[],"x":641,"y":196}]}',
];

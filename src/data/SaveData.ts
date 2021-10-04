import { CommandType } from '../engine/Mechanics/CrawlerCommands/_BaseCommand';
import { INodeSave } from '../engine/nodes/PlantNode';
import { NodeSlug } from './NodeData';

export type CurrencySlug = 'gold' | 'tokens' | 'refresh' | 'suns' | 'souls';

export const CURRENT_VERSION = 23;

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
  '{"achievements":[],"currency":{"gold":0,"tokens":0,"refresh":0,"suns":0,"souls":0},"options":{"autoFill":false},"skillsCurrent":["skill-2-1","skill-2-2","skill-2-4","skill-2-3","skill-2-6","skill-2-5","skill-tier-2"],"skillsNext":[],"skillTier":2,"crawlers":[{"preference":1,"health":0.4593562500005972,"location":158},{"preference":5,"health":0.8301250000007385,"location":79},{"preference":5,"health":0.7856000000005192,"location":1},{"preference":5,"health":0.785212500000722,"location":1},{"preference":6,"health":1.0930281250004885,"location":3},{"preference":9,"health":1.0932281250004885,"location":79}],"stageState":[{"uid":1,"slug":"core","powerCurrent":318,"researchCurrent":0,"outlets":[3,13,34,36],"x":512,"y":287},{"uid":3,"slug":"stem","powerCurrent":93,"researchCurrent":0,"outlets":[5,30,40,79],"x":584,"y":275},{"uid":5,"slug":"stem","powerCurrent":85,"researchCurrent":0,"outlets":[7,15,19,142],"x":626,"y":274},{"uid":7,"slug":"stem","powerCurrent":60,"researchCurrent":0,"outlets":[24,28,83,158],"x":665,"y":274},{"uid":13,"slug":"stem","powerCurrent":75,"researchCurrent":0,"outlets":[46,47,62,86],"x":440,"y":259},{"uid":15,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":641,"y":271},{"uid":19,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":638,"y":283},{"uid":24,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":672,"y":260},{"uid":28,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":680,"y":277},{"uid":30,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":595,"y":266},{"uid":34,"slug":"battery","powerCurrent":120878,"researchCurrent":0,"outlets":[38],"x":534,"y":256},{"uid":36,"slug":"battery","powerCurrent":120356,"researchCurrent":0,"outlets":[48],"x":517,"y":249},{"uid":38,"slug":"battery","powerCurrent":50,"researchCurrent":0,"outlets":[41],"x":540,"y":247},{"uid":40,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":584,"y":259},{"uid":41,"slug":"battery","powerCurrent":50,"researchCurrent":0,"outlets":[],"x":546,"y":238},{"uid":46,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":441,"y":244},{"uid":47,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":450,"y":248},{"uid":48,"slug":"battery","powerCurrent":51,"researchCurrent":0,"outlets":[51],"x":518,"y":238},{"uid":51,"slug":"battery","powerCurrent":50,"researchCurrent":0,"outlets":[],"x":520,"y":227},{"uid":62,"slug":"seedling","powerCurrent":49927,"researchCurrent":2297.8443574963953,"outlets":[],"x":376,"y":218},{"uid":79,"slug":"stem","powerCurrent":70,"researchCurrent":0,"outlets":[81,87,96,312],"x":606,"y":254},{"uid":81,"slug":"home","powerCurrent":87,"researchCurrent":0,"outlets":[],"x":609,"y":221},{"uid":83,"slug":"stem","powerCurrent":61,"researchCurrent":0,"outlets":[84,85,88,104],"x":696,"y":254},{"uid":84,"slug":"generator","powerCurrent":84,"researchCurrent":0,"outlets":[110,117,122],"x":736,"y":258},{"uid":85,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":706,"y":265},{"uid":86,"slug":"generator","powerCurrent":115,"researchCurrent":0,"outlets":[95,105,137],"x":421,"y":296},{"uid":87,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":610,"y":238},{"uid":88,"slug":"stem","powerCurrent":85,"researchCurrent":0,"outlets":[89,91,94,99],"x":714,"y":224},{"uid":89,"slug":"grove","powerCurrent":78,"researchCurrent":0,"outlets":[],"x":705,"y":195},{"uid":91,"slug":"generator","powerCurrent":85,"researchCurrent":0,"outlets":[102,108,124],"x":744,"y":197},{"uid":94,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":720,"y":211},{"uid":95,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":411,"y":314},{"uid":96,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":619,"y":248},{"uid":99,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":702,"y":215},{"uid":102,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":764,"y":193},{"uid":104,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":692,"y":239},{"uid":105,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":422,"y":316},{"uid":108,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":759,"y":183},{"uid":110,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":753,"y":270},{"uid":117,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":757,"y":260},{"uid":122,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":755,"y":249},{"uid":124,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":748,"y":177},{"uid":137,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":404,"y":307},{"uid":142,"slug":"grove","powerCurrent":95,"researchCurrent":0,"outlets":[379],"x":644,"y":249},{"uid":158,"slug":"stem","powerCurrent":66,"researchCurrent":0,"outlets":[159,171,172,215],"x":688,"y":301},{"uid":159,"slug":"generator","powerCurrent":66,"researchCurrent":0,"outlets":[175,186,204],"x":721,"y":325},{"uid":171,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":702,"y":297},{"uid":172,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":677,"y":311},{"uid":175,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":741,"y":326},{"uid":186,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":737,"y":337},{"uid":204,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":727,"y":344},{"uid":215,"slug":"home","powerCurrent":51,"researchCurrent":0,"outlets":[366,369,380],"x":686,"y":333},{"uid":312,"slug":"lab","powerCurrent":168,"researchCurrent":0,"outlets":[378],"x":601,"y":295},{"uid":366,"slug":"food","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":692,"y":347},{"uid":369,"slug":"food","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":680,"y":347},{"uid":378,"slug":"research","powerCurrent":85,"researchCurrent":0,"outlets":[],"x":609,"y":313},{"uid":379,"slug":"gen","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":649,"y":234},{"uid":380,"slug":"food","powerCurrent":30,"researchCurrent":0,"outlets":[],"x":670,"y":328}]}',
  null,
  null,
  null,
  null,
  null,
  null,
  null,
  '{"achievements":[],"currency":{"gold":0,"tokens":0,"refresh":0,"suns":0,"souls":0},"options":{"autoFill":false},"skillsCurrent":["skill-1","skill-2","skill-3","skill-6","skill-4","skill-5"],"skillsNext":[],"skillTier":0,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":150,"researchCurrent":0,"outlets":[],"x":600,"y":300}]}',
  '{"achievements":[],"currency":{"gold":0,"tokens":0,"refresh":0,"suns":0,"souls":0},"options":{"autoFill":false},"skillsCurrent":["skill-2-1","skill-2-2","skill-2-3","skill-2-4","skill-2-5","skill-2-6"],"skillsNext":[],"skillTier":1,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":150,"researchCurrent":0,"outlets":[],"x":600,"y":300}]}',
  '{"achievements":[],"currency":{"gold":0,"tokens":0,"refresh":0,"suns":0,"souls":0},"options":{"autoFill":false},"skillsCurrent":[],"skillsNext":[],"skillTier":2,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":150,"researchCurrent":0,"outlets":[],"x":600,"y":300}]}',
];

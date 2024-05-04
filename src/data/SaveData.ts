import { CommandType } from '../engine/Mechanics/CrawlerCommands/_CommandTypes';
import { INodeSave } from '../engine/nodes/PlantNode';
import { CrawlerSlug } from './CrawlerData';

export const CURRENT_VERSION = 24;

export interface IExtrinsicModel {
  achievements: boolean[];
  // flags: boolean[];
  scores: number[];

  stageState?: INodeSave[];
  skillsCurrent: string[];
  skillsNext: string[];
  hubLevels: [string, number][];
  skillTier: number;

  prestigeTime: number;
  totalTime: number;

  firstVersion?: number;
  logins?: number;
  tutorialStep: number

  crawlers: ICrawlerSave[];

  options: {
    autoFill: boolean;
  };
}

export interface ICrawlerSave {
  slug: CrawlerSlug;
  preference: CommandType;
  health: number;
  location: number;
}

export const dExtrinsicModel: IExtrinsicModel = {
  achievements: [],
  scores: [0, 0, 0, 0],

  crawlers: [],

  options: {
    autoFill: false,
  },

  skillsCurrent: [],
  skillsNext: [],
  hubLevels: [],
  skillTier: 0,
  tutorialStep: 0,

  prestigeTime: 0,
  totalTime: 0,
};

export const TierSaves: string[] = [
  '{"hubLevels":[],"achievements":[false],"prestigeTime":0,"totalTime":0,"scores":[0, 0, 0],"options":{"autoFill":false},"skillsCurrent":[],"skillsNext":[],"skillTier":0,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":281,"researchCurrent":0,"outlets":[2],"x":621,"y":309},{"uid":2,"slug":"stem","powerCurrent":91,"researchCurrent":0,"outlets":[4,8],"x":549,"y":299},{"uid":4,"slug":"stem","powerCurrent":95,"researchCurrent":0,"outlets":[5,6],"x":511,"y":314},{"uid":5,"slug":"stem","powerCurrent":84,"researchCurrent":0,"outlets":[7],"x":477,"y":307},{"uid":6,"slug":"generator","powerCurrent":79,"researchCurrent":0,"outlets":[],"x":483,"y":347},{"uid":7,"slug":"generator","powerCurrent":78,"researchCurrent":0,"outlets":[],"x":437,"y":293},{"uid":8,"slug":"seedling","powerCurrent":80,"researchCurrent":0,"outlets":[],"x":520,"y":237}]}',
  '{"hubLevels":[],"achievements":[false],"prestigeTime":0,"totalTime":0,"scores":[0, 0, 0],"options":{"autoFill":false},"skillsCurrent":[],"skillsNext":[],"skillTier":1,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":300,"researchCurrent":0,"outlets":[8,9,10,17],"x":589,"y":269},{"uid":8,"slug":"battery","powerCurrent":25,"researchCurrent":0,"outlets":[24],"x":625,"y":279},{"uid":9,"slug":"stem","powerCurrent":51,"researchCurrent":0,"outlets":[13,16,21,25],"x":582,"y":341},{"uid":10,"slug":"stem","powerCurrent":80,"researchCurrent":0,"outlets":[15,20,42,79],"x":601,"y":194},{"uid":13,"slug":"generator","powerCurrent":58,"researchCurrent":0,"outlets":[26,36,37],"x":616,"y":364},{"uid":15,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":615,"y":187},{"uid":16,"slug":"stem","powerCurrent":80,"researchCurrent":0,"outlets":[19,31,33,43],"x":567,"y":379},{"uid":17,"slug":"battery","powerCurrent":64,"researchCurrent":0,"outlets":[35],"x":552,"y":272},{"uid":19,"slug":"generator","powerCurrent":66,"researchCurrent":0,"outlets":[23,27,39],"x":591,"y":411},{"uid":20,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":616,"y":200},{"uid":21,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":576,"y":355},{"uid":23,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":611,"y":417},{"uid":24,"slug":"battery","powerCurrent":0,"researchCurrent":0,"outlets":[34],"x":636,"y":282},{"uid":25,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":567,"y":346},{"uid":26,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":636,"y":365},{"uid":27,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":606,"y":426},{"uid":31,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":553,"y":374},{"uid":33,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":554,"y":386},{"uid":34,"slug":"battery","powerCurrent":20,"researchCurrent":0,"outlets":[],"x":647,"y":284},{"uid":35,"slug":"battery","powerCurrent":0,"researchCurrent":0,"outlets":[38],"x":541,"y":273},{"uid":36,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":634,"y":374},{"uid":37,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":627,"y":381},{"uid":38,"slug":"battery","powerCurrent":20,"researchCurrent":0,"outlets":[],"x":530,"y":273},{"uid":39,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":596,"y":431},{"uid":42,"slug":"seedling","powerCurrent":4484,"researchCurrent":481.65477692913043,"outlets":[],"x":604,"y":119},{"uid":43,"slug":"stem","powerCurrent":78,"researchCurrent":0,"outlets":[45,46,50,52],"x":546,"y":411},{"uid":45,"slug":"stem","powerCurrent":48,"researchCurrent":0,"outlets":[47,51,53,78],"x":553,"y":445},{"uid":46,"slug":"stem","powerCurrent":50,"researchCurrent":0,"outlets":[57,58,59,77],"x":514,"y":426},{"uid":47,"slug":"generator","powerCurrent":75,"researchCurrent":0,"outlets":[54,56,60],"x":572,"y":481},{"uid":50,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":533,"y":404},{"uid":51,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":567,"y":451},{"uid":52,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":537,"y":423},{"uid":53,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":540,"y":454},{"uid":54,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":590,"y":491},{"uid":56,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":581,"y":499},{"uid":57,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":512,"y":441},{"uid":58,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":499,"y":427},{"uid":59,"slug":"stem","powerCurrent":93,"researchCurrent":0,"outlets":[62,63,68,76],"x":487,"y":447},{"uid":60,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":569,"y":501},{"uid":62,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":489,"y":462},{"uid":63,"slug":"stem","powerCurrent":104,"researchCurrent":0,"outlets":[67,70,73,75],"x":466,"y":473},{"uid":67,"slug":"lab","powerCurrent":187,"researchCurrent":0,"outlets":[71],"x":431,"y":489},{"uid":68,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":471,"y":446},{"uid":70,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":466,"y":489},{"uid":71,"slug":"research","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":413,"y":498},{"uid":73,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":451,"y":473},{"uid":75,"slug":"lab","powerCurrent":180,"researchCurrent":0,"outlets":[],"x":461,"y":512},{"uid":76,"slug":"lab","powerCurrent":143,"researchCurrent":0,"outlets":[],"x":448,"y":440},{"uid":77,"slug":"lab","powerCurrent":169,"researchCurrent":0,"outlets":[],"x":481,"y":406},{"uid":78,"slug":"lab","powerCurrent":148,"researchCurrent":0,"outlets":[],"x":533,"y":479},{"uid":79,"slug":"lab","powerCurrent":182,"researchCurrent":0,"outlets":[],"x":641,"y":196}]}',
  '{"hubLevels":[],"achievements":[true, true, true],"prestigeTime":0,"totalTime":0,"scores":[0, 0, 0],"options":{"autoFill":false},"skillsCurrent":["skill-2-1","skill-2-2","skill-2-4","skill-2-3","skill-2-6","skill-2-5","skill-tier-2"],"skillsNext":[],"skillTier":2,"crawlers":[{"preference":1,"health":0.4593562500005972,"location":158},{"preference":5,"health":0.8301250000007385,"location":79},{"preference":5,"health":0.7856000000005192,"location":1},{"preference":5,"health":0.785212500000722,"location":1},{"preference":6,"health":1.0930281250004885,"location":3},{"preference":9,"health":1.0932281250004885,"location":79}],"stageState":[{"uid":1,"slug":"core","powerCurrent":318,"researchCurrent":0,"outlets":[3,13,34,36],"x":512,"y":287},{"uid":3,"slug":"stem","powerCurrent":93,"researchCurrent":0,"outlets":[5,30,40,79],"x":584,"y":275},{"uid":5,"slug":"stem","powerCurrent":85,"researchCurrent":0,"outlets":[7,15,19,142],"x":626,"y":274},{"uid":7,"slug":"stem","powerCurrent":60,"researchCurrent":0,"outlets":[24,28,83,158],"x":665,"y":274},{"uid":13,"slug":"stem","powerCurrent":75,"researchCurrent":0,"outlets":[46,47,62,86],"x":440,"y":259},{"uid":15,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":641,"y":271},{"uid":19,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":638,"y":283},{"uid":24,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":672,"y":260},{"uid":28,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":680,"y":277},{"uid":30,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":595,"y":266},{"uid":34,"slug":"battery","powerCurrent":100,"researchCurrent":0,"outlets":[38],"x":534,"y":256},{"uid":36,"slug":"battery","powerCurrent":100,"researchCurrent":0,"outlets":[48],"x":517,"y":249},{"uid":38,"slug":"battery","powerCurrent":50,"researchCurrent":0,"outlets":[41],"x":540,"y":247},{"uid":40,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":584,"y":259},{"uid":41,"slug":"battery","powerCurrent":50,"researchCurrent":0,"outlets":[],"x":546,"y":238},{"uid":46,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":441,"y":244},{"uid":47,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":450,"y":248},{"uid":48,"slug":"battery","powerCurrent":51,"researchCurrent":0,"outlets":[51],"x":518,"y":238},{"uid":51,"slug":"battery","powerCurrent":50,"researchCurrent":0,"outlets":[],"x":520,"y":227},{"uid":62,"slug":"seedling","powerCurrent":49927,"researchCurrent":2297.8443574963953,"outlets":[],"x":376,"y":218},{"uid":79,"slug":"stem","powerCurrent":70,"researchCurrent":0,"outlets":[81,87,96,312],"x":606,"y":254},{"uid":81,"slug":"home","powerCurrent":87,"researchCurrent":0,"outlets":[],"x":609,"y":221},{"uid":83,"slug":"stem","powerCurrent":61,"researchCurrent":0,"outlets":[84,85,88,104],"x":696,"y":254},{"uid":84,"slug":"generator","powerCurrent":84,"researchCurrent":0,"outlets":[110,117,122],"x":736,"y":258},{"uid":85,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":706,"y":265},{"uid":86,"slug":"generator","powerCurrent":115,"researchCurrent":0,"outlets":[95,105,137],"x":421,"y":296},{"uid":87,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":610,"y":238},{"uid":88,"slug":"stem","powerCurrent":85,"researchCurrent":0,"outlets":[89,91,94,99],"x":714,"y":224},{"uid":89,"slug":"grove","powerCurrent":78,"researchCurrent":0,"outlets":[],"x":705,"y":195},{"uid":91,"slug":"generator","powerCurrent":85,"researchCurrent":0,"outlets":[102,108,124],"x":744,"y":197},{"uid":94,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":720,"y":211},{"uid":95,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":411,"y":314},{"uid":96,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":619,"y":248},{"uid":99,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":702,"y":215},{"uid":102,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":764,"y":193},{"uid":104,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":692,"y":239},{"uid":105,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":422,"y":316},{"uid":108,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":759,"y":183},{"uid":110,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":753,"y":270},{"uid":117,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":757,"y":260},{"uid":122,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":755,"y":249},{"uid":124,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":748,"y":177},{"uid":137,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":404,"y":307},{"uid":142,"slug":"grove","powerCurrent":95,"researchCurrent":0,"outlets":[379],"x":644,"y":249},{"uid":158,"slug":"stem","powerCurrent":66,"researchCurrent":0,"outlets":[159,171,172,215],"x":688,"y":301},{"uid":159,"slug":"generator","powerCurrent":66,"researchCurrent":0,"outlets":[175,186,204],"x":721,"y":325},{"uid":171,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":702,"y":297},{"uid":172,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":677,"y":311},{"uid":175,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":741,"y":326},{"uid":186,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":737,"y":337},{"uid":204,"slug":"burr","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":727,"y":344},{"uid":215,"slug":"home","powerCurrent":51,"researchCurrent":0,"outlets":[366,369,380],"x":686,"y":333},{"uid":312,"slug":"lab","powerCurrent":168,"researchCurrent":0,"outlets":[378],"x":601,"y":295},{"uid":366,"slug":"food","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":692,"y":347},{"uid":369,"slug":"food","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":680,"y":347},{"uid":378,"slug":"research","powerCurrent":85,"researchCurrent":0,"outlets":[],"x":609,"y":313},{"uid":379,"slug":"gen","powerCurrent":100,"researchCurrent":0,"outlets":[],"x":649,"y":234},{"uid":380,"slug":"food","powerCurrent":30,"researchCurrent":0,"outlets":[],"x":670,"y":328}]}',
  '{"hubLevels":[],"achievements":[true,true,true,null,null,true],"prestigeTime":0,"totalTime":0,"scores":[1,0,0],"options":{"autoFill":false},"skillsCurrent":["skill-3-1","skill-3-2"],"skillsNext":[],"skillTier":2,"crawlers":[{"slug":"crawler","preference":1,"health":0.7249000000001262,"location":2},{"slug":"crawler","preference":9,"health":0.9050437500000439,"location":4},{"slug":"crawler","preference":4,"health":0.5353500000001228,"location":4},{"slug":"crawler","preference":4,"health":0.9865750000000821,"location":1},{"slug":"crawler","preference":9,"health":0.9494375000001167,"location":38},{"slug":"crawler","preference":5,"health":0.9376937500000655,"location":9}],"stageState":[{"uid":1,"slug":"core","powerCurrent":332,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[2,9,49,55],"x":597,"y":291},{"uid":2,"slug":"stem","powerCurrent":101,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[3,4,36,57],"x":565,"y":230},{"uid":3,"slug":"generator","powerCurrent":104,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[51,61,66,77],"x":527,"y":214},{"uid":4,"slug":"hub","powerCurrent":298,"researchCurrent":652.0689294597885,"fruitCurrent":120.30107434185642,"storedPowerCurrent":2658.6292023131195,"outlets":[5,6,7,8],"x":562,"y":172},{"uid":5,"slug":"generator","powerCurrent":78,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[42,68,79,92],"x":536,"y":114},{"uid":6,"slug":"generator","powerCurrent":88,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[39,45,60,63],"x":612,"y":137},{"uid":7,"slug":"lab","powerCurrent":132,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":504,"y":154},{"uid":8,"slug":"grove","powerCurrent":66,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[106,110],"x":572,"y":120},{"uid":9,"slug":"stem","powerCurrent":121,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[10,12,30,46],"x":630,"y":354},{"uid":10,"slug":"generator","powerCurrent":87,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[59,62,65,67],"x":611,"y":391},{"uid":12,"slug":"stem","powerCurrent":115,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[13,18,22,24],"x":660,"y":376},{"uid":13,"slug":"stem","powerCurrent":169,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[15,16,31,33],"x":695,"y":376},{"uid":15,"slug":"volatile","powerCurrent":302,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[28,40,43,47],"x":741,"y":360},{"uid":16,"slug":"lab","powerCurrent":187,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[37,76,80],"x":727,"y":399},{"uid":18,"slug":"bigstem","powerCurrent":153,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[19,20,25,26,38],"x":671,"y":417},{"uid":19,"slug":"grove","powerCurrent":87,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[96,107],"x":704,"y":436},{"uid":20,"slug":"grove","powerCurrent":79,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[93,100],"x":687,"y":452},{"uid":22,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":667,"y":390},{"uid":24,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":673,"y":367},{"uid":25,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":656,"y":431},{"uid":26,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":690,"y":414},{"uid":28,"slug":"battery","powerCurrent":30,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":766,"y":361},{"uid":30,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":645,"y":355},{"uid":31,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":710,"y":380},{"uid":33,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":702,"y":362},{"uid":36,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":574,"y":217},{"uid":37,"slug":"research","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":744,"y":411},{"uid":38,"slug":"stem","powerCurrent":56,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[41,44,64],"x":661,"y":455},{"uid":39,"slug":"grove","powerCurrent":91,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":616,"y":98},{"uid":40,"slug":"battery","powerCurrent":26,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":763,"y":349},{"uid":41,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":655,"y":469},{"uid":42,"slug":"grove","powerCurrent":101,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[109],"x":502,"y":96},{"uid":43,"slug":"battery","powerCurrent":30,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":763,"y":372},{"uid":44,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":647,"y":461},{"uid":45,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":633,"y":134},{"uid":46,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":637,"y":367},{"uid":47,"slug":"battery","powerCurrent":28,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":755,"y":339},{"uid":49,"slug":"battery","powerCurrent":101,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[58],"x":566,"y":312},{"uid":51,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":508,"y":223},{"uid":55,"slug":"battery","powerCurrent":222,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[84],"x":559,"y":290},{"uid":57,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":563,"y":215},{"uid":58,"slug":"battery","powerCurrent":53,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[82],"x":557,"y":319},{"uid":59,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":619,"y":410},{"uid":60,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":627,"y":122},{"uid":61,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":522,"y":194},{"uid":62,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":607,"y":411},{"uid":63,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":594,"y":126},{"uid":64,"slug":"home","powerCurrent":112,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[101],"x":663,"y":487},{"uid":65,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":591,"y":398},{"uid":66,"slug":"home","powerCurrent":86,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[108],"x":488,"y":210},{"uid":67,"slug":"stem","powerCurrent":77,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[70,71,75],"x":588,"y":424},{"uid":68,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":516,"y":114},{"uid":70,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":590,"y":439},{"uid":71,"slug":"home","powerCurrent":101,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":570,"y":450},{"uid":75,"slug":"leaf","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":573,"y":428},{"uid":76,"slug":"research","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":747,"y":401},{"uid":77,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":511,"y":202},{"uid":79,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":542,"y":94},{"uid":80,"slug":"research","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":736,"y":418},{"uid":82,"slug":"battery","powerCurrent":0,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":548,"y":325},{"uid":84,"slug":"battery","powerCurrent":51,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[89],"x":548,"y":289},{"uid":89,"slug":"battery","powerCurrent":51,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":538,"y":290},{"uid":92,"slug":"burr","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":529,"y":95},{"uid":93,"slug":"gen","powerCurrent":81,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":698,"y":462},{"uid":96,"slug":"gen","powerCurrent":84,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":716,"y":446},{"uid":100,"slug":"gen","powerCurrent":74,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":690,"y":467},{"uid":101,"slug":"food","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":665,"y":502},{"uid":106,"slug":"gen","powerCurrent":57,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":572,"y":104},{"uid":107,"slug":"gen","powerCurrent":85,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":708,"y":422},{"uid":108,"slug":"food","powerCurrent":100,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":473,"y":208},{"uid":109,"slug":"gen","powerCurrent":73,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":504,"y":82},{"uid":110,"slug":"gen","powerCurrent":0,"researchCurrent":0,"fruitCurrent":0,"storedPowerCurrent":0,"outlets":[],"x":571,"y":120}]}',
  '{"hubLevels":[],"achievements":[true,true,true,true,true,true,true,true],"prestigeTime":17016.900000000023,"totalTime":143701.59999999998,"scores":[1,0,0],"options":{"autoFill":false},"skillsCurrent":["skill-3-1","skill-3-5","skill-3-2","skill-3-3","skill-3-6","skill-3-4","skill-tier-3"],"skillsNext":[],"skillTier":3,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":150,"outlets":[],"x":600,"y":300}]}',
  null,
  null,
  null,
  null,
  null,
  '{"hubLevels":[],"achievements":[false],"scores":[0, 0, 0],"prestigeTime":0,"totalTime":0,"options":{"autoFill":false},"skillsCurrent":["skill-1","skill-2","skill-3","skill-6","skill-4","skill-5"],"skillsNext":[],"skillTier":0,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":150,"researchCurrent":0,"outlets":[],"x":600,"y":300}]}',
  '{"hubLevels":[],"achievements":[false],"scores":[0, 0, 0],"prestigeTime":0,"totalTime":0,"options":{"autoFill":false},"skillsCurrent":["skill-2-1","skill-2-2","skill-2-3","skill-2-4","skill-2-5","skill-2-6"],"skillsNext":[],"skillTier":1,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":150,"researchCurrent":0,"outlets":[],"x":600,"y":300}]}',
  '{"hubLevels":[],"achievements":[false],"scores":[0, 0, 0],"prestigeTime":0,"totalTime":0,"options":{"autoFill":false},"skillsCurrent":[],"skillsNext":[],"skillTier":2,"crawlers":[],"stageState":[{"uid":1,"slug":"core","powerCurrent":150,"researchCurrent":0,"outlets":[],"x":600,"y":300}]}',
];

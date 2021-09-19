import { Colors } from "./Colors";

const NodeBase = {
  powerMax: 100,
  powerDrain: -0.05,
  corGen: 0.45,
  smallGen: 0.15,
  powerDelay: 20,
  powerClump: 10,
  powerClumpSub: 5,
  powerWeight: 1,
  fruitClump: 5,
}

export const NodeData: INodeData = {
  Nodes: [
    {
      slug: 'core', type: 'normal', color: Colors.Node.yellow, shape: 'circle',
      radius: 30, mass: 30, force: 30, maxLinks: 1, maxCount: 1,
      powerMax: NodeBase.powerMax * 3, powerGen: NodeBase.corGen, powerWeight: NodeBase.powerWeight * 5,
      powerDelay: NodeBase.powerDelay, powerClump: NodeBase.powerClump * 1.5,
      fruitType: 'battery', fruitChain: 3, maxFruits: 2, fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'seedling', type: 'normal', color: Colors.Node.darkgreen, shape: 'hexagon',
      radius: 30, mass: 30, force: 30, maxLinks: 1, maxCount: 1,
      powerMax: NodeBase.powerMax * 100, powerGen: 0, powerWeight: NodeBase.powerWeight * 0.75,
      powerDelay: NodeBase.powerDelay, powerClump: 0,
      fruitChain: 0,
    },
    {
      slug: 'stem', type: 'normal', color: Colors.Node.green, shape: 'circle',
      radius: 10, mass: 1, force: 1, maxLinks: 3, maxCount: 5,
      powerMax: NodeBase.powerMax, powerGen: NodeBase.powerDrain, powerWeight: NodeBase.powerWeight,
      powerDelay: NodeBase.powerDelay, powerClump: NodeBase.powerClump,
      fruitType: 'leaf', fruitChain: 1, maxFruits: 2, fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'lab', type: 'normal', color: Colors.Node.purple, shape: 'hexagon',
      radius: 15, mass: 2, force: 1.5, maxLinks: 1,
      powerMax: NodeBase.powerMax * 2, powerGen: NodeBase.powerDrain, powerWeight: NodeBase.powerWeight,
      powerDelay: NodeBase.powerDelay, powerClump: NodeBase.powerClumpSub,
      researchGen: 0.1,
      fruitType: 'research', fruitChain: 1, maxFruits: 3, fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'generator', type: 'normal', color: Colors.Node.yellow, shape: 'pentagon',
      radius: 15, mass: 3, force: 3, maxLinks: 1, maxCount: 2,
      powerMax: NodeBase.powerMax, powerGen: NodeBase.smallGen, powerWeight: NodeBase.powerWeight * 1.5,
      powerDelay: NodeBase.powerDelay, powerClump: NodeBase.powerClump,
      fruitType: 'burr', fruitChain: 1, maxFruits: 3, fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'home', type: 'normal', color: Colors.Node.blue, shape: 'circle',
      radius: 10, mass: 1, force: 1, maxLinks: 1,
      powerMax: NodeBase.powerMax, powerGen: NodeBase.powerDrain, powerWeight: NodeBase.powerWeight,
      powerDelay: NodeBase.powerDelay, powerClump: NodeBase.powerClumpSub,
      fruitType: 'food', fruitChain: 2, maxFruits: 3, fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'grove', type: 'normal', color: Colors.Node.orange, shape: 'square',
      radius: 10, mass: 1, force: 1, maxLinks: 1,
      fruitGen: 0.07,
      powerMax: NodeBase.powerMax, powerGen: NodeBase.powerDrain, powerWeight: NodeBase.powerWeight,
      powerDelay: NodeBase.powerDelay, powerClump: NodeBase.powerClumpSub,
      fruitType: 'gen', fruitChain: 1, maxFruits: 2, fruitClump: NodeBase.fruitClump,
    },
    // fruits \\
    {
      slug: 'battery', type: 'fruit', color: Colors.Node.darkyellow, shape: 'circle',
      radius: 5, mass: 0.5, force: 0.2, maxLinks: 0,
      powerMax: NodeBase.powerMax / 2, powerGen: 0.01, powerWeight: NodeBase.powerWeight / 2,
      powerDelay: NodeBase.powerDelay, powerClump: NodeBase.powerMax / 2,
      maxFruits: 1, fruitClump: NodeBase.fruitClump * 4,
    },
    {
      slug: 'leaf', type: 'fruit', color: Colors.Node.darkgreen, shape: 'triangle',
      radius: 5, mass: 0.5, force: 0.2, maxLinks: 0,
      powerMax: NodeBase.powerMax, powerGen: 0, powerWeight: NodeBase.powerWeight / 2,
      powerDelay: NodeBase.powerDelay, powerClump: 0,
      fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'food', type: 'fruit', color: Colors.Node.orange, shape: 'square',
      radius: 5, mass: 0.5, force: 0.2, maxLinks: 0,
      powerMax: NodeBase.powerMax, powerGen: 0, powerWeight: NodeBase.powerWeight / 2,
      powerDelay: NodeBase.powerDelay, powerClump: 0,
      fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'research', type: 'fruit', color: Colors.Node.purple, shape: 'pentagon',
      radius: 5, mass: 0.5, force: 0.2, maxLinks: 0,
      powerMax: NodeBase.powerMax, powerGen: 0, powerWeight: NodeBase.powerWeight / 2,
      powerDelay: NodeBase.powerDelay, powerClump: 0,
      fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'gen', type: 'fruit', color: Colors.Node.yellow, shape: 'triangle',
      radius: 5, mass: 0.5, force: 0.2, maxLinks: 0,
      powerMax: NodeBase.powerMax, powerGen: 0, powerWeight: NodeBase.powerWeight / 2,
      powerDelay: NodeBase.powerDelay, powerClump: 0,
      fruitClump: NodeBase.fruitClump,
    },
    {
      slug: 'burr', type: 'fruit', color: Colors.Node.red, shape: 'triangle',
      radius: 5, mass: 0.5, force: 0.2, maxLinks: 0,
      powerMax: NodeBase.powerMax, powerGen: 0, powerWeight: NodeBase.powerWeight / 2,
      powerDelay: NodeBase.powerDelay, powerClump: 0,
      fruitClump: NodeBase.fruitClump,
    },

    // {slug: 'enemy-core', type: 'normal', color: Colors.Node.red, shape: 'fat-rect', 
    //     radius: 20, mass: 20, force: 4, powerGen: 0.5,
    //     fruitType: 'big-evil', fruitChain: 3, maxFruits: 3, maxLinks: 6, powerMax: 300},
    // {slug: 'enemy-box', type: 'normal', color: Colors.Node.red, shape: 'thin-rect', 
    //     radius: 10, mass: 1, force: 1,
    //     fruitType: 'small-evil', fruitChain: 1, maxFruits: 5, maxLinks: 4, powerMax: 100},

    // {slug: 'big-evil', type: 'fruit', color: Colors.Node.red, shape: 'triangle', 
    // radius: 10, mass: 1, force: 3, maxFruits: 1, powerMax: 100, powerClump: 0},
    // {slug: 'small-evil', type: 'fruit', color: Colors.Node.red, shape: 'thin-rect', 
    // radius: 5, mass: 0.5, force: 0.2, maxFruits: 1, powerMax: 100, powerClump: 0},
  ]
}

export type NodeSlug = 'home' | 'lab' | 'generator' | 'grove' | 'stem' | 'core' | 'enemy-core' | 'enemy-box' |
  'food' | 'research' | 'battery' | 'gen' | 'burr' | 'big-evil' | 'small-evil' | 'leaf' | 'seedling';
export type NodeColor = 'blue' | 'purple' | 'yellow' | 'orange' | 'green' | 'yellow';
export type NodeShape = 'circle' | 'square' | 'triangle' | 'pentagon' | 'hexagon' | 'thin-rect' | 'fat-rect';

interface INodeData {
  Nodes: NodeConfig[];
}

export interface NodeConfig {
  slug: NodeSlug;
  type: 'normal' | 'fruit';
  color: number;
  shape: NodeShape;
  radius: number;
  mass: number;
  force: number;
  maxCount?: number;

  maxLinks: number;
  powerGen: number;
  powerMax: number;
  powerDelay: number;
  powerClump: number;
  powerWeight: number;

  fruitType?: NodeSlug;
  fruitChain?: number;
  maxFruits?: number;
  fruitClump?: number;

  researchGen?: number;
  fruitGen?: number;
}

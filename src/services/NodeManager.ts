import { NodeConfig, NodeData, NodeSlug } from "../data/NodeData";

export class NodeManager {
  public static getNodeConfig(slug: NodeSlug): NodeConfig {
    let raw = NodeData.Nodes.find(config => config.name === slug);

    return raw;
  }
}
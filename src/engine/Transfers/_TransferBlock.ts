import { PlantNode } from '../nodes/PlantNode';

export class TransferBlock {
  public type: 'grow' | 'fruit' | 'research';
  public amount: number;
  public fade: number;
  public removeOrigin?: boolean;

  constructor(private origin: PlantNode, private target: PlantNode) {

  }

  public executeStart() {

  }

  public executeComplete() {

  }
}

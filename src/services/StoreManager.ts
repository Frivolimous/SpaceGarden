import { SaveManager } from './SaveManager';

export const StoreManager = {
  // sellItem: (item: IItem, onComplete: (result: IPurchaseResult) => void) => {
  //   SaveManager.getExtrinsic().currency.gold += item.cost;
  //   onComplete({success: true});
  // },

  // purchaseAttempt: (value: number, currency: CurrencySlug, onComplete: (result: IPurchaseResult) => void) => {
  //   let current = SaveManager.getExtrinsic().currency[currency];
  //   if (current >= value) {
  //     if (value > Infinity) {
  //       onComplete({success: false, confirmation: () => {
  //         SaveManager.getExtrinsic().currency[currency] = current - value;
  //         onComplete({success: true});
  //       }});
  //     } else {
  //       SaveManager.getExtrinsic().currency[currency] = current - value;
  //       onComplete({success: true});
  //     }
  //   } else {
  //     onComplete({success: false});
  //   }
  // },
};

export interface IPurchaseResult {
  success: boolean;
  confirmation?: () => void;
  message?: string;
}

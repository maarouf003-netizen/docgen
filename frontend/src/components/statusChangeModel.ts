import { SUB_STATUS_FULL } from '../utils/documentStatus';

/** نمط التاريخ الحر المعتمد: «مثال: 1/8/2026» (يوم/شهر/سنة) لكل حقول التواريخ. */
export const DATE_PLACEHOLDER = 'مثال: 1/8/2026';

export const COLLECTED_AMOUNT_KEYS = ['collectedAmount', 'collectedAmount2', 'collectedAmount3'] as const;
export const COLLECTED_CURRENCY_KEYS = ['collectedCurrency', 'collectedCurrency2', 'collectedCurrency3'] as const;

export type StatusFields = {
  tarithNumber: string;
  tarithDate: string;
  tarithRegNumber: string;
  tarithRegDate: string;
  baraetNumber: string;
  baraetDate: string;
  baraetRegNumber: string;
  baraetRegDate: string;
  sayerNumber: string;
  sayerDate: string;
  sayerRegNumber: string;
  sayerRegDate: string;
  execSubStatus: string;
  forcedExecutionDate: string;
  forcedTransferDate: string;
  forcedTransferNoticeNumber: string;
  collectedAmount?: number;
  collectedAmount2?: number;
  collectedAmount3?: number;
  collectedCurrency: string;
  collectedCurrency2: string;
  collectedCurrency3: string;
  struckOffDate: string;
  soldAssetIds: number[];
  noFundsDemandNumber: string;
  noFundsDemandDate: string;
  startReferralNumber: string;
  startReferralDate: string;
  renewalFileNumber: string;
  renewalFileType: string;
  renewalDate: string;
  renewalYear: string;
};

export function emptyFields(): StatusFields {
  return {
    tarithNumber: '',
    tarithDate: '',
    tarithRegNumber: '',
    tarithRegDate: '',
    baraetNumber: '',
    baraetDate: '',
    baraetRegNumber: '',
    baraetRegDate: '',
    sayerNumber: '',
    sayerDate: '',
    sayerRegNumber: '',
    sayerRegDate: '',
    execSubStatus: SUB_STATUS_FULL,
    forcedExecutionDate: '',
    forcedTransferDate: '',
    forcedTransferNoticeNumber: '',
    collectedCurrency: 'ليرة سورية',
    collectedCurrency2: 'دولار أمريكي',
    collectedCurrency3: 'يورو',
    struckOffDate: '',
    soldAssetIds: [],
    noFundsDemandNumber: '',
    noFundsDemandDate: '',
    startReferralNumber: '',
    startReferralDate: '',
    renewalFileNumber: '',
    renewalFileType: '',
    renewalDate: '',
    renewalYear: '',
  };
}

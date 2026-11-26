/** مفاتيح المبالغ (حتى ثلاثة) بعملاتها في المواضع الثلاثة في النموذج. */
export const requiredAmountKeys = [
  'executedRequiredAmount',
  'executedRequiredAmount2',
  'executedRequiredAmount3',
] as const;
export const requiredCurrencyKeys = [
  'executedRequiredCurrency',
  'executedRequiredCurrency2',
  'executedRequiredCurrency3',
] as const;
/** مفاتيح المبالغ المدفوعة (حتى ثلاثة) بعملاتها في وضع «منفذ عليه»/«عرض وايداع». */
export const paidAmountKeys = [
  'executedPaidAmount',
  'executedPaidAmount2',
  'executedPaidAmount3',
] as const;
export const paidCurrencyKeys = [
  'executedPaidCurrency',
  'executedPaidCurrency2',
  'executedPaidCurrency3',
] as const;
export const bankingAmountKeys = [
  'amountNumeric',
  'amount2Numeric',
  'amount3Numeric',
] as const;
export const bankingCurrencyKeys = ['currency', 'currency2', 'currency3'] as const;
export const ordinaryAmountKeys = [
  'inclusionAmountNumeric',
  'inclusionAmount2Numeric',
  'inclusionAmount3Numeric',
] as const;
export const ordinaryCurrencyKeys = [
  'inclusionCurrency',
  'inclusionCurrency2',
  'inclusionCurrency3',
] as const;

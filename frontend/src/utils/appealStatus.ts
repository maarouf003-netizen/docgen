import type { AppealDirection, AppealOutcome, AppealStatus } from '../types';

/** المصدر الوحيد لتسميات وشارات حالات الاستئناف ونتائجه واتجاهاته في الواجهة. */

export const APPEAL_STATUS_PENDING: AppealStatus = 'pending';
export const APPEAL_STATUS_DECIDED: AppealStatus = 'decided';
export const APPEAL_STATUS_STRUCK_OFF: AppealStatus = 'struck-off';

export const APPEAL_OUTCOME_IN_FAVOR: AppealOutcome = 'in-favor';
export const APPEAL_OUTCOME_AGAINST: AppealOutcome = 'against';

export const APPEAL_DIRECTION_APPELLANTS: AppealDirection = 'appellants';
export const APPEAL_DIRECTION_AGAINST_US: AppealDirection = 'against-us';

/** التسمية العربية لحالة الاستئناف (الفارغة/غير المعروفة تُعامل منظورًا اتساقًا مع الافتراض الخلفي). */
export function appealStatusLabel(status?: string): string {
  switch (status) {
    case APPEAL_STATUS_PENDING:
      return 'منظور';
    case APPEAL_STATUS_DECIDED:
      return 'محسوم';
    case APPEAL_STATUS_STRUCK_OFF:
      return 'مشطوب';
    default:
      return 'منظور';
  }
}

export interface AppealBadge {
  text: string;
  cls: string;
}

/**
 * شارة حالة الاستئناف:
 * منظور حمراء (قيد النظر)، محسوم خضراء، مشطوب رمادية.
 */
export function appealStatusBadge(status?: string): AppealBadge {
  switch (status) {
    case APPEAL_STATUS_DECIDED:
      return { text: 'محسوم', cls: 'bg-green-100 text-green-700' };
    case APPEAL_STATUS_STRUCK_OFF:
      return { text: 'مشطوب', cls: 'bg-gray-200 text-gray-700' };
    default:
      return { text: 'منظور', cls: 'bg-red-100 text-red-800' };
  }
}

/** التسمية العربية لاتجاه الاستئناف. */
export function appealDirectionLabel(direction?: string): string {
  switch (direction) {
    case APPEAL_DIRECTION_AGAINST_US:
      return 'مستأنف علينا';
    default:
      return 'مستأنِفين';
  }
}

/** التسمية العربية لنتيجة الاستئناف المحسوم. */
export function appealOutcomeLabel(outcome?: string): string {
  switch (outcome) {
    case APPEAL_OUTCOME_IN_FAVOR:
      return 'للصالح';
    case APPEAL_OUTCOME_AGAINST:
      return 'للضد';
    default:
      return '—';
  }
}

/** صنف لون نتيجة الاستئناف: للصالح أخضر وللضد أحمر. */
export function appealOutcomeCls(outcome?: string): string {
  switch (outcome) {
    case APPEAL_OUTCOME_IN_FAVOR:
      return 'text-green-700 font-semibold';
    case APPEAL_OUTCOME_AGAINST:
      return 'text-red-700 font-semibold';
    default:
      return '';
  }
}

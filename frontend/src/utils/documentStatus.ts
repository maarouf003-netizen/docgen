import type { DocumentResponse } from '../types';
import { isExecutedLike } from './documentDisplay';

export type DocumentStatus = 'منفذ' | 'تريث' | 'تحت رفع' | 'متداول' | 'متداول / منفذ جزئيا' | 'مشطوب';

export const STATUS_OPTIONS: Exclude<DocumentStatus, 'متداول / منفذ جزئيا' | 'مشطوب'>[] = ['منفذ', 'تريث', 'تحت رفع', 'متداول'];

export const STATUS_BADGES: Record<DocumentStatus, { text: string; cls: string }> = {
  منفذ: { text: 'منفذ', cls: 'bg-green-100 text-green-700' },
  تريث: { text: 'تريث', cls: 'bg-red-100 text-red-700' },
  'تحت رفع': { text: 'تحت رفع', cls: 'bg-amber-100 text-amber-700' },
  متداول: { text: 'متداول', cls: 'bg-blue-100 text-blue-700' },
  'متداول / منفذ جزئيا': { text: 'متداول / منفذ جزئيا', cls: 'bg-cyan-100 text-cyan-700' },
  مشطوب: { text: 'مشطوب', cls: 'bg-gray-200 text-gray-700' },
};

export type StatusSource = Pick<
  DocumentResponse,
  'execStatus' | 'execSubStatus' | 'isDraft' | 'generalEntitySide' | 'executedStatus'
>;

/** حالة وضع «منفذ عليه»/«عرض وايداع» (متداول/منفذ/مشطوب)، معزولة تمامًا عن نظام «طالبة تنفيذ». */
export function getExecutedStatus(doc: StatusSource): DocumentStatus {
  if (doc.executedStatus === 'مشطوب') return 'مشطوب';
  if (doc.executedStatus === 'منفذ') return 'منفذ';
  return 'متداول';
}

export function getDocumentStatus(doc: StatusSource): DocumentStatus {
  if (isExecutedLike(doc.generalEntitySide)) return getExecutedStatus(doc);
  // «مشطوب» في نظام «طالبة تنفيذ» موحّد مع صفحة «الملفات المشطوبة».
  if (doc.execStatus === 'مشطوب') return 'مشطوب';
  if (doc.execStatus === 'تريث') return 'تريث';
  if (doc.execStatus === 'منفذ جبريا' && doc.execSubStatus === 'منفذ جزئيا') return 'متداول / منفذ جزئيا';
  if (doc.execStatus === 'منفذ جبريا' || doc.execStatus === 'منفذ بالتسوية') return 'منفذ';
  return doc.isDraft ? 'تحت رفع' : 'متداول';
}

export function getDocumentBadge(doc: StatusSource) {
  return STATUS_BADGES[getDocumentStatus(doc)];
}

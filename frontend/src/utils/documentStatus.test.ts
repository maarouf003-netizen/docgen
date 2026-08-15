import { describe, it, expect } from 'vitest';
import { getDocumentStatus, getDocumentBadge, getExecutedStatus } from './documentStatus';
import type { DocumentResponse } from '../types';

function doc(overrides: Partial<Pick<DocumentResponse, 'execStatus' | 'execSubStatus' | 'isDraft'>>) {
  return { execStatus: '', execSubStatus: '', isDraft: false, ...overrides };
}

describe('getDocumentStatus', () => {
  it('يرجّع الحالة التنفيذية الجديدة عند وجودها', () => {
    expect(getDocumentStatus(doc({ execStatus: 'منفذ بالتسوية', isDraft: true }))).toBe('منفذ');
    expect(getDocumentStatus(doc({ execStatus: 'منفذ جبريا', execSubStatus: 'منفذ كاملا' }))).toBe('منفذ');
    expect(getDocumentStatus(doc({ execStatus: 'منفذ جبريا', execSubStatus: 'منفذ جزئيا' }))).toBe('متداول / منفذ جزئيا');
    expect(getDocumentStatus(doc({ execStatus: 'تريث', isDraft: false }))).toBe('تريث');
  });

  it('يرجّع «تحت رفع» للمسودة و«متداول» للمتداول دون حالة تنفيذية', () => {
    expect(getDocumentStatus(doc({ isDraft: true }))).toBe('تحت رفع');
    expect(getDocumentStatus(doc({ isDraft: false }))).toBe('متداول');
  });

  it('يعطي كل حالة شارة العرض الصحيحة', () => {
    expect(getDocumentBadge(doc({ execStatus: 'منفذ بالتسوية' }))).toEqual({
      text: 'منفذ',
      cls: 'bg-green-100 text-green-700',
    });
    expect(getDocumentBadge(doc({ execStatus: 'منفذ جبريا', execSubStatus: 'منفذ جزئيا' }))).toEqual({
      text: 'متداول / منفذ جزئيا',
      cls: 'bg-cyan-100 text-cyan-700',
    });
    expect(getDocumentBadge(doc({ isDraft: true }))).toEqual({
      text: 'تحت رفع',
      cls: 'bg-amber-100 text-amber-700',
    });
    expect(getDocumentBadge(doc({ isDraft: false }))).toEqual({
      text: 'متداول',
      cls: 'bg-blue-100 text-blue-700',
    });
  });
});

describe('getExecutedStatus', () => {
  const executed = (executedStatus: string) => ({
    execStatus: '',
    execSubStatus: '',
    isDraft: false,
    generalEntitySide: 'executed' as const,
    executedStatus,
  });

  it('يعزل حالة وضع «منفذ عليه» عن نظام «طالبة تنفيذ»', () => {
    expect(getExecutedStatus(executed(''))).toBe('متداول');
    expect(getExecutedStatus(executed('منفذ'))).toBe('منفذ');
    expect(getExecutedStatus(executed('مشطوب'))).toBe('مشطوب');
  });

  it('getDocumentStatus يعالج ملفات الصفة executed بحالتها المعزولة حتى لو حملت execStatus قديمًا', () => {
    expect(
      getDocumentStatus({ ...executed('مشطوب'), execStatus: 'منفذ بالتسوية', isDraft: true }),
    ).toBe('مشطوب');
    expect(
      getDocumentStatus({ ...executed(''), execStatus: 'تريث', isDraft: true }),
    ).toBe('متداول');
  });

  it('getDocumentStatus يعالج ملفات صفة «عرض وايداع» بحالتها المعزولة مثل executed', () => {
    expect(
      getDocumentStatus({
        ...executed('منفذ'),
        generalEntitySide: 'deposit' as const,
      }),
    ).toBe('منفذ');
    expect(
      getDocumentStatus({
        ...executed(''),
        generalEntitySide: 'deposit' as const,
        execStatus: 'تريث',
        isDraft: true,
      }),
    ).toBe('متداول');
  });

  it('يعطي شارة العرض الصحيحة للمشطوب', () => {
    expect(getDocumentBadge(executed('مشطوب'))).toEqual({
      text: 'مشطوب',
      cls: 'bg-gray-200 text-gray-700',
    });
  });
});

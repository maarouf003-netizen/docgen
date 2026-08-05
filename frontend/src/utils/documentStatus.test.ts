import { describe, it, expect } from 'vitest';
import { getDocumentStatus, getDocumentBadge } from './documentStatus';
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

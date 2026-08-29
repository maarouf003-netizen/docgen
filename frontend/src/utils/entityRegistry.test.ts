import { describe, it, expect } from 'vitest';
import {
  entityTypeLabel,
  citationFormulaLabel,
  isEntryPendingReview,
  publicEntityStatusLabel,
} from './entityRegistry';

describe('entityRegistry catalogs', () => {
  it('يعرض تسميات أنواع الجهات الخمسة', () => {
    expect(entityTypeLabel('ministry')).toBe('وزارة');
    expect(entityTypeLabel('administration')).toBe('إدارة');
    expect(entityTypeLabel('authority')).toBe('هيئة');
    expect(entityTypeLabel('foundation')).toBe('مؤسسة');
    expect(entityTypeLabel('company')).toBe('شركة');
  });

  it('يرجع القيمة نفسها للنوع غير المعروف أو الفارغ', () => {
    expect(entityTypeLabel('unknown')).toBe('unknown');
    expect(entityTypeLabel(null)).toBe('');
    expect(citationFormulaLabel(undefined)).toBe('');
  });

  it('يعرض صيغ المناداة (د8)', () => {
    expect(citationFormulaLabel('add-to-job')).toBe('إضافة لوظيفته');
    expect(citationFormulaLabel('add-to-position')).toBe('إضافة لمنصبه');
  });

  it('يعرض حالات القيد بالعربية', () => {
    expect(publicEntityStatusLabel('final')).toBe('نهائي');
    expect(publicEntityStatusLabel('pending')).toBe('بانتظار المراجعة');
    expect(publicEntityStatusLabel('other')).toBe('other');
  });

  it('يحدد قيد بانتظار المراجعة (status=pending أو needsReview=true)', () => {
    expect(isEntryPendingReview({ status: 'pending' })).toBe(true);
    expect(isEntryPendingReview({ status: 'final', needsReview: true })).toBe(true);
    expect(isEntryPendingReview({ status: 'final', needsReview: false })).toBe(false);
    expect(isEntryPendingReview({ status: 'final' })).toBe(false);
    expect(isEntryPendingReview({ status: null, needsReview: null })).toBe(false);
  });
});

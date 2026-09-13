import { describe, it, expect } from 'vitest';
import {
  entityTypeLabel,
  ENTITY_TYPE_OPTIONS,
  citationFormulaLabel,
  isEntryPendingReview,
  publicEntityStatusLabel,
} from './entityRegistry';

describe('entityRegistry catalogs', () => {
  it('يعرض تسميات أنواع الجهات الأحد عشر', () => {
    expect(entityTypeLabel('ministry')).toBe('وزارة');
    expect(entityTypeLabel('administration')).toBe('إدارة عامة');
    expect(entityTypeLabel('authority')).toBe('هيئة عامة');
    expect(entityTypeLabel('foundation')).toBe('مؤسسة عامة');
    expect(entityTypeLabel('company')).toBe('شركة عامة');
    expect(entityTypeLabel('directorate')).toBe('مديرية');
    expect(entityTypeLabel('sub-administration')).toBe('إدارة فرعية');
    expect(entityTypeLabel('general-secretariat')).toBe('أمانة عامة');
    expect(entityTypeLabel('governorate-body')).toBe('محافظة');
    expect(entityTypeLabel('city-council')).toBe('مجلس مدينة');
    expect(entityTypeLabel('town-council')).toBe('مجلس بلدة');
  });

  it('يوفر خيارات نوع الجهة الـ11 بالترتيب المعتمد للعرض', () => {
    expect(ENTITY_TYPE_OPTIONS).toHaveLength(11);
    expect(ENTITY_TYPE_OPTIONS.map((o) => o.value)).toEqual([
      'foundation',
      'company',
      'directorate',
      'administration',
      'sub-administration',
      'authority',
      'general-secretariat',
      'governorate-body',
      'city-council',
      'town-council',
      'ministry',
    ]);
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

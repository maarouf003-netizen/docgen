import { describe, expect, it } from 'vitest';
import { ALERTS_PAGE_SIZE, readAlertsPage } from './alerts';
import { ContractError } from './contractError';
import { makeHeadAlertSummary } from '../test/factories';

/**
 * حارس فكّ `PagedResult` في قوائم التنبيهات (`D10=atomic`): الشكل السليم يُمرَّر،
 * وأي مخالفة عقد **تُرمى** بـ`ContractError` صريح — لا تُترجم صفحة فارغة كاذبة —
 * و`totalPages` تُحسب محليًا لا تُقرأ من السلك.
 */
describe('readAlertsPage', () => {
  it('يمرّر `PagedResult` السليم كما هو', () => {
    const items = [makeHeadAlertSummary({ id: 1 }), makeHeadAlertSummary({ id: 2 })];

    expect(readAlertsPage({ items, page: 2, perPage: 100, totalCount: 250, totalPages: 3 })).toEqual({
      items,
      page: 2,
      perPage: 100,
      totalCount: 250,
      totalPages: 3,
    });
  });

  it('يطرح كل جسم مخالف — لا صفحة فارغة صامتة', () => {
    const bodies: unknown[] = [
      undefined,
      null,
      'نص',
      42,
      // الشكل القديم (مصفوفة مجرّدة) مرفوض عمدًا بقرار `D10=atomic`.
      [{ id: 1 }],
      { items: 'ليست مصفوفة' },
      { items: [] },
      { items: [], totalCount: 0 },
      { items: [], page: 1, totalCount: 0 },
      { items: [], page: 0, perPage: 100, totalCount: 0 },
      { items: [], page: 1, perPage: 0, totalCount: 0 },
      { items: [], page: 1, perPage: 100, totalCount: -3 },
      { items: [], page: 1, perPage: 100, totalCount: 1.5 },
      { items: [], page: 1, perPage: 100, totalCount: '7' },
      { items: [], page: 1, perPage: 100, totalCount: Number.NaN },
    ];

    for (const body of bodies) {
      expect(() => readAlertsPage(body)).toThrow(ContractError);
    }
  });

  it('رسالة المخالفة عربية صريحة وقابلة للعرض', () => {
    expect(() => readAlertsPage({ items: 'ليست مصفوفة' })).toThrow(
      'تعذّر قراءة قائمة التنبيهات — أعِد تحميل الصفحة، وإن استمر الخطأ فأبلغ الدعم',
    );
  });

  it('قائمة فارغة مشروعة (لا استثناء) وحجم الصفحة الافتراضي محفوظ', () => {
    expect(readAlertsPage({ items: [], page: 1, perPage: ALERTS_PAGE_SIZE, totalCount: 0 })).toEqual({
      items: [],
      page: 1,
      perPage: ALERTS_PAGE_SIZE,
      totalCount: 0,
      totalPages: 0,
    });
  });

  it('يحسب `totalPages` من الإجمالي ولا يثق بقيمة السلك', () => {
    const items = [makeHeadAlertSummary()];

    expect(readAlertsPage({ items, page: 1, perPage: 100, totalCount: 250, totalPages: 1 }).totalPages).toBe(3);
    expect(readAlertsPage({ items, page: 1, perPage: 100, totalCount: 0 }).totalPages).toBe(0);
  });
});

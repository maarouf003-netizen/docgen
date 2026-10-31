import { describe, expect, it } from 'vitest';
import { ALERTS_PAGE_SIZE, readAlertsPage } from './alerts';
import { makeHeadAlertSummary } from '../test/factories';

/**
 * حارس فكّ `PagedResult` في قوائم التنبيهات: الشكل السليم يُمرَّر كما هو، وأي جسم
 * مخالف يعطي صفحة فارغة صريحة بدل انهيار `items.map` — ولا مؤشر «المزيد» بلا أساس.
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

  it('أي جسم غير مطابق أو `items` غير مصفوفة ⇒ صفحة فارغة (لا استثناء)', () => {
    expect(readAlertsPage(undefined).items).toEqual([]);
    expect(readAlertsPage(null).items).toEqual([]);
    expect(readAlertsPage({ items: 'ليست مصفوفة' }).items).toEqual([]);
    // الشكل القديم (مصفوفة مجرّدة) مرفوض عمدًا بقرار `D10=atomic`.
    expect(readAlertsPage([{ id: 1 }]).items).toEqual([]);
  });

  it('يُسقط `totalCount` إلى عدد العناصر ويكمل القيم الناقصة/الفاسدة بالافتراضيات', () => {
    const items = [makeHeadAlertSummary()];

    const page = readAlertsPage({ items, totalCount: -3, perPage: 0, page: 0 });
    expect(page.totalCount).toBe(items.length);
    expect(page.perPage).toBe(ALERTS_PAGE_SIZE);
    expect(page.page).toBe(1);
    expect(page.totalPages).toBe(1);
  });

  it('يحسب `totalPages` من الإجمالي عند غيابها', () => {
    const items = [makeHeadAlertSummary()];

    expect(readAlertsPage({ items, totalCount: 250 }).totalPages).toBe(3);
    expect(readAlertsPage({ items, totalCount: 0 }).totalPages).toBe(0);
  });
});

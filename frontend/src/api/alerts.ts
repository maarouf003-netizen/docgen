import type { HeadAlertSummaryDto, PagedResult } from '../types';

/**
 * حدّ صفحة قوائم التنبيهات: الخادم يقبل `1..100` (`AlertPageRequest.MaxPageSize`).
 * يُمرَّر صراحةً في كل استدعاء قائمة لأن الافتراضي الخلفي `50` كان يقصّ القائمة
 * صامتًا بعد تحويل الأسطح إلى `PagedResult` (الخطة §4.2 البند 16).
 */
export const ALERTS_PAGE_SIZE = 100;

function readCount(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : fallback;
}

function readPositive(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) && value >= 1 ? Math.floor(value) : fallback;
}

/**
 * يفكّ `PagedResult<HeadAlertSummaryDto>` القادم من السلك بحارس مصفوفة صريح.
 *
 * الشكل الوحيد المقبول هو `{ items, page, perPage, totalCount, totalPages }`
 * (قرار `D10=atomic`: لا طبقة توافق للشكل القديم). أي جسم غير مطابق — أو `items`
 * غير مصفوفة — يعطي صفحة فارغة صريحة بدل انهيار `items.map` في العرض، و`totalCount`
 * يسقط إلى عدد العناصر الفعلي كي لا يُعرض مؤشر «المزيد» بلا أساس.
 */
export function readAlertsPage(data: unknown): PagedResult<HeadAlertSummaryDto> {
  const body = (data ?? {}) as Partial<Record<keyof PagedResult<HeadAlertSummaryDto>, unknown>>;
  const items = Array.isArray(body.items) ? (body.items as HeadAlertSummaryDto[]) : [];
  const totalCount = readCount(body.totalCount, items.length);
  const perPage = readPositive(body.perPage, ALERTS_PAGE_SIZE);
  return {
    items,
    page: readPositive(body.page, 1),
    perPage,
    totalCount,
    totalPages: readCount(body.totalPages, Math.ceil(totalCount / perPage)),
  };
}

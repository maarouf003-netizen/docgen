import type { HeadAlertDto, HeadAlertRecipientDto, HeadAlertSummaryDto, PagedResult } from '../types';
import { ContractError } from './contractError';

/**
 * حدّ صفحة قوائم التنبيهات: الخادم يقبل `1..100` (`AlertPageRequest.MaxPageSize`).
 * يُمرَّر صراحةً في كل استدعاء قائمة لأن الافتراضي الخلفي `50` كان يقصّ القائمة
 * صامتًا بعد تحويل الأسطح إلى `PagedResult` (الخطة §4.2 البند 16).
 */
export const ALERTS_PAGE_SIZE = 100;

/**
 * رسالة مخالفة عقد القوائم. صياغتها للعرض المباشر: تُثبت للمستخدم أن الخلل في
 * القراءة لا في وجود تنبيهات، وتدعوه لمحاولة واحدة ثم الإبلاغ — بدل «حدث خطأ غير
 * متوقع» المبهم أو، أسوأ، صفحة فارغة تُقرأ «لا توجد تنبيهات».
 */
const CONTRACT_ERROR_MESSAGE =
  'تعذّر قراءة قائمة التنبيهات — أعِد تحميل الصفحة، وإن استمر الخطأ فأبلغ الدعم';

/** عدد صحيح غير سالب في السلك (`totalCount`). */
function isNonNegativeInt(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0;
}

/** عدد صحيح موجب في السلك (`page`/`perPage`). */
function isPositiveInt(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 1;
}

/**
 * يفكّ `PagedResult<HeadAlertSummaryDto>` القادم من السلك بحارس شكل صريح.
 *
 * الشكل الوحيد المقبول هو `{ items, page, perPage, totalCount }` (قرار `D10=atomic`:
 * لا طبقة توافق للشكل القديم). أي جسم مخالف — جسم ليس كائنًا، أو `items` غير مصفوفة
 * (ومنها الشكل القديم: مصفوفة مجرّدة)، أو عدّاد مفقود/فاسد — **يُرمى** بـ`ContractError`
 * صريح فيصل إلى مسار الخطأ القائم (لافتة + إعادة محاولة).
 *
 * سابقًا كانت المخالفة تُترجم صفحةً فارغة صامتة، وهو أسوأ من الخطأ: يُخفي عطلًا
 * سلكيًا (نحو نشر خلفي بلا نشر واجهي) ويُظهر «لا توجد تنبيهات» كاذبة. والمخالفة
 * ليست حالة بيانات (قائمة فارغة مشروعة) بل خرق عقد.
 *
 * `totalPages` **لا تُقرأ من السلك**: قيمة مشتقة من `totalCount`/`perPage`، فتُحسب
 * هنا لتبقى صحيحة ولو أرسلها الخادم قديمة.
 */
export function readAlertsPage(data: unknown): PagedResult<HeadAlertSummaryDto> {
  const body = (typeof data === 'object' && data !== null ? data : {}) as Record<string, unknown>;
  const { items, page, perPage, totalCount } = body;
  if (
    !Array.isArray(items) ||
    !isPositiveInt(page) ||
    !isPositiveInt(perPage) ||
    !isNonNegativeInt(totalCount)
  ) {
    throw new ContractError(CONTRACT_ERROR_MESSAGE);
  }
  return {
    items: items as HeadAlertSummaryDto[],
    page,
    perPage,
    totalCount,
    totalPages: Math.ceil(totalCount / perPage),
  };
}

/**
 * رسالة مخالفة عقد التفاصيل/المستلمين — صياغة عرض مباشرة مثل رسالة القوائم:
 * العطل في القراءة لا في وجود البيانات، والمستخدم يُدعى لمحاولة واحدة ثم الإبلاغ.
 */
const DETAIL_CONTRACT_ERROR_MESSAGE =
  'تعذّر قراءة نص التنبيه — أعِد المحاولة، وإن استمر الخطأ فأبلغ الدعم';
const RECIPIENTS_CONTRACT_ERROR_MESSAGE =
  'تعذّر قراءة قائمة المستلمين — أعِد المحاولة، وإن استمر الخطأ فأبلغ الدعم';

/**
 * يفكّ `HeadAlertDto` القادم من `GET /alerts/{id}` بحارس شكل أدنى: كائن يحمل
 * `id` صحيحًا و`message` نصًا و`kind` نصًا. لا نُعيد بناء العقد كله هنا (الأنواع
 * تحرسه وقت الترجمة عبر `contract_sync_alerts.test.ts`)، لكن جسمًا غير متوقع
 * (صفحة HTML، `null`، جسم قائمة) يجب أن يُعلن لا أن يُعرض كنص فارغ.
 */
export function readAlertDetail(data: unknown): HeadAlertDto {
  const body = (typeof data === 'object' && data !== null ? data : {}) as Record<string, unknown>;
  if (
    typeof body.id !== 'number' ||
    typeof body.message !== 'string' ||
    typeof body.kind !== 'string'
  ) {
    throw new ContractError(DETAIL_CONTRACT_ERROR_MESSAGE);
  }
  return data as HeadAlertDto;
}

/** يفكّ مستلمي التنبيه — المصفوفة هي الشكل الوحيد المقبول، وغيره خرق عقد. */
export function readAlertRecipients(data: unknown): HeadAlertRecipientDto[] {
  if (!Array.isArray(data)) throw new ContractError(RECIPIENTS_CONTRACT_ERROR_MESSAGE);
  return data as HeadAlertRecipientDto[];
}

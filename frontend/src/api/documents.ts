import type { DocumentListItemDto, PagedResult } from '../types';
import { ContractError } from './contractError';

/**
 * رسالة مخالفة عقد قائمة المستندات — للعرض المباشر، وتقول إن الخلل في القراءة
 * لا في وجود ملفات (بدل «لا توجد نتائج» كاذبة أو انفجار `render`).
 */
export const DOCUMENT_LIST_CONTRACT_ERROR =
  'تعذّر قراءة قائمة الملفات — أعِد تحميل الصفحة، وإن استمر الخطأ فأبلغ الدعم';

function isNonNegativeInt(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0;
}

function isPositiveInt(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 1;
}

/**
 * يفكّ `PagedResult<DocumentListItemDto>` القادم من `/documents/list` بحارس شكل
 * صريح (نمط `readAlertsPage`): جسم غير كائن، أو `items` غير مصفوفة، أو عدّاد
 * مفقود/فاسد، أو `totalPages` لا يطابق `ceil(totalCount / perPage)` (قيمة الخادم
 * هي المرجع، ولا تُعاد حسابها صامتة)، أو `nextCursor` غير نصي/`null` ⇒
 * `ContractError` يصل إلى مسار الخطأ القائم (لافتة + إعادة محاولة) بدل قيمة
 * ناقصة تُعرض كصفحة فارغة أو ترقيم خاطئ.
 */
export function readDocumentListPage(data: unknown): PagedResult<DocumentListItemDto> {
  const body = (typeof data === 'object' && data !== null ? data : {}) as Record<string, unknown>;
  const { items, page, perPage, totalCount, totalPages, nextCursor } = body;
  if (
    !Array.isArray(items)
    || !isPositiveInt(page)
    || !isPositiveInt(perPage)
    || !isNonNegativeInt(totalCount)
    || !isNonNegativeInt(totalPages)
    || totalPages !== Math.ceil(totalCount / perPage)
    || !(nextCursor == null || (typeof nextCursor === 'string' && nextCursor.trim().length > 0))
  ) {
    throw new ContractError(DOCUMENT_LIST_CONTRACT_ERROR);
  }
  return {
    items: items as DocumentListItemDto[],
    page,
    perPage,
    totalCount,
    totalPages,
    ...(nextCursor == null ? {} : { nextCursor }),
  };
}

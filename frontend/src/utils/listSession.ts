/** موضع قائمة الملفات التنفيذية المحفوظ في الجلسة لاستعادته عند العودة من صفحة ملف. */
export interface DocumentsListPosition {
  query: string;
  status: string;
  applicant: string;
  court: string;
  lawyer: string;
  administrativeBranch: string;
  executedEntity: string;
  publicEntityBranch: string;
  page: number;
}

const POSITION_KEY = 'documentsListPosition';
const FOCUS_KEY = 'lastViewedDocument';
const LEGACY_FOCUS_KEY = 'lastViewedDocumentId';
const FOCUS_RECORD_VERSION = 2;

/** سجل «آخر ملف فُتح» موقّع بالمستخدم؛ لا يُقرأ سجل مستخدم آخر. */
interface LastViewedDocumentRecord {
  version: number;
  userId: number;
  documentId: number;
}

/** قراءة موضع القائمة المحفوظ (آمنة؛ تُعيد null عند غيابه أو تلفه أو حظر الجلسة). */
export function loadDocumentsListPosition(): DocumentsListPosition | null {
  try {
    const raw = sessionStorage.getItem(POSITION_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<DocumentsListPosition>;
    return {
      query: typeof parsed.query === 'string' ? parsed.query : '',
      status: typeof parsed.status === 'string' ? parsed.status : '',
      applicant: typeof parsed.applicant === 'string' ? parsed.applicant : '',
      court: typeof parsed.court === 'string' ? parsed.court : '',
      lawyer: typeof parsed.lawyer === 'string' ? parsed.lawyer : '',
      administrativeBranch: typeof parsed.administrativeBranch === 'string' ? parsed.administrativeBranch : '',
      executedEntity: typeof parsed.executedEntity === 'string' ? parsed.executedEntity : '',
      publicEntityBranch: typeof parsed.publicEntityBranch === 'string' ? parsed.publicEntityBranch : '',
      page: typeof parsed.page === 'number' && parsed.page > 0 ? parsed.page : 1,
    };
  } catch {
    return null;
  }
}

/** حفظ موضع القائمة الحالي في الجلسة (آمن ضد فشل الجلسة). */
export function saveDocumentsListPosition(position: DocumentsListPosition) {
  try {
    sessionStorage.setItem(POSITION_KEY, JSON.stringify(position));
  } catch {
    // الجلسة محجوبة (تصفح خاص مقيد) — لا يهم فشل الحفظ.
  }
}

/**
 * آخر ملف فُتح في هذه الجلسة (لتسليط الضوء عليه عند العودة إلى القائمة).
 * سجل موقّع بالمستخدم: سجل مستخدم آخر يُتجاهل ويُعيد null بلا حذف (القراءة بلا أثر جانبي)،
 * والقيم التالفة تُعيد null كما يفعل loadDocumentsListPosition. المفتاح القديم الرقمي
 * (lastViewedDocumentId) لم يعد يُقرأ بعد التحول إلى السجل.
 */
export function loadLastViewedDocumentId(userId: number | null): number | null {
  if (userId == null) return null;
  try {
    const raw = sessionStorage.getItem(FOCUS_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<LastViewedDocumentRecord>;
    const shapeValid =
      parsed != null &&
      typeof parsed === 'object' &&
      parsed.version === FOCUS_RECORD_VERSION &&
      typeof parsed.userId === 'number' &&
      Number.isInteger(parsed.userId) &&
      parsed.userId > 0 &&
      typeof parsed.documentId === 'number' &&
      Number.isInteger(parsed.documentId) &&
      parsed.documentId > 0;
    if (!shapeValid) return null;
    const record = parsed as LastViewedDocumentRecord;
    if (record.userId !== userId) return null; // سجل مستخدم آخر: تجاهل بلا حذف
    return record.documentId;
  } catch {
    return null;
  }
}

export function saveLastViewedDocumentId(id: number | null, userId: number | null) {
  try {
    // القيم غير الصالحة (null أو NaN من معرف غير رقمي) أو غياب هوية المالك تُمسح من الجلسة
    // بدل تخزين سجل بلا مالك؛ وعند الكتابة الناجحة يُنظَّف المفتاح القديم الرقمي لمرة واحدة.
    if (id == null || typeof id !== 'number' || !Number.isFinite(id) || userId == null) sessionStorage.removeItem(FOCUS_KEY);
    else {
      sessionStorage.setItem(
        FOCUS_KEY,
        JSON.stringify({ version: FOCUS_RECORD_VERSION, userId, documentId: id } satisfies LastViewedDocumentRecord),
      );
      sessionStorage.removeItem(LEGACY_FOCUS_KEY);
    }
  } catch {
    // تجاهل فشل الجلسة.
  }
}

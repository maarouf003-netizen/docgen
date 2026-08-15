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
const FOCUS_KEY = 'lastViewedDocumentId';

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

/** آخر ملف فُتح في هذه الجلسة (لتسليط الضوء عليه عند العودة إلى القائمة). */
export function loadLastViewedDocumentId(): number | null {
  try {
    const raw = sessionStorage.getItem(FOCUS_KEY);
    const id = raw ? Number(raw) : NaN;
    return Number.isFinite(id) && id > 0 ? id : null;
  } catch {
    return null;
  }
}

export function saveLastViewedDocumentId(id: number | null) {
  try {
    // القيم غير الصالحة (null أو NaN من معرف غير رقمي) تُمسح من الجلسة بدل تخزين قمامة.
    if (id == null || !Number.isFinite(id)) sessionStorage.removeItem(FOCUS_KEY);
    else sessionStorage.setItem(FOCUS_KEY, String(id));
  } catch {
    // تجاهل فشل الجلسة.
  }
}

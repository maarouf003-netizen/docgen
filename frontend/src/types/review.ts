/* ── كتب المطالعة ─────────────────────────────────────────────────────── */

export type ReviewLetterMessageKind = 'letter' | 'addendum' | 'reply';

/** سياق الملف المرتبط بصيغة العرض: مطالعة بملف (الاسم الثلاثي) رقم.. نوع.. لعام.. دائرة تنفيذ.. */
export interface ReviewLetterFileContext {
  executedName: string;
  fileNumber?: string | null;
  fileType?: string | null;
  fileYear?: string | null;
  court?: string | null;
}

/** رسالة واحدة ضمن كتاب المطالعة (الأصل letter أو لاحق addendum أو رد reply). */
export interface ReviewLetterMessageDto {
  id: number;
  kind: ReviewLetterMessageKind;
  bodyHtml: string;
  messageNumber: string;
  messageDate: string;
  authorId: number;
  authorName: string;
  authorRole: 'lawyer' | 'head' | 'subhead';
}

/** سطر كتاب في القائمة؛ fileContext فارغ للكتاب العام غير المرتبط بملف. */
export interface ReviewLetterListItemDto {
  id: number;
  letterNumber: string;
  letterDate: string;
  isAnswered: boolean;
  documentId?: number | null;
  fileContext?: ReviewLetterFileContext | null;
  lawyerName: string;
  snippet: string;
  lastKind: ReviewLetterMessageKind;
  /** فيه ردّ رئيس قسم لم يطّلع عليه محامي الكتاب بعد. */
  hasUnseenReply: boolean;
  messagesCount: number;
  /** اسم فرع الإدارة — يُعرض للمدير/المشرف فقط. */
  administrativeBranchName?: string | null;
  updatedAt: string;
  /** الشعبة المستلمة (لبلا ملف — غائب يعني رئيس القسم؛ مع ملف مشتقة من دائرته). */
  recipientSectionId?: number | null;
  /** اسم الشعبة المستلمة — للعرض فقط. */
  recipientSectionName?: string | null;
}

export interface ReviewLetterDto {
  id: number;
  letterNumber: string;
  letterDate: string;
  isAnswered: boolean;
  documentId?: number | null;
  fileContext?: ReviewLetterFileContext | null;
  branchId: number;
  /** اسم فرع الإدارة — يُعرض للمدير/المشرف فقط. */
  administrativeBranchName?: string | null;
  lawyerName: string;
  /** فيه ردّ لم يُطَّلع بعد — يُعلَّم مقروءًا تلقائيًا عند فتح المحامي للصفحة. */
  hasUnseenReply?: boolean;
  messages: ReviewLetterMessageDto[];
  createdAt: string;
  /** الشعبة المستلمة (لبلا ملف — غائب يعني رئيس القسم؛ مع ملف مشتقة من دائرته). */
  recipientSectionId?: number | null;
  /** اسم الشعبة المستلمة — للعرض فقط. */
  recipientSectionName?: string | null;
}

export interface CreateReviewLetterRequest {
  documentId?: number | null;
  bodyHtml: string;
  /** اختيار المستلم لبلا ملف (شعبة) — غائب يعني رئيس القسم؛ مع ملف يُتجاهل. */
  recipientSectionId?: number | null;
}

export interface AddReviewLetterAddendumRequest {
  bodyHtml: string;
}

export interface ReplyReviewLetterRequest {
  bodyHtml: string;
}

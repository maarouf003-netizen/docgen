/* ── المراسلات ─────────────────────────────────────────────────────────── */

export type CorrespondenceMessageKind = 'letter' | 'addendum' | 'reply';

export type CorrespondenceImportance = 'normal' | 'important' | 'urgent';

/** حالة اطلاع الطرف المستلم كما يقرّرها الخادم — لا تُشتقّ في الواجهة. */
export type CorrespondenceViewStatus = 'seen' | 'pending';

/** سياق الملف المرتبط بصيغة العرض: مراسلة بملف (الاسم الثلاثي) رقم.. نوع.. لعام.. دائرة تنفيذ.. */
export interface CorrespondenceFileContext {
  executedName: string;
  fileNumber: string | null;
  fileType: string | null;
  fileYear: string | null;
  court: string | null;
}

/** رسالة واحدة ضمن مراسلة (الأصل letter أو لاحق addendum أو رد reply) — الكاتب قد يكون رئيس شعبة كمستلم. */
export interface CorrespondenceMessageDto {
  id: number;
  kind: CorrespondenceMessageKind;
  bodyHtml: string;
  messageNumber: string;
  messageDate: string;
  authorId: number;
  authorName: string;
  authorRole: 'lawyer' | 'head' | 'subhead' | 'entitymanager';
}

/** توثيق مشاهدة واحدة: من شاهد ومتى. */
export interface CorrespondenceReceiptDto {
  userId: number;
  userName: string;
  seenAt: string;
}

/** سطر مراسلة في القائمة؛ fileContext فارغ للمراسلة العامة غير المرتبطة بملف. */
export interface CorrespondenceListItemDto {
  id: number;
  correspondenceNumber: string;
  correspondenceDate: string;
  importance: CorrespondenceImportance;
  documentId: number | null;
  fileContext: CorrespondenceFileContext | null;
  creatorName: string;
  targetName: string;
  snippet: string;
  lastKind: CorrespondenceMessageKind;
  /** حالة اطلاع الطرف المستلم: 'seen' أو 'pending'. */
  viewStatus: CorrespondenceViewStatus;
  /** هل لهذا القارئ حق التوثيق؟ (المستلم وحده). */
  canMarkSeen: boolean;
  /** هل لهذا القارئ حق الرد؟ (المستلم وحده — من الخادم). */
  canReply: boolean;
  messagesCount: number;
  /** اسم فرع الإدارة — يُعرض للمدير/المشرف فقط. */
  administrativeBranchName: string | null;
  governorate: string;
  updatedAt: string;
}

export interface CorrespondenceDto {
  id: number;
  correspondenceNumber: string;
  correspondenceDate: string;
  importance: CorrespondenceImportance;
  documentId: number | null;
  fileContext: CorrespondenceFileContext | null;
  branchId: number | null;
  governorate: string;
  /** اسم فرع الإدارة — يُعرض للمدير/المشرف فقط. */
  administrativeBranchName: string | null;
  creatorId: number;
  creatorName: string;
  creatorRole: string;
  targetUserId: number;
  targetName: string;
  targetRole: string;
  /** حالة اطلاع الطرف المستلم: 'seen' أو 'pending'. */
  viewStatus: CorrespondenceViewStatus;
  /** هل لهذا القارئ حق التوثيق؟ (المستلم وحده — من الخادم). */
  canMarkSeen: boolean;
  /** هل لهذا القارئ حق الرد؟ (المستلم وحده — من الخادم). */
  canReply: boolean;
  messages: CorrespondenceMessageDto[];
  /** توثيق مشاهدة المستلم وحده. */
  receipts: CorrespondenceReceiptDto[];
  createdAt: string;
  /** الشعبة المجمدة للمستلم الرئيس (§10.2) — غائب لغيره. */
  recipientSectionId?: number | null;
}

export interface CreateCorrespondenceRequest {
  documentId?: number | null;
  targetUserId: number;
  importance: CorrespondenceImportance;
  bodyHtml: string;
  /** عقد المستلم الرئيس (§10.2): شعبة مجمدة — غائب يعني رئيس القسم؛ لغير الرئيس تُرفض. */
  recipientSectionId?: number | null;
}

/** مستلم مرشح لمراسلة جديدة (بحث بالاسم). */
export interface CorrespondenceTargetDto {
  userId: number;
  fullName: string;
  role: string;
  branchName?: string | null;
  governorate?: string | null;
}

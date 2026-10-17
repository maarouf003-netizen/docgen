/* ── السجل المرجعي للجهات العامة (بوابة الجهات — المرحلة 1) ───────────── */

/** نوع الجهة (كتالوج الأحد عشر نوعًا المعتمد). */
export type PublicEntityType =
  | 'ministry'
  | 'administration'
  | 'authority'
  | 'foundation'
  | 'company'
  | 'directorate'
  | 'sub-administration'
  | 'general-secretariat'
  | 'governorate-body'
  | 'city-council'
  | 'town-council';

/** حالة قيد الجهة: نهائي (يظهر للمندوبين) أو بانتظار المراجعة. */
export type PublicEntityStatus = 'final' | 'pending';

/** صيغة مناداة ممثل الجهة القانونية: إضافة لوظيفته / إضافة لمنصبه. */
export type CitationFormula = 'add-to-job' | 'add-to-position';

/** قيد جهة في السجل بمستوى محافظة + فرع. */
export interface PublicEntityEntryDto {
  id: number;
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  governorate: string;
  branchName: string;
  citationFormula: CitationFormula;
  status: PublicEntityStatus;
  isActive: boolean;
  createdAt: string;
  aliases: string[];
  createdByName?: string | null;
  /** أدخلها محامٍ وهي بانتظار مراجعة رئيس القسم (نموذج الحوكمة الجديد). */
  needsReview?: boolean;
  /** تسمية التغطية الجغرافية (تظهر بدل المحافظة في البطاقات والبحث). */
  coverageLabel?: string | null;
  /** قيد «الجهة الأم» (بلا فرع): يغطي كل المحافظات ويظهر مرة واحدة — وفروعه تحته. */
  isParentEntity?: boolean;
}

/** نتيجة قائمة/بحث السجل المصدّرة. */
export interface PublicEntityListResponse {
  items: PublicEntityEntryDto[];
  page: number;
  perPage: number;
  totalCount: number;
  totalPages: number;
}

export interface CreatePublicEntityRequest {
  canonicalName: string;
  entityType: PublicEntityType;
  governorate: string;
  branchName: string;
  citationFormula?: CitationFormula | null;
  aliases?: string[] | null;
  coverageLabel?: string | null;
  /** جعل القيد قيد «الجهة الأم» (بلا فرع) — يُخزَّن مرة واحدة ويغطي كل المحافظات. */
  isParentEntity?: boolean;
}

/** أي حقل يُترك undefined يبقى كما هو؛ canonicalName يعني إعادة تسمية جماعية. حقول المرسوم للتعديلات العامة بمرسوم. */
export interface UpdatePublicEntityRequest {
  canonicalName?: string | null;
  entityType?: PublicEntityType | null;
  governorate?: string | null;
  branchName?: string | null;
  citationFormula?: CitationFormula | null;
  status?: PublicEntityStatus | null;
  isActive?: boolean | null;
  coverageLabel?: string | null;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  isParentEntity?: boolean | null;
}

export interface AddPublicEntityAliasRequest {
  aliasText: string;
}

export interface ProposeEditRequest {
  canonicalName?: string | null;
  entityType?: PublicEntityType | null;
  governorate?: string | null;
  branchName?: string | null;
  citationFormula?: CitationFormula | null;
  coverageLabel?: string | null;
  isParentEntity?: boolean | null;
}

/** كتابة متمايزة واحدة لنص جهة في الاستيراد مع عدّاد ملفاتها. */
export interface ImportVariantDto {
  text: string;
  /** مصدر الكتابة: applicant = طالب تنفيذ، executed = منفذ عليها. */
  side: 'applicant' | 'executed';
  governorate?: string | null;
  documentCount: number;
}

/** مرشّح استيراد يجمع كتابات متطابقة بعد التطبيع تحت اسم مقترح. */
export interface ImportPreviewItemDto {
  normalizedName: string;
  suggestedCanonicalName: string;
  totalDocuments: number;
  governorates: string[];
  variants: ImportVariantDto[];
}

export interface ImportPreviewResponse {
  generatedAtUtc: string;
  items: ImportPreviewItemDto[];
}

export interface ImportCommitItemRequest {
  normalizedName: string;
  canonicalName: string;
  entityType: PublicEntityType;
  governorate: string;
  branchName: string;
  citationFormula?: CitationFormula | null;
  addVariantsAsAliases?: boolean;
}

export interface ImportCommitRequest {
  items: ImportCommitItemRequest[];
}

export interface ImportCommitResultDto {
  groupsCreated: number;
  entriesCreated: number;
  aliasesAdded: number;
}

/* ── النقل (MoveEntry) ──────────────────────────────────────────────── */

/** طلب نقل قيد جهة من هوية إلى أخرى أو طيّه في قيد قائم. */
export interface MoveEntryRequest {
  targetGroupId?: number | null;
  targetEntryId?: number | null;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  note?: string | null;
}

/** طلب نقل جميع قيود مجموعة إلى مجموعة أخرى (تبعية كاملة). */
export interface MoveAllEntriesRequest {
  sourceGroupId: number;
  targetGroupId: number;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  note?: string | null;
}

/** نتيجة نقل قيد واحد. */
export interface MoveEntryResponse {
  entryId: number;
  fromGroupId: number;
  toGroupId: number;
  affectedDocuments: number;
  changeEventId: number;
}

/** نتيجة نقل جميع قيود مجموعة. */
export interface MoveAllEntriesResponse {
  sourceGroupId: number;
  targetGroupId: number;
  entriesMoved: number;
  affectedDocuments: number;
  changeEventId: number;
}

/* ── الدمج N←1 (د5 §4) ─────────────────────────────────────────── */

/** طلب معاينة الدمج قبل الاعتماد. */
export interface MergePreviewRequest {
  survivorGroupId: number;
  absorbedGroupIds: number[];
}

/** قيد مُهمَل في المعاينة مع مسار امتصاصه. */
export interface AbsorbedEntryPreviewDto {
  entryId: number;
  governorate: string;
  branchName: string;
  documentCount: number;
  mappedToEntryId: number;
  conflictsWithSurvivor: boolean;
}

/** هوية أم مُهمَلة في المعاينة. */
export interface AbsorbedGroupPreviewDto {
  groupId: number;
  name: string;
  entries: AbsorbedEntryPreviewDto[];
  totalDocuments: number;
  aliases: string[];
}

/** نتيجة معاينة الدمج. */
export interface MergePreviewResponse {
  survivorName: string;
  absorbedGroups: AbsorbedGroupPreviewDto[];
  totalAffectedDocuments: number;
  warnings: string[];
}

/** طلب اعتماد الدمج. */
export interface MergeCommitRequest {
  survivorGroupId: number;
  absorbedGroupIds: number[];
  unifyTexts?: boolean;
  newCanonicalName?: string | null;
  decreeKind?: string;
  decreeNumber?: string;
  decreeDate?: string;
}

/** نتيجة الدمج. */
export interface MergeCommitResponse {
  absorbedGroupsCount: number;
  entriesMigrated: number;
  aliasesAdded: number;
  totalAffectedDocuments: number;
  changeEventId: number;
}

/* ── إعادة تسمية هوية أم (المدير/المشرف) ─────────────────────────────── */

/** طلب معاينة إعادة تسمية هوية أم قبل الاعتماد. */
export interface RenameGroupPreviewRequest {
  groupId: number;
  newCanonicalName: string;
}

/** نتيجة معاينة إعادة التسمية. */
export interface RenameGroupPreviewResponse {
  oldCanonicalName: string;
  newCanonicalName: string;
  affectedDocuments: number;
  branches: string[];
}

/** طلب إعادة تسمية هوية أم واحدة بموجب مرجع (قرار/قانون/مرسوم). */
export interface RenameGroupRequest {
  groupId: number;
  newCanonicalName: string;
  decreeKind: string;
  decreeNumber: string;
  decreeDate: string;
}

/** نتيجة إعادة تسمية الهوية الأم. */
export interface RenameGroupResponse {
  groupId: number;
  oldCanonicalName: string;
  newCanonicalName: string;
  affectedDocuments: number;
  changeEventId: number;
}

/* ── الحلول: إلغاء هويات أم واستبدالها بهوية جديدة ─────────────────────── */

/** طلب معاينة الإلغاء والاستبدال قبل الاعتماد. */
export interface AbolishReplacePreviewRequest {
  abolishedGroupIds: number[];
}

/** نتيجة معاينة الإلغاء والاستبدال. */
export interface AbolishReplacePreviewResponse {
  abolishedNames: string[];
  abolishedGroups: number;
  activeEntries: number;
  affectedDocuments: number;
  delegatesToReassign: number;
  branches: string[];
}

/** طلب إلغاء عدة هويات أم واستبدالها بهوية أم جديدة تحلّ محلها. */
export interface AbolishAndReplaceRequest {
  abolishedGroupIds: number[];
  newCanonicalName: string;
  entityType: PublicEntityType;
  governorate: string;
  citationFormula?: string | null;
  aliases?: string[];
  coverageLabel?: string | null;
  decreeKind: string;
  decreeNumber: string;
  decreeDate: string;
}

/** نتيجة الإلغاء والاستبدال. */
export interface AbolishAndReplaceResponse {
  newGroupId: number;
  newCanonicalName: string;
  abolishedGroups: number;
  entriesMoved: number;
  affectedDocuments: number;
  changeEventId: number;
}

/* ── قائمة المجموعات (الهويات الأم) وتوحيد التسمية N←1 ───────────────── */

export interface PublicEntityGroupDto {
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  isActive: boolean;
  entryCount: number;
  governorates: string[];
  linkedDocumentCount?: number;
}

export interface PublicEntityGroupListResponse {
  items: PublicEntityGroupDto[];
  page: number;
  perPage: number;
  totalCount: number;
  totalPages: number;
}

export interface UnifyPreviewRequest {
  targetGroupId: number;
  absorbedGroupIds: number[];
}

export interface AbsorbedGroupUnifyPreviewDto {
  groupId: number;
  name: string;
  entryCount: number;
  governorates: string[];
}

export interface EntryFoldPreviewDto {
  absorbedGroupId: number;
  absorbedGroupName: string;
  governorate: string;
  branchName: string;
  linkedDocumentCount: number;
}

export interface UnifyPreviewResponse {
  targetName: string;
  absorbedGroups: AbsorbedGroupUnifyPreviewDto[];
  totalEntriesToMove: number;
  totalEntriesFolded: number;
  foldsToApply: EntryFoldPreviewDto[];
  warnings: string[];
}

export interface UnifyRequest {
  targetGroupId: number;
  absorbedGroupIds: number[];
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
}

export interface UnifyResponse {
  targetGroupId: number;
  canonicalName: string;
  groupsUnified: number;
  entriesMoved: number;
  entriesFolded: number;
  changeEventId: number;
}

/* ── مشابهات جهة محددة (توحيد التسمية) ───────────────────────────────── */

export interface SimilarToItemDto {
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  entryCount: number;
  linkedDocumentCount: number;
  similarity: number;
}

export interface SimilarToResponse {
  targetGroupId: number;
  targetCanonicalName: string;
  items: SimilarToItemDto[];
  threshold: number;
}

/* ── سجل تغييرات الجهات (د5 §7) ───────────────────────────────────── */

export interface EntityChangeEventDto {
  id: number;
  entryId?: number | null;
  groupId?: number | null;
  actionKind: string;
  actionKindLabel: string;
  decreeKind?: string | null;
  decreeNumber?: string | null;
  decreeDate?: string | null;
  summaryAr: string;
  /** وسم جودة الملخّص: حمل غير فارغ لا يُنتج تفاصيل فسقط إلى التسمية — يُعرض موسومًا. */
  summaryDegraded: boolean;
  actorUserId: number;
  actorName?: string | null;
  createdAtUtc: string;
  governorate?: string | null;
  canonicalName?: string | null;
}

export interface EntityChangeEventQuery {
  governorate?: string | null;
  actionKind?: string | null;
  actorUserId?: number | null;
  from?: string | null;
  to?: string | null;
  page?: number;
  perPage?: number;
}

/* ── بوابة مندوب الجهة العامة (المرحلة 3) ────────────────────────────── */

/* ── إدارة فروع رئيس القسم (ضمن محافظته — بلا مرسوم) ─────────────────── */

export interface RenameBranchRequest {
  newBranchName: string;
  coverageLabel?: string | null;
}

export interface MergeBranchesRequest {
  sourceEntryId: number;
  targetEntryId: number;
}

export interface AbolishBranchRequest {
  targetEntryId?: number | null;
}

export interface UnifyBranchesRequest {
  targetEntryId: number;
  absorbedEntryIds: number[];
  correctedName?: string | null;
}

export type BranchActionKind = 'rename' | 'merge' | 'abolish' | 'unify';

export interface PreviewBranchActionRequest {
  action: BranchActionKind;
  /** الفرع المعالَج (المعاد تسميته / المصدر في الدمج / المُلغى / الناجي في التوحيد). */
  entryId: number;
  targetId?: number | null;
  absorbedIds?: number[] | null;
  newBranchName?: string | null;
  correctedName?: string | null;
}

export interface BranchPreviewEntryDto {
  entryId: number;
  branchName: string;
  governorate: string;
  documentCount: number;
}

export interface BranchActionPreviewResponse {
  action: BranchActionKind;
  summary: string;
  targetBranchName: string;
  entries: BranchPreviewEntryDto[];
  totalAffectedDocuments: number;
  warnings: string[];
  errors: string[];
}

export interface RenameBranchResponse {
  entryId: number;
  oldBranchName: string;
  newBranchName: string;
  affectedDocuments: number;
  changeEventId: number;
}

export interface MergeBranchesResponse {
  sourceEntryId: number;
  targetEntryId: number;
  affectedDocuments: number;
  changeEventId: number;
}

export interface AbolishBranchResponse {
  entryId: number;
  targetEntryId?: number | null;
  affectedDocuments: number;
  changeEventId: number;
}

export interface UnifyBranchesResponse {
  targetEntryId: number;
  entriesUnified: number;
  affectedDocuments: number;
  changeEventId: number;
}

/* ── اقتراح تعديل الجهة الأم (رئيس القسم → مدير/مشرف) ────────────────── */

export type ParentEditSuggestionStatus = 'pending' | 'approved' | 'rejected' | 'withdrawn';

export interface SuggestParentEditRequest {
  proposedCanonicalName?: string | null;
  proposedEntityType?: PublicEntityType | null;
  proposedCitationFormula?: string | null;
  reason?: string | null;
}

export interface ReviewParentEditSuggestionRequest {
  status: 'approved' | 'rejected';
  reviewReason?: string | null;
}

export interface ParentEditSuggestionDto {
  id: number;
  groupId: number;
  entryId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  proposedCanonicalName?: string | null;
  proposedEntityType?: PublicEntityType | null;
  proposedCitationFormula?: string | null;
  reason: string;
  status: ParentEditSuggestionStatus;
  createdById: number;
  createdByName: string;
  createdBranchId: number;
  reviewedById?: number | null;
  reviewReason?: string | null;
  createdAtUtc: string;
  reviewedAtUtc?: string | null;
}

export interface ParentEditSuggestionListQuery {
  status?: string | null;
  groupId?: number | null;
  page?: number;
  perPage?: number;
}

export interface ParentEditSuggestionListResponse {
items: ParentEditSuggestionDto[];
total: number;
}

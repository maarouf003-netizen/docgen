import type { PagedResult } from './common';
import type { PublicEntityType } from './entity';

/** نطاق المندوب: هوية أم بكل قيودها أو قيد بعينه. */
export interface PortalScopeDto {
  scopeType: 'group' | 'entry';
  groupId: number;
  canonicalName: string;
  entityType: PublicEntityType;
  entries: PortalScopeEntryDto[];
}

export interface PortalScopeEntryDto {
  id: number;
  governorate: string;
  branchName: string;
  isActive: boolean;
}

/** ملف في قائمة البوابة — قراءة فقط بلا حقول داخلية للمحامين. */
export interface PortalFileListItemDto {
  id: number;
  documentType: string;
  isDraft: boolean;
  borrowerName?: string | null;
  applicant?: string | null;
  executedEntitiesSummary: string;
  amountNumeric: number;
  currency?: string | null;
  execStatus?: string | null;
  createdAt: string;
  updatedAt: string;
  /** مكوّنا الاسم الثلاثي للمنفذ عليه. */
  borrowerFather?: string | null;
  borrowerFamily?: string | null;
  /** نوع الملف (FileType: سند مصارف، تأمين، …). */
  fileType?: string | null;
  /** دائرة التنفيذ المختصة — تُخفى عرضيًا عند فراغها. */
  court?: string | null;
  /** أحدث رقم أساس دائمًا وإلا رقم الملف الأصلي. */
  displayBaseNumber?: string | null;
  /** سنة رقم الأساس المعروض وإلا سنة قيد الملف الأصلية. */
  displayBaseYear?: string | null;
  /** قيود النطاق المطابقة لهذا الملف (للسطر الثاني «فرع الجهة العامة»). */
  matchedEntries?: PortalScopeEntryDto[] | null;
  /** حالة العرض من الخادم (`DocumentStatusResolver`) — تشتق منها الشارة مباشرة بلا إعادة تصنيف. */
  displayStatus?: string | null;
}

export type PortalFilesResponse = PagedResult<PortalFileListItemDto>;

/** استئناف قرائي على بطاقة استئنافات البوابة. */
export interface PortalAppealDto {
  id: number;
  direction: string;
  status: string;
  appealTypeLabel?: string | null;
  appealBaseNumber?: string | null;
  appealYear?: string | null;
  createdAt: string;
  decisionDate?: string | null;
  decisionRuling?: string | null;
}

/** إجراء تنفيذي قرائي على بطاقة «الإجراءات التنفيذية» — نوع action فقط، بلا شارة تذكير (ق7). */
export interface PortalExecutionActionDto {
  id: number;
  /** نص الإجراء الغني — يُعقَّم عبر sanitizeRichText قبل العرض. */
  text: string;
  /** تاريخ الإجراء (نص حر يُعرض كما هو). */
  actionDate?: string;
  /** المحامي الذي أدخل الإجراء. */
  createdByName?: string;
  createdAt: string;
}

export interface CreateDelegateRequest {
  username: string;
  fullName: string;
  password: string;
  portalGroupId?: number | null;
  portalEntryId?: number | null;
}

export interface UpdateDelegateRequest {
  fullName?: string | null;
  isActive?: boolean | null;
  newPassword?: string | null;
  portalGroupId?: number | null;
  portalEntryId?: number | null;
}

export interface DelegateDto {
  id: number;
  username: string;
  fullName: string;
  isActive: boolean;
  portalGroupId?: number | null;
  portalGroupName?: string | null;
  portalEntryId?: number | null;
  portalEntryLabel?: string | null;
  createdAt: string;
}

/* ── إحصاءات الجهة (المرحلة 4) ───────────────────────────────────────── */

export interface PortalMonthlyCountDto {
  year: number;
  month: number;
  files: number;
}

/** توزيع الارتباط على قيود النطاق؛ قد يُحتسب الملف تحت أكثر من قيد. */
export interface PortalEntryStatDto {
  entryId: number;
  governorate: string;
  branchName: string;
  files: number;
}

export interface PortalCurrencyStatDto {
  currency: string;
  files: number;
  totalAmount: number;
}

/** مبالغ سلّة حالة واحدة مكسّرة حسب العملة (لا خلط بين العملات). */
export interface PortalStatusAmountDto {
  status: string;
  files: number;
  totals: PortalCurrencyStatDto[];
}

export interface PortalStatsDto {
  totalFiles: number;
  draftFiles: number;
  circulatingFiles: number;
  executedFiles: number;
  deferredFiles: number;
  /** عدد ملفات «محال الى البداية» في النطاق (فلتر مستقل — لا يشملها فلتر «منفذ»). */
  referredToStartFiles?: number;
  pendingAppeals: number;
  closedAppeals: number;
  /** آخر 12 شهرًا متصلة حتى الشهر الحالي شاملة الأشهر الصفرية. */
  monthly: PortalMonthlyCountDto[];
  perEntry: PortalEntryStatDto[];
  topCurrencies: PortalCurrencyStatDto[];
  /** إجمالي المبالغ مكسّرًا حسب العملة. */
  amountTotals?: PortalCurrencyStatDto[] | null;
  /** المبالغ لكل سلّة حالة مكسّرة حسب العملة. */
  amountByStatus?: PortalStatusAmountDto[] | null;
}

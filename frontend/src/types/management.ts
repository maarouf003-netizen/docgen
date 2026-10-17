import type { AppealsStatsDto } from './appeal';
import type { Role } from './common';

export interface BranchDto {
  id: number;
  name: string;
  code: string;
  address?: string;
  phone?: string;
  /** المحافظة التابعة لها الفرع — تحدد نطاق رئيس القسم في سجل الجهات العامة. */
  governorate?: string | null;
  isActive?: boolean;
  userCount?: number;
  documentCount?: number;
}

export interface CreateBranchRequest {
  name: string;
  code: string;
  address?: string | null;
  phone?: string | null;
  governorate?: string | null;
}

export interface UpdateBranchRequest {
  name: string;
  code: string;
  address?: string | null;
  phone?: string | null;
  isActive: boolean;
  governorate?: string | null;
}

/** شعبة داخل فرع — تطابق `SectionDto` الخلفي (§8). */
export interface SectionDto {
  id: number;
  branchId: number;
  branchName?: string | null;
  name: string;
  isActive: boolean;
  circuitCount: number;
  headName?: string | null;
}

export interface DashboardStatsDto {
  totalDocuments: number;
  totalDrafts: number;
  totalExecuted: number;
  totalDeferred: number;
  totalActive: number;
  totalBorrowers: number;
  totalAmount: number;
  totalCollectedAmount: number;
}

export interface ReminderDto {
  actionId: number;
  documentId: number;
  documentType?: string;
  borrowerName?: string;
  borrowerFather?: string;
  borrowerFamily?: string;
  actionText: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
  dueDate: string;
  /** وسم جودة: تاريخ/مدة قديمة لا تُحلَّل فسقط الحساب إلى احتياط — يُعرض موسومًا. */
  dueDateSuspect: boolean;
}

export interface MonthlyStatDto {
  year: number;
  month: number;
  count: number;
}

export type StatsPeriod = 'yearly' | 'quarterly' | 'monthly';

/** مبلغ مجمّع بعملة محددة (ليرة سورية / دولار أمريكي / يورو)، بقيمة غير صفرية. */
export interface CurrencyAmountDto {
  currency: string;
  amount: number;
}

/**
 * توزيع حالة على نوعَي العقد (مصرفي/عادي) مع مبالغ كل نوع مجمّعة حسب العملة الفعلية،
 * بالترتيب الثابت: ليرة سورية، دولار أمريكي، يورو.
 */
export interface ManagerContractSplitDto {
  bankingCount: number;
  ordinaryCount: number;
  bankingAmounts: CurrencyAmountDto[];
  ordinaryAmounts: CurrencyAmountDto[];
}

export interface ManagerStatsDto {
  totalFiles: number;
  active: number;
  drafts: number;
  deferred: number;
  /** توزيع «متداول للصالح» (الدولة طالبة التنفيذ) مصرفي/عادي مع مبالغه بالعملات. */
  activeSplit: ManagerContractSplitDto;
  /** توزيع «تحت رفع» مصرفي/عادي مع مبالغه بالعملات. */
  draftsSplit: ManagerContractSplitDto;
/** توزيع «التريث» مصرفي/عادي مع مبالغه بالعملات. */
  deferredSplit: ManagerContractSplitDto;
  /** عدد ملفات «محال الى البداية» في نطاق الفترة (عدّاد مستقل مرآة بطاقة التريث). */
  referredToStartCount?: number;
  /** توزيع «محال الى البداية» مصرفي/عادي مع مبالغه بالعملات (مرآة deferredSplit). */
  referredSplit?: ManagerContractSplitDto;
  /** إجمالي مبالغ ملفات «طالبة التنفيذ» (دون المنفذ) مجمّعة حسب العملة. */
  totalAmounts: CurrencyAmountDto[];
  /** المبالغ المطلوب دفعها من الجهات العامة في «متداول للضد» كلٌّ بعملتها (حتى ثلاثة). */
  tradingAgainstAmounts: CurrencyAmountDto[];
  settledCount: number;
  settledCollected: number;
  /** المبالغ المحصلة في «منفذ بالتسوية» مجمّعة حسب العملة الفعلية (كل عملة على حدة). */
  settledCollectedAmounts: CurrencyAmountDto[];
  forcibleCount: number;
  forcibleCollected: number;
  /** المبالغ المحصلة في «منفذ جبريا» مجمّعة حسب العملة الفعلية (كل عملة على حدة). */
  forcibleCollectedAmounts: CurrencyAmountDto[];
  /** عدد ملفات وضع «الجهة العامة منفذ عليها» المتداولة في الفترة (صف «متداول للضد»). */
  tradingAgainstCount: number;
  /** عدد ملفات وضع «الجهة العامة منفذ عليها» المنفذة في الفترة (بطاقة «منفذ للضد»). */
  executedAgainstCount: number;
  /** سلة «ليرة سورية» من المبالغ التي دفعتها الجهات العامة (للتوافق — الفصل الكامل في `executedAgainstAmounts`). */
  executedAgainstAmount: number;
  /** المبالغ التي دفعتها الجهات العامة مفصولة لكل عملة (`BQ-029`) — بما فيها سلة «أخرى» (`BQ-030`). */
  executedAgainstAmounts?: CurrencyAmountDto[];
  /** عدد ملفات «عرض وايداع» المتداولة في الفترة (سطر «عرض وايداع» داخل بطاقة «متداول للصالح»). */
  depositTradingCount: number;
  /** عدد ملفات «عرض وايداع» المنفذة في الفترة (سطر «عرض وايداع» داخل بطاقة «منفذ للصالح»). */
  depositExecutedCount: number;
  /** سلة «ليرة سورية» من المبالغ المودعة (للتوافق — الفصل الكامل في `depositExecutedAmounts`). */
  depositExecutedAmount: number;
  /** المبالغ المودعة لملفات «عرض وايداع» مفصولة لكل عملة (`BQ-029`) — بما فيها سلة «أخرى» (`BQ-030`). */
  depositExecutedAmounts?: CurrencyAmountDto[];
  periodYear: number;
  periodQuarter: number | null;
  periodMonth: number | null;
  /** وسم مصدر الفترة (سبب القيد فقط): ملفات النطاق المحسوبة بتاريخ إدخالها لغياب تاريخ قيدها أو تعذّر تحليله. */
  periodDateFallbackCount: number;
  /** وسم مصدر الفترة (سبب الإخطار فقط): ملفات جهتي «منفذ عليها/إيداع» المحسوبة بتاريخ إدخالها لغياب تاريخ ورود الإخطار. */
  periodDateFromReceiptCount: number;
  /** إحصاء استئنافات المحامي وفق فلاتر الفترة — null عند غياب أي استئناف (تُخفى البطاقة). */
  appeals?: AppealsStatsDto | null;
}

export interface ManagerPeriodPointDto {
  year: number;
  month: number;
  count: number;
  /** وسم المصدر: ملفات هذه النقطة المحسوبة بتاريخ إدخالها لغياب تاريخ قيدها أو تعذّر تحليله. */
  fromCreatedAtCount: number;
}

export interface ManagerLawyerStatDto {
  lawyerId: number;
  lawyerName: string;
  totalCount: number;
  points: ManagerPeriodPointDto[];
}

export interface BranchSummaryDto {
  branchId: number;
  branchName: string;
  totalDocuments: number;
  totalDrafts: number;
  totalAmount: number;
}

export interface LawyerListItem {
  id: number;
  username: string;
  fullName: string;
  isActive: boolean;
  branchId: number | null;
  branchName?: string | null;
}

export interface CreateLawyerRequest {
  username: string;
  fullName: string;
  password: string;
  branchId?: number | null;
}

export interface SetUserActiveRequest {
  isActive: boolean;
}

export interface UserListItem {
  id: number;
  username: string;
  fullName: string;
  role: Role;
  branchId: number | null;
  branchName?: string | null;
  isActive: boolean;
  sectionId?: number | null;
  sectionName?: string | null;
}

export interface CreateUserRequest {
  username: string;
  fullName: string;
  role: Role;
  branchId: number | null;
  password: string;
  sectionId?: number | null;
}

export interface UpdateUserRequest {
  fullName: string;
  role: Role;
  branchId: number | null;
  isActive: boolean;
  password?: string | null;
  sectionId?: number | null;
  successorId?: number | null;
}

/** صف سجل تعاقب رئاسة — للمدير والمشرف فقط (قرار §2.17). */
export interface HeadSuccessionDto {
  id: number;
  branchId: number;
  branchName?: string | null;
  sectionId?: number | null;
  sectionName?: string | null;
  userId: number;
  userName?: string | null;
  role: string;
  event: string;
  at: string;
  actorName?: string | null;
  reason?: string | null;
}

/** نقل ملكية دائرة لمالك جديد داخل الفرع — `null` = قسم الفرع (§8.3). */
export interface TransferCircuitRequest {
  targetSectionId: number | null;
  version?: number | null;
}

export interface TransferDocumentRequest {
  targetLawyerId: number;
}

export interface TransferAllRequest {
  sourceLawyerId: number;
  targetLawyerId: number;
}

export type HeadAlertTargetType = 'document' | 'lawyer' | 'branch' | 'head';

export interface HeadAlertDto {
  id: number;
  message: string;
  targetType: HeadAlertTargetType;
  documentId?: number | null;
  documentTitle?: string | null;
  targetLawyerId?: number | null;
  targetLawyerName?: string | null;
  isRead?: boolean;
  recipientCount?: number;
  unreadCount?: number;
  /** الاستئناف المرتبط بالتنبيه — للانتقال المباشر إلى تفاصيله. */
  appealId?: number;
  /** الإنابة المرتبطة بالتنبيه (تنبيهات المرآة/المتابعة) — لشريطي نشاط بطاقتي الإنابة. */
  delegationId?: number | null;
  /** كتاب المطالعة المرتبط (تنبيه الرد) — للانتقال المباشر إلى صفحة الكتاب. */
  reviewLetterId?: number | null;
  createdAt: string;
  createdByName?: string;
}

/** تذكير إجراء على استئناف يتابعه المحامي (بطاقة التذكيرات). */
export interface AppealReminderDto {
  actionId: number;
  appealId: number;
  documentId: number;
  appealTitle: string;
  actionText: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
  dueDate: string;
  /** وسم جودة: تاريخ/مدة قديمة لا تُحلَّل فسقط الحساب إلى احتياط — يُعرض موسومًا. */
  dueDateSuspect: boolean;
}

export interface CreateHeadAlertRequest {
  targetType: HeadAlertTargetType;
  documentId?: number | null;
  targetLawyerId?: number | null;
  message: string;
}

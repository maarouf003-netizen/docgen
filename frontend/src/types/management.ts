import type { AppealsStatsDto } from './appeal';
import type { Role } from './common';

export interface BranchDto {
  id: number;
  name: string;
  code: string;
  address?: string;
  phone?: string;
  /** ط§ظ„ظ…ط­ط§ظپط¸ط© ط§ظ„طھط§ط¨ط¹ط© ظ„ظ‡ط§ ط§ظ„ظپط±ط¹ â€” طھط­ط¯ط¯ ظ†ط·ط§ظ‚ ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ… ظپظٹ ط³ط¬ظ„ ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط©. */
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

/** ط´ط¹ط¨ط© ط¯ط§ط®ظ„ ظپط±ط¹ â€” طھط·ط§ط¨ظ‚ `SectionDto` ط§ظ„ط®ظ„ظپظٹ (آ§8). */
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
  /** ظˆط³ظ… ط¬ظˆط¯ط©: طھط§ط±ظٹط®/ظ…ط¯ط© ظ‚ط¯ظٹظ…ط© ظ„ط§ طھظڈط­ظ„ظژظ‘ظ„ ظپط³ظ‚ط· ط§ظ„ط­ط³ط§ط¨ ط¥ظ„ظ‰ ط§ط­طھظٹط§ط· â€” ظٹظڈط¹ط±ط¶ ظ…ظˆط³ظˆظ…ظ‹ط§. */
  dueDateSuspect: boolean;
}

export interface MonthlyStatDto {
  year: number;
  month: number;
  count: number;
}

export type StatsPeriod = 'yearly' | 'quarterly' | 'monthly';

/** ظ…ط¨ظ„ط؛ ظ…ط¬ظ…ظ‘ط¹ ط¨ط¹ظ…ظ„ط© ظ…ط­ط¯ط¯ط© (ظ„ظٹط±ط© ط³ظˆط±ظٹط© / ط¯ظˆظ„ط§ط± ط£ظ…ط±ظٹظƒظٹ / ظٹظˆط±ظˆ)طŒ ط¨ظ‚ظٹظ…ط© ط؛ظٹط± طµظپط±ظٹط©. */
export interface CurrencyAmountDto {
  currency: string;
  amount: number;
}

/**
 * طھظˆط²ظٹط¹ ط­ط§ظ„ط© ط¹ظ„ظ‰ ظ†ظˆط¹ظژظٹ ط§ظ„ط¹ظ‚ط¯ (ظ…طµط±ظپظٹ/ط¹ط§ط¯ظٹ) ظ…ط¹ ظ…ط¨ط§ظ„ط؛ ظƒظ„ ظ†ظˆط¹ ظ…ط¬ظ…ظ‘ط¹ط© ط­ط³ط¨ ط§ظ„ط¹ظ…ظ„ط© ط§ظ„ظپط¹ظ„ظٹط©طŒ
 * ط¨ط§ظ„طھط±طھظٹط¨ ط§ظ„ط«ط§ط¨طھ: ظ„ظٹط±ط© ط³ظˆط±ظٹط©طŒ ط¯ظˆظ„ط§ط± ط£ظ…ط±ظٹظƒظٹطŒ ظٹظˆط±ظˆ.
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
  /** طھظˆط²ظٹط¹ آ«ظ…طھط¯ط§ظˆظ„ ظ„ظ„طµط§ظ„ط­آ» (ط§ظ„ط¯ظˆظ„ط© ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط°) ظ…طµط±ظپظٹ/ط¹ط§ط¯ظٹ ظ…ط¹ ظ…ط¨ط§ظ„ط؛ظ‡ ط¨ط§ظ„ط¹ظ…ظ„ط§طھ. */
  activeSplit: ManagerContractSplitDto;
  /** طھظˆط²ظٹط¹ آ«طھط­طھ ط±ظپط¹آ» ظ…طµط±ظپظٹ/ط¹ط§ط¯ظٹ ظ…ط¹ ظ…ط¨ط§ظ„ط؛ظ‡ ط¨ط§ظ„ط¹ظ…ظ„ط§طھ. */
  draftsSplit: ManagerContractSplitDto;
/** طھظˆط²ظٹط¹ آ«ط§ظ„طھط±ظٹط«آ» ظ…طµط±ظپظٹ/ط¹ط§ط¯ظٹ ظ…ط¹ ظ…ط¨ط§ظ„ط؛ظ‡ ط¨ط§ظ„ط¹ظ…ظ„ط§طھ. */
  deferredSplit: ManagerContractSplitDto;
  /** ط¹ط¯ط¯ ظ…ظ„ظپط§طھ آ«ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©آ» ظپظٹ ظ†ط·ط§ظ‚ ط§ظ„ظپطھط±ط© (ط¹ط¯ظ‘ط§ط¯ ظ…ط³طھظ‚ظ„ ظ…ط±ط¢ط© ط¨ط·ط§ظ‚ط© ط§ظ„طھط±ظٹط«). */
  referredToStartCount?: number;
  /** طھظˆط²ظٹط¹ آ«ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©آ» ظ…طµط±ظپظٹ/ط¹ط§ط¯ظٹ ظ…ط¹ ظ…ط¨ط§ظ„ط؛ظ‡ ط¨ط§ظ„ط¹ظ…ظ„ط§طھ (ظ…ط±ط¢ط© deferredSplit). */
  referredSplit?: ManagerContractSplitDto;
  /** ط¥ط¬ظ…ط§ظ„ظٹ ظ…ط¨ط§ظ„ط؛ ظ…ظ„ظپط§طھ آ«ط·ط§ظ„ط¨ط© ط§ظ„طھظ†ظپظٹط°آ» (ط¯ظˆظ† ط§ظ„ظ…ظ†ظپط°) ظ…ط¬ظ…ظ‘ط¹ط© ط­ط³ط¨ ط§ظ„ط¹ظ…ظ„ط©. */
  totalAmounts: CurrencyAmountDto[];
  /** ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„ظ…ط·ظ„ظˆط¨ ط¯ظپط¹ظ‡ط§ ظ…ظ† ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© ظپظٹ آ«ظ…طھط¯ط§ظˆظ„ ظ„ظ„ط¶ط¯آ» ظƒظ„ظŒظ‘ ط¨ط¹ظ…ظ„طھظ‡ط§ (ط­طھظ‰ ط«ظ„ط§ط«ط©). */
  tradingAgainstAmounts: CurrencyAmountDto[];
  settledCount: number;
  settledCollected: number;
  /** ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„ظ…ط­طµظ„ط© ظپظٹ آ«ظ…ظ†ظپط° ط¨ط§ظ„طھط³ظˆظٹط©آ» ظ…ط¬ظ…ظ‘ط¹ط© ط­ط³ط¨ ط§ظ„ط¹ظ…ظ„ط© ط§ظ„ظپط¹ظ„ظٹط© (ظƒظ„ ط¹ظ…ظ„ط© ط¹ظ„ظ‰ ط­ط¯ط©). */
  settledCollectedAmounts: CurrencyAmountDto[];
  forcibleCount: number;
  forcibleCollected: number;
  /** ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„ظ…ط­طµظ„ط© ظپظٹ آ«ظ…ظ†ظپط° ط¬ط¨ط±ظٹط§آ» ظ…ط¬ظ…ظ‘ط¹ط© ط­ط³ط¨ ط§ظ„ط¹ظ…ظ„ط© ط§ظ„ظپط¹ظ„ظٹط© (ظƒظ„ ط¹ظ…ظ„ط© ط¹ظ„ظ‰ ط­ط¯ط©). */
  forcibleCollectedAmounts: CurrencyAmountDto[];
  /** ط¹ط¯ط¯ ظ…ظ„ظپط§طھ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» ط§ظ„ظ…طھط¯ط§ظˆظ„ط© ظپظٹ ط§ظ„ظپطھط±ط© (طµظپ آ«ظ…طھط¯ط§ظˆظ„ ظ„ظ„ط¶ط¯آ»). */
  tradingAgainstCount: number;
  /** ط¹ط¯ط¯ ظ…ظ„ظپط§طھ ظˆط¶ط¹ آ«ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط© ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§آ» ط§ظ„ظ…ظ†ظپط°ط© ظپظٹ ط§ظ„ظپطھط±ط© (ط¨ط·ط§ظ‚ط© آ«ظ…ظ†ظپط° ظ„ظ„ط¶ط¯آ»). */
  executedAgainstCount: number;
  /** ط³ظ„ط© آ«ظ„ظٹط±ط© ط³ظˆط±ظٹط©آ» ظ…ظ† ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„طھظٹ ط¯ظپط¹طھظ‡ط§ ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© (ظ„ظ„طھظˆط§ظپظ‚ â€” ط§ظ„ظپطµظ„ ط§ظ„ظƒط§ظ…ظ„ ظپظٹ `executedAgainstAmounts`). */
  executedAgainstAmount: number;
  /** ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„طھظٹ ط¯ظپط¹طھظ‡ط§ ط§ظ„ط¬ظ‡ط§طھ ط§ظ„ط¹ط§ظ…ط© ظ…ظپطµظˆظ„ط© ظ„ظƒظ„ ط¹ظ…ظ„ط© (`BQ-029`) â€” ط¨ظ…ط§ ظپظٹظ‡ط§ ط³ظ„ط© آ«ط£ط®ط±ظ‰آ» (`BQ-030`). */
  executedAgainstAmounts?: CurrencyAmountDto[];
  /** ط¹ط¯ط¯ ظ…ظ„ظپط§طھ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» ط§ظ„ظ…طھط¯ط§ظˆظ„ط© ظپظٹ ط§ظ„ظپطھط±ط© (ط³ط·ط± آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» ط¯ط§ط®ظ„ ط¨ط·ط§ظ‚ط© آ«ظ…طھط¯ط§ظˆظ„ ظ„ظ„طµط§ظ„ط­آ»). */
  depositTradingCount: number;
  /** ط¹ط¯ط¯ ظ…ظ„ظپط§طھ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» ط§ظ„ظ…ظ†ظپط°ط© ظپظٹ ط§ظ„ظپطھط±ط© (ط³ط·ط± آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» ط¯ط§ط®ظ„ ط¨ط·ط§ظ‚ط© آ«ظ…ظ†ظپط° ظ„ظ„طµط§ظ„ط­آ»). */
  depositExecutedCount: number;
  /** ط³ظ„ط© آ«ظ„ظٹط±ط© ط³ظˆط±ظٹط©آ» ظ…ظ† ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„ظ…ظˆط¯ط¹ط© (ظ„ظ„طھظˆط§ظپظ‚ â€” ط§ظ„ظپطµظ„ ط§ظ„ظƒط§ظ…ظ„ ظپظٹ `depositExecutedAmounts`). */
  depositExecutedAmount: number;
  /** ط§ظ„ظ…ط¨ط§ظ„ط؛ ط§ظ„ظ…ظˆط¯ط¹ط© ظ„ظ…ظ„ظپط§طھ آ«ط¹ط±ط¶ ظˆط§ظٹط¯ط§ط¹آ» ظ…ظپطµظˆظ„ط© ظ„ظƒظ„ ط¹ظ…ظ„ط© (`BQ-029`) â€” ط¨ظ…ط§ ظپظٹظ‡ط§ ط³ظ„ط© آ«ط£ط®ط±ظ‰آ» (`BQ-030`). */
  depositExecutedAmounts?: CurrencyAmountDto[];
  periodYear: number;
  periodQuarter: number | null;
  periodMonth: number | null;
  /** ظˆط³ظ… ظ…طµط¯ط± ط§ظ„ظپطھط±ط© (ط³ط¨ط¨ ط§ظ„ظ‚ظٹط¯ ظپظ‚ط·): ظ…ظ„ظپط§طھ ط§ظ„ظ†ط·ط§ظ‚ ط§ظ„ظ…ط­ط³ظˆط¨ط© ط¨طھط§ط±ظٹط® ط¥ط¯ط®ط§ظ„ظ‡ط§ ظ„ط؛ظٹط§ط¨ طھط§ط±ظٹط® ظ‚ظٹط¯ظ‡ط§ ط£ظˆ طھط¹ط°ظ‘ط± طھط­ظ„ظٹظ„ظ‡. */
  periodDateFallbackCount: number;
  /** ظˆط³ظ… ظ…طµط¯ط± ط§ظ„ظپطھط±ط© (ط³ط¨ط¨ ط§ظ„ط¥ط®ط·ط§ط± ظپظ‚ط·): ظ…ظ„ظپط§طھ ط¬ظ‡طھظٹ آ«ظ…ظ†ظپط° ط¹ظ„ظٹظ‡ط§/ط¥ظٹط¯ط§ط¹آ» ط§ظ„ظ…ط­ط³ظˆط¨ط© ط¨طھط§ط±ظٹط® ط¥ط¯ط®ط§ظ„ظ‡ط§ ظ„ط؛ظٹط§ط¨ طھط§ط±ظٹط® ظˆط±ظˆط¯ ط§ظ„ط¥ط®ط·ط§ط±. */
  periodDateFromReceiptCount: number;
  /** ط¥ط­طµط§ط، ط§ط³طھط¦ظ†ط§ظپط§طھ ط§ظ„ظ…ط­ط§ظ…ظٹ ظˆظپظ‚ ظپظ„ط§طھط± ط§ظ„ظپطھط±ط© â€” null ط¹ظ†ط¯ ط؛ظٹط§ط¨ ط£ظٹ ط§ط³طھط¦ظ†ط§ظپ (طھظڈط®ظپظ‰ ط§ظ„ط¨ط·ط§ظ‚ط©). */
  appeals?: AppealsStatsDto | null;
}

export interface ManagerPeriodPointDto {
  year: number;
  month: number;
  count: number;
  /** ظˆط³ظ… ط§ظ„ظ…طµط¯ط±: ظ…ظ„ظپط§طھ ظ‡ط°ظ‡ ط§ظ„ظ†ظ‚ط·ط© ط§ظ„ظ…ط­ط³ظˆط¨ط© ط¨طھط§ط±ظٹط® ط¥ط¯ط®ط§ظ„ظ‡ط§ ظ„ط؛ظٹط§ط¨ طھط§ط±ظٹط® ظ‚ظٹط¯ظ‡ط§ ط£ظˆ طھط¹ط°ظ‘ط± طھط­ظ„ظٹظ„ظ‡. */
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

/** طµظپ ط³ط¬ظ„ طھط¹ط§ظ‚ط¨ ط±ط¦ط§ط³ط© â€” ظ„ظ„ظ…ط¯ظٹط± ظˆط§ظ„ظ…ط´ط±ظپ ظپظ‚ط· (ظ‚ط±ط§ط± آ§2.17). */
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

/** ظ†ظ‚ظ„ ظ…ظ„ظƒظٹط© ط¯ط§ط¦ط±ط© ظ„ظ…ط§ظ„ظƒ ط¬ط¯ظٹط¯ ط¯ط§ط®ظ„ ط§ظ„ظپط±ط¹ â€” `null` = ظ‚ط³ظ… ط§ظ„ظپط±ط¹ (آ§8.3). */
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
  /** ط§ظ„ط§ط³طھط¦ظ†ط§ظپ ط§ظ„ظ…ط±طھط¨ط· ط¨ط§ظ„طھظ†ط¨ظٹظ‡ â€” ظ„ظ„ط§ظ†طھظ‚ط§ظ„ ط§ظ„ظ…ط¨ط§ط´ط± ط¥ظ„ظ‰ طھظپط§طµظٹظ„ظ‡. */
  appealId?: number;
  /** ط§ظ„ط¥ظ†ط§ط¨ط© ط§ظ„ظ…ط±طھط¨ط·ط© ط¨ط§ظ„طھظ†ط¨ظٹظ‡ (طھظ†ط¨ظٹظ‡ط§طھ ط§ظ„ظ…ط±ط¢ط©/ط§ظ„ظ…طھط§ط¨ط¹ط©) â€” ظ„ط´ط±ظٹط·ظٹ ظ†ط´ط§ط· ط¨ط·ط§ظ‚طھظٹ ط§ظ„ط¥ظ†ط§ط¨ط©. */
  delegationId?: number | null;
  /** ظƒطھط§ط¨ ط§ظ„ظ…ط·ط§ظ„ط¹ط© ط§ظ„ظ…ط±طھط¨ط· (طھظ†ط¨ظٹظ‡ ط§ظ„ط±ط¯) â€” ظ„ظ„ط§ظ†طھظ‚ط§ظ„ ط§ظ„ظ…ط¨ط§ط´ط± ط¥ظ„ظ‰ طµظپط­ط© ط§ظ„ظƒطھط§ط¨. */
  reviewLetterId?: number | null;
  createdAt: string;
  createdByName?: string;
}

/** طھط°ظƒظٹط± ط¥ط¬ط±ط§ط، ط¹ظ„ظ‰ ط§ط³طھط¦ظ†ط§ظپ ظٹطھط§ط¨ط¹ظ‡ ط§ظ„ظ…ط­ط§ظ…ظٹ (ط¨ط·ط§ظ‚ط© ط§ظ„طھط°ظƒظٹط±ط§طھ). */
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
  /** ظˆط³ظ… ط¬ظˆط¯ط©: طھط§ط±ظٹط®/ظ…ط¯ط© ظ‚ط¯ظٹظ…ط© ظ„ط§ طھظڈط­ظ„ظژظ‘ظ„ ظپط³ظ‚ط· ط§ظ„ط­ط³ط§ط¨ ط¥ظ„ظ‰ ط§ط­طھظٹط§ط· â€” ظٹظڈط¹ط±ط¶ ظ…ظˆط³ظˆظ…ظ‹ط§. */
  dueDateSuspect: boolean;
}

export interface CreateHeadAlertRequest {
  targetType: HeadAlertTargetType;
  documentId?: number | null;
  targetLawyerId?: number | null;
  message: string;
}

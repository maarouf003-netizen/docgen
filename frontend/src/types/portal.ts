import type { PagedResult } from './common';
import type { PublicEntityType } from './entity';

/** ظ†ط·ط§ظ‚ ط§ظ„ظ…ظ†ط¯ظˆط¨: ظ‡ظˆظٹط© ط£ظ… ط¨ظƒظ„ ظ‚ظٹظˆط¯ظ‡ط§ ط£ظˆ ظ‚ظٹط¯ ط¨ط¹ظٹظ†ظ‡. */
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

/** ظ…ظ„ظپ ظپظٹ ظ‚ط§ط¦ظ…ط© ط§ظ„ط¨ظˆط§ط¨ط© â€” ظ‚ط±ط§ط،ط© ظپظ‚ط· ط¨ظ„ط§ ط­ظ‚ظˆظ„ ط¯ط§ط®ظ„ظٹط© ظ„ظ„ظ…ط­ط§ظ…ظٹظ†. */
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
  /** ظ…ظƒظˆظ‘ظ†ط§ ط§ظ„ط§ط³ظ… ط§ظ„ط«ظ„ط§ط«ظٹ ظ„ظ„ظ…ظ†ظپط° ط¹ظ„ظٹظ‡. */
  borrowerFather?: string | null;
  borrowerFamily?: string | null;
  /** ظ†ظˆط¹ ط§ظ„ظ…ظ„ظپ (FileType: ط³ظ†ط¯ ظ…طµط§ط±ظپطŒ طھط£ظ…ظٹظ†طŒ â€¦). */
  fileType?: string | null;
  /** ط¯ط§ط¦ط±ط© ط§ظ„طھظ†ظپظٹط° ط§ظ„ظ…ط®طھطµط© â€” طھظڈط®ظپظ‰ ط¹ط±ط¶ظٹظ‹ط§ ط¹ظ†ط¯ ظپط±ط§ط؛ظ‡ط§. */
  court?: string | null;
  /** ط£ط­ط¯ط« ط±ظ‚ظ… ط£ط³ط§ط³ ط¯ط§ط¦ظ…ظ‹ط§ ظˆط¥ظ„ط§ ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ط£طµظ„ظٹ. */
  displayBaseNumber?: string | null;
  /** ط³ظ†ط© ط±ظ‚ظ… ط§ظ„ط£ط³ط§ط³ ط§ظ„ظ…ط¹ط±ظˆط¶ ظˆط¥ظ„ط§ ط³ظ†ط© ظ‚ظٹط¯ ط§ظ„ظ…ظ„ظپ ط§ظ„ط£طµظ„ظٹط©. */
  displayBaseYear?: string | null;
  /** ظ‚ظٹظˆط¯ ط§ظ„ظ†ط·ط§ظ‚ ط§ظ„ظ…ط·ط§ط¨ظ‚ط© ظ„ظ‡ط°ط§ ط§ظ„ظ…ظ„ظپ (ظ„ظ„ط³ط·ط± ط§ظ„ط«ط§ظ†ظٹ آ«ظپط±ط¹ ط§ظ„ط¬ظ‡ط© ط§ظ„ط¹ط§ظ…ط©آ»). */
  matchedEntries?: PortalScopeEntryDto[] | null;
  /** ط­ط§ظ„ط© ط§ظ„ط¹ط±ط¶ ظ…ظ† ط§ظ„ط®ط§ط¯ظ… (`DocumentStatusResolver`) â€” طھط´طھظ‚ ظ…ظ†ظ‡ط§ ط§ظ„ط´ط§ط±ط© ظ…ط¨ط§ط´ط±ط© ط¨ظ„ط§ ط¥ط¹ط§ط¯ط© طھطµظ†ظٹظپ. */
  displayStatus?: string | null;
}

export type PortalFilesResponse = PagedResult<PortalFileListItemDto>;

/** ط§ط³طھط¦ظ†ط§ظپ ظ‚ط±ط§ط¦ظٹ ط¹ظ„ظ‰ ط¨ط·ط§ظ‚ط© ط§ط³طھط¦ظ†ط§ظپط§طھ ط§ظ„ط¨ظˆط§ط¨ط©. */
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

/** ط¥ط¬ط±ط§ط، طھظ†ظپظٹط°ظٹ ظ‚ط±ط§ط¦ظٹ ط¹ظ„ظ‰ ط¨ط·ط§ظ‚ط© آ«ط§ظ„ط¥ط¬ط±ط§ط،ط§طھ ط§ظ„طھظ†ظپظٹط°ظٹط©آ» â€” ظ†ظˆط¹ action ظپظ‚ط·طŒ ط¨ظ„ط§ ط´ط§ط±ط© طھط°ظƒظٹط± (ظ‚7). */
export interface PortalExecutionActionDto {
  id: number;
  /** ظ†طµ ط§ظ„ط¥ط¬ط±ط§ط، ط§ظ„ط؛ظ†ظٹ â€” ظٹظڈط¹ظ‚ظژظ‘ظ… ط¹ط¨ط± sanitizeRichText ظ‚ط¨ظ„ ط§ظ„ط¹ط±ط¶. */
  text: string;
  /** طھط§ط±ظٹط® ط§ظ„ط¥ط¬ط±ط§ط، (ظ†طµ ط­ط± ظٹظڈط¹ط±ط¶ ظƒظ…ط§ ظ‡ظˆ). */
  actionDate?: string;
  /** ط§ظ„ظ…ط­ط§ظ…ظٹ ط§ظ„ط°ظٹ ط£ط¯ط®ظ„ ط§ظ„ط¥ط¬ط±ط§ط،. */
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

/* â”€â”€ ط¥ط­طµط§ط،ط§طھ ط§ظ„ط¬ظ‡ط© (ط§ظ„ظ…ط±ط­ظ„ط© 4) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

export interface PortalMonthlyCountDto {
  year: number;
  month: number;
  files: number;
}

/** طھظˆط²ظٹط¹ ط§ظ„ط§ط±طھط¨ط§ط· ط¹ظ„ظ‰ ظ‚ظٹظˆط¯ ط§ظ„ظ†ط·ط§ظ‚ط› ظ‚ط¯ ظٹظڈط­طھط³ط¨ ط§ظ„ظ…ظ„ظپ طھط­طھ ط£ظƒط«ط± ظ…ظ† ظ‚ظٹط¯. */
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

/** ظ…ط¨ط§ظ„ط؛ ط³ظ„ظ‘ط© ط­ط§ظ„ط© ظˆط§ط­ط¯ط© ظ…ظƒط³ظ‘ط±ط© ط­ط³ط¨ ط§ظ„ط¹ظ…ظ„ط© (ظ„ط§ ط®ظ„ط· ط¨ظٹظ† ط§ظ„ط¹ظ…ظ„ط§طھ). */
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
  /** ط¹ط¯ط¯ ظ…ظ„ظپط§طھ آ«ظ…ط­ط§ظ„ ط§ظ„ظ‰ ط§ظ„ط¨ط¯ط§ظٹط©آ» ظپظٹ ط§ظ„ظ†ط·ط§ظ‚ (ظپظ„طھط± ظ…ط³طھظ‚ظ„ â€” ظ„ط§ ظٹط´ظ…ظ„ظ‡ط§ ظپظ„طھط± آ«ظ…ظ†ظپط°آ»). */
  referredToStartFiles?: number;
  pendingAppeals: number;
  closedAppeals: number;
  /** ط¢ط®ط± 12 ط´ظ‡ط±ظ‹ط§ ظ…طھطµظ„ط© ط­طھظ‰ ط§ظ„ط´ظ‡ط± ط§ظ„ط­ط§ظ„ظٹ ط´ط§ظ…ظ„ط© ط§ظ„ط£ط´ظ‡ط± ط§ظ„طµظپط±ظٹط©. */
  monthly: PortalMonthlyCountDto[];
  perEntry: PortalEntryStatDto[];
  topCurrencies: PortalCurrencyStatDto[];
  /** ط¥ط¬ظ…ط§ظ„ظٹ ط§ظ„ظ…ط¨ط§ظ„ط؛ ظ…ظƒط³ظ‘ط±ظ‹ط§ ط­ط³ط¨ ط§ظ„ط¹ظ…ظ„ط©. */
  amountTotals?: PortalCurrencyStatDto[] | null;
  /** ط§ظ„ظ…ط¨ط§ظ„ط؛ ظ„ظƒظ„ ط³ظ„ظ‘ط© ط­ط§ظ„ط© ظ…ظƒط³ظ‘ط±ط© ط­ط³ط¨ ط§ظ„ط¹ظ…ظ„ط©. */
  amountByStatus?: PortalStatusAmountDto[] | null;
}

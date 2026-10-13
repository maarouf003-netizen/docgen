/** ط¹ط¯ط§ط¯ط§طھ ط¨ط·ط§ظ‚ط© آ«ط§ظ„ط§ط³طھط¦ظ†ط§ظپط§طھآ» ظپظٹ ظ„ظˆط­ط© ط§ظ„ظ…ط­ط§ظ…ظٹ. */
export interface AppealsStatsDto {
  pendingCount: number;
  decidedInFavor: number;
  decidedAgainst: number;
}

/* â”€â”€ ط§ظ„ط§ط³طھط¦ظ†ط§ظپط§طھ ط¹ظ„ظ‰ ط§ظ„ظ…ظ„ظپ ط§ظ„طھظ†ظپظٹط°ظٹ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** ط§طھط¬ط§ظ‡ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ: ظ…ط³طھط£ظ†ظگظپظٹظ† (ظ†ط­ظ†) ط£ظˆ ظ…ط³طھط£ظ†ظپ ط¹ظ„ظٹظ†ط§. */
export type AppealDirection = 'appellants' | 'against-us';

/** ط­ط§ظ„ط© ط§ظ„ط§ط³طھط¦ظ†ط§ظپ: ظ…ظ†ط¸ظˆط± (ظ‚ظٹط¯ ط§ظ„ظ†ط¸ط±) / ظ…ط­ط³ظˆظ… / ظ…ط´ط·ظˆط¨. */
export type AppealStatus = 'pending' | 'decided' | 'struck-off';

/** ظ†طھظٹط¬ط© ط§ظ„ط§ط³طھط¦ظ†ط§ظپ ط§ظ„ظ…ط­ط³ظˆظ…: ظ„ظ„طµط§ظ„ط­ / ظ„ظ„ط¶ط¯. */
export type AppealOutcome = 'in-favor' | 'against';

/** ط·ط±ظپ ط¶ظ…ظ† ط§ط³طھط¦ظ†ط§ظپ (ظ„ظ‚ط·ط© ظˆظ‚طھ ط§ظ„ط¥ظ†ط´ط§ط،): ظ†ظˆط¹ظ‡ ط§ظ„ظ…ط±ط¬ط¹ظٹ ظˆظ…ط¹ط±ظ‘ظپظ‡ ظˆط§ط³ظ…ظ‡ ط§ظ„ظ…ط¹ط±ظˆط¶. */
export interface AppealPartyDto {
  kind: string;
  partyId: number;
  name: string;
}

/** ط§ط®طھظٹط§ط± ط§ظ„ظ…ط³طھط£ظ†ظپ ظ…ظ† ط§ظ„ظˆط§ط¬ظ‡ط© (ظٹظڈط¹ط§ط¯ ط¨ظ†ط§ط، ط§ظ„ط§ط³ظ… ظ…ظ† ط§ظ„ظ…ظ„ظپ ط§ظ„ط£ط³ط§ط³ ط¹ظ„ظ‰ ط§ظ„ط®ط§ط¯ظ…). */
export interface AppealPartySelectionDto {
  kind: string;
  partyId: number;
}

/** ط§ط³طھط¦ظ†ط§ظپ ظƒط§ظ…ظ„ ط§ظ„طھظپط§طµظٹظ„ (ط§ظ„طھظˆط§ط±ظٹط® ظ†طµظٹط© ط¨طµظٹط؛ط© yyyy-MM-dd ظ…ظ† ط§ظ„ط®ظ„ظپظٹط©). */
export interface AppealDto {
  id: number;
  documentId: number;
  documentLabel?: string;
  fileNumber?: string;
  fileType?: string;
  fileYear?: string;
  court?: string;
  direction: AppealDirection;
  directionLabel: string;
  status: AppealStatus;
  statusLabel: string;
  appealTypeLabel?: string;
  appellants: AppealPartyDto[];
  appellees: AppealPartyDto[];
  appealedDecisionText?: string;
  appealedDecisionSummary?: string;
  appealedDecisionDate?: string;
  inspectionBookNumber?: string;
  inspectionBookDate?: string;
  groundsSummary?: string;
  noticeNumber?: string;
  noticeDate?: string;
  appellateCourt?: string;
  appealBaseNumber?: string;
  appealYear?: string;
  depositBookNumber?: string;
  depositBookDate?: string;
  defenseOpinion?: string;
  registrationDate?: string;
  decisionNumber?: string;
  decisionDate?: string;
  decisionRuling?: string;
  outcome?: AppealOutcome;
  outcomeLabel?: string;
  struckOffDate?: string;
  struckOffDecisionNumber?: string;
  notes?: string;
  /** ظٹط­طھط§ط¬ طھط¯ظˆظٹط± ط±ظ‚ظ… ط£ط³ط§ط³ظ‡ ظ„ط³ظ†ط© ط§ظ„طھط¯ظˆظٹط± ط§ظ„ط­ط§ظ„ظٹط© (ظٹط¸ظ‡ط± ط¨ط§ظ„ط£ط­ظ…ط±). */
  needsRotation: boolean;
  /** ط±ظ‚ظ… ط§ظ„ط£ط³ط§ط³ ط§ظ„ظ…ط¹ط±ظˆط¶: ط³ط¬ظ„ ط§ظ„ط³ظ†ط© ط§ظ„ط­ط§ظ„ظٹط© ط¥ظ† ظˆظڈط¬ط¯ ظˆط¥ظ„ط§ ط§ظ„ط±ظ‚ظ… ط§ظ„ط£طµظ„ظٹ ط§ظ„ظ…ط³ط¬ظ‘ظ„. */
  currentBaseNumber?: string;
  assignedLawyerId?: number;
  assignedLawyerName?: string;
  createdAt: string;
  createdByName?: string;
  createdById: number;
  /** ط±ظ‚ظ… ط§ظ„ظ…ظ„ظپ ط§ظ„ظپط¹ظ‘ط§ظ„ (ظ…ظ† ط£ط±ظ‚ط§ظ… ط£ط³ط§ط³ ط§ظ„ظ…ظ„ظپ ظ†ظپط³ظ‡) ط§ظ„ظ…ط¹ط±ظˆط¶ ظپظٹ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ. */
  documentEffectiveNumber?: string | null;
  /** ط³ظ†ط© ط§ظ„ط±ظ‚ظ… ط§ظ„ظپط¹ظ‘ط§ظ„ ط§ظ„ظ…ط¹ط±ظˆط¶ ظ„ظ„ظ…ظ„ظپ ظپظٹ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ. */
  documentEffectiveYear?: string | null;
  /** ظˆط³ظ… ط¬ظˆط¯ط© ط§ظ„ظ„ظ‚ط·ط§طھ: ط¥ط­ط¯ظ‰ ظ„ظ‚ط·طھظٹ ط§ظ„ط£ط·ط±ط§ظپ طھط§ظ„ظپط© ظپط¹ظڈط±ط¶طھ ظپط§ط±ط؛ط© â€” طھظڈط¹ط±ط¶ ظ…ظˆط³ظˆظ…ط©. */
  partiesDegraded: boolean;
  /** ط­ط§ظ„ط© ط§ظ„ط¥ط­ط§ظ„ط© (`Owned` ط£ظˆ `ForwardedToHead`) â€” ظ„ظ„ط¨ط­ط« ظˆط§ظ„ط¹ط±ط¶ (ط§ظ„ظ…ط±ط­ظ„ط© 5ط¨). */
  forwardState?: string;
  /** ط´ط¹ط¨ط© ط¯ط§ط¦ط±ط© ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط§ظ„ظƒط© (`undefined` = ظ…ظ„ظƒ ط§ظ„ظ‚ط³ظ…) â€” ظ„ظ„ط¨ط­ط« ظˆط§ظ„ط¹ط±ط¶. */
  sectionId?: number | null;
  /** ط¯ط§ط¦ط±ط© ط§ظ„ظ…ظ„ظپ â€” ظ„ظ„ط¨ط­ط« ظˆط§ظ„ط¹ط±ط¶. */
  executionCircuitId?: number | null;
  /** ط±ظ…ط² ط§ظ„طھط²ط§ظ…ظ† ط§ظ„طھظپط§ط¤ظ„ظٹ â€” ظٹظڈط±ط³ظژظ„ ظپظٹ ط·ظ„ط¨ط§طھ ط§ظ„ظƒطھط§ط¨ط© ط§ظ„ط¯ط§ط¹ظ…ط© ظ„ظƒط´ظپ ط§ظ„ط³ط¨ط§ظ‚. */
  version?: number;
}

/** طھط³ط·ظٹط±/طھط¹ط¯ظٹظ„ ط§ط³طھط¦ظ†ط§ظپ ظ‚ط¨ظ„ ط§ظ„ط¥ط³ظ†ط§ط¯ (ط§ظ„طھظˆط§ط±ظٹط® ظ†طµظˆطµ ط­ط±ط© ط¨طµظٹط؛ط© آ«1/8/2026آ»). */
export interface UpsertAppealRequest {
  direction: AppealDirection;
  appellants?: AppealPartySelectionDto[];
  appealTypeLabel?: string;
  appealedDecisionText?: string;
  appealedDecisionSummary?: string;
  appealedDecisionDate?: string;
  inspectionBookNumber?: string;
  inspectionBookDate?: string;
  groundsSummary?: string;
  noticeNumber?: string;
  noticeDate?: string;
  appellateCourt?: string;
  appealBaseNumber?: string;
  appealYear?: string;
  depositBookNumber?: string;
  depositBookDate?: string;
  defenseOpinion?: string;
  notes?: string;
}

/** طھط­ط¯ظٹط« ط­ظ‚ظˆظ„ ط§ظ„ظ‚ظٹط¯ ظ„ظ„ط§ط³طھط¦ظ†ط§ظپ â€” ط§ظ„ظ…ط­ط§ظ…ظٹ ط§ظ„ظ…طھط§ط¨ط¹. */
export interface UpdateAppealRegistrationRequest {
  appealTypeLabel?: string;
  appellateCourt?: string;
  appealBaseNumber?: string;
  appealYear?: string;
  registrationDate?: string;
}

/** ط­ط³ظ… ط§ظ„ط§ط³طھط¦ظ†ط§ظپ ط¨ط±ظ‚ظ… ظ‚ط±ط§ط± ط§ظ„ط­ط³ظ… ظˆطھط§ط±ظٹط®ظ‡ ظˆظ…ظ†ط·ظˆظ‚ظ‡ ظˆظ†طھظٹط¬طھظ‡. */
export interface DecideAppealRequest {
  decisionNumber: string;
  decisionDate: string;
  decisionRuling: string;
  outcome: AppealOutcome;
}

/** ط´ط·ط¨ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ ط¨طھط§ط±ظٹط® ط§ظ„ط´ط·ط¨ ظˆط±ظ‚ظ… ظ‚ط±ط§ط± ط§ظ„ط´ط·ط¨. */
export interface StrikeAppealRequest {
  struckOffDecisionNumber: string;
  struckOffDate: string;
}

/** ط¥ط³ظ†ط§ط¯ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ ط¥ظ„ظ‰ ظ…ط­ط§ظ…ظچ ظ„ظ„ظ…طھط§ط¨ط¹ط© â€” ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ…. */
export interface AssignAppealRequest {
  assignedLawyerId: number;
  /** ط±ظ…ط² ط§ظ„طھط²ط§ظ…ظ† ط§ظ„طھظپط§ط¤ظ„ظٹ (ظ…ظ† `AppealDto.version`) â€” ط§ط®طھظٹط§ط±ظٹطŒ ط؛ظٹط§ط¨ظ‡ ظٹط¹ظ†ظٹ ظپط­طµ ط§ظ„ظ‚ط§ط¹ط¯ط© ظپظ‚ط·. */
  version?: number;
}

/** ظ†ظ‚ظ„ ط§ط³طھط¦ظ†ط§ظپ ظ…ظپط±ط¯ ط¨ظٹظ† ظ…ط­ط§ظ…ظٹ ط§ظ„ظپط±ط¹ â€” ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ…. */
export interface TransferAppealRequest {
  targetLawyerId: number;
  /** ط±ظ…ط² ط§ظ„طھط²ط§ظ…ظ† ط§ظ„طھظپط§ط¤ظ„ظٹ (ظ…ظ† `AppealDto.version`) â€” ط§ط®طھظٹط§ط±ظٹ. */
  version?: number;
}

/** ط¥ط­ط§ظ„ط© ط§ط³طھط¦ظ†ط§ظپ ظ…ظ† ط±ط¦ظٹط³ ط§ظ„ط´ط¹ط¨ط© ظ„ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ… (ط§طھط¬ط§ظ‡ ظˆط§ط­ط¯) â€” ط§ظ„ط³ط¨ط¨ ط§ط®طھظٹط§ط±ظٹ. */
export interface ForwardAppealRequest {
  reason?: string | null;
  /** ط±ظ…ط² ط§ظ„طھط²ط§ظ…ظ† ط§ظ„طھظپط§ط¤ظ„ظٹ (ظ…ظ† `AppealDto.version`) â€” ط§ط®طھظٹط§ط±ظٹ. */
  version?: number;
}

/** ظ†ظ‚ظ„ ظƒظ„ ط§ط³طھط¦ظ†ط§ظپط§طھ ظ…ط­ط§ظ…ظچ ط¥ظ„ظ‰ ظ…ط­ط§ظ…ظچ ط¢ط®ط± ط¶ظ…ظ† ط§ظ„ظپط±ط¹ ظ†ظپط³ظ‡. */
export interface TransferAllAppealsRequest {
  sourceLawyerId: number;
  targetLawyerId: number;
}

/** ط¥ط¯ط®ط§ظ„ ط±ظ‚ظ… ط§ظ„ط£ط³ط§ط³ ط§ظ„ط§ط³طھط¦ظ†ط§ظپظٹ ظ„ط³ظ†ط© ط§ظ„طھط¯ظˆظٹط± ط§ظ„ط­ط§ظ„ظٹط©. */
export interface AppealBaseNumberEntry {
  baseNumber?: string;
}

export interface SaveAppealBaseNumbersRequest {
  entries: AppealBaseNumberEntry[];
}

/** ط³ط¬ظ„ ط±ظ‚ظ… ط£ط³ط§ط³ ط§ط³طھط¦ظ†ط§ظپظٹ ظ„ط³ظ†ط© ط³ط§ط¨ظ‚ط© ط£ظˆ ط­ط§ظ„ظٹط©. */
export interface AppealBaseNumberHistoryDto {
  year: number;
  baseNumber: string;
}

/** ط¥ط¬ط±ط§ط،/ظ…ظ„ط§ط­ط¸ط© ط¹ظ„ظ‰ ط§ظ„ط§ط³طھط¦ظ†ط§ظپ ظ…ط¹ طھط°ظƒظٹط±ظ‡ ط§ظ„ط§ط®طھظٹط§ط±ظٹ (ظ…ط¯ط© + ظ„ظˆظ†). */
export interface AddAppealActionRequest {
  type?: string;
  text: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
}

export interface UpdateAppealActionRequest extends AddAppealActionRequest {}

export interface AppealActionDto {
  id: number;
  type: string;
  text: string;
  actionDate?: string;
  reminderDuration?: string;
  reminderColor?: string;
  createdByName?: string;
  createdAt: string;
}

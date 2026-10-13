/* â”€â”€ ط³ط¬ظ„ ط¯ظˆط§ط¦ط± ط§ظ„طھظ†ظپظٹط° (BQ-004) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** ط¯ط§ط¦ط±ط© طھظ†ظپظٹط° ظˆط§ط­ط¯ط© ظ…ط¹ ط¹ط¯ط§ط¯ط§طھظ‡ط§ (ظ…ظ„ظپط§طھ + ظ…ط¹ظ„ظ‚ط§طھ). */
export interface ExecutionCircuitDto {
  id: number;
  branchId: number;
  branchName?: string | null;
  name: string;
  isActive: boolean;
  fileCount: number;
  pendingCount: number;
  version: number;
  /** ظ…ط¹ط±ظ‘ظپ ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط§ظ„ظƒط© â€” ط؛ط§ط¦ط¨/`null` ظٹط¹ظ†ظٹ ظ‚ط³ظ… ط§ظ„ظپط±ط¹ (آ§8). */
  sectionId?: number | null;
  /** ط§ط³ظ… ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط§ظ„ظƒط© â€” ظ„ظ„ط¹ط±ط¶ ظپظ‚ط·. */
  sectionName?: string | null;
}

/** ط¥ط¯ط®ط§ظ„/طھط³ظ…ظٹط© ط¯ط§ط¦ط±ط© (ط§ظ„ط§ط³ظ… ظپظ‚ط· â€” ط§ظ„ظپط±ط¹ ظ…ظ† ط³ظٹط§ظ‚ ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ…). */
export interface UpsertExecutionCircuitRequest {
  name?: string | null;
  version?: number | null;
}

/** طھط¹ط·ظٹظ„/طھظپط¹ظٹظ„ ط¯ط§ط¦ط±ط©. */
export interface SetCircuitActiveRequest {
  isActive: boolean;
  version?: number | null;
}

/** ط¥ط­ط§ظ„ط© ط¯ظپط¹ط© ظ…ظ„ظپط§طھ ظ…ظ† ط¯ط§ط¦ط±ط© ط¥ظ„ظ‰ ظ…ط­ط§ظ…ظچ (ظ†ظپط³ ط§ظ„ظپط±ط¹طŒ ط°ط±ظٹ). */
export interface ReferCircuitFilesRequest {
  fileIds: number[];
  targetLawyerId: number;
  targetCircuitId: number;
}

/** طµظپ ظ…ظ„ظپ ط¨ط§ظ†طھط¸ط§ط± ط¥ط¹ط§ط¯ط© ط§ظ„ظ‚ظٹط¯ (ط§ظ„ظ…ط¹ط±ظˆط¶ ط­ط§ظ„ظٹظ‹ط§ ظ‚ط¯ظٹظ… ظ…ظ† EffectiveFileIdentity). */
export interface PendingRegistrationDto {
  documentId: number;
  circuitId: number;
  circuitName?: string | null;
  borrowerName?: string | null;
  oldFileNumber?: string | null;
  oldFileType?: string | null;
  oldFileYear?: string | null;
}

/** ط­ظپط¸ ط°ط±ظٹ ظ„ط¥ط¹ط§ط¯ط© ط§ظ„ظ‚ظٹط¯ (ظ…ظ„ظƒ ط§ظ„ظ…ط­ط§ظ…ظٹ ظپظ‚ط· + ظˆط­ط¯ط§ظ†ظٹط©). */
export interface CompleteRegistrationsRequest {
  entries: Array<{
    documentId: number;
    fileNumber?: string | null;
    fileType?: string | null;
    fileYear?: string | null;
  }>;
}

/** طµظپ ط¥ط­طµط§ط¦ظٹط© ط¯ط§ط¦ط±ط© (ط§ظ„ط¯ط§ط¦ط±ط© أ— ظ…ظ„ظپط§طھظ‡ط§ أ— ظ…ط­ط§ظ…ظˆظ† ظ†ط´ط·ظˆظ† أ— ظ…ط¹ظ„ظ‚ط§طھ) â€” ظ…ط¹ ط¹ظ…ظˆط¯ ط§ظ„ط´ط¹ط¨ط© (آ§12). */
export interface CircuitStatsDto {
  circuitId: number;
  circuitName: string;
  branchId: number;
  branchName?: string | null;
  isActive: boolean;
  fileCount: number;
  lawyerCount: number;
  pendingCount: number;
  /** ط´ط¹ط¨ط© ط§ظ„ط¯ط§ط¦ط±ط© ط§ظ„ظ…ط§ظ„ظƒط© â€” ط؛ط§ط¦ط¨ ظٹط¹ظ†ظٹ ط§ظ„ظ‚ط³ظ…. */
  sectionId?: number | null;
  /** ط§ط³ظ… ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط§ظ„ظƒط© â€” ظ„ظ„ط¹ط±ط¶ ظپظ‚ط·. */
  sectionName?: string | null;
  /** ط±ظ…ط² ط§ظ„طھط²ط§ظ…ظ† ط§ظ„طھظپط§ط¤ظ„ظٹ â€” ظٹظڈط±ط³ظژظ„ ظپظٹ ظ†ظ‚ظ„ ط§ظ„ط¯ط§ط¦ط±ط© ظ„ظƒط´ظپ ط§ظ„ط³ط¨ط§ظ‚. */
  version?: number | null;
}

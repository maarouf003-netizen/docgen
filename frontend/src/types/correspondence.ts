/* â”€â”€ ط§ظ„ظ…ط±ط§ط³ظ„ط§طھ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

export type CorrespondenceMessageKind = 'letter' | 'addendum' | 'reply';

export type CorrespondenceImportance = 'normal' | 'important' | 'urgent';

/** ط­ط§ظ„ط© ط§ط·ظ„ط§ط¹ ط§ظ„ط·ط±ظپ ط§ظ„ظ…ط³طھظ„ظ… ظƒظ…ط§ ظٹظ‚ط±ظ‘ط±ظ‡ط§ ط§ظ„ط®ط§ط¯ظ… â€” ظ„ط§ طھظڈط´طھظ‚ظ‘ ظپظٹ ط§ظ„ظˆط§ط¬ظ‡ط©. */
export type CorrespondenceViewStatus = 'seen' | 'pending';

/** ط³ظٹط§ظ‚ ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط±طھط¨ط· ط¨طµظٹط؛ط© ط§ظ„ط¹ط±ط¶: ظ…ط±ط§ط³ظ„ط© ط¨ظ…ظ„ظپ (ط§ظ„ط§ط³ظ… ط§ظ„ط«ظ„ط§ط«ظٹ) ط±ظ‚ظ….. ظ†ظˆط¹.. ظ„ط¹ط§ظ….. ط¯ط§ط¦ط±ط© طھظ†ظپظٹط°.. */
export interface CorrespondenceFileContext {
  executedName: string;
  fileNumber: string | null;
  fileType: string | null;
  fileYear: string | null;
  court: string | null;
}

/** ط±ط³ط§ظ„ط© ظˆط§ط­ط¯ط© ط¶ظ…ظ† ظ…ط±ط§ط³ظ„ط© (ط§ظ„ط£طµظ„ letter ط£ظˆ ظ„ط§ط­ظ‚ addendum ط£ظˆ ط±ط¯ reply) â€” ط§ظ„ظƒط§طھط¨ ظ‚ط¯ ظٹظƒظˆظ† ط±ط¦ظٹط³ ط´ط¹ط¨ط© ظƒظ…ط³طھظ„ظ…. */
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

/** طھظˆط«ظٹظ‚ ظ…ط´ط§ظ‡ط¯ط© ظˆط§ط­ط¯ط©: ظ…ظ† ط´ط§ظ‡ط¯ ظˆظ…طھظ‰. */
export interface CorrespondenceReceiptDto {
  userId: number;
  userName: string;
  seenAt: string;
}

/** ط³ط·ط± ظ…ط±ط§ط³ظ„ط© ظپظٹ ط§ظ„ظ‚ط§ط¦ظ…ط©ط› fileContext ظپط§ط±ط؛ ظ„ظ„ظ…ط±ط§ط³ظ„ط© ط§ظ„ط¹ط§ظ…ط© ط؛ظٹط± ط§ظ„ظ…ط±طھط¨ط·ط© ط¨ظ…ظ„ظپ. */
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
  /** ط­ط§ظ„ط© ط§ط·ظ„ط§ط¹ ط§ظ„ط·ط±ظپ ط§ظ„ظ…ط³طھظ„ظ…: 'seen' ط£ظˆ 'pending'. */
  viewStatus: CorrespondenceViewStatus;
  /** ظ‡ظ„ ظ„ظ‡ط°ط§ ط§ظ„ظ‚ط§ط±ط¦ ط­ظ‚ ط§ظ„طھظˆط«ظٹظ‚طں (ط§ظ„ظ…ط³طھظ„ظ… ظˆط­ط¯ظ‡). */
  canMarkSeen: boolean;
  /** ظ‡ظ„ ظ„ظ‡ط°ط§ ط§ظ„ظ‚ط§ط±ط¦ ط­ظ‚ ط§ظ„ط±ط¯طں (ط§ظ„ظ…ط³طھظ„ظ… ظˆط­ط¯ظ‡ â€” ظ…ظ† ط§ظ„ط®ط§ط¯ظ…). */
  canReply: boolean;
  messagesCount: number;
  /** ط§ط³ظ… ظپط±ط¹ ط§ظ„ط¥ط¯ط§ط±ط© â€” ظٹظڈط¹ط±ط¶ ظ„ظ„ظ…ط¯ظٹط±/ط§ظ„ظ…ط´ط±ظپ ظپظ‚ط·. */
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
  /** ط§ط³ظ… ظپط±ط¹ ط§ظ„ط¥ط¯ط§ط±ط© â€” ظٹظڈط¹ط±ط¶ ظ„ظ„ظ…ط¯ظٹط±/ط§ظ„ظ…ط´ط±ظپ ظپظ‚ط·. */
  administrativeBranchName: string | null;
  creatorId: number;
  creatorName: string;
  creatorRole: string;
  targetUserId: number;
  targetName: string;
  targetRole: string;
  /** ط­ط§ظ„ط© ط§ط·ظ„ط§ط¹ ط§ظ„ط·ط±ظپ ط§ظ„ظ…ط³طھظ„ظ…: 'seen' ط£ظˆ 'pending'. */
  viewStatus: CorrespondenceViewStatus;
  /** ظ‡ظ„ ظ„ظ‡ط°ط§ ط§ظ„ظ‚ط§ط±ط¦ ط­ظ‚ ط§ظ„طھظˆط«ظٹظ‚طں (ط§ظ„ظ…ط³طھظ„ظ… ظˆط­ط¯ظ‡ â€” ظ…ظ† ط§ظ„ط®ط§ط¯ظ…). */
  canMarkSeen: boolean;
  /** ظ‡ظ„ ظ„ظ‡ط°ط§ ط§ظ„ظ‚ط§ط±ط¦ ط­ظ‚ ط§ظ„ط±ط¯طں (ط§ظ„ظ…ط³طھظ„ظ… ظˆط­ط¯ظ‡ â€” ظ…ظ† ط§ظ„ط®ط§ط¯ظ…). */
  canReply: boolean;
  messages: CorrespondenceMessageDto[];
  /** طھظˆط«ظٹظ‚ ظ…ط´ط§ظ‡ط¯ط© ط§ظ„ظ…ط³طھظ„ظ… ظˆط­ط¯ظ‡. */
  receipts: CorrespondenceReceiptDto[];
  createdAt: string;
  /** ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط¬ظ…ط¯ط© ظ„ظ„ظ…ط³طھظ„ظ… ط§ظ„ط±ط¦ظٹط³ (آ§10.2) â€” ط؛ط§ط¦ط¨ ظ„ط؛ظٹط±ظ‡. */
  recipientSectionId?: number | null;
}

export interface CreateCorrespondenceRequest {
  documentId?: number | null;
  targetUserId: number;
  importance: CorrespondenceImportance;
  bodyHtml: string;
  /** ط¹ظ‚ط¯ ط§ظ„ظ…ط³طھظ„ظ… ط§ظ„ط±ط¦ظٹط³ (آ§10.2): ط´ط¹ط¨ط© ظ…ط¬ظ…ط¯ط© â€” ط؛ط§ط¦ط¨ ظٹط¹ظ†ظٹ ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ…ط› ظ„ط؛ظٹط± ط§ظ„ط±ط¦ظٹط³ طھظڈط±ظپط¶. */
  recipientSectionId?: number | null;
}

/** ظ…ط³طھظ„ظ… ظ…ط±ط´ط­ ظ„ظ…ط±ط§ط³ظ„ط© ط¬ط¯ظٹط¯ط© (ط¨ط­ط« ط¨ط§ظ„ط§ط³ظ…). */
export interface CorrespondenceTargetDto {
  userId: number;
  fullName: string;
  role: string;
  branchName?: string | null;
  governorate?: string | null;
}

/* â”€â”€ ظƒطھط¨ ط§ظ„ظ…ط·ط§ظ„ط¹ط© â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

export type ReviewLetterMessageKind = 'letter' | 'addendum' | 'reply';

/** ط³ظٹط§ظ‚ ط§ظ„ظ…ظ„ظپ ط§ظ„ظ…ط±طھط¨ط· ط¨طµظٹط؛ط© ط§ظ„ط¹ط±ط¶: ظ…ط·ط§ظ„ط¹ط© ط¨ظ…ظ„ظپ (ط§ظ„ط§ط³ظ… ط§ظ„ط«ظ„ط§ط«ظٹ) ط±ظ‚ظ….. ظ†ظˆط¹.. ظ„ط¹ط§ظ….. ط¯ط§ط¦ط±ط© طھظ†ظپظٹط°.. */
export interface ReviewLetterFileContext {
  executedName: string;
  fileNumber?: string | null;
  fileType?: string | null;
  fileYear?: string | null;
  court?: string | null;
}

/** ط±ط³ط§ظ„ط© ظˆط§ط­ط¯ط© ط¶ظ…ظ† ظƒطھط§ط¨ ط§ظ„ظ…ط·ط§ظ„ط¹ط© (ط§ظ„ط£طµظ„ letter ط£ظˆ ظ„ط§ط­ظ‚ addendum ط£ظˆ ط±ط¯ reply). */
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

/** ط³ط·ط± ظƒطھط§ط¨ ظپظٹ ط§ظ„ظ‚ط§ط¦ظ…ط©ط› fileContext ظپط§ط±ط؛ ظ„ظ„ظƒطھط§ط¨ ط§ظ„ط¹ط§ظ… ط؛ظٹط± ط§ظ„ظ…ط±طھط¨ط· ط¨ظ…ظ„ظپ. */
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
  /** ظپظٹظ‡ ط±ط¯ظ‘ ط±ط¦ظٹط³ ظ‚ط³ظ… ظ„ظ… ظٹط·ظ‘ظ„ط¹ ط¹ظ„ظٹظ‡ ظ…ط­ط§ظ…ظٹ ط§ظ„ظƒطھط§ط¨ ط¨ط¹ط¯. */
  hasUnseenReply: boolean;
  messagesCount: number;
  /** ط§ط³ظ… ظپط±ط¹ ط§ظ„ط¥ط¯ط§ط±ط© â€” ظٹظڈط¹ط±ط¶ ظ„ظ„ظ…ط¯ظٹط±/ط§ظ„ظ…ط´ط±ظپ ظپظ‚ط·. */
  administrativeBranchName?: string | null;
  updatedAt: string;
  /** ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط³طھظ„ظ…ط© (ظ„ط¨ظ„ط§ ظ…ظ„ظپ â€” ط؛ط§ط¦ط¨ ظٹط¹ظ†ظٹ ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ…ط› ظ…ط¹ ظ…ظ„ظپ ظ…ط´طھظ‚ط© ظ…ظ† ط¯ط§ط¦ط±طھظ‡). */
  recipientSectionId?: number | null;
  /** ط§ط³ظ… ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط³طھظ„ظ…ط© â€” ظ„ظ„ط¹ط±ط¶ ظپظ‚ط·. */
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
  /** ط§ط³ظ… ظپط±ط¹ ط§ظ„ط¥ط¯ط§ط±ط© â€” ظٹظڈط¹ط±ط¶ ظ„ظ„ظ…ط¯ظٹط±/ط§ظ„ظ…ط´ط±ظپ ظپظ‚ط·. */
  administrativeBranchName?: string | null;
  lawyerName: string;
  /** ظپظٹظ‡ ط±ط¯ظ‘ ظ„ظ… ظٹظڈط·ظژظ‘ظ„ط¹ ط¨ط¹ط¯ â€” ظٹظڈط¹ظ„ظژظ‘ظ… ظ…ظ‚ط±ظˆط،ظ‹ط§ طھظ„ظ‚ط§ط¦ظٹظ‹ط§ ط¹ظ†ط¯ ظپطھط­ ط§ظ„ظ…ط­ط§ظ…ظٹ ظ„ظ„طµظپط­ط©. */
  hasUnseenReply?: boolean;
  messages: ReviewLetterMessageDto[];
  createdAt: string;
  /** ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط³طھظ„ظ…ط© (ظ„ط¨ظ„ط§ ظ…ظ„ظپ â€” ط؛ط§ط¦ط¨ ظٹط¹ظ†ظٹ ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ…ط› ظ…ط¹ ظ…ظ„ظپ ظ…ط´طھظ‚ط© ظ…ظ† ط¯ط§ط¦ط±طھظ‡). */
  recipientSectionId?: number | null;
  /** ط§ط³ظ… ط§ظ„ط´ط¹ط¨ط© ط§ظ„ظ…ط³طھظ„ظ…ط© â€” ظ„ظ„ط¹ط±ط¶ ظپظ‚ط·. */
  recipientSectionName?: string | null;
}

export interface CreateReviewLetterRequest {
  documentId?: number | null;
  bodyHtml: string;
  /** ط§ط®طھظٹط§ط± ط§ظ„ظ…ط³طھظ„ظ… ظ„ط¨ظ„ط§ ظ…ظ„ظپ (ط´ط¹ط¨ط©) â€” ط؛ط§ط¦ط¨ ظٹط¹ظ†ظٹ ط±ط¦ظٹط³ ط§ظ„ظ‚ط³ظ…ط› ظ…ط¹ ظ…ظ„ظپ ظٹظڈطھط¬ط§ظ‡ظ„. */
  recipientSectionId?: number | null;
}

export interface AddReviewLetterAddendumRequest {
  bodyHtml: string;
}

export interface ReplyReviewLetterRequest {
  bodyHtml: string;
}

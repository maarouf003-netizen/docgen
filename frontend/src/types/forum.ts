/* â”€â”€ ظ…ظ†طھط¯ظ‰ ط§ظ„ظ…ط­ط§ظ…ظٹظ† â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ */

/** ط±ط³ط§ظ„ط© ظ…ظ†طھط¯ظ‰: ط§ظ„ظ†طµ + ط£ط¬ط²ط§ط، ظ‡ظˆظٹط© ط§ظ„ظƒط§طھط¨ (ظٹظڈط±ظƒظ‘ط¨ظ‡ط§ ط§ظ„ط¹ظ…ظٹظ„ ط¹ط¨ط± `formatRoleScope`). */
export interface ForumMessageDto {
  id: number;
  body: string;
  authorId: number;
  authorName: string;
  authorRole: string;
  authorLocation: string;
  authorSection: string | null;
  isPinned: boolean;
  quotedMessageId: number | null;
  quotedAuthorName: string | null;
  quotedExcerpt: string | null;
  createdAt: string;
  editedById: number | null;
  editedAtUtc: string | null;
  /** ط¹ط¯ط¯ ط§ظ„ظ…ط·ظ„ط¹ظٹظ† â€” ظ„ط±ط³ط§ط¦ظ„ ط§ظ„ظƒط§طھط¨ ظ†ظپط³ظ‡ ظپظ‚ط· (0 ظ„ط؛ظٹط±ظ‡ط§). */
  readCount: number;
}

/** طµظپط­ط© طھظٹط§ط± ط¨ظ…ظپطھط§ط­ `Id`: طھطµط§ط¹ط¯ظٹط©طŒ ظˆ`hasOlder` ظ„ط²ط± آ«طھط­ظ…ظٹظ„ ط§ظ„ظ…ط²ظٹط¯آ». */
export interface ForumMessagesPageDto {
  items: ForumMessageDto[];
  hasOlder: boolean;
}

/** طµظپ ظ†ط§ظپط°ط© آ«ط´ظˆظ‡ط¯طھ ط¨ظˆط§ط³ط·ط©آ»: ظ‡ظˆظٹط© ط§ظ„ظ‚ط§ط±ط¦ ظˆظ„ط­ط¸ط© ط§ط·ظ„ط§ط¹ظ‡. */
export interface ForumReaderDto {
  userId: number;
  userName: string;
  readAtUtc: string;
}

/** ظ…ط±ط´ط­ ط°ظگظƒط±: ط£ط¬ط²ط§ط، ط§ظ„ظ‡ظˆظٹط© ظ†ظپط³ظ‡ط§ (ظٹظڈط¹ط±ط¶ ط¨ط§ظ„طµظٹط؛ط© ظ†ظپط³ظ‡ط§ ط¹ط¨ط± `formatRoleScope`). */
export interface ForumUserLookupDto {
  id: number;
  name: string;
  role: string;
  location: string;
  section: string | null;
}

/** ظ…ط±ط´ط­ ط°ظگظƒط±: ط£ط¬ط²ط§ط، ط§ظ„ظ‡ظˆظٹط© ظ†ظپط³ظ‡ط§ (ظٹظڈط¹ط±ط¶ ط¨ط§ظ„طµظٹط؛ط© ظ†ظپط³ظ‡ط§ ط¹ط¨ط± `formatRoleScope`). */
export interface ForumUserLookupDto {
  id: number;
  name: string;
  role: string;
  location: string;
  section: string | null;
}

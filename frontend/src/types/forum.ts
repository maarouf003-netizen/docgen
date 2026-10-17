/* ── منتدى المحامين ────────────────────────────────────────────────────── */

/** رسالة منتدى: النص + أجزاء هوية الكاتب (يُركّبها العميل عبر `formatRoleScope`). */
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
  /** عدد المطلعين — لرسائل الكاتب نفسه فقط (0 لغيرها). */
  readCount: number;
}

/** صفحة تيار بمفتاح `Id`: تصاعدية، و`hasOlder` لزر «تحميل المزيد». */
export interface ForumMessagesPageDto {
  items: ForumMessageDto[];
  hasOlder: boolean;
}

/** صف نافذة «شوهدت بواسطة»: هوية القارئ ولحظة اطلاعه. */
export interface ForumReaderDto {
  userId: number;
  userName: string;
  readAtUtc: string;
}

/** مرشح ذِكر: أجزاء الهوية نفسها (يُعرض بالصيغة نفسها عبر `formatRoleScope`). */
export interface ForumUserLookupDto {
  id: number;
  name: string;
  role: string;
  location: string;
  section: string | null;
}

/** مرشح ذِكر: أجزاء الهوية نفسها (يُعرض بالصيغة نفسها عبر `formatRoleScope`). */
export interface ForumUserLookupDto {
  id: number;
  name: string;
  role: string;
  location: string;
  section: string | null;
}

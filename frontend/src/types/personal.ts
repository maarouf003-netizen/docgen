/** ط§ظ„طھط°ظƒظٹط± ط§ظ„ط´ط®طµظٹ ط§ظ„ط­ط± â€” ط§ظ„طھظˆط§ط±ظٹط® ظ†طµظˆطµ `yyyy-MM-dd` (ظ‚ط§ط¹ط¯ط© `Date Fields Rule`). */
export interface PersonalReminderDto {
id: number;
title: string;
notes?: string | null;
dueDate: string;
color?: string | null;
recurrence: string;
recurrenceEnd?: string | null;
isArchived: boolean;
completedOccurrenceKeys: string[];
createdAt: string;
}

export interface CreatePersonalReminderRequest {
title: string;
notes?: string | null;
dueDate: string;
color?: string | null;
recurrence: string;
recurrenceEnd?: string | null;
}

export interface UpdatePersonalReminderRequest {
title?: string | null;
notes?: string | null;
dueDate?: string | null;
color?: string | null;
recurrence?: string | null;
recurrenceEnd?: string | null;
isArchived?: boolean | null;
}

/** ظ‚ظٹظ… ط§ظ„طھظƒط±ط§ط± ط§ظ„ظ…ط؛ظ„ظ‚ط© â€” ظ…ط·ط§ط¨ظ‚ط© `PersonalReminderCatalog` ظپظٹ ط§ظ„ط®ظ„ظپظٹط©. */
export const PERSONAL_RECURRENCES = ['ظ…ط±ط© ظˆط§ط­ط¯ط©', 'ظٹظˆظ…ظٹ', 'ط£ط³ط¨ظˆط¹ظٹ', 'ط´ظ‡ط±ظٹ'] as const;
export type PersonalRecurrence = (typeof PERSONAL_RECURRENCES)[number];

/** ط£ظ„ظˆط§ظ† ط§ظ„طھط°ظƒظٹط± ط§ظ„ظ…ط؛ظ„ظ‚ط© â€” ط§ظ„ط«ظ„ط§ط«ط© ط§ظ„ط£ظˆظ„ظ‰ ظ…ط·ط§ط¨ظ‚ط© ظ„ط£ظ„ظˆط§ظ† طھط°ظƒظٹط±ط§طھ ط§ظ„ظ…ظ„ظپط§طھ. */
export const PERSONAL_COLORS = ['ط£ط­ظ…ط±', 'ط¨ظ†ظپط³ط¬ظٹ', 'ط£طµظپط±', 'ط²ظ…ط±ط¯ظٹ'] as const;
export type PersonalColor = (typeof PERSONAL_COLORS)[number];

/** ط§ظ‚طھط±ط§ط­ طھط·ظˆظٹط± â€” `senderName` ظ„ظ„ظ…ط´ط±ظپ ظپظ‚ط·. */
export interface AppSuggestionDto {
id: number;
message: string;
createdAt: string;
isRead: boolean;
senderName?: string | null;
}

/** التذكير الشخصي الحر — التواريخ نصوص `yyyy-MM-dd` (قاعدة `Date Fields Rule`). */
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

/** قيم التكرار المغلقة — مطابقة `PersonalReminderCatalog` في الخلفية. */
export const PERSONAL_RECURRENCES = ['مرة واحدة', 'يومي', 'أسبوعي', 'شهري'] as const;
export type PersonalRecurrence = (typeof PERSONAL_RECURRENCES)[number];

/** ألوان التذكير المغلقة — الثلاثة الأولى مطابقة لألوان تذكيرات الملفات. */
export const PERSONAL_COLORS = ['أحمر', 'بنفسجي', 'أصفر', 'زمردي'] as const;
export type PersonalColor = (typeof PERSONAL_COLORS)[number];

/** اقتراح تطوير — `senderName` للمشرف فقط. */
export interface AppSuggestionDto {
id: number;
message: string;
createdAt: string;
isRead: boolean;
senderName?: string | null;
}

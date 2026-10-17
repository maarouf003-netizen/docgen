/** تنسيق العدّات في مراجعة السجل (أرقام عربية مشرقية) — مشترك النوافذ والصفحة. */
export const f = new Intl.NumberFormat('ar-EG');

export interface GroupPick {
  groupId: number;
  canonicalName: string;
  entryCount: number;
  governorates: string[];
}

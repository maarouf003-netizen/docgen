/** أسماء المحافظات السورية المعروفة (تُطابق داخل اسم الفرع). */
const SYRIAN_GOVERNORATES = [
  'ريف دمشق',
  'دير الزور',
  'القنيطرة',
  'اللاذقية',
  'السويداء',
  'الحسكة',
  'طرطوس',
  'دمشق',
  'حلب',
  'حمص',
  'حماة',
  'إدلب',
  'الرقة',
  'درعا',
] as const;

/** صيغ بديلة شائعة تُرجع التسمية الرسمية للمحافظة. */
const GOVERNORATE_ALIASES: Array<readonly [alias: string, canonical: string]> = [
  ['حماه', 'حماة'],
];

const MATCHERS: ReadonlyArray<readonly [needle: string, governorate: string]> = [
  ...GOVERNORATE_ALIASES,
  ...SYRIAN_GOVERNORATES.map((g) => [g, g] as const),
].sort((a, b) => b[0].length - a[0].length);

/**
 * يستخرج اسم المحافظة من اسم فرع المحامي:
 * «الفرع الرئيسي - دمشق» ← دمشق، «فرع حلب» ← حلب، «فرع ريف دمشق» ← ريف دمشق.
 * يُرجع النص الفارغ إن لم يُطابق اسم فرعٍ أيّ محافظة معروفة (مثل «فرع المزة»).
 */
export function governorateFromBranch(branchName: string | null | undefined): string {
  const name = (branchName ?? '').trim();
  if (!name) return '';
  const hit = MATCHERS.find(([needle]) => name.includes(needle));
  return hit ? hit[1] : '';
}

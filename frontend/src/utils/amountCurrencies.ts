/** العملات المعروضة في حقول المبالغ بترتيب العرض الثابت. */
export const CURRENCIES = ['ليرة سورية', 'دولار أمريكي', 'يورو'] as const;

/** اقرأ قيمة حقل داخل كائن نموذج عشوائي (قد يكون واجهة بلا فهرس رموز). */
function rawValue(values: object, key: string): unknown {
  return (values as Record<string, unknown>)[key];
}

/**
 * عملة الخانة الافتراضية: القيمة المحفوظة، وإلا أول العملات غير المستعملة في الخانات
 * السابقة (بحسب العملة المعروضة لكلٍّ منها، فتُشتق الخانات اللاحقة من سابقاتها).
 */
export function slotDefaultCurrency(
  values: object,
  currencyKeys: readonly string[],
  i: number,
): string {
  const stored = rawValue(values, currencyKeys[i]) as string | undefined;
  if (stored) return stored;
  return slotCurrencyOptions(values, currencyKeys, i)[0] ?? 'ليرة سورية';
}

/**
 * عملات الخانة `i` المتاحة: تُستثنى عملات الخانات السابقة منها («لا تكرار للعملة»)،
 * وتُحسب المستثنيات بحسب العملة المعروضة فعليًا لكل خانة سابقة (افتراضها إن لم تُحدد
 * صراحةً)، فلا تتكرر العملة حتى في الخانات غير المعبأة. وإن حملت الخانة عملة مستعملة
 * سابقًا (بيانات محفوظة قديمة متكررة) تُبقى أول خيار كي لا تضيع بياناتها.
 */
export function slotCurrencyOptions(
  values: object,
  currencyKeys: readonly string[],
  i: number,
): string[] {
  const used = new Set<string>();
  for (let j = 0; j < i; j++) {
    used.add(slotDefaultCurrency(values, currencyKeys, j));
  }
  const available = CURRENCIES.filter((c) => !used.has(c));
  const current = rawValue(values, currencyKeys[i]) as string | undefined;
  return current && used.has(current) ? [current, ...available] : available;
}

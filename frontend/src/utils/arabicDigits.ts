/** الأرقام العربية (٠-٩: U+0660–U+0669) والفارسية (۰-۹: U+06F0–U+06F9) إلى ASCII. */
export function normalizeArabicDigits(value: string): string {
  return value
    .replace(/[٠-٩]/g, (d) => String(d.charCodeAt(0) - 0x0660))
    .replace(/[۰-۹]/g, (d) => String(d.charCodeAt(0) - 0x06f0));
}

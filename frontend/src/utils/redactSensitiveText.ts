/**
 * تنقية نص الخطأ قبل تسجيله في وحدة التحكم أو إرساله إلى نقطة تقارير الأخطاء.
 *
 * الرسائل والـ`stack` تأتي من المتصفح/المكتبات وقد تحمل — من غير قصد —
 * كوكي الجلسة أو رمز `CSRF` أو `Bearer` أو `JWT` أو كلمة مرور في استعلام،
 * فضلًا عن بريد/هاتف (بيانات شخصية). سياسة `P6-01a`: لا رموز ولا بيانات
 * شخصية في سجلات العميل، فيُستبدل كل نمط معروف بعلامة صريحة.
 *
 * القاعدة **لا تغيّر** النص المعروض للمستخدم؛ تُطبَّق على مسار السجل/الإبلاغ فقط.
 */

const MASK = '<محجوب>';

const PATTERNS: ReadonlyArray<readonly [RegExp, string]> = [
  // كوكي الجلسة/الحماية: القيمة بعد علامة المساواة تُحجب.
  [/(\b(?:docgen_csrf|docgen_session|docgen_token)\s*=\s*)[^;\s]+/gi, `$1${MASK}`],
  // ترويسة مصادقة.
  [/\bBearer\s+[A-Za-z0-9._~+/=-]{6,}/gi, `Bearer ${MASK}`],
  [/\bX-CSRF-Token\s*:\s*\S+/gi, `X-CSRF-Token: ${MASK}`],
  // JWT (ثلاثة مقاطع base64url).
  [/\beyJ[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{6,}\b/g, '<JWT>'],
  // أسرار في سلسلة الاستعلام.
  [
    /([?&](?:token|access_token|refresh_token|id_token|api_key|key|password|passwd|secret|csrf)=)[^&#\s]+/gi,
    `$1${MASK}`,
  ],
  // بريد إلكتروني (بيان شخصي شائع في رسائل التحقق).
  [/[\w.+-]+@[\w-]+(?:\.[\w-]+)+/g, '<بريد>'],
  // هاتف/رقم طويل: 9 أرقام فأكثر مع فواصل اختيارية (يحجب أيضًا أرقامًا مرجعية).
  [/(?<![\w])\+?\d[\d\s-]{7,}\d(?![\w])/g, '<رقم>'],
  // رموز base64/hex الطويلة (مفاتيح مُولَّدة، توقيعات، معرّفات جلسة).
  [/\b[A-Za-z0-9+/=_-]{48,}\b/g, '<رمز>'],
];

/** يعيد النص بعد حجب الرموز/البيانات الشخصية المعروفة (آمن على أي مدخل). */
export function redactSensitiveText(input: unknown, maxLength = 4000): string {
  if (input == null) return '';
  let text: string;
  try {
    text = typeof input === 'string' ? input : String(input);
  } catch {
    // كائن بروتوكول `toString` مرفوض — لا نص نُسجّله.
    return '<نص غير قابل للقراءة>';
  }
  for (const [pattern, replacement] of PATTERNS) {
    text = text.replace(pattern, replacement);
  }
  return text.slice(0, Math.max(0, maxLength));
}

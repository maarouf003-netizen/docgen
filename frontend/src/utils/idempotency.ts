/** مفتاح عدم التكرار (RF-011): ترويسة `X-Idempotency-Key` لطلبات الكتابة المحمية. */
export const IDEMPOTENCY_KEY_HEADER = 'X-Idempotency-Key';

/**
 * مفتاح `uuid` جديد لكل نية إرسال: `randomUUID` حيث توفرت، وإلا بديل `v4`
 * كافٍ لعدم التخمين والتكرار (النطاق مسنود للمستخدم والعملية خادميًا).
 */
export function newIdempotencyKey(): string {
  const webCrypto = (globalThis as unknown as { crypto?: { randomUUID?: () => string } }).crypto;
  if (webCrypto && typeof webCrypto.randomUUID === 'function') {
    try {
      return webCrypto.randomUUID();
    } catch {
      // يُتجاهَل ويُستخدَم البديل أدناه.
    }
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (ch) => {
    const r = Math.floor(Math.random() * 16);
    return (ch === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

/** كائن الإعدادات الثالث لـ`api.post` حاملًا ترويسة المفتاح. */
export function idempotencyHeaders(key: string): { headers: Record<string, string> } {
  return { headers: { [IDEMPOTENCY_KEY_HEADER]: key } };
}

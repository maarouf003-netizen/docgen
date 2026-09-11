// إبلاغ أخطاء الواجهة إلى `POST /api/client-errors` — نارٍ-وانسَ وصمت تام.
// مستقل عمدًا عن مثيل `api` (axios): اعتراض `401` فيه يُعيد التوجيه إلى `/login`،
// وهذا الإبلاغ يجب ألا يُعيد توجيهًا ولا يُطلق حلقة أبدًا — لذا `fetch` خام مع
// `keepalive` (يبقى حيًا عند تفريغ الصفحة) و`credentials: same-origin` (كوكي الجلسة).
// قراءة CSRF مكررة هنا عمدًا (3 أسطر) لتفادي دورة استيراد مع `api/client`.

const ENDPOINT = '/api/client-errors';
const CSRF_COOKIE = 'docgen_csrf';
const CSRF_HEADER = 'X-CSRF-Token';

// سقوف أدنى من سقوف الخادم (2048/8192) بهامش أمان — الخادم يقصّ أيضًا دفاعًا مزدوجًا.
const MAX_MESSAGE = 2000;
const MAX_STACK = 8000;
const MAX_COMPONENT = 100;
const MAX_URL = 500;
// سقف الجلسة يمنع فيضًا من عميل مخترق/معطوب قبل خنق الخادم (30/دقيقة).
const MAX_PER_SESSION = 20;

export interface ClientErrorInput {
  message: string;
  stack?: string;
  component?: string;
  url?: string;
}

const seenFingerprints = new Set<string>();
let sentCount = 0;

function readCsrfToken(): string | null {
  try {
    const match = document.cookie.match(new RegExp(`(?:^|;\\s*)${CSRF_COOKIE}=([^;]*)`));
    return match ? decodeURIComponent(match[1]) : null;
  } catch {
    return null;
  }
}

function stripQuery(url: string): string {
  const cut = url.search(/[?#]/);
  return cut < 0 ? url : url.slice(0, cut);
}

/** يُبلغ عن خطأ واجهة؛ يعيد `true` عند الإرسال و`false` عند التجاهل — لا يرمي أبدًا. */
export function reportClientError(input: ClientErrorInput): boolean {
  try {
    const message = (input?.message ?? '').trim().slice(0, MAX_MESSAGE);
    if (message.length === 0) return false;
    if (sentCount >= MAX_PER_SESSION) return false;

    const component = (input.component ?? '').slice(0, MAX_COMPONENT);
    const fingerprint = `${component}|${message.slice(0, 200)}`;
    if (seenFingerprints.has(fingerprint)) return false;
    seenFingerprints.add(fingerprint);
    sentCount += 1;

    const rawUrl =
      input.url ??
      (typeof window !== 'undefined' && window.location ? window.location.pathname : '');
    const url = stripQuery(rawUrl).slice(0, MAX_URL);
    const stack = (input.stack ?? '').slice(0, MAX_STACK);

    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    const csrf = typeof document !== 'undefined' ? readCsrfToken() : null;
    if (csrf) headers[CSRF_HEADER] = csrf;

    void fetch(ENDPOINT, {
      method: 'POST',
      credentials: 'same-origin',
      keepalive: true,
      headers,
      body: JSON.stringify({ message, stack, component, url }),
    }).catch(() => {
      // صمت تام: فشل الإبلاغ لا يُبلغ عن نفسه (منع الحلقة) ولا يُزعج المستخدم.
    });
    return true;
  } catch {
    return false;
  }
}

/** للاختبارات فقط: تصفير إزالة التكرار وعدّاد الجلسة. */
export function __resetErrorReportingForTests(): void {
  seenFingerprints.clear();
  sentCount = 0;
}

import axios, { CanceledError, type AxiosRequestConfig } from 'axios';
import { newIdempotencyKey } from '../utils/idempotency';
import { reportClientError } from '../utils/errorReporting';
import { ContractError } from './contractError';

const CSRF_COOKIE = 'docgen_csrf';
const CSRF_HEADER = 'X-CSRF-Token';

/// سياسة رسائل أخطاء الخادم (P4-03): العقد الموحّد هو وثيقة `ApiProblem`
/// (`application/problem+json`) بمفاتيح `status`/`code`/`message`/`traceId`،
/// و`field`/`errors`/`warningToken` عند الحاجة. `message` عربي دائمًا، وهو
/// المصدر الوحيد للعرض؛ وما عداه للقرار البرمجي وربط الحقول.
///
/// استثناءات بلا جسم باقية عمدًا: `403` من `Forbid()` وبعض `404` الفارغة —
/// فغياب `message` يُترجم لرسالة عربية ثابتة حسب الحالة. حقلا `title` و`detail`
/// الإطاريان لا يُقرآن أبدًا (قد يكونان إنجليزيين)، و`errors` يُقرأ للعرض فقط
/// عند غياب `message`.
export interface ApiProblemBody {
  status?: number;
  code?: string;
  message?: string;
  field?: string;
  errors?: Record<string, string[]>;
  warningToken?: string;
  traceId?: string;
}

/// يقرأ جسم خطأ الخادم بأمان من `unknown` (بيانات axios غير موثوقة الشكل):
/// يعيد الكائن فقط إن حمل مفتاحًا معروفًا واحدًا على الأقل بقيمة صالحة.
export function readApiProblemBody(data: unknown): ApiProblemBody | null {
  if (!data || typeof data !== 'object' || Array.isArray(data)) return null;
  const raw = data as Record<string, unknown>;
  const body: ApiProblemBody = {};
  if (typeof raw.status === 'number') body.status = raw.status;
  if (typeof raw.code === 'string' && raw.code.trim().length > 0) body.code = raw.code;
  if (typeof raw.message === 'string' && raw.message.trim().length > 0) body.message = raw.message;
  if (typeof raw.field === 'string' && raw.field.trim().length > 0) body.field = raw.field;
  if (typeof raw.warningToken === 'string' && raw.warningToken.trim().length > 0) {
    body.warningToken = raw.warningToken;
  }
  if (typeof raw.traceId === 'string' && raw.traceId.trim().length > 0) body.traceId = raw.traceId;
  if (raw.errors && typeof raw.errors === 'object' && !Array.isArray(raw.errors)) {
    const errors: Record<string, string[]> = {};
    for (const [field, messages] of Object.entries(raw.errors as Record<string, unknown>)) {
      if (!Array.isArray(messages)) continue;
      const arabic = messages.filter(
        (message): message is string => typeof message === 'string' && message.trim().length > 0,
      );
      if (arabic.length > 0) errors[field] = arabic;
    }
    if (Object.keys(errors).length > 0) body.errors = errors;
  }
  return Object.keys(body).length > 0 ? body : null;
}

export function getApiErrorMessage(error: unknown): string {
  // مخالفة عقد سلكي: الرسالة من كودنا ومصوغة للعرض، فتُعاد كما هي — بخلاف أي
  // `Error` عائم تبقى رسالته مستورة خلف النص العام أدناه.
  if (error instanceof ContractError) return error.message;
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    if (!error.response) return 'تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة';
    // رسالة الخادم العربية تسبق رسالة الحالة الثابتة لكل الحالات — بما فيها
    // 401/403: الردود الفارغة وحدها (`Forbid()`/جلسة منتهية بلا جسم) تسقط للثابت.
    const body = readApiProblemBody(error.response.data);
    if (body?.message) return body.message;
    if (status === 401) return 'انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً';
    if (status === 403) return 'لا تملك صلاحية تنفيذ هذا الإجراء';
    if (status === 400) return 'الطلب غير صالح — تحقق من الحقول وأعد المحاولة';
    if (status === 404) return 'العنصر المطلوب غير موجود — ربما حُذف أو نُقل';
    if (status === 409) return 'تعارض في البيانات — حدّث الصفحة وحاول مجددًا';
    if (status === 429) return 'طلبات كثيرة في وقت قصير — انتظر قليلًا وحاول مجددًا';
    if (status && status >= 500) return 'حدث خطأ في الخادم. حاول مرة أخرى لاحقاً';
  }
  return 'حدث خطأ غير متوقع';
}

/// استخراج رسالة خطأ طلبات التنزيل (`responseType: 'blob'`): جسم الخطأ يصل
/// `Blob` لا `JSON` مُفسَّرًا، فيُقرأ نصًا ويُفكّ (وثيقة `ApiProblem`) قبل السقوط
/// إلى `getApiErrorMessage` — وإلا ظهرت رسالة عامة حتى مع رسالة خادم دقيقة
/// (كـ429 حارس التصدير «لديك تصدير قيد التنفيذ…»).
export async function getDownloadErrorMessage(error: unknown): Promise<string> {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (data instanceof Blob) {
      try {
        const text = await data.text();
        const body = readApiProblemBody(JSON.parse(text) as unknown);
        if (body?.message) return body.message;
      } catch {
        // جسم غير JSON (مقطوع/فارغ) — نسقط للرسالة العامة أدناه.
      }
    }
  }
  return getApiErrorMessage(error);
}

export const api = axios.create({
  baseURL: '/api',
  // الجلسة كوكي `HttpOnly` — تُرسل صراحةً حتى لا يعتمد السلوك على إعداد المتصفح
  // الافتراضي (وسيط التطوير/نطاق فرعي يشتركان في الأصل نفسه).
  withCredentials: true,
  // سقف موحّد يمنع تعليق الواجهة إلى الأبد عند خادم لا يستجيب. الاختبارات
  // والتنزيلات الكبيرة قد تتجاوزه عبر `timeout` صريح في الطلب.
  timeout: 60_000,
  headers: { 'Content-Type': 'application/json' },
  paramsSerializer: {
    serialize: (params) => {
      const searchParams = new URLSearchParams();
      for (const [key, value] of Object.entries(params ?? {})) {
        if (value === undefined || value === null || value === '') continue;
        if (Array.isArray(value)) {
          for (const item of value) searchParams.append(key, String(item));
        } else {
          searchParams.append(key, String(value));
        }
      }
      return searchParams.toString();
    },
  },
});

// الجلسة Cookie مصادقة HttpOnly (SameSite=Strict): لا يُخزَّن أي توكن في localStorage
// ولا تُرسل ترويسة Authorization. حماية CSRF دفاعًا إضافيًا: كل طلب يغيّر الحالة يحمل
// ترويسة تقابل قيمة Cookie CSRF القابلة للقراءة (المتصفح لا يرسلها عبر المواقع المخالفة).
export function getCsrfToken(): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|;\\s*)${CSRF_COOKIE}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

/* ------------------------------------------------------------------ */
/* P6-01a — إعادة المحاولة الآمنة + تطبيع الخطأ المطبَّع               */
/* ------------------------------------------------------------------ */

/** أقصى عدد محاولات لطلب القراءة (المحاولة الأولى + إعادتان). */
export const RETRY_MAX_ATTEMPTS = 3;
/** أساس التباعد الأُسّي (يُضاعَف لكل محاولة). */
export const RETRY_BASE_DELAY_MS = 300;
/** سقف التباعد المحلي (الخادم يمكن أن يطلب أكثر عبر `Retry-After`). */
export const RETRY_MAX_DELAY_MS = 10_000;
/** سقف احترام `Retry-After` — لا نُجمّد الواجهة أكثر من نصف دقيقة. */
export const RETRY_AFTER_MAX_MS = 30_000;
/**
 * تنزيلات `blob` (تصدير/توليد ملفات) بطبيعتها أطول من نداء JSON: تُمنح 5 دقائق
 * بدل 60 ثانية، مع بقاء سقف يمنع التعليق الأبدي. يتجاوزها المستدعي بـ`timeout`.
 */
export const BLOB_TIMEOUT_MS = 300_000;

const SAFE_RETRY_METHODS = new Set(['get', 'head']);

type RetryableConfig = AxiosRequestConfig & { _retryAttempt?: number };

export type ApiErrorKind = 'aborted' | 'timeout' | 'network' | 'http' | 'contract' | 'unknown';

/** الشكل المطبَّع الواحد لأي خطأ API — للمنطق البرمجي والعرض معًا. */
export interface ApiErrorInfo {
  kind: ApiErrorKind;
  /** حالة HTTP إن وُجدت استجابة. */
  status: number | null;
  /** رمز الخطأ: `code` من جسم `ApiProblem` أو رمز axios (`ECONNABORTED`...). */
  code: string | null;
  message: string;
  /** حقل النموذج المرتبط بالخطأ (إن حدده الخادم). */
  field: string | null;
  /** معرّف تتبّع الخادم — يُعرض للمستخدم للإبلاغ. */
  traceId: string | null;
  /** هل الطلب آمن لإعادة المحاولة تلقائيًا؟ */
  retryable: boolean;
}

/** هل الخطأ ناتج إلغاء صريح (`AbortController`/`CancelToken`)؟ */
export function isAbortError(error: unknown): boolean {
  if (axios.isCancel(error)) return true;
  const candidate = error as
    | { code?: unknown; config?: { signal?: { aborted?: boolean } } }
    | null
    | undefined;
  if (candidate?.code === 'ERR_CANCELED') return true;
  return candidate?.config?.signal?.aborted === true;
}

function readHeaderValue(headers: unknown, name: string): string | null {
  if (!headers || typeof headers !== 'object') return null;
  const bag = headers as {
    get?: (key: string) => unknown;
    [key: string]: unknown;
  };
  const raw =
    typeof bag.get === 'function'
      ? bag.get(name)
      : (bag[name] ?? bag[name.toLowerCase()] ?? bag[name.toUpperCase()]);
  if (raw == null) return null;
  const text = String(raw).trim();
  return text.length > 0 ? text : null;
}

/**
 * سياسة إعادة المحاولة — **قراءات فقط**:
 * - `GET`/`HEAD` عند خطأ شبكة أو مهلة أو `5xx` أو `429`.
 * - لا إعادة أبدًا لـ`POST`/`PUT`/`PATCH`/`DELETE` (منع تكرار الأثر)،
 *   ولا لأي `4xx` أخرى (بما فيها `401`/`403`)، ولا لطلب مُلغى.
 * - سقف `RETRY_MAX_ATTEMPTS` محاولات إجمالًا.
 */
export function isRetryableReadError(error: unknown): boolean {
  if (!axios.isAxiosError(error)) return false;
  if (isAbortError(error)) return false;
  const config = error.config as RetryableConfig | undefined;
  const method = (config?.method ?? 'get').toLowerCase();
  if (!SAFE_RETRY_METHODS.has(method)) return false;
  const attemptsMade = config?._retryAttempt ?? 1;
  if (attemptsMade >= RETRY_MAX_ATTEMPTS) return false;
  const response = error.response;
  if (!response) return true; // خطأ شبكة/مهلة — لا استجابة أصلًا.
  const status = response.status;
  if (status === 429) return true;
  return status >= 500 && status <= 599;
}

/** تأخير `Retry-After` بالمللي ثانية (ثوانٍ أو تاريخ HTTP) أو `null`. */
export function retryAfterMs(error: unknown, now = Date.now()): number | null {
  if (!axios.isAxiosError(error)) return null;
  const raw = readHeaderValue(error.response?.headers, 'retry-after');
  if (!raw) return null;
  if (/^\d+$/.test(raw)) {
    return Math.min(Number(raw) * 1000, RETRY_AFTER_MAX_MS);
  }
  const date = Date.parse(raw);
  if (Number.isNaN(date)) return null;
  return Math.min(Math.max(date - now, 0), RETRY_AFTER_MAX_MS);
}

/**
 * تأخير المحاولة التالية: `Retry-After` يحكم إن وُجد (احترام الخادم)، وإلا
 * تباعد أُسّي مع تشويش (jitter) في النطاق `[delay/2, delay]` لمنع عاصفة
 * محاولات متزامنة من عدة تبويبات.
 *
 * @param attemptsMade عدد المحاولات المكتملة (1 = بعد فشل المحاولة الأولى).
 */
export function retryDelayMs(error: unknown, attemptsMade: number): number {
  const serverDelay = retryAfterMs(error);
  if (serverDelay !== null) return serverDelay;
  const exponential = Math.min(
    RETRY_BASE_DELAY_MS * 2 ** Math.max(attemptsMade - 1, 0),
    RETRY_MAX_DELAY_MS,
  );
  const half = exponential / 2;
  return Math.round(half + Math.random() * half);
}

type RetryWaitSignal = {
  aborted: boolean;
  addEventListener?: (type: 'abort', listener: () => void, options?: { once?: boolean }) => void;
  removeEventListener?: (type: 'abort', listener: () => void) => void;
};

/** انتظار قابل للإلغاء: أي `abort` أثناء الانتظار يرفض فورًا بخطأ إلغاء. */
export function waitForRetry(ms: number, signal?: RetryWaitSignal | null): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(new CanceledError('تم إلغاء الطلب'));
      return;
    }
    let timer: ReturnType<typeof setTimeout> | undefined;
    const onAbort = () => {
      if (timer !== undefined) clearTimeout(timer);
      signal?.removeEventListener?.('abort', onAbort);
      reject(new CanceledError('تم إلغاء الطلب'));
    };
    timer = setTimeout(() => {
      signal?.removeEventListener?.('abort', onAbort);
      resolve();
    }, ms);
    signal?.addEventListener?.('abort', onAbort, { once: true });
  });
}

/**
 * تطبيع أي خطأ إلى شكل واحد مطبَّع. `message` يبقى عربيًا مصوغًا للعرض
 * (نفس `getApiErrorMessage`)، وبقية الحقول للقرار البرمجي وربط الحقول.
 */
export function toApiErrorInfo(error: unknown): ApiErrorInfo {
  const base: ApiErrorInfo = {
    kind: 'unknown',
    status: null,
    code: null,
    message: getApiErrorMessage(error),
    field: null,
    traceId: null,
    retryable: isRetryableReadError(error),
  };
  if (error instanceof ContractError) return { ...base, kind: 'contract', code: 'contract' };
  if (!axios.isAxiosError(error)) return base;
  if (isAbortError(error)) return { ...base, kind: 'aborted', code: 'ERR_CANCELED' };

  const response = error.response;
  if (!response) {
    const timedOut = error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT';
    return { ...base, kind: timedOut ? 'timeout' : 'network', code: error.code ?? null };
  }

  const body = readApiProblemBody(response.data);
  return {
    ...base,
    kind: 'http',
    status: response.status,
    code: body?.code ?? error.code ?? null,
    field: body?.field ?? null,
    traceId: body?.traceId ?? null,
  };
}

api.interceptors.request.use((config) => {
  // سقف أطول للتنزيلات كما يشرح `BLOB_TIMEOUT_MS` (افتراضي فقط، لا يطغى على
  // اختيار المستدعي الصريح).
  if (config.responseType === 'blob' && config.timeout === api.defaults.timeout) {
    config.timeout = BLOB_TIMEOUT_MS;
  }
  const method = (config.method ?? 'get').toLowerCase();
  if (method !== 'get' && method !== 'head' && method !== 'options') {
    const csrf = getCsrfToken();
    if (csrf) config.headers[CSRF_HEADER] = csrf;

    // P0-04: مفتاح عدم تكرار لكل طلب كتابة — يُولَّد هنا إن لم يمرره المستدعي
    // صراحةً (نماذج critical تمرر مفتاحًا ثابتًا عبر `idempotencyHeaders`).
    // الدخول/الخروج وتليمترية الأخطاء مستثناة (لا نية كتابة قابلة للتكرار).
    const url = config.url ?? '';
    const excluded = url.startsWith('/auth/login') || url.startsWith('/auth/logout') || url.startsWith('/client-errors');
    if (!excluded && !config.headers['X-Idempotency-Key']) {
      config.headers['X-Idempotency-Key'] = newIdempotencyKey();
    }
  }
  return config;
});

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    // P6-01a: إعادة المحاولة الآمنة قبل أي معالجة أخرى — قراءات فقط،
    // بسقف محاولات، واحترام `Retry-After`. الإلغاء يوقف السلسلة فورًا.
    if (isRetryableReadError(error)) {
      const config = (error as { config?: RetryableConfig }).config;
      if (config) {
        const attemptsMade = config._retryAttempt ?? 1;
        config._retryAttempt = attemptsMade + 1;
        try {
          await waitForRetry(
            retryDelayMs(error, attemptsMade),
            config.signal as RetryWaitSignal | undefined,
          );
        } catch (abortError) {
          return Promise.reject(abortError);
        }
        return api.request(config);
      }
    }
    if (error.response?.status === 401) {
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
    // إبلاغ أخطاء 5xx فقط بعد استنفاد المحاولات — بلا مساس بمنطق 401 أعلاه.
    // حارس منع الحلقة: لا إبلاغ عن فشل نقطة الإبلاغ نفسها.
    const status = error.response?.status as number | undefined;
    const failedUrl = (error.config?.url as string | undefined) ?? '';
    if (status && status >= 500 && !failedUrl.endsWith('/client-errors')) {
      reportClientError({ message: getApiErrorMessage(error), component: 'api-client', url: failedUrl });
    }
    return Promise.reject(error);
  },
);

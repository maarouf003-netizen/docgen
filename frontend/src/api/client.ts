import axios, { type AxiosRequestConfig } from 'axios';
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

/** تأخير إعادة المحاولة الوحيدة عند حد المعدل (بالميلي ثانية). */
const RATE_LIMIT_RETRY_DELAY_MS = 1000;

type RetryableConfig = AxiosRequestConfig & { _rateLimitRetried?: boolean };

/**
 * إعداد إعادة المحاولة عند `429`: طلبات القراءة (`GET`/`HEAD`) التي لم تُعَد
 * من قبل فقط — الكتابات لا تُعاد أبدًا (عدم تكرار الأثر)، والعلم يمنع التتالي.
 */
function rateLimitRetryConfig(error: unknown): RetryableConfig | null {
  if (!axios.isAxiosError(error)) return null;
  if (error.response?.status !== 429) return null;
  const method = (error.config?.method ?? 'get').toLowerCase();
  if (method !== 'get' && method !== 'head') return null;
  const config = error.config as RetryableConfig | undefined;
  if (!config || config._rateLimitRetried) return null;
  config._rateLimitRetried = true;
  return config;
}

api.interceptors.request.use((config) => {
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
  (error) => {
    // عثرة حد المعدل العابرة (كعنقود لوحة الإحصائيات): إعادة واحدة لطلبات
    // القراءة فقط — قبل أي معالجة أخرى، وبعلم يمنع التتالي.
    const retryConfig = rateLimitRetryConfig(error);
    if (retryConfig) {
      return new Promise((resolve, reject) => {
        window.setTimeout(() => {
          api.request(retryConfig).then(resolve, reject);
        }, RATE_LIMIT_RETRY_DELAY_MS);
      });
    }
    if (error.response?.status === 401) {
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
    // إبلاغ أخطاء 5xx فقط — بلا مساس بمنطق 401 أعلاه. حارس منع الحلقة: لا إبلاغ عن
    // فشل نقطة الإبلاغ نفسها (reportClientError صامت أصلًا، وهذا حزام ثانٍ).
    const status = error.response?.status as number | undefined;
    const failedUrl = (error.config?.url as string | undefined) ?? '';
    if (status && status >= 500 && !failedUrl.endsWith('/client-errors')) {
      reportClientError({ message: getApiErrorMessage(error), component: 'api-client', url: failedUrl });
    }
    return Promise.reject(error);
  },
);

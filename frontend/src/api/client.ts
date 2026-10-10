import axios, { type AxiosRequestConfig } from 'axios';
import { reportClientError } from '../utils/errorReporting';

const CSRF_COOKIE = 'docgen_csrf';
const CSRF_HEADER = 'X-CSRF-Token';

/// سياسة رسائل أخطاء الخادم: `‎{ message }‎` عربيًا هو **العرف** لا الضمان
/// (`GlobalExceptionHandler` + `BadRequest(new { message })` في المتحكمات)؛
/// واستثناءات بلا جسم قائمة: `ValidationProblemDetails` (ربط النموذج) و`403`
/// المختصرة من الوسطاء — فغياب `message` يُترجم لرسالة عربية ثابتة حسب الحالة.
/// حقلا `errors` و`title` الإطاريان لا يُقرآن أبدًا عمدًا (إنجليزية إطارية).
export function getApiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    if (!error.response) return 'تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة';
    if (status === 401) return 'انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً';
    if (status === 403) return 'لا تملك صلاحية تنفيذ هذا الإجراء';
    const data = error.response.data as { message?: unknown } | undefined;
    if (typeof data?.message === 'string' && data.message.trim().length > 0) return data.message;
    if (status === 400) return 'الطلب غير صالح — تحقق من الحقول وأعد المحاولة';
    if (status === 404) return 'العنصر المطلوب غير موجود — ربما حُذف أو نُقل';
    if (status === 409) return 'تعارض في البيانات — حدّث الصفحة وحاول مجددًا';
    if (status === 429) return 'طلبات كثيرة في وقت قصير — انتظر قليلًا وحاول مجددًا';
    if (status && status >= 500) return 'حدث خطأ في الخادم. حاول مرة أخرى لاحقاً';
  }
  return 'حدث خطأ غير متوقع';
}

/// استخراج رسالة خطأ طلبات التنزيل (`responseType: 'blob'`): جسم الخطأ يصل
/// `Blob` لا `JSON` مُفسَّرًا، فيُقرأ نصًا ويُفكّ (`{ message }`) قبل السقوط
/// إلى `getApiErrorMessage` — وإلا ظهرت رسالة عامة حتى مع رسالة خادم دقيقة
/// (كـ429 حارس التصدير «لديك تصدير قيد التنفيذ…»).
export async function getDownloadErrorMessage(error: unknown): Promise<string> {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (data instanceof Blob) {
      try {
        const text = await data.text();
        const parsed = JSON.parse(text) as { message?: unknown };
        if (typeof parsed?.message === 'string' && parsed.message.trim().length > 0) {
          return parsed.message;
        }
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

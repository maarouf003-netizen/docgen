import axios from 'axios';

const CSRF_COOKIE = 'docgen_csrf';
const CSRF_HEADER = 'X-CSRF-Token';

export function getApiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status;
    if (!error.response) return 'تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة';
    if (status === 401) return 'انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً';
    if (status === 403) return 'لا تملك صلاحية تنفيذ هذا الإجراء';
    const data = error.response.data as { message?: string } | undefined;
    if (data?.message) return data.message;
    if (status && status >= 500) return 'حدث خطأ في الخادم. حاول مرة أخرى لاحقاً';
  }
  return 'حدث خطأ غير متوقع';
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
    if (error.response?.status === 401) {
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  },
);

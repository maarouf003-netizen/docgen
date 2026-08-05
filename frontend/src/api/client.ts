import axios from 'axios';

const TOKEN_KEY = 'docgen_token';

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

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

api.interceptors.request.use((config) => {
  const token = getToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (error) => {
    if (error.response?.status === 401) {
      setToken(null);
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  },
);

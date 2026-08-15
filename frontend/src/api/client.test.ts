import { describe, it, expect, afterEach } from 'vitest';
import { api, getApiErrorMessage, getCsrfToken } from './client';

describe('getApiErrorMessage', () => {
  it('يعيد رسالة تعذر الاتصال عند خطأ شبكة دون استجابة', () => {
    expect(getApiErrorMessage({ isAxiosError: true, response: undefined })).toBe(
      'تعذر الاتصال بالخادم. تحقق من الاتصال وأعد المحاولة',
    );
  });

  it('يعيد رسالة صلاحية عند 403', () => {
    expect(getApiErrorMessage({ isAxiosError: true, response: { status: 403, data: {} } })).toBe(
      'لا تملك صلاحية تنفيذ هذا الإجراء',
    );
  });

  it('يعيد رسالة الخادم عند خطأ 400 يحمل message', () => {
    expect(
      getApiErrorMessage({
        isAxiosError: true,
        response: { status: 400, data: { message: 'حالة غير صالحة' } },
      }),
    ).toBe('حالة غير صالحة');
  });

  it('يعيد رسالة عامة عند خطأ غير معروف', () => {
    expect(getApiErrorMessage(new Error('something'))).toBe('حدث خطأ غير متوقع');
  });
});

function clearCsrfCookie() {
  document.cookie = 'docgen_csrf=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
}

describe('getCsrfToken', () => {
  afterEach(clearCsrfCookie);

  it('يعيد قيمة الـ Cookie عند وجودها', () => {
    document.cookie = 'docgen_csrf=csrf-token-1; path=/';
    expect(getCsrfToken()).toBe('csrf-token-1');
  });

  it('يعيد null عند غياب الـ Cookie', () => {
    expect(getCsrfToken()).toBeNull();
  });
});

describe('api CSRF interceptor', () => {
  afterEach(() => {
    clearCsrfCookie();
    delete api.defaults.adapter;
  });

  it('يضيف ترويسة CSRF لطلبات تغيير الحالة ولا يضيفها لطلبات القراءة', async () => {
    document.cookie = 'docgen_csrf=csrf-token-1; path=/';
    const captured: { method?: string; headers: Record<string, unknown> }[] = [];
    api.defaults.adapter = async (config) => {
      captured.push({ method: config.method, headers: config.headers as unknown as Record<string, unknown> });
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    await api.post('/auth/logout');
    await api.get('/auth/me');

    expect(captured).toHaveLength(2);
    const postHeaders = captured[0].headers as { get?: (k: string) => unknown; [k: string]: unknown };
    const getHeaders = captured[1].headers as { get?: (k: string) => unknown; [k: string]: unknown };
    const read = (h: typeof postHeaders) => h['X-CSRF-Token'] ?? h.get?.('X-CSRF-Token');
    expect(read(postHeaders)).toBe('csrf-token-1');
    expect(read(getHeaders)).toBeUndefined();
  });

  it('لا يضيف ترويسة CSRF عند غياب الـ Cookie', async () => {
    const captured: { headers: Record<string, unknown> }[] = [];
    api.defaults.adapter = async (config) => {
      captured.push({ headers: config.headers as unknown as Record<string, unknown> });
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    await api.post('/auth/logout');

    const headers = captured[0].headers as { get?: (k: string) => unknown; [k: string]: unknown };
    const value = headers['X-CSRF-Token'] ?? headers.get?.('X-CSRF-Token');
    expect(value).toBeUndefined();
  });
});

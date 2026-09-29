import { describe, it, expect, vi, afterEach } from 'vitest';
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

  it('يعيد رسالة عربية ثابتة عند 400 إطاري بلا message (ويتجاهل errors الإنجليزية)', () => {
    expect(
      getApiErrorMessage({
        isAxiosError: true,
        response: {
          status: 400,
          data: {
            title: 'One or more validation errors occurred.',
            errors: { FileNumber: ['The FileNumber field is required.'] },
          },
        },
      }),
    ).toBe('الطلب غير صالح — تحقق من الحقول وأعد المحاولة');
  });

  it('يعيد رسالة الجلسة عند 401', () => {
    expect(getApiErrorMessage({ isAxiosError: true, response: { status: 401, data: {} } })).toBe(
      'انتهت صلاحية الجلسة، يرجى تسجيل الدخول مجدداً',
    );
  });

  it('يعيد رسالة الخادم العامة عند 500 بلا message', () => {
    expect(getApiErrorMessage({ isAxiosError: true, response: { status: 500, data: {} } })).toBe(
      'حدث خطأ في الخادم. حاول مرة أخرى لاحقاً',
    );
  });

  it('يعيد رسائل عربية ثابتة لـ 404/409/429 بلا message', () => {
    expect(getApiErrorMessage({ isAxiosError: true, response: { status: 404, data: {} } })).toBe(
      'العنصر المطلوب غير موجود — ربما حُذف أو نُقل',
    );
    expect(getApiErrorMessage({ isAxiosError: true, response: { status: 409, data: {} } })).toBe(
      'تعارض في البيانات — حدّث الصفحة وحاول مجددًا',
    );
    expect(getApiErrorMessage({ isAxiosError: true, response: { status: 429, data: {} } })).toBe(
      'طلبات كثيرة في وقت قصير — انتظر قليلًا وحاول مجددًا',
    );
  });

  it('رسالة الخادم النصية تسبق رسالة الحالة الثابتة', () => {
    expect(
      getApiErrorMessage({
        isAxiosError: true,
        response: { status: 404, data: { message: 'الملف غير موجود' } },
      }),
    ).toBe('الملف غير موجود');
  });

  it('يتجاهل message غير النصية ويسقط للرسالة المناسبة للحالة', () => {
    expect(
      getApiErrorMessage({ isAxiosError: true, response: { status: 400, data: { message: 42 } } }),
    ).toBe('الطلب غير صالح — تحقق من الحقول وأعد المحاولة');
    expect(
      getApiErrorMessage({ isAxiosError: true, response: { status: 400, data: { message: '   ' } } }),
    ).toBe('الطلب غير صالح — تحقق من الحقول وأعد المحاولة');
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

describe('api 429 retry', () => {
  afterEach(() => {
    vi.useRealTimers();
    delete api.defaults.adapter;
  });

  it('لا يجدول أي مؤقت عند النجاح من أول مرة', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config };
    };

    const res = await api.get('/stats/me');
    expect(res.status).toBe(200);
    expect(calls).toBe(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  /** خطأ شبكي بشكل AxiosError يحمل الحالة المطلوبة (المحوّل المخصص يتجاوز فحص الحالة المدمج). */
  const rateLimitError = (config: unknown) => ({
    isAxiosError: true,
    message: 'Request failed with status code 429',
    config,
    response: { status: 429, data: {}, headers: {}, config },
  });

  it('يعيد طلب القراءة مرة واحدة عند 429 ثم ينجح', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      if (calls === 1) throw rateLimitError(config);
      return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config };
    };

    const pending = api.get('/stats/me');
    await vi.advanceTimersByTimeAsync(1100);
    const res = await pending;

    expect(res.status).toBe(200);
    expect(calls).toBe(2);
  });

  it('لا يتتالي عند 429 متكرر — يرفض بعد المحاولة الوحيدة', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      throw rateLimitError(config);
    };

    const pending = api.get('/stats/me');
    const assertion = expect(pending).rejects.toMatchObject({ response: { status: 429 } });
    await vi.advanceTimersByTimeAsync(1100);
    await assertion;
    expect(calls).toBe(2);
  });

  it('لا يعيد طلبات الكتابة عند 429', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      throw rateLimitError(config);
    };

    await expect(api.post('/personal-reminders', {})).rejects.toMatchObject({
      response: { status: 429 },
    });
    expect(calls).toBe(1);
  });
});

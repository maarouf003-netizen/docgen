import { describe, it, expect, vi, afterEach } from 'vitest';
import { CanceledError } from 'axios';
import {
  api,
  getApiErrorMessage,
  getCsrfToken,
  isRetryableReadError,
  readApiProblemBody,
  retryAfterMs,
  retryDelayMs,
  toApiErrorInfo,
  waitForRetry,
  BLOB_TIMEOUT_MS,
  RETRY_MAX_ATTEMPTS,
} from './client';
import { ContractError } from './contractError';

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

  it('رسالة الخادم تسبق الرسالة الثابتة حتى في 401/403 (جسم `ApiProblem`)', () => {
    expect(
      getApiErrorMessage({
        isAxiosError: true,
        response: {
          status: 401,
          data: { status: 401, code: 'unauthorized', message: 'اسم المستخدم أو كلمة المرور غير صحيحة' },
        },
      }),
    ).toBe('اسم المستخدم أو كلمة المرور غير صحيحة');
    expect(
      getApiErrorMessage({
        isAxiosError: true,
        response: {
          status: 403,
          data: { status: 403, code: 'csrf_rejected', message: 'طلب مرفوض: ترويسة الحماية غير صحيحة' },
        },
      }),
    ).toBe('طلب مرفوض: ترويسة الحماية غير صحيحة');
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

  it('يعيد رسالة `ContractError` كما هي (نصّ من كودنا مصوغ للعرض)', () => {
    expect(getApiErrorMessage(new ContractError('تعذّر قراءة قائمة التنبيهات'))).toBe(
      'تعذّر قراءة قائمة التنبيهات',
    );
  });
});

describe('readApiProblemBody (عقد الخطأ الموحّد)', () => {
  it('يقرأ المفاتيح المعروفة ويتجاهل غير الصالح منها', () => {
    const body = readApiProblemBody({
      status: 400,
      code: 'validation_failed',
      message: 'سنة غير صالحة',
      field: 'year',
      traceId: 'trace-1',
      title: 'Bad Request',
    });

    expect(body).toEqual({
      status: 400,
      code: 'validation_failed',
      message: 'سنة غير صالحة',
      field: 'year',
      traceId: 'trace-1',
    });
  });

  it('يُبقي رسائل errors النصية غير الفارغة فقط', () => {
    const body = readApiProblemBody({
      message: 'تحقق من الحقول',
      errors: { year: ['سنة غير صالحة', '', 42], month: 'ليست مصفوفة' },
    });

    expect(body?.errors).toEqual({ year: ['سنة غير صالحة'] });
  });

  it('يعيد null لجسم فارغ أو ليس كائنًا', () => {
    expect(readApiProblemBody(undefined)).toBeNull();
    expect(readApiProblemBody('نص')).toBeNull();
    expect(readApiProblemBody([1, 2])).toBeNull();
    expect(readApiProblemBody({ unknown: true })).toBeNull();
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

  it('يضيف ترويسة CSRF لكل طرق التغيير المطابقة لوسيط الخادم، ولا يضيفها للقراءة', async () => {
    document.cookie = 'docgen_csrf=csrf-token-1; path=/';
    const captured: { method?: string; headers: Record<string, unknown> }[] = [];
    api.defaults.adapter = async (config) => {
      captured.push({ method: config.method, headers: config.headers as unknown as Record<string, unknown> });
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    await api.post('/auth/logout');
    await api.put('/documents/1');
    await api.patch('/documents/1');
    await api.delete('/documents/1');
    await api.get('/auth/me');
    await api.head('/auth/me');

    const read = (h: Record<string, unknown>) =>
      h['X-CSRF-Token'] ?? (h as { get?: (k: string) => unknown }).get?.('X-CSRF-Token');
    expect(captured).toHaveLength(6);
    for (const request of captured.slice(0, 4)) {
      expect(read(request.headers)).toBe('csrf-token-1');
    }
    expect(read(captured[4].headers)).toBeUndefined();
    expect(read(captured[5].headers)).toBeUndefined();
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

describe('api idempotency interceptor (P0-04)', () => {
  afterEach(() => {
    delete api.defaults.adapter;
  });

  const readHeader = (headers: Record<string, unknown>, name: string) =>
    headers[name] ?? (headers as { get?: (k: string) => unknown }).get?.(name);

  it('يضيف مفتاحًا لكل طلب كتابة ولا يضيفه للقراءة', async () => {
    const captured: { method?: string; headers: Record<string, unknown> }[] = [];
    api.defaults.adapter = async (config) => {
      captured.push({ method: config.method, headers: config.headers as unknown as Record<string, unknown> });
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    await api.post('/documents/1/restore');
    await api.get('/documents/1');

    const postKey = readHeader(captured[0].headers, 'X-Idempotency-Key');
    expect(postKey).toMatch(/^[0-9a-f-]{36}$/);
    expect(readHeader(captured[1].headers, 'X-Idempotency-Key')).toBeUndefined();
  });

  it('يحترم مفتاح المستدعي الصريح ولا يستبدله', async () => {
    let captured: Record<string, unknown> = {};
    api.defaults.adapter = async (config) => {
      captured = config.headers as unknown as Record<string, unknown>;
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    await api.post('/documents', {}, { headers: { 'X-Idempotency-Key': 'explicit-key' } });

    expect(readHeader(captured, 'X-Idempotency-Key')).toBe('explicit-key');
  });

  it('يستثني الدخول والخروج من المفتاح التلقائي', async () => {
    const captured: Record<string, unknown>[] = [];
    api.defaults.adapter = async (config) => {
      captured.push(config.headers as unknown as Record<string, unknown>);
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    await api.post('/auth/logout');
    await api.post('/auth/login', {});

    expect(readHeader(captured[0], 'X-Idempotency-Key')).toBeUndefined();
    expect(readHeader(captured[1], 'X-Idempotency-Key')).toBeUndefined();
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

  it('لا يتتالي عند 429 متكرر — يرفض بعد استنفاد المحاولات الثلاث', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      throw rateLimitError(config);
    };

    const pending = api.get('/stats/me');
    const assertion = expect(pending).rejects.toMatchObject({ response: { status: 429 } });
    await vi.advanceTimersByTimeAsync(5000);
    await assertion;
    expect(calls).toBe(RETRY_MAX_ATTEMPTS);
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

/* ------------------------------------------------------------------ */
/* P6-01a — إعادة المحاولة الآمنة وتطبيع الخطأ                        */
/* ------------------------------------------------------------------ */

const networkError = (config: unknown) => ({
  isAxiosError: true,
  message: 'Network Error',
  code: 'ERR_NETWORK',
  config,
});

const httpError = (config: unknown, status: number, headers: Record<string, string> = {}) => ({
  isAxiosError: true,
  message: `Request failed with status code ${status}`,
  config,
  response: { status, data: {}, headers, config },
});

describe('instance موحّد', () => {
  afterEach(() => {
    delete api.defaults.adapter;
  });

  it('يحمل baseURL وwithCredentials وtimeout صريحًا', () => {
    expect(api.defaults.baseURL).toBe('/api');
    expect(api.defaults.withCredentials).toBe(true);
    expect(api.defaults.timeout).toBeGreaterThan(0);
  });

  it('يمنح تنزيلات blob سقفًا أطول ويحترم timeout المستدعي الصريح', async () => {
    const timeouts: Array<number | undefined> = [];
    api.defaults.adapter = async (config) => {
      timeouts.push(config.timeout);
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config };
    };

    await api.get('/documents/1/export', { responseType: 'blob' });
    await api.get('/documents/1/export', { responseType: 'blob', timeout: 1234 });

    expect(timeouts).toEqual([BLOB_TIMEOUT_MS, 1234]);
  });
});

describe('isRetryableReadError (مصفوفة السياسة)', () => {
  it('يعيد المحاولة لـ GET/HEAD عند خطأ الشبكة و5xx و429', () => {
    expect(isRetryableReadError(networkError({ method: 'get' }))).toBe(true);
    expect(isRetryableReadError(networkError({ method: 'head' }))).toBe(true);
    expect(isRetryableReadError(httpError({ method: 'get' }, 500))).toBe(true);
    expect(isRetryableReadError(httpError({ method: 'get' }, 503))).toBe(true);
    expect(isRetryableReadError(httpError({ method: 'get' }, 429))).toBe(true);
  });

  it('لا يعيد الكتابات ولا 4xx (بما فيها 401/403/404)', () => {
    for (const method of ['post', 'put', 'patch', 'delete']) {
      expect(isRetryableReadError(networkError({ method }))).toBe(false);
      expect(isRetryableReadError(httpError({ method }, 500))).toBe(false);
    }
    for (const status of [400, 401, 403, 404, 409]) {
      expect(isRetryableReadError(httpError({ method: 'get' }, status))).toBe(false);
    }
  });

  it('لا يعيد طلبًا مُلغى ولا يتجاوز سقف المحاولات', () => {
    expect(isRetryableReadError({ isAxiosError: true, code: 'ERR_CANCELED', config: { method: 'get' } })).toBe(false);
    expect(
      isRetryableReadError({
        isAxiosError: true,
        config: { method: 'get', signal: { aborted: true } },
      }),
    ).toBe(false);
    expect(
      isRetryableReadError(networkError({ method: 'get', _retryAttempt: RETRY_MAX_ATTEMPTS })),
    ).toBe(false);
  });
});

describe('retryDelayMs / retryAfterMs', () => {
  it('يحترم Retry-After بالثواني وبصيغة تاريخ HTTP', () => {
    const now = Date.parse('2026-11-26T10:00:00Z');
    expect(retryAfterMs(httpError({ method: 'get' }, 429, { 'retry-after': '2' }))).toBe(2000);
    expect(
      retryAfterMs(
        httpError({ method: 'get' }, 429, { 'retry-after': new Date(now + 4000).toUTCString() }),
        now,
      ),
    ).toBe(4000);
    expect(retryAfterMs(httpError({ method: 'get' }, 429, { 'retry-after': 'غير صالح' }))).toBeNull();
  });

  it('يقيّد Retry-After بسقف معلن', () => {
    expect(retryAfterMs(httpError({ method: 'get' }, 429, { 'retry-after': '9999' }))).toBe(30_000);
  });

  it('يبني تباعدًا أُسّيًا مشوّشًا في النطاق [half, full]', () => {
    const randomSpy = vi.spyOn(Math, 'random').mockReturnValue(0.5);
    try {
      // المحاولة الأولى: 300ms → [150, 300]، والتشويش 0.5 يعطي 225.
      expect(retryDelayMs(networkError({ method: 'get' }), 1)).toBe(225);
      // المحاولة الثانية: 600ms → [300, 600]، والتشويش 0.5 يعطي 450.
      expect(retryDelayMs(networkError({ method: 'get' }), 2)).toBe(450);
    } finally {
      randomSpy.mockRestore();
    }
  });
});

describe('waitForRetry (إلغاء)', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('يرفض فورًا عند إلغاء الإشارة أثناء الانتظار بلا مؤقت متبقٍ', async () => {
    vi.useFakeTimers();
    const controller = new AbortController();
    const waiting = waitForRetry(5000, controller.signal);
    const assertion = expect(waiting).rejects.toBeInstanceOf(CanceledError);
    controller.abort();
    await assertion;
    expect(vi.getTimerCount()).toBe(0);
  });

  it('يرفض فورًا إذا كانت الإشارة ملغاة أصلًا', async () => {
    const controller = new AbortController();
    controller.abort();
    await expect(waitForRetry(1000, controller.signal)).rejects.toBeInstanceOf(CanceledError);
  });
});

describe('api retry integration', () => {
  afterEach(() => {
    vi.useRealTimers();
    delete api.defaults.adapter;
  });

  it('GET على خطأ شبكة: 3 محاولات إجمالًا ثم يرفض', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      throw networkError(config);
    };

    const pending = api.get('/documents');
    const assertion = expect(pending).rejects.toMatchObject({ code: 'ERR_NETWORK' });
    await vi.advanceTimersByTimeAsync(5000);
    await assertion;
    expect(calls).toBe(RETRY_MAX_ATTEMPTS);
  });

  it('GET على 500 ينجح في المحاولة الثانية', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      if (calls === 1) throw httpError(config, 500);
      return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config };
    };

    const pending = api.get('/stats');
    await vi.advanceTimersByTimeAsync(1000);
    const res = await pending;

    expect(res.status).toBe(200);
    expect(calls).toBe(2);
  });

  it('POST على 500 لا يُعاد أبدًا ولا يجدول مؤقتًا', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      throw httpError(config, 500);
    };

    await expect(api.post('/documents', {})).rejects.toMatchObject({
      response: { status: 500 },
    });
    expect(calls).toBe(1);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('يحترم Retry-After لـ429 ولا يبكر قبل انقضائه', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      if (calls === 1) throw httpError(config, 429, { 'retry-after': '2' });
      return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config };
    };

    const pending = api.get('/stats/me');
    await vi.advanceTimersByTimeAsync(1999);
    expect(calls).toBe(1);
    await vi.advanceTimersByTimeAsync(1);
    const res = await pending;

    expect(res.status).toBe(200);
    expect(calls).toBe(2);
  });

  it('إلغاء أثناء انتظار الإعادة يوقف السلسلة ولا يرسل محاولة ثانية', async () => {
    vi.useFakeTimers();
    let calls = 0;
    api.defaults.adapter = async (config) => {
      calls += 1;
      throw networkError(config);
    };
    const controller = new AbortController();

    const pending = api.get('/documents', { signal: controller.signal });
    const assertion = expect(pending).rejects.toMatchObject({ code: 'ERR_CANCELED' });
    await vi.advanceTimersByTimeAsync(0);
    expect(calls).toBe(1);
    controller.abort();
    await assertion;
    expect(calls).toBe(1);
  });
});

describe('toApiErrorInfo (تطبيع مطبَّع)', () => {
  it('يصنّف الشبكة والمهلة والإلغاء والعقد', () => {
    expect(toApiErrorInfo({ isAxiosError: true, code: 'ERR_NETWORK', config: {} })).toMatchObject({
      kind: 'network',
      status: null,
      code: 'ERR_NETWORK',
    });
    expect(
      toApiErrorInfo({ isAxiosError: true, code: 'ECONNABORTED', config: {} }),
    ).toMatchObject({ kind: 'timeout', code: 'ECONNABORTED' });
    expect(
      toApiErrorInfo({ isAxiosError: true, code: 'ERR_CANCELED', config: {} }),
    ).toMatchObject({ kind: 'aborted' });
    expect(toApiErrorInfo(new ContractError('عقد غير صالح'))).toMatchObject({
      kind: 'contract',
      message: 'عقد غير صالح',
    });
  });

  it('يستخرج الحالة وcode/field/traceId من جسم ApiProblem', () => {
    expect(
      toApiErrorInfo({
        isAxiosError: true,
        config: { method: 'get' },
        response: {
          status: 400,
          data: { code: 'validation_failed', message: 'سنة غير صالحة', field: 'year', traceId: 't-1' },
        },
      }),
    ).toEqual({
      kind: 'http',
      status: 400,
      code: 'validation_failed',
      message: 'سنة غير صالحة',
      field: 'year',
      traceId: 't-1',
      retryable: false,
    });
  });
});

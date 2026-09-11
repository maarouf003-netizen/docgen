import { beforeEach, describe, expect, it, vi } from 'vitest';
import { __resetErrorReportingForTests, reportClientError } from './errorReporting';

function mockFetch() {
  const calls: { url: string; init: RequestInit }[] = [];
  const fetchMock = vi.fn((url: string, init: RequestInit) => {
    calls.push({ url, init });
    return Promise.resolve(new Response(JSON.stringify({ ok: true }), { status: 202 }));
  });
  vi.stubGlobal('fetch', fetchMock);
  return { calls, fetchMock };
}

describe('reportClientError', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
    __resetErrorReportingForTests();
  });

  it('يرسل POST keepalive مع JSON إلى نقطة الإبلاغ', () => {
    const { calls } = mockFetch();

    expect(reportClientError({ message: 'عطل عرض', component: 'X', url: '/a?b=1#c' })).toBe(true);

    expect(calls).toHaveLength(1);
    expect(calls[0].url).toBe('/api/client-errors');
    expect(calls[0].init.method).toBe('POST');
    expect(calls[0].init.keepalive).toBe(true);
    const body = JSON.parse(calls[0].init.body as string) as Record<string, string>;
    expect(body.message).toBe('عطل عرض');
    expect(body.url).toBe('/a');
  });

  it('يزيل تكرار البصمة نفسها داخل الجلسة', () => {
    const { calls } = mockFetch();

    expect(reportClientError({ message: 'نفس العطل', component: 'C' })).toBe(true);
    expect(reportClientError({ message: 'نفس العطل', component: 'C' })).toBe(false);

    expect(calls).toHaveLength(1);
  });

  it('يخنق بعد سقف الجلسة', () => {
    const { calls } = mockFetch();

    for (let i = 0; i < 25; i++) {
      reportClientError({ message: `عطل ${i}`, component: 'C' });
    }

    expect(calls.length).toBeLessThanOrEqual(20);
  });

  it('يقصّ الرسالة والمكدس ضمن سقوف الخادم', () => {
    const { calls } = mockFetch();

    reportClientError({ message: 'm'.repeat(5000), stack: 's'.repeat(20000) });

    const body = JSON.parse(calls[0].init.body as string) as Record<string, string>;
    expect(body.message.length).toBeLessThanOrEqual(2048);
    expect(body.stack.length).toBeLessThanOrEqual(8192);
  });

  it('يرفق ترويسة CSRF عند وجود الكوكي', () => {
    Object.defineProperty(document, 'cookie', {
      value: 'docgen_csrf=abc123',
      configurable: true,
    });
    const { calls } = mockFetch();

    reportClientError({ message: 'مع CSRF' });

    expect((calls[0].init.headers as Record<string, string>)['X-CSRF-Token']).toBe('abc123');
  });

  it('يفشل بصمت ولا يرمي على رسالة فارغة أو fetch مكسور', () => {
    const { calls } = mockFetch();

    expect(reportClientError({ message: '   ' })).toBe(false);
    expect(calls).toHaveLength(0);

    vi.stubGlobal('fetch', () => {
      throw new Error('network down');
    });
    expect(reportClientError({ message: 'عطل' })).toBe(false);
  });
});

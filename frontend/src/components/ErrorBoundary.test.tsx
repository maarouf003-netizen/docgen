import { describe, it, expect, vi, afterEach } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import ErrorBoundary from './ErrorBoundary';

function Boom(): never {
  throw new Error('boom');
}

describe('ErrorBoundary', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
    Reflect.deleteProperty(navigator, 'clipboard');
  });

  it('يلتقط خطأ العرض ويعرض بديلًا عربيًا مع role="alert" وعنوان قابل للتركيز', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
    const heading = screen.getAllByRole('heading')[0];
    expect(heading).toHaveFocus();
    expect(screen.getByText(/حدث خطأ غير متوقع في التطبيق/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'العودة إلى الرئيسية' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('button', { name: 'نسخ معرّف الخطأ' })).toBeInTheDocument();
  });

  it('يعرض المحتوى الطبيعي عند عدم وجود خطأ', () => {
    render(
      <ErrorBoundary>
        <div>محتوى سليم</div>
      </ErrorBoundary>,
    );

    expect(screen.getByText('محتوى سليم')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('«إعادة المحاولة» تُصفّر الحدّ وتعيد عرض المحتوى بعد زوال الخطأ', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    let shouldThrow = true;
    function Flaky() {
      if (shouldThrow) throw new Error('boom');
      return <div>نجح العرض</div>;
    }

    render(
      <ErrorBoundary>
        <Flaky />
      </ErrorBoundary>,
    );
    expect(screen.getByRole('alert')).toBeInTheDocument();

    shouldThrow = false;
    fireEvent.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));

    expect(screen.getByText('نجح العرض')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('تغيّر resetKeys (تنقل المسار) يعيد الضبط تلقائيًا', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const { rerender } = render(
      <ErrorBoundary resetKeys={['/documents']}>
        <Boom />
      </ErrorBoundary>,
    );
    expect(screen.getByRole('alert')).toBeInTheDocument();

    rerender(
      <ErrorBoundary resetKeys={['/documents/5']}>
        <div>صفحة أخرى</div>
      </ErrorBoundary>,
    );

    expect(screen.getByText('صفحة أخرى')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('نسخ معرّف الخطأ يعرض تأكيدًا بصريًا (aria-live)', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText },
    });

    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );
    const errorId = screen.getByText(/معرّف الخطأ:/).textContent ?? '';
    fireEvent.click(screen.getByRole('button', { name: 'نسخ معرّف الخطأ' }));

    expect(writeText).toHaveBeenCalledTimes(1);
    expect(writeText.mock.calls[0][0]).toMatch(/^[0-9a-f-]{36}$/);
    expect(errorId).toContain(writeText.mock.calls[0][0] as string);
    expect(await screen.findByText('تم نسخ معرّف الخطأ.')).toHaveAttribute(
      'class',
      expect.stringContaining('text-emerald-700'),
    );
  });

  it('لا يسجّل رموزًا أو بيانات شخصية في console (تنقية الرسالة)', () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    function Leaky(): never {
      throw new Error(
        'فشل الطلب docgen_csrf=csrf-secret eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature مع ali@example.com و0933333333',
      );
    }

    render(
      <ErrorBoundary>
        <Leaky />
      </ErrorBoundary>,
    );

    const ourLogs = consoleSpy.mock.calls
      .filter((call) => typeof call[0] === 'string' && call[0].startsWith('[ErrorBoundary:'))
      .flat()
      .join(' ');
    // React نفسها تطبع الخطأ الخام في التطوير (سلوك إطار لا نتحكم به)؛ نفحص سجلنا نحن.
    expect(ourLogs).not.toBe('');
    expect(ourLogs).not.toContain('csrf-secret');
    expect(ourLogs).not.toContain('eyJhbGciOiJIUzI1NiJ9');
    expect(ourLogs).not.toContain('ali@example.com');
    expect(ourLogs).not.toContain('0933333333');
    expect(ourLogs).toContain('<محجوب>');
  });

  it('لا يسجّل سطرًا خاصًا به في بناء الإنتاج (MODE=production)', () => {
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.stubEnv('MODE', 'production');
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    const ourLogs = consoleSpy.mock.calls.filter(
      (call) => typeof call[0] === 'string' && call[0].startsWith('[ErrorBoundary:'),
    );
    expect(ourLogs).toHaveLength(0);
  });

  it('يعرض عنوانًا أصغر داخل المحتوى (variant=content) ليحفظ هرمية العناوين', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    render(
      <ErrorBoundary variant="content" scopeLabel="هذه الصفحة">
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent('تعذّر عرض هذه الصفحة');
  });
});

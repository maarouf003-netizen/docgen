import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import ErrorBoundary from './ErrorBoundary';

function Boom(): never {
  throw new Error('boom');
}

describe('ErrorBoundary', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('يعرض شاشة احتياطية عند حدوث خطأ أثناء العرض', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );

    expect(screen.getByText('حدث خطأ غير متوقع')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'إعادة تحميل الصفحة' })).toBeInTheDocument();
  });

  it('يعرض المحتوى الطبيعي عند عدم وجود خطأ', () => {
    render(
      <ErrorBoundary>
        <div>محتوى سليم</div>
      </ErrorBoundary>,
    );

    expect(screen.getByText('محتوى سليم')).toBeInTheDocument();
  });
});

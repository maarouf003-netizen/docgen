import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AlertsFeedFooter } from './AlertsFeedFooter';
import { makeHeadAlertSummary } from '../../test/factories';
import type { AlertsFeed } from '../../hooks/useAlertsFeed';

function feed(overrides: Partial<AlertsFeed> = {}): AlertsFeed {
  return {
    items: [makeHeadAlertSummary({ id: 1 })],
    totalCount: 1,
    hasMore: false,
    isLoading: false,
    isLoadingMore: false,
    error: null,
    loadMore: vi.fn(),
    reload: vi.fn(),
    updateItems: vi.fn(),
    ...overrides,
  };
}

describe('AlertsFeedFooter', () => {
  it('لا يعرض شيئًا عند اكتمال التحميل', () => {
    const { container } = render(<AlertsFeedFooter feed={feed()} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('يعرض العدّاد و«تحميل المزيد» عند وجود مزيد', async () => {
    const user = userEvent.setup();
    const loadMore = vi.fn();
    render(
      <AlertsFeedFooter
        feed={feed({
          items: [makeHeadAlertSummary({ id: 1 }), makeHeadAlertSummary({ id: 2 })],
          totalCount: 3,
          hasMore: true,
          loadMore,
        })}
      />,
    );

    expect(screen.getByText('يُعرض 2 من أصل 3 تنبيهًا')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تحميل المزيد' }));
    expect(loadMore).toHaveBeenCalledTimes(1);
  });

  it('فشل بلا عناصر: «إعادة المحاولة» يعيد التحميل من الأولى لا «تحميل المزيد»', async () => {
    const user = userEvent.setup();
    const reload = vi.fn();
    const loadMore = vi.fn();
    render(
      <AlertsFeedFooter
        feed={feed({ items: [], totalCount: 0, error: 'تعذّر الجلب', reload, loadMore })}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));
    expect(reload).toHaveBeenCalledTimes(1);
    expect(loadMore).not.toHaveBeenCalled();
  });

  it('فشل بعد تحميل عناصر: «إعادة المحاولة» يظهر ويُبقي العدّاد حتى لو انعدم «مزيد»', async () => {
    const user = userEvent.setup();
    const loadMore = vi.fn();
    render(
      <AlertsFeedFooter feed={feed({ totalCount: 1, hasMore: false, error: 'تعذّر الجلب', loadMore })} />,
    );

    // الحالة الحرجة: خطأ + لا مزيد ⇒ لا يجوز أن يختفي التذييل فيبقى الخطأ بلا مخرج.
    expect(screen.getByText('يُعرض 1 من أصل 1 تنبيهًا')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));
    expect(loadMore).toHaveBeenCalledTimes(1);
  });

  it('أثناء تحميل صفحة إضافية: الزر معطّل ونصه «جارٍ التحميل…»', () => {
    render(<AlertsFeedFooter feed={feed({ totalCount: 3, hasMore: true, isLoadingMore: true })} />);

    expect(screen.getByRole('button', { name: 'جارٍ التحميل…' })).toBeDisabled();
  });

  it('لا يعرض زرًا حين لا مزيد ولا خطأ ولا تحميل جارٍ', () => {
    render(<AlertsFeedFooter feed={feed({ totalCount: 5, hasMore: false })} />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});

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
    scopeDrift: false,
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
  it('عند اكتمال التحميل: سطر اكتمال صادق بعدد المحمّل (لا اختفاء يُخفي قصًّا)', () => {
    render(<AlertsFeedFooter feed={feed()} />);

    expect(screen.getByText('اكتمل — يُعرض 1 تنبيهًا')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('قائمة فارغة مكتملة: لا تذييل (حالة الفراغ لـ`AlertList` وحدها)', () => {
    const { container } = render(<AlertsFeedFooter feed={feed({ items: [], totalCount: 0 })} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('تغيّر القائمة أثناء التصفح: إعلان صريح و«تحديث القائمة» يعيد التحميل من الأولى', async () => {
    const user = userEvent.setup();
    const reload = vi.fn();
    render(
      <AlertsFeedFooter
        feed={feed({
          items: [makeHeadAlertSummary({ id: 1 }), makeHeadAlertSummary({ id: 2 })],
          totalCount: 4,
          hasMore: false,
          scopeDrift: true,
          reload,
        })}
      />,
    );

    expect(
      screen.getByRole('status'),
    ).toHaveTextContent('تغيّرت القائمة أثناء التصفح — أعِد التحميل لعرض الحالة الأخيرة');
    await user.click(screen.getByRole('button', { name: 'تحديث القائمة' }));
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('سطر الاكتمال الثابت ليس منطقة حيّة (لا إعلان متكرر لقارئ الشاشة)', () => {
    render(<AlertsFeedFooter feed={feed()} />);

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(screen.getByText('اكتمل — يُعرض 1 تنبيهًا')).toBeInTheDocument();
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

  it('المحمّل يسبق الإجمالي (تقلّص القائمة): لا يُعرض «N من أصل M» معكوسًا', () => {
    render(
      <AlertsFeedFooter
        feed={feed({
          items: [
            makeHeadAlertSummary({ id: 1 }),
            makeHeadAlertSummary({ id: 2 }),
            makeHeadAlertSummary({ id: 3 }),
          ],
          totalCount: 2,
          hasMore: true,
        })}
      />,
    );

    // «3 من أصل 2» كان يُقرأ كعطل؛ يُسقط «من أصل» ويبقى العدّاد صادقًا.
    expect(screen.getByText('يُعرض 3 تنبيهًا')).toBeInTheDocument();
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

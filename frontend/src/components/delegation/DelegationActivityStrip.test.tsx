import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DelegationActivityStrip } from './DelegationActivityStrip';
import { makeHeadAlertSummary } from '../../test/factories';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn() },
  getApiErrorMessage: (error: unknown) =>
    (error as { message?: string })?.message ?? 'حدث خطأ غير متوقع',
}));

import { api } from '../../api/client';

const get = api.get as unknown as ReturnType<typeof vi.fn>;

function row(id: number) {
  return makeHeadAlertSummary({ id, messagePreview: `معاينة ${id}`, delegationId: 9 });
}

function pageBody(items: ReturnType<typeof row>[], totalCount = items.length) {
  return {
    data: { items, page: 1, perPage: 100, totalCount, totalPages: Math.ceil(totalCount / 100) },
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('DelegationActivityStrip', () => {
  it('يعرض آخر ثلاث معاينات ابتداءً من `PagedResult` (بلا نص كامل) مع زر كشف البقية', async () => {
    get.mockResolvedValue(pageBody([row(1), row(2), row(3), row(4)]));

    render(<DelegationActivityStrip delegationId={9} />);

    expect(await screen.findByText('معاينة 1')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    expect(screen.queryByText('معاينة 4')).not.toBeInTheDocument();
    // الكشف صريح لا صامت: زر ظاهر يدل على وجود بقية.
    expect(screen.getByRole('button', { name: 'عرض المزيد' })).toBeInTheDocument();
    expect(get).toHaveBeenCalledWith('/alerts/by-delegation/9', expect.any(Object));
  });

  it('«عرض المزيد» يكشف بقية الصفحة المحمّلة خطوةً خطوة (بلا طلب جديد)', async () => {
    const user = userEvent.setup();
    const items = Array.from({ length: 25 }, (_, index) => row(index + 1));
    get.mockResolvedValue(pageBody(items));

    render(<DelegationActivityStrip delegationId={9} />);
    await screen.findByText('معاينة 1');
    expect(screen.getAllByRole('listitem')).toHaveLength(3);

    await user.click(screen.getByRole('button', { name: 'عرض المزيد' }));
    expect(screen.getAllByRole('listitem')).toHaveLength(23);

    await user.click(screen.getByRole('button', { name: 'عرض المزيد' }));
    expect(screen.getAllByRole('listitem')).toHaveLength(25);
    expect(screen.queryByRole('button', { name: 'عرض المزيد' })).not.toBeInTheDocument();
    // لا طلب جديد: التوسيع من الصفحة المحمّلة وحدها.
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('إجمالي يتجاوز الصفحة المحمّلة (سقف 100) مُعلَن نصًا لا مخفيًا', async () => {
    get.mockResolvedValue(pageBody([row(1), row(2)], 250));

    render(<DelegationActivityStrip delegationId={9} />);

    expect(await screen.findByText('تُعرض أحدث 2 من أصل 250 تنبيهًا')).toBeInTheDocument();
  });

  it('الفراغ لا يعرض شيئًا (ملحق اختياري في البطاقة)', async () => {
    get.mockResolvedValue(pageBody([]));

    const { container } = render(<DelegationActivityStrip delegationId={9} />);

    await vi.waitFor(() => expect(get).toHaveBeenCalled());
    expect(container).toBeEmptyDOMElement();
  });

  it('فشل الجلب يُعلن الخطأ مع «إعادة المحاولة» ويستعيد الشريط (لا صمت)', async () => {
    const user = userEvent.setup();
    get.mockRejectedValueOnce(new Error('تعذّر جلب تنبيهات الإنابة'));
    get.mockResolvedValueOnce(pageBody([row(1)]));

    render(<DelegationActivityStrip delegationId={9} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذّر جلب تنبيهات الإنابة');
    await user.click(screen.getByRole('button', { name: 'إعادة المحاولة' }));

    expect(await screen.findByText('معاينة 1')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(get).toHaveBeenCalledTimes(2);
  });

  it('جسم مخالف للعقد يُعلن خطأً صريحًا لا شريطًا صامتًا فارغًا', async () => {
    // الشكل القديم (مصفوفة مجرّدة، بلا `PagedResult`) — خادم لم يُحدَّث مثلًا.
    // سابقًا كان يُترجم فراغًا صامتًا فيختفي الشريط كأن لا تنبيهات إنابة أصلًا.
    get.mockResolvedValueOnce({ data: [row(1)] });

    const { container } = render(<DelegationActivityStrip delegationId={9} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('تعذّر قراءة قائمة التنبيهات');
    expect(container).not.toBeEmptyDOMElement();
    expect(screen.getByRole('button', { name: 'إعادة المحاولة' })).toBeInTheDocument();
  });
});

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

function pageBody(items: ReturnType<typeof row>[]) {
  return {
    data: { items, page: 1, perPage: 100, totalCount: items.length, totalPages: 1 },
  };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('DelegationActivityStrip', () => {
  it('يعرض آخر ثلاث معاينات فقط من `PagedResult` (بلا نص كامل)', async () => {
    get.mockResolvedValue(pageBody([row(1), row(2), row(3), row(4)]));

    render(<DelegationActivityStrip delegationId={9} />);

    expect(await screen.findByText('معاينة 1')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
    expect(screen.queryByText('معاينة 4')).not.toBeInTheDocument();
    expect(get).toHaveBeenCalledWith('/alerts/by-delegation/9', expect.any(Object));
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

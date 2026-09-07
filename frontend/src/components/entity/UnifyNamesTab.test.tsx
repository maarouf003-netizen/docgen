import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { UnifyNamesTab } from './UnifyNamesTab';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

const groupsResponse = {
  data: {
    items: [
      { groupId: 1, canonicalName: 'هيئة الاستثمار', entityType: 'authority', isActive: true, entryCount: 1, governorates: ['دمشق'], linkedDocumentCount: 2 },
      { groupId: 2, canonicalName: 'هيئة الاستثمار والتجارة', entityType: 'authority', isActive: true, entryCount: 1, governorates: ['حلب'], linkedDocumentCount: 0 },
    ],
    page: 1,
    perPage: 100,
    totalCount: 2,
    totalPages: 1,
  },
};

const similarToResponse = {
  data: {
    targetGroupId: 1,
    targetCanonicalName: 'هيئة الاستثمار',
    items: [
      { groupId: 2, canonicalName: 'هيئة الاستثمار والتجارة', entityType: 'authority', entryCount: 1, linkedDocumentCount: 0, similarity: 0.8 },
    ],
    threshold: 0.55,
  },
};

const staleSimilarForGroup1 = {
  data: {
    targetGroupId: 1,
    targetCanonicalName: 'هيئة الاستثمار',
    items: [
      { groupId: 7, canonicalName: 'هيئة الاستثمار القديمة', entityType: 'authority', entryCount: 1, linkedDocumentCount: 0, similarity: 0.6 },
    ],
    threshold: 0.55,
  },
};

const recentSimilarForGroup2 = {
  data: {
    targetGroupId: 2,
    targetCanonicalName: 'هيئة الاستثمار والتجارة',
    items: [
      { groupId: 5, canonicalName: 'شركة الاستثمار السورية', entityType: 'company', entryCount: 1, linkedDocumentCount: 0, similarity: 0.7 },
    ],
    threshold: 0.55,
  },
};

const unifyPreviewResponse = {
  data: {
    targetName: 'هيئة الاستثمار',
    absorbedGroups: [{ groupId: 2, name: 'هيئة الاستثمار والتجارة', entryCount: 1, governorates: ['حلب'] }],
    totalEntriesToMove: 1,
    totalEntriesFolded: 0,
    foldsToApply: [],
    warnings: [],
  },
};

const unifyCommitResponse = {
  data: { targetGroupId: 1, canonicalName: 'هيئة الاستثمار', groupsUnified: 1, entriesMoved: 1, entriesFolded: 0, changeEventId: 7 },
};

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(groupsResponse);
});

describe('UnifyNamesTab', () => {
  it('يعرض بطاقتي كافة الجهات والمشابهات مباشرة دون تبويبات فرعية', async () => {
    render(<UnifyNamesTab />);

    expect(screen.getByRole('heading', { name: 'كافة الجهات العامة' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'أقرب المشابهات' })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'المجموعات المتشابهة' })).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'كافة الجهات العامة' })).not.toBeInTheDocument();

    expect(await screen.findByText('هيئة الاستثمار')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith(
      '/entity-registry/groups',
      expect.objectContaining({ params: expect.objectContaining({ perPage: 100 }) }),
    );
    expect(api.get).not.toHaveBeenCalledWith('/entity-registry/groups/similar-groups');
  });

  it('يعرض قائمة الجهات ويحمّل الاقتراحات بالاسم الجديد لزر التوحيد عند اختيار جهة', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce(groupsResponse)
      .mockResolvedValueOnce(similarToResponse);
    render(<UnifyNamesTab />);

    await screen.findByText('هيئة الاستثمار');

    await user.click(screen.getAllByRole('button', { name: /هيئة الاستثمار/ })[0]);
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/entity-registry/groups/1/similar-to', expect.objectContaining({ params: expect.objectContaining({ threshold: 0.55 }) }));
    });
    expect((await screen.findAllByText('هيئة الاستثمار والتجارة')).length).toBeGreaterThanOrEqual(1);
    expect(
      screen.getByRole('button', { name: 'توحيد تسمية المتشابهات مع الجهة المحددة ذات التسمية الصحيحة' }),
    ).toBeInTheDocument();
    // حالة التحديد تصل لقارئات الشاشة عبر aria-current وليست لونًا فقط
    expect(screen.getAllByRole('button', { name: /هيئة الاستثمار/ })[0]).toHaveAttribute('aria-current', 'true');
    expect(screen.getAllByRole('button', { name: /هيئة الاستثمار/ })[1]).not.toHaveAttribute('aria-current');
  });

  it('عند النقر على جهة ثم أخرى سريعًا يعرض اقتراحات آخر نقرة ويتجاهل نتيجة الأولى المتأخرة', async () => {
    const user = userEvent.setup();
    const get = api.get as unknown as ReturnType<typeof vi.fn>;
    const deferred = () => {
      let resolve!: (value: unknown) => void;
      const promise = new Promise<unknown>((r) => { resolve = r; });
      return { promise, resolve };
    };
    const firstSimilar = deferred();
    const secondSimilar = deferred();
    get
      .mockResolvedValueOnce(groupsResponse)
      .mockImplementationOnce(() => firstSimilar.promise)
      .mockImplementationOnce(() => secondSimilar.promise);

    render(<UnifyNamesTab />);

    await screen.findByText('هيئة الاستثمار');

    await user.click(screen.getAllByRole('button', { name: /هيئة الاستثمار/ })[0]);
    await user.click(screen.getAllByRole('button', { name: /هيئة الاستثمار والتجارة/ })[0]);
    expect(get).toHaveBeenCalledTimes(3);
    expect(get).toHaveBeenLastCalledWith(
      '/entity-registry/groups/2/similar-to',
      expect.objectContaining({ params: expect.objectContaining({ threshold: 0.55 }) }),
    );

    secondSimilar.resolve(recentSimilarForGroup2);
    await waitFor(() => expect(screen.getByText('شركة الاستثمار السورية')).toBeInTheDocument());

    firstSimilar.resolve(staleSimilarForGroup1);
    await waitFor(() => {
      expect(screen.getByText('شركة الاستثمار السورية')).toBeInTheDocument();
      expect(screen.queryByText('هيئة الاستثمار القديمة')).not.toBeInTheDocument();
    });
  });

  it('يعرض إشعار قطع النتائج عندما تتجاوز النتيجة الإجمالية الصفحة المعروضة', async () => {
    // totalCount أكبر من عدد العناصر المعروضة (التقميم من جانب الخادم)
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      data: {
        items: [
          { groupId: 1, canonicalName: 'هيئة الاستثمار', entityType: 'authority', isActive: true, entryCount: 1, governorates: ['دمشق'], linkedDocumentCount: 2 },
        ],
        page: 1,
        perPage: 100,
        totalCount: 150,
        totalPages: 2,
      },
    });
    render(<UnifyNamesTab />);

    await screen.findByText('هيئة الاستثمار');
    expect(screen.getByText(/تُعرض أول ١ من أصل ١٥٠ — جرّب بحثًا أدقّ/)).toBeInTheDocument();
  });

  it('لا يعرض إشعار القطع عندما تساوي النتيجة الإجمالية ما عُرض', async () => {
    render(<UnifyNamesTab />);

    await screen.findByText('هيئة الاستثمار');
    expect(screen.queryByText(/تُعرض أول/)).not.toBeInTheDocument();
  });

  it('زر التحديث في بطاقة كافة الجهات يعيد جلب القائمة دون تحديث الصفحة', async () => {
    const user = userEvent.setup();
    const get = api.get as unknown as ReturnType<typeof vi.fn>;
    render(<UnifyNamesTab />);

    await screen.findByText('هيئة الاستثمار');
    expect(get).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole('button', { name: 'تحديث' }));
    await waitFor(() => expect(get).toHaveBeenCalledTimes(2));
    expect(get).toHaveBeenLastCalledWith(
      '/entity-registry/groups',
      expect.objectContaining({ params: expect.objectContaining({ perPage: 100 }) }),
    );
  });

  it('بعد نجاح التوحيد من بطاقة المشابهات تُعاد جلب قائمة الجهات تلقائيًا', async () => {
    const user = userEvent.setup();
    const get = api.get as unknown as ReturnType<typeof vi.fn>;
    const post = api.post as unknown as ReturnType<typeof vi.fn>;
    get.mockImplementation((url: unknown) =>
      String(url).includes('similar-to') ? Promise.resolve(similarToResponse) : Promise.resolve(groupsResponse),
    );
    post.mockImplementation((url: unknown) =>
      String(url).includes('unify-preview') ? Promise.resolve(unifyPreviewResponse) : Promise.resolve(unifyCommitResponse),
    );
    render(<UnifyNamesTab />);

    await screen.findByText('هيئة الاستثمار');
    await user.click(screen.getAllByRole('button', { name: /هيئة الاستثمار/ })[0]);
    await user.click(
      await screen.findByRole('button', { name: 'توحيد تسمية المتشابهات مع الجهة المحددة ذات التسمية الصحيحة' }),
    );

    expect(await screen.findByRole('dialog', { name: 'توحيد تسمية جهات عامة' })).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'معاينة التوحيد' }));
    await screen.findByText('معاينة توحيد التسمية');
    await user.click(screen.getByRole('button', { name: 'تأكيد التوحيد' }));

    // بعد الاعتماد: عودة للبطاقتين مع إعادة جلب القائمة (طلب قائمة بلا includeIds مرتين: التركيب + ما بعد التوحيد)
    await waitFor(() => {
      const panelLoads = get.mock.calls.filter(
        ([url, cfg]) => url === '/entity-registry/groups' && !(cfg as { params?: Record<string, unknown> } | undefined)?.params?.includeIds,
      );
      expect(panelLoads.length).toBeGreaterThanOrEqual(2);
    });
    // ملخص النجاح المرفوع من المودال يظهر كبانر status فوق البطاقتين
    expect(screen.getByRole('status')).toHaveTextContent('تم توحيد 1 هويات في «هيئة الاستثمار»');
  });
});

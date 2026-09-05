import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SimilarGroupsUnifyTab } from './SimilarGroupsUnifyTab';

vi.mock('../../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../../api/client';

const clustersResponse = {
  data: {
    clusters: [
      {
        clusterId: 1,
        avgSimilarity: 0.9,
        groups: [
          { groupId: 1, canonicalName: 'المصرف التجاري السوري', entityType: 'company', entryCount: 2, linkedDocumentCount: 3, avgSimilarityToCluster: 0.9 },
          { groupId: 2, canonicalName: 'المصرف التجاري السوري - المدير العام', entityType: 'company', entryCount: 1, linkedDocumentCount: 0, avgSimilarityToCluster: 0.9 },
        ],
      },
    ],
    totalGroupsAnalyzed: 2,
    threshold: 0.55,
  },
};

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

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(clustersResponse);
});

describe('SimilarGroupsUnifyTab', () => {
  it('يعرض التبويبين الفرعيين ويحمّل المجموعات المتشابهة افتراضيًا', async () => {
    render(<SimilarGroupsUnifyTab />);

    expect(screen.getByRole('tab', { name: 'المجموعات المتشابهة' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'كافة الجهات العامة' })).toBeInTheDocument();
    expect(await screen.findByText('المصرف التجاري السوري')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/entity-registry/groups/similar-groups');
  });

  it('يمرر المجموعة المحددة واقتراحاتها إلى نافذة التوحيد عند الضغط', async () => {
    const user = userEvent.setup();
    render(<SimilarGroupsUnifyTab />);
    await screen.findByText('المصرف التجاري السوري');

    await user.click(screen.getByRole('button', { name: /توحيد تسمية هذه المجموعة/ }));
    expect(await screen.findByRole('dialog', { name: 'توحيد تسمية جهات عامة' })).toBeInTheDocument();
  });

  it('يعرض قائمة الجهات ويحمّل الاقتراحات عند اختيار جهة في «كافة الجهات»', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce(clustersResponse)
      .mockResolvedValueOnce(groupsResponse)
      .mockResolvedValueOnce(similarToResponse);
    render(<SimilarGroupsUnifyTab />);

    await user.click(screen.getByRole('tab', { name: 'كافة الجهات العامة' }));
    await screen.findByText('هيئة الاستثمار');

    await user.click(screen.getAllByRole('button', { name: /هيئة الاستثمار/ })[0]);
    await waitFor(() => {
      expect(api.get).toHaveBeenCalledWith('/entity-registry/groups/1/similar-to', expect.objectContaining({ params: expect.objectContaining({ threshold: 0.55 }) }));
    });
    expect((await screen.findAllByText('هيئة الاستثمار والتجارة')).length).toBeGreaterThanOrEqual(1);
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
      .mockResolvedValueOnce(clustersResponse)
      .mockResolvedValueOnce(groupsResponse)
      .mockImplementationOnce(() => firstSimilar.promise)
      .mockImplementationOnce(() => secondSimilar.promise);

    render(<SimilarGroupsUnifyTab />);

    await user.click(screen.getByRole('tab', { name: 'كافة الجهات العامة' }));
    await screen.findByText('هيئة الاستثمار');

    await user.click(screen.getAllByRole('button', { name: /هيئة الاستثمار/ })[0]);
    await user.click(screen.getAllByRole('button', { name: /هيئة الاستثمار والتجارة/ })[0]);
    expect(get).toHaveBeenCalledTimes(4);
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

  it('يختار الهدف الافتراضي الأعلى ارتباطًا بالملفات ويمرر الباقي كممتصة', async () => {
    const user = userEvent.setup();
    // أول نداء لعناقيد الكتلة، وثانٍ لنافذة التوحيد (قائمة مجموعات بصيغة items)
    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce(clustersResponse)
      .mockResolvedValueOnce({
        data: {
          items: [
            { groupId: 1, canonicalName: 'المصرف التجاري السوري', entityType: 'company', isActive: true, entryCount: 2, governorates: ['دمشق'] },
            { groupId: 2, canonicalName: 'المصرف التجاري السوري - المدير العام', entityType: 'company', isActive: true, entryCount: 1, governorates: ['دمشق'] },
          ],
          page: 1,
          perPage: 100,
          totalCount: 2,
          totalPages: 1,
        },
      });
    render(<SimilarGroupsUnifyTab />);

    await screen.findByText('المصرف التجاري السوري');
    await user.click(screen.getByRole('button', { name: /توحيد تسمية هذه المجموعة/ }));
    const dialog = await screen.findByRole('dialog', { name: 'توحيد تسمية جهات عامة' });
    expect(dialog).toBeInTheDocument();

    // الهدف الافتراضي = الأعلى linkedDocumentCount (المجموعة 1 بثلاثة ملفات) وليس الأبجدي الأول
    expect((screen.getByLabelText('الهوية الهدف (يبقى اسمها)') as HTMLSelectElement).value).toBe('1');
    // الممتصة = باقي أفراد الكتلة (المجموعة 2) مُعلَّمة مسبقًا
    const label2 = screen.getByText(/المدير العام/).closest('label') as HTMLLabelElement;
    const input2 = label2.querySelector('input') as HTMLInputElement;
    expect(input2.checked).toBe(true);
  });

  it('يعرض إشعار قطع النتائج عندما تتجاوز النتيجة الإجمالية الصفحة المعروضة', async () => {
    const user = userEvent.setup();
    // totalCount أكبر من عدد العناصر المعروضة (التقميم من جانب الخادم)
    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce(clustersResponse)
      .mockResolvedValueOnce({
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
    render(<SimilarGroupsUnifyTab />);

    await user.click(screen.getByRole('tab', { name: 'كافة الجهات العامة' }));
    await screen.findByText('هيئة الاستثمار');
    expect(screen.getByText(/تُعرض أول 1 من أصل ١٥٠ — جرّب بحثًا أدقّ/)).toBeInTheDocument();
  });

  it('لا يعرض إشعار القطع عندما تساوي النتيجة الإجمالية ما عُرض', async () => {
    const user = userEvent.setup();
    (api.get as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce(clustersResponse)
      .mockResolvedValueOnce(groupsResponse);
    render(<SimilarGroupsUnifyTab />);

    await user.click(screen.getByRole('tab', { name: 'كافة الجهات العامة' }));
    await screen.findByText('هيئة الاستثمار');
    expect(screen.queryByText(/تُعرض أول/)).not.toBeInTheDocument();
  });
});

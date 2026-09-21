import { describe, it, expect, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { DelegationsCard } from './DelegationsCard';
import type { DelegationDto } from '../../types';

function delegation(overrides: Partial<DelegationDto> = {}): DelegationDto {
  return {
    id: 1,
    sourceDocumentId: 10,
    sourceDocumentLabel: 'أحمد محمد خالد',
    targetDocumentId: null,
    delegatedCourt: 'محكمة التنفيذ الأولى',
    isExternal: false,
    externalBranchId: null,
    externalBranchName: null,
    delegationDate: '2026-08-01',
    delegationText: '',
    depositBookNumber: 'K-1',
    depositBookDate: '2026-08-02',
    assignedLawyerId: null,
    assignedLawyerName: null,
    returnDate: '',
    status: 'بانتظار رئيس القسم',
    createdAt: '2026-08-01',
    createdByName: 'سامر',
    createdById: 7,
    assets: [{ id: 100, assetKind: 'مركبة', assetLabel: 'مركبة سيارة — لوحة 123', snapshotAdjusted: false }],
    ...overrides,
  };
}

const noop = () => {};

describe('DelegationsCard', () => {
  it('يعرض «تشعبات الملف» مع تفاصيل كل إنابة وحالتها', () => {
    render(
      <DelegationsCard
        delegations={[delegation()]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('تشعبات الملف')).toBeInTheDocument();
    expect(screen.getByText('محكمة التنفيذ الأولى')).toBeInTheDocument();
    expect(screen.getByText('بانتظار رئيس القسم')).toBeInTheDocument();
    expect(screen.getByText('إنابة داخلية')).toBeInTheDocument();
    expect(screen.getByText('مركبة سيارة — لوحة 123')).toBeInTheDocument();
    expect(screen.getByText(/كتاب الإيداع رقم K-1 بتاريخ/)).toBeInTheDocument();
  });

  it('يعرض «الملف المناب» برقمه وسنته الفعّالين عند توافرهما', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ targetFileNumber: '1500', targetFileYear: '2026' })]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('الملف المناب')).toBeInTheDocument();
    expect(screen.getByText('1500/2026')).toBeInTheDocument();
  });

  it('لا يعرض «الملف المناب» عندما لا يكون رقم المناب معروفًا بعد', () => {
    render(
      <DelegationsCard
        delegations={[delegation()]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.queryByText('الملف المناب')).not.toBeInTheDocument();
  });

  it('يعرض شارة «حالة الملف المناب» (مسترد) عند توافر targetExecStatus', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ targetDocumentId: 5, targetExecStatus: 'مسترد' })]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('حالة الملف المناب')).toBeInTheDocument();
    expect(screen.getByText('مسترد')).toBeInTheDocument();
  });

  it('يعرض شارة «حالة الملف المناب» (تريث) للمناب المتريث', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ targetDocumentId: 5, targetExecStatus: 'تريث' })]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('حالة الملف المناب')).toBeInTheDocument();
    expect(screen.getByText('تريث')).toBeInTheDocument();
  });

  it('لا يعرض «حالة الملف المناب» قبل إنشاء ملف المناب', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ targetDocumentId: null, targetExecStatus: null })]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.queryByText('حالة الملف المناب')).not.toBeInTheDocument();
  });

  it('يعرض «إنابة خارجية» مع اسم الفرع المناب', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ isExternal: true, externalBranchName: 'فرع حمص' })]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('إنابة خارجية — الفرع المناب: فرع حمص')).toBeInTheDocument();
  });

  it('يعرض المحامي المختص عند اعتماد الإنابة', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ status: 'محالة', assignedLawyerName: 'المحامي هشام' })]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.getByText('المحامي هشام')).toBeInTheDocument();
  });

  it('يعرض زر «تسطير إنابة» عند الإذن ويستدعي onCreate', () => {    const onCreate = vi.fn();
    render(
      <DelegationsCard delegations={[]} canCreate onCreate={onCreate} onEdit={noop} onDelete={noop} />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'تسطير إنابة' }));
    expect(onCreate).toHaveBeenCalledTimes(1);
  });

  it('لا يعرض زر «تسطير إنابة» دون إذن، ويعرض رسالة الفراغ', () => {
    render(
      <DelegationsCard delegations={[]} canCreate={false} onCreate={noop} onEdit={noop} onDelete={noop} />,
    );

    expect(screen.queryByRole('button', { name: 'تسطير إنابة' })).not.toBeInTheDocument();
    expect(screen.getByText('لا توجد إنابات مسجلة لهذا الملف')).toBeInTheDocument();
  });

  it('يعرض «تعديل» و«حذف» للإنابة المعلّقة لمحامي المنيب المالك فقط', () => {
    const onEdit = vi.fn();
    const onDelete = vi.fn();
    const d = delegation({ createdById: 7, status: 'بانتظار رئيس القسم' });

    const { unmount } = render(
      <DelegationsCard
        delegations={[d]}
        canCreate={false}
        currentUserId={7}
        onCreate={noop}
        onEdit={onEdit}
        onDelete={onDelete}
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'تعديل' }));
    expect(onEdit).toHaveBeenCalledWith(d);
    fireEvent.click(screen.getByRole('button', { name: 'حذف' }));
    expect(onDelete).toHaveBeenCalledWith(d);

    unmount();
    render(
      <DelegationsCard
        delegations={[d]}
        canCreate={false}
        currentUserId={8}
        onCreate={noop}
        onEdit={onEdit}
        onDelete={onDelete}
      />,
    );
    expect(screen.queryByRole('button', { name: 'تعديل' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حذف' })).not.toBeInTheDocument();
  });

  it('لا يعرض «تعديل» و«حذف» لإنابة لم تعد معلّقة حتى للمالك', () => {
    render(
      <DelegationsCard
        delegations={[delegation({ createdById: 7, status: 'محالة' })]}
        canCreate={false}
        currentUserId={7}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.queryByRole('button', { name: 'تعديل' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'حذف' })).not.toBeInTheDocument();
  });

  it('يعرض شرح «بانتظار الإتمام» برقم المناب ودائرته للمسجلة أصولًا', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({
            status: 'مسجلة أصولًا',
            targetFileNumber: '77',
            targetFileYear: '2026',
            delegatedCourt: 'القرداحة',
          }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(
      screen.getByText('بانتظار الإتمام — سُجّل الملف المناب أصولًا برقم أساس 77/2026 دائرة تنفيذ القرداحة'),
    ).toBeInTheDocument();
  });

  it('توحيد ملاحظتي الإتمام: سطر بنفسجي واحد بلا شريط كهرماني في بطاقة المنيب', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({
            status: 'مسجلة أصولًا',
            targetFileNumber: '77',
            targetFileYear: '2026',
            delegatedCourt: 'القرداحة',
          }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    // سطر «بانتظار الإتمام» البنفسجي وحده — وشريط تنبيهات المرآة محذوف من سياق المنيب
    // (يبقى حيًا في بطاقة «حالة الإنابة» للمناب).
    expect(screen.getAllByText(/بانتظار الإتمام/).length).toBe(1);
    expect(
      screen.getByText('بانتظار الإتمام — سُجّل الملف المناب أصولًا برقم أساس 77/2026 دائرة تنفيذ القرداحة'),
    ).toBeInTheDocument();
    expect(screen.queryByText('حالة الإنابة')).not.toBeInTheDocument();
  });

  it('لا يضاعف بادئة الدائرة في شرح الإتمام عندما تحمل القيمة البادئة', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({
            status: 'مسجلة أصولًا',
            targetFileNumber: '77',
            targetFileYear: '2026',
            delegatedCourt: 'دائرة تنفيذ القرداحة',
          }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(
      screen.getByText('بانتظار الإتمام — سُجّل الملف المناب أصولًا برقم أساس 77/2026 دائرة تنفيذ القرداحة'),
    ).toBeInTheDocument();
    expect(screen.queryByText(/دائرة دائرة/)).not.toBeInTheDocument();
  });

  it('لا يعرض شرح الإتمام للمعلّقة والمحالة والمنفذة', () => {
    for (const status of ['بانتظار رئيس القسم', 'محالة', 'منفذ إنابة']) {
      const { unmount } = render(
        <DelegationsCard
          delegations={[
            delegation({
              status,
              targetFileNumber: '77',
              targetFileYear: '2026',
              delegatedCourt: 'القرداحة',
            }),
          ]}
          canCreate={false}
          onCreate={noop}
          onEdit={noop}
          onDelete={noop}
        />,
      );

      expect(screen.queryByText(/بانتظار الإتمام — سُجّل/)).not.toBeInTheDocument();
      unmount();
    }
  });

  it('لا يعرض شرح الإتمام للمسجلة بلا رقم مناب معروف', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({ status: 'مسجلة أصولًا', delegatedCourt: 'القرداحة' }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.queryByText(/بانتظار الإتمام — سُجّل/)).not.toBeInTheDocument();
  });

  it('لا يعرض شرح الإتمام للمسجلة برقم دون سنة (الرقم والسنة معًا شرط)', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({
            status: 'مسجلة أصولًا',
            targetFileNumber: '77',
            targetFileYear: null,
            delegatedCourt: 'القرداحة',
          }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(screen.queryByText(/بانتظار الإتمام — سُجّل/)).not.toBeInTheDocument();
  });

  it('لا يعرض شرح الإتمام للمسجلة بمناب نهائي مع بقاء شارة حالته', () => {
    // [الحالة, نص الشارة المعروضة]: «منفذ إنابة» تُعامل منفذًا فشارتها «منفذ».
    const cases: Array<[string, string]> = [
      ['مسترد', 'مسترد'],
      ['مشطوب', 'مشطوب'],
      ['منفذ إنابة', 'منفذ'],
    ];
    for (const [targetExecStatus, badgeText] of cases) {
      const { unmount } = render(
        <DelegationsCard
          delegations={[
            delegation({
              status: 'مسجلة أصولًا',
              targetDocumentId: 5,
              targetFileNumber: '77',
              targetFileYear: '2026',
              targetExecStatus,
              targetTerminal: true,
              delegatedCourt: 'القرداحة',
            }),
          ]}
          canCreate={false}
          onCreate={noop}
          onEdit={noop}
          onDelete={noop}
        />,
      );

      expect(screen.queryByText(/بانتظار الإتمام — سُجّل/)).not.toBeInTheDocument();
      expect(screen.getByText('حالة الملف المناب')).toBeInTheDocument();
      expect(screen.getByText(badgeText)).toBeInTheDocument();
      unmount();
    }
  });

  it('يعرض تحذير اليتيمة للقطة لا تطابق أي أصل حالي (C2)', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({
            status: 'مسجلة أصولًا',
            assets: [{ id: 100, assetKind: 'عقار', assetLabel: 'عقار رقم 77', snapshotAdjusted: false }],
          }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
        sourceAssets={[{ id: 5, assetKind: 'عقار', property: 'عقار رقم 99' } as never]}
      />,
    );

    expect(screen.getByText(/لم يعد في الملف المنيب/)).toBeInTheDocument();
    expect(screen.getByText(/لم يعد في الملف المنيب/).textContent).toContain('عقار رقم 77');
  });

  it('لا يعرض تحذير اليتيمة عند مطابقة اللقطة لأصل حالي', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({
            status: 'مسجلة أصولًا',
            assets: [{ id: 100, assetKind: 'عقار', assetLabel: 'عقار رقم 77', snapshotAdjusted: false }],
          }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
        sourceAssets={[{ id: 5, assetKind: 'عقار', property: 'عقار رقم 77' } as never]}
      />,
    );

    expect(screen.queryByText(/لم يعد في الملف المنيب/)).not.toBeInTheDocument();
  });

  it('لا يعرض تحذير اليتيمة بلا أصول مصدر (توافق خلفي) ولا للمنفذة', () => {
    const orphan = delegation({
      status: 'مسجلة أصولًا',
      assets: [{ id: 100, assetKind: 'عقار', assetLabel: 'عقار رقم 77', snapshotAdjusted: false }],
    });
    const { unmount } = render(
      <DelegationsCard
        delegations={[orphan]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );
    expect(screen.queryByText(/لم يعد في الملف المنيب/)).not.toBeInTheDocument();
    unmount();

    render(
      <DelegationsCard
        delegations={[{ ...orphan, status: 'منفذ إنابة' }]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
        sourceAssets={[{ id: 5, assetKind: 'عقار', property: 'عقار رقم 99' } as never]}
      />,
    );
    expect(screen.queryByText(/لم يعد في الملف المنيب/)).not.toBeInTheDocument();
  });

  it('المعدّلة تلقائيًا لا تُحسب يتيمة (تحذيرها الخاص يبقى)', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({
            status: 'مسجلة أصولًا',
            assets: [{ id: 100, assetKind: 'عقار', assetLabel: 'عقار رقم 771', snapshotAdjusted: true }],
          }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
        sourceAssets={[{ id: 5, assetKind: 'عقار', property: 'عقار رقم 99' } as never]}
      />,
    );

    expect(screen.queryByText(/لم يعد في الملف المنيب/)).not.toBeInTheDocument();
    expect(screen.getByText(/حُدِّثت لقطة الإنابة تلقائيًا/)).toBeInTheDocument();
  });

  it('يعرض شرح الإتمام للمسجلة بمناب حي (targetTerminal=false صراحة)', () => {
    render(
      <DelegationsCard
        delegations={[
          delegation({
            status: 'مسجلة أصولًا',
            targetFileNumber: '77',
            targetFileYear: '2026',
            targetTerminal: false,
            delegatedCourt: 'القرداحة',
          }),
        ]}
        canCreate={false}
        onCreate={noop}
        onEdit={noop}
        onDelete={noop}
      />,
    );

    expect(
      screen.getByText('بانتظار الإتمام — سُجّل الملف المناب أصولًا برقم أساس 77/2026 دائرة تنفيذ القرداحة'),
    ).toBeInTheDocument();
  });

  it('E1: التحميل الأول يعطّل الزر ويعرض هيكل تحميل بدل نص الفراغ', () => {
    const onCreate = vi.fn();
    render(
      <DelegationsCard
        delegations={[]}
        canCreate
        onCreate={onCreate}
        onEdit={noop}
        onDelete={noop}
        delegationsLoading
      />,
    );

    const button = screen.getByRole('button', { name: 'جارٍ التحميل…' });
    expect(button).toBeDisabled();
    expect(screen.getByText('جارٍ تحميل الإنابات…')).toBeInTheDocument();
    expect(screen.queryByText('لا توجد إنابات مسجلة لهذا الملف')).not.toBeInTheDocument();
  });

  it('E1: بيانات حاضرة (ولو أثناء إعادة جلب) تبقي الزر مفعّلًا والقائمة ظاهرة', () => {
    const onCreate = vi.fn();
    render(
      <DelegationsCard
        delegations={[delegation({ status: 'مسجلة أصولًا' })]}
        canCreate
        onCreate={onCreate}
        onEdit={noop}
        onDelete={noop}
        delegationsLoading={false}
      />,
    );

    const button = screen.getByRole('button', { name: 'تسطير إنابة' });
    expect(button).not.toBeDisabled();
    fireEvent.click(button);
    expect(onCreate).toHaveBeenCalledTimes(1);
    expect(screen.queryByText('جارٍ تحميل الإنابات…')).not.toBeInTheDocument();
  });
});

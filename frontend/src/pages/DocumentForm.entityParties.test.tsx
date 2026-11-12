// ملف اختبار موزّع من DocumentForm.test.tsx (سابقًا ملفًا أحاديًا ضخمًا).
// vi.hoisted/vi.mock تُكرَّر عمدًا في كل ملف موزَّع — vitest يعزل الملفات وكذا تقلبات المحاكاة.
// كتلة الاستيراد كاملة إلزامية (لا تختصرها — أي نقص يكسر tsc/oxlint):
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import DocumentForm from './DocumentForm';
import { api } from '../api/client';
import type { DocumentResponse } from '../types';
import { mockDoc } from './test/documentFormFixtures'; // البيانات الخام المشتركة (§7.3)

const { navigateMock, paramsMock, useAuthMock } = vi.hoisted(() => ({
  navigateMock: vi.fn(),
  paramsMock: { id: undefined as string | undefined },
  useAuthMock: vi.fn(),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
  useParams: () => paramsMock,
  Link: ({ children, to }: { children: ReactNode; to: string }) => <a href={to}>{children}</a>,
}));

vi.mock('../auth/useAuth', () => ({ useAuth: () => useAuthMock() }));

vi.mock('../api/client', async (importOriginal) => {
  const original = await importOriginal<typeof import('../api/client')>();
  return { ...original, api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } };
});

describe('DocumentForm · ربط الأطراف بالسجل', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    paramsMock.id = undefined;
    useAuthMock.mockReturnValue({ user: { role: 'lawyer' } });
  });

  async function renderEdit(doc: DocumentResponse = mockDoc) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: doc });
render(<DocumentForm />);
    return screen.findByRole('button', { name: 'حفظ التعديلات' }, { timeout: 5000 });
  }

  async function renderExecutedEdit(doc: Partial<DocumentResponse>) {
    paramsMock.id = '1';
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { ...mockDoc, ...doc } });
    render(<DocumentForm />);
    return screen.findByText('📋 حالة الملف', {}, { timeout: 5000 });
  }

  it('يربط صف جهة الطالب بقيد السجل عبر نافذة الاختيار ويملأ نصوصه (المرحلة 2)', async () => {
    const user = userEvent.setup();
    await renderEdit();

    // نافذة الاختيار تجلب النتائج بعد كتابة كلمة البحث (لا نتائج قبلها).
    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        items: [
          {
            id: 11, groupId: 5, canonicalName: 'وزارة التعليم', entityType: 'ministry',
            governorate: 'دمشق', branchName: 'الفرع الرئيسي', citationFormula: 'add-to-job',
            status: 'final', isActive: true, createdAt: '2026-08-24', aliases: [],
          },
        ],
        page: 1, perPage: 50, totalCount: 1, totalPages: 1,
      },
    });

    await user.click(await screen.findByRole('button', { name: 'اختيار من السجل…' }));
    const dialog = screen.getByRole('dialog', { name: 'اختيار الجهة العامة' });
    await user.type(within(dialog).getByLabelText('بحث باسم الجهة'), 'وزارة');
    await user.click(
      await within(dialog).findByRole('button', { name: /^وزارة التعليم/ }),
    );

    const nameInput = screen.getByLabelText('اسم الجهة 1') as HTMLInputElement;
    expect(nameInput.value).toBe('وزارة التعليم');
    expect((screen.getByLabelText('فرع الجهة 1') as HTMLInputElement).value).toBe('الفرع الرئيسي');
    expect((screen.getByLabelText('المحافظة 1') as HTMLInputElement).value).toBe('دمشق');
    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'اختيار الجهة العامة' })).not.toBeInTheDocument();
  });


  it('يقفل حقول الجهة الطالبة (التحرير اليدوي مستحيل) ويفك ربطها حصرًا عبر الزر', async () => {
    const user = userEvent.setup();
    await renderEdit({
      ...mockDoc,
      applicantPublicEntities: [
        { id: 3, name: 'وزارة التعليم', branch: 'الفرع الرئيسي', governorate: 'دمشق', registryId: 11 },
      ],
    } as DocumentResponse);

    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    expect(screen.getByLabelText('اسم الجهة 1')).toHaveAttribute('readonly');
    expect(screen.getByLabelText('فرع الجهة 1')).toHaveAttribute('readonly');
    expect(screen.getByLabelText('المحافظة 1')).toHaveAttribute('readonly');

    await user.click(screen.getByRole('button', { name: 'فك الربط' }));

    expect(screen.queryByText('مرتبطة بالسجل ✓')).not.toBeInTheDocument();
    expect(screen.getByLabelText('اسم الجهة 1')).toHaveValue('');
    expect(screen.getByLabelText('فرع الجهة 1')).toHaveValue('');
    expect(screen.getByLabelText('المحافظة 1')).toHaveValue('');
  });


  it('يسمح بتحرير اسم طالب التنفيذ الاعتباري يدويًا بلا أي ربط سجلي', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executionApplicants: [{ id: 1, name: 'المؤسسة السورية للتجارة', nature: 'legal' }],
    });

    const applicantCard = screen.getByText('طالب التنفيذ 1').closest('.rounded-xl') as HTMLElement;
    const input = within(applicantCard).getByLabelText('الشخص الاعتباري');
    expect(input).not.toHaveAttribute('readonly');
    expect(within(applicantCard).queryByText('مرتبطة بالسجل ✓')).not.toBeInTheDocument();
    expect(within(applicantCard).queryByRole('button', { name: /السجل/ })).not.toBeInTheDocument();

    await user.clear(input);
    await user.type(input, 'شركة بركات التجارية');
    expect(input).toHaveValue('شركة بركات التجارية');
  });


  it('يمنع الحفظ عند بقاء جهة طالبة نصّية بلا ارتباط بالسجل (مستند قديم)', async () => {
    const user = userEvent.setup();
    await renderEdit({
      ...mockDoc,
      applicantPublicEntities: [
        { id: 4, name: 'وزارة التعليم', branch: '', governorate: 'دمشق' },
      ],
    } as DocumentResponse);

    await user.click(screen.getByRole('button', { name: /حفظ/ }));

    expect(screen.getByText('يجب اختيار جميع الجهات العامة الطالبة للتنفيذ من السجل المرجعي قبل الحفظ')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
    expect(api.put).not.toHaveBeenCalled();
  });


  it('يحوّل تسمية حقل عنوان المقترض إلى «الوكيل القانوني» عند اختيار «وكيله القانوني»', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    expect(screen.getByLabelText('العنوان')).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('نوع العنوان'), 'يمثله');

    expect(screen.getByLabelText('الوكيل القانوني')).toBeInTheDocument();
    expect(screen.queryByLabelText('العنوان')).not.toBeInTheDocument();
  });


  it('يبدّل المقترض إلى «شخص اعتباري» فيعرض حقوله ويخفي الهوية الطبيعية', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    // قائمة «نوع الطرف» للمقترض تظهر أولًا في DOM (قبل بطاقة الكفيل الأولى).
    await user.selectOptions(screen.getAllByLabelText('نوع الطرف')[0], 'legal');

    expect(screen.getByLabelText('الشخص الاعتباري')).toBeInTheDocument();
    expect(screen.getByLabelText('رقم تسجيله')).toBeInTheDocument();
    expect(screen.getByLabelText('يمثلها')).toBeInTheDocument();
    expect(screen.queryByLabelText('اسم الأب')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('الرقم الوطني')).not.toBeInTheDocument();
  });


  it('يضيف كفيلًا اعتباريًا عبر زر «إضافة كفيل (شخص اعتباري)» ويعرض حقوله', async () => {
    const user = userEvent.setup();
    render(<DocumentForm />);

    await user.click(screen.getByRole('button', { name: '➕ إضافة كفيل (شخص اعتباري)' }));

    const card = screen.getByText('كفيل 2').closest('.rounded-xl') as HTMLElement;
    expect(within(card).getByText('الشخص الاعتباري')).toBeInTheDocument();
    expect(within(card).getByText('رقم تسجيله')).toBeInTheDocument();
    expect(within(card).getByText('يمثلها')).toBeInTheDocument();
  });


  it('زر «مسح» يفرّغ صف الجهة الطالبة اليتيم ويسمح بالحفظ', async () => {
    const user = userEvent.setup();
    await renderEdit({
      ...mockDoc,
      applicantPublicEntities: [
        { id: 4, name: 'وزارة التعليم', branch: '', governorate: 'دمشق' },
      ],
    } as DocumentResponse);

    expect(screen.getByText('يجب اختيار هذه الجهة من السجل المرجعي')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'مسح' }));

    expect(screen.getByLabelText('اسم الجهة 1')).toHaveValue('');
    expect(screen.queryByText('يجب اختيار هذه الجهة من السجل المرجعي')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'مسح' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /حفظ/ }));
    await waitFor(() => expect(api.put).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(api.put).mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.applicantPublicEntities).toEqual([]);
  });

  it('يعرض زر «إضافة متدخل» في نهاية سطر صفة الجهة العامة لملف التعديل ويفتح النافذة', async () => {
    const user = userEvent.setup();
    await renderEdit();

    const button = screen.getByRole('button', { name: '＋ إضافة متدخل' });
    expect(button).toBeInTheDocument();
    await user.click(button);
    expect(screen.getByRole('dialog', { name: 'إضافة متدخل' })).toBeInTheDocument();
  });

  it('لا يعرض زر «إضافة متدخل» في ملف «عرض وايداع»', async () => {
    await renderExecutedEdit({ generalEntitySide: 'deposit' });
    expect(screen.queryByRole('button', { name: '＋ إضافة متدخل' })).not.toBeInTheDocument();
  });

});

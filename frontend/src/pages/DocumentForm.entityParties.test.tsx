// ملف اختبار موزّع من DocumentForm.test.tsx (سابقًا ملفًا أحاديًا ضخمًا).
// vi.hoisted/vi.mock تُكرَّر عمدًا في كل ملف موزَّع — vitest يعزل الملفات وكذا تقلبات المحاكاة.
// كتلة الاستيراد كاملة إلزامية (لا تختصرها — أي نقص يكسر tsc/oxlint):
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
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
    return screen.findByText('📂 وقوعات الملف', {}, { timeout: 5000 });
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

    // نافذة الاختيار تجلب نتائج البحث عند الفتح.
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
    await user.click(
      within(dialog).getByRole('button', { name: /^وزارة التعليم/ }),
    );

    const nameInput = screen.getByLabelText('اسم الجهة 1') as HTMLInputElement;
    expect(nameInput.value).toBe('وزارة التعليم');
    expect((screen.getByLabelText('فرع الجهة 1') as HTMLInputElement).value).toBe('الفرع الرئيسي');
    expect((screen.getByLabelText('المحافظة 1') as HTMLInputElement).value).toBe('دمشق');
    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'اختيار الجهة العامة' })).not.toBeInTheDocument();
  });


  it('يفكّ ربط السجل تلقائيًا عند التحرير اليدوي لنص الجهة', async () => {
    const user = userEvent.setup();
    await renderEdit({
      ...mockDoc,
      applicantPublicEntities: [
        { id: 3, name: 'وزارة التعليم', branch: 'الفرع الرئيسي', governorate: 'دمشق', registryId: 11 },
      ],
    } as DocumentResponse);

    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    await user.clear(screen.getByLabelText('اسم الجهة 1'));
    await user.type(screen.getByLabelText('اسم الجهة 1'), 'وزارة التعليم العالي');

    expect(screen.queryByText('مرتبطة بالسجل ✓')).not.toBeInTheDocument();
  });


  it('يربط طالب تنفيذ اعتباري بقيد السجل عبر نافذة الاختيار ويملأ اسمه ورقم ربطه', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executionApplicants: [{ id: 1, name: 'المؤسسة السورية للتجارة', nature: 'legal' }],
    });

    (api.get as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: {
        items: [
          {
            id: 22, groupId: 9, canonicalName: 'هيئة التجارة الموحدة', entityType: 'authority',
            governorate: 'دمشق', branchName: 'الفرع الرئيسي', citationFormula: 'add-to-position',
            status: 'final', isActive: true, createdAt: '2026-08-24', aliases: [],
          },
        ],
        page: 1, perPage: 50, totalCount: 1, totalPages: 1,
      },
    });

    const applicantCard = screen.getByText('طالب التنفيذ 1').closest('.rounded-xl') as HTMLElement;
    await user.click(within(applicantCard).getByRole('button', { name: 'اختيار من السجل…' }));
    const dialog = screen.getByRole('dialog', { name: 'اختيار الجهة العامة' });
    await user.click(within(dialog).getByRole('button', { name: /^هيئة التجارة الموحدة/ }));

    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    expect(screen.getByLabelText('الشخص الاعتباري')).toHaveValue('هيئة التجارة الموحدة');
    expect(screen.queryByRole('dialog', { name: 'اختيار الجهة العامة' })).not.toBeInTheDocument();
  });


  it('يفكّ ربط طالب التنفيذ الاعتباري عند التحرير اليدوي لاسمه', async () => {
    const user = userEvent.setup();
    await renderExecutedEdit({
      generalEntitySide: 'executed',
      executionApplicants: [{ id: 1, name: 'هيئة التجارة الموحدة', nature: 'legal', registryId: 22 }],
    });

    expect(screen.getByText('مرتبطة بالسجل ✓')).toBeInTheDocument();
    await user.clear(screen.getByLabelText('الشخص الاعتباري'));
    await user.type(screen.getByLabelText('الشخص الاعتباري'), 'المؤسسة السورية للتجارة');

    expect(screen.queryByText('مرتبطة بالسجل ✓')).not.toBeInTheDocument();
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

});

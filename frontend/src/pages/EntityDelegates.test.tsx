import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import EntityDelegates from './EntityDelegates';
import type { DelegateDto } from '../types';

vi.mock('../api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  getApiErrorMessage: () => 'خطأ من الخادم',
}));

import { api } from '../api/client';

function delegate(overrides: Partial<DelegateDto> = {}): DelegateDto {
  return {
    id: 1,
    username: 'delegate.one',
    fullName: 'مندوب الوزارة',
    isActive: true,
    portalGroupId: 5,
    portalGroupName: 'وزارة التعليم',
    portalEntryId: null,
    portalEntryLabel: null,
    createdAt: '2026-08-24',
    ...overrides,
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  (api.get as unknown as ReturnType<typeof vi.fn>).mockImplementation((url: string) => {
    if (url === '/entity-portal/delegates') {
      return Promise.resolve({ data: [delegate()] });
    }
    if (url === '/entity-registry/search') {
      return Promise.resolve({
        data: {
          items: [
            { id: 11, groupId: 5, canonicalName: 'وزارة التعليم', entityType: 'ministry', governorate: 'دمشق', branchName: 'الفرع الرئيسي', status: 'final', isActive: true, createdAt: 'x', aliases: [], citationFormula: 'add-to-job' },
          ],
        },
      });
    }
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
});

describe('EntityDelegates', () => {
  it('يعرض المندوبين مع نطاقهم وحالتهم', async () => {
    render(<EntityDelegates />);

    expect(await screen.findByText('مندوب الوزارة')).toBeInTheDocument();
    expect(screen.getByText(/النطاق: وزارة التعليم/)).toBeInTheDocument();
    expect(screen.getByText('مفعّل')).toBeInTheDocument();
  });

  it('يرفض إنشاء مندوب دون تحديد النطاق قبل الإرسال', async () => {
    const user = userEvent.setup();
    render(<EntityDelegates />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة مندوب' }));
    await user.type(screen.getByLabelText('اسم الدخول'), 'new.delegate');
    await user.type(screen.getByLabelText('الاسم الكامل'), 'مندوب جديد');
    await user.type(screen.getByLabelText('كلمة المرور'), 'secret6');
    // اختيار «قيد بعينه» بلا قيد فعلي → خطأ تحقق محلي.
    const entryRadio = screen.getByRole('radio', { name: /قيد بعينه/ });
    await user.click(entryRadio);
    await user.click(screen.getByRole('button', { name: 'إنشاء المندوب' }));

    expect(await screen.findByText('اختر قيد الجهة')).toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();
  });

  it('ينشئ مندوبًا مربوطًا بهوية أم', async () => {
    const user = userEvent.setup();
    render(<EntityDelegates />);

    await user.click(await screen.findByRole('button', { name: '+ إضافة مندوب' }));
    await user.type(screen.getByLabelText('اسم الدخول'), 'new.delegate');
    await user.type(screen.getByLabelText('الاسم الكامل'), 'مندوب جديد');
    await user.type(screen.getByLabelText('كلمة المرور'), 'secret6');
    await user.selectOptions(screen.getByLabelText('الهوية الأم'), '5');
    await user.click(screen.getByRole('button', { name: 'إنشاء المندوب' }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith('/entity-portal/delegates', {
        username: 'new.delegate',
        fullName: 'مندوب جديد',
        password: 'secret6',
        portalGroupId: 5,
        portalEntryId: null,
      });
    });
  });

  it('يفتح نافذة التعديل بنطاق الحساب الحالي ويرسل إعادة الربط', async () => {
    (api.put as unknown as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} });
    const user = userEvent.setup();
    render(<EntityDelegates />);

    await user.click(await screen.findByRole('button', { name: 'تعديل' }));
    const dialog = screen.getByRole('dialog', { name: /تعديل مندوب/ });
    expect((screen.getByLabelText('الهوية الأم') as HTMLSelectElement).value).toBe('5');

    await user.clear(screen.getByLabelText('الاسم الكامل'));
    await user.type(screen.getByLabelText('الاسم الكامل'), 'الاسم بعد التعديل');
    await user.click(withinDialog(dialog).getByRole('button', { name: 'حفظ التعديل' }));

    await waitFor(() => {
      expect(api.put).toHaveBeenCalledWith('/entity-portal/delegates/1', expect.objectContaining({
        fullName: 'الاسم بعد التعديل',
        portalGroupId: 5,
      }));
    });
  });
});

function withinDialog(el: HTMLElement) {
  // مساعد صغير يقيّد الاستعلامات داخل نافذة الحوار.
  return {
    getByRole: (role: string, opts?: { name?: RegExp | string }) => {
      const found = Array.from(el.querySelectorAll<HTMLElement>('button')).filter(
        (b) => b.getAttribute('role') === role || role === 'button',
      );
      return found.find((b) =>
        typeof opts?.name === 'string' ? b.textContent?.includes(opts.name) : true,
      ) as HTMLElement;
    },
  };
}

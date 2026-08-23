import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { FileNumber } from '../../pages/DocumentsList';
import { ReminderList } from './ReminderList';
import { AlertRow } from './AlertRow';
import { appealAwareTypeLabel } from './dashboardFormat';
import { makeDocument } from '../../test/factories';
import type { AppealReminderDto, DocumentResponse, HeadAlertDto, ReminderDto } from '../../types';

describe('تكامل الاستئنافات مع اللوحة والقائمة', () => {
  it('شارة «استئناف» تظهر للملف المستأنف وتربط بتفاصيل استئنافه، ولا تظهر لغيره', () => {
    const appealed = {
      ...makeDocument({ id: 9 }),
      hasAppeals: true,
      matchedAppealId: 42,
    } as unknown as DocumentResponse;
    const plain = makeDocument({ id: 8 }) as unknown as DocumentResponse;

    const { rerender } = render(<FileNumber d={appealed} />, { wrapper: MemoryRouter });
    const badge = screen.getByRole('link', { name: /فتح تفاصيل استئناف الملف/ });
    expect(badge).toHaveAttribute('href', '/appeals/42');
    expect(badge).toHaveTextContent('استئناف');

    rerender(<FileNumber d={plain} />);
    expect(screen.queryByRole('link', { name: /استئناف/ })).not.toBeInTheDocument();
  });

  it('ReminderList يدمج تذكيرات الملفات والاستئنافات مرتبة بالأقرب أولًا', async () => {
    const file: ReminderDto = {
      actionId: 1,
      documentId: 100,
      borrowerName: 'أحمد',
      borrowerFather: 'خالد',
      borrowerFamily: 'الخطيب',
      actionText: '<p>إحالة عقار للمزاد</p>',
      reminderColor: 'أحمر',
      dueDate: '2026-09-10T00:00:00Z',
    };
    const appeal: AppealReminderDto = {
      actionId: 2,
      appealId: 7,
      documentId: 100,
      appealTitle: 'استئناف قرار رئيس التنفيذ — مستأنِفين',
      actionText: '<p>إيداع موجبات الاستئناف</p>',
      reminderColor: 'أصفر',
      dueDate: '2026-09-01T00:00:00Z',
    };
    const onCancelAppeal = vi.fn();

    render(
      <MemoryRouter>
        <ReminderList reminders={[file]} appealReminders={[appeal]} onCancelAppeal={onCancelAppeal} />
      </MemoryRouter>,
    );

    const items = screen.getAllByRole('listitem');
    // الأقرب أولًا: تذكير الاستئناف (1 أيلول) قبل تذكير الملف (10 أيلول).
    expect(items[0]).toHaveTextContent('استئناف قرار رئيس التنفيذ');
    expect(items[1]).toHaveTextContent('أحمد خالد الخطيب');

    const appealLink = screen.getByRole('link', { name: /استئناف قرار رئيس التنفيذ/ });
    expect(appealLink).toHaveAttribute('href', '/appeals/7');

    await userEvent.click(screen.getByRole('button', { name: 'إلغاء التذكير' }));
    expect(onCancelAppeal).toHaveBeenCalledWith(appeal);
  });

  it('AlertRow المرتبط باستئناف يفتح تفاصيل الاستئناف لا الملف', () => {
    const alert = {
      id: 3,
      message: 'أحال إليك رئيس القسم استئناف لمتابعته أصولًا (ملف أحمد)',
      targetType: 'lawyer',
      documentId: 100,
      appealId: 12,
      createdAt: '2026-08-01T00:00:00Z',
    } as unknown as HeadAlertDto;

    render(
      <MemoryRouter>
        <ul><AlertRow alert={alert} /></ul>
      </MemoryRouter>,
    );

    const link = screen.getByRole('link', { name: /أحال إليك رئيس القسم استئناف/ });
    expect(link).toHaveAttribute('href', '/appeals/12');
    // اللافتة واعية بالاستئناف لا «رسالة لمحامٍ».
    expect(screen.getByText('إسناد استئناف')).toBeInTheDocument();
  });

  it('تسميات أنواع التنبيهات: واعية بالاستئناف عند وجوده وأصلية بدونه', () => {
    expect(appealAwareTypeLabel({ targetType: 'head', appealId: 1 })).toBe(
      'بانتظار اختيار محامٍ للاستئناف',
    );
    expect(appealAwareTypeLabel({ targetType: 'document', appealId: 1 })).toBe('حالة استئناف');
    // بلا استئناف تبقى التسميات الأصلية (منها تسمية مرحلة الإنابة).
    expect(appealAwareTypeLabel({ targetType: 'head' })).toBe('مرحلة إنابة');
    expect(appealAwareTypeLabel({ targetType: 'lawyer' })).toBe('رسالة لمحامٍ');
  });
});

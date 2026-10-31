import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { AlertsPanel } from './AlertsPanel';
import { makeHeadAlertSummary } from '../../test/factories';

const ALERT = makeHeadAlertSummary({
  id: 1,
  messagePreview: 'تعميم اجتماع الفرع',
  recipientCount: 2,
  unreadCount: 1,
});

function renderPanel(props?: Partial<React.ComponentProps<typeof AlertsPanel>>) {
  return render(
    <MemoryRouter>
      <AlertsPanel
        badge={<span>badge</span>}
        error=""
        alerts={[ALERT]}
        {...props}
      />
    </MemoryRouter>,
  );
}

describe('AlertsPanel', () => {
  it('يعرض الشارة والطرف الأيمن والقائمة', () => {
    renderPanel({ headerExtra: <span>extra</span> });

    expect(screen.getByRole('heading', { name: 'تنبيهات رئيس القسم' })).toBeInTheDocument();
    expect(screen.getByText('badge')).toBeInTheDocument();
    expect(screen.getByText('extra')).toBeInTheDocument();
    expect(screen.getByText('تعميم اجتماع الفرع')).toBeInTheDocument();
    expect(screen.queryByText('لا توجد تنبيهات حالياً')).not.toBeInTheDocument();
  });

  it('يعرض النموذج والخطأ والحالة الفارغة عند غياب التنبيهات', () => {
    renderPanel({ alerts: [], form: <form aria-label="نموذج" />, error: 'تعذّر الجلب' });

    expect(screen.getByRole('form', { name: 'نموذج' })).toBeInTheDocument();
    // الخطأ معلن لقارئ الشاشة.
    expect(screen.getByRole('alert')).toHaveTextContent('تعذّر الجلب');
    expect(screen.getByText('لا توجد تنبيهات حالياً')).toBeInTheDocument();
  });

  it('يعرض التذييل (سطر «المزيد») داخل البطاقة بعد القائمة', () => {
    renderPanel({ footer: <p>يُعرض 2 من أصل 7 تنبيهًا</p> });

    expect(screen.getByText('يُعرض 2 من أصل 7 تنبيهًا')).toBeInTheDocument();
  });

  it('يمرر زر القراءة للصف عند توفره فقط', async () => {
    const onMarkRead = vi.fn();
    const { unmount } = renderPanel({ onMarkRead, markingKey: null });

    expect(screen.getByRole('button', { name: 'تمت القراءة' })).toBeInTheDocument();
    unmount();

    // غياب `onMarkRead` (صف الرئيس) يُخفي الزر — عقد موثّق في الواجهة.
    renderPanel({});
    expect(screen.queryByRole('button', { name: 'تمت القراءة' })).not.toBeInTheDocument();
  });
});

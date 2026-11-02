import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AlertRow } from './AlertRow';
import { makeHeadAlertSummary } from '../../test/factories';

vi.mock('react-router-dom', () => ({
  Link: ({ children, to, ...rest }: { children: React.ReactNode; to: string } & Record<string, unknown>) => (
    <a href={to} {...rest}>
      {children}
    </a>
  ),
}));

// `AlertDetails` لا يُستدعى هنا (المعاينة غير مقطوعة في الأمثلة) — والجلب عند الطلب
// له اختباره المستقل، فلا حاجة لتهيئة `api` في هذا الملف.
const PENDING_PREVIEW = 'أحال لك رئيس القسم ملفات من دائرة دمشق الأولى — يرجى تحديث معلوماتها';

function pendingAlert() {
  return makeHeadAlertSummary({
    id: 5,
    kind: 'circuit_pending_documents',
    messagePreview: PENDING_PREVIEW,
    targetType: 'lawyer',
    documentId: 7,
    targetLawyerId: 3,
    isRead: false,
  });
}

describe('AlertRow', () => {
  it('تنبيه إعادة القيد يفتح صفحة المعلقات لا الملف المفرد — بالدلالة `kind` لا بالنص', async () => {
    render(<AlertRow alert={pendingAlert()} />);

    // رغم وجود documentId، تنبيه (محامٍ × دائرة) المدمج يفتح صفحة معلقاته كلها.
    expect(screen.getByRole('link', { name: PENDING_PREVIEW })).toHaveAttribute(
      'href',
      '/pending-registrations',
    );
  });

  it('نص يبدأ ببادئة «أحال لك…» بدون `kind` الدوائر لا يُوجّه إلى المعلقات', () => {
    render(
      <AlertRow
        alert={makeHeadAlertSummary({
          kind: 'manual_to_lawyer',
          messagePreview: PENDING_PREVIEW,
          targetType: 'lawyer',
          documentId: null,
        })}
      />,
    );

    // التوجيه بالدلالة لا ببادئة نص قد تُبتر أو تتغيّر صياغتها.
    expect(screen.queryByRole('link', { name: PENDING_PREVIEW })).not.toBeInTheDocument();
    expect(screen.getByText(PENDING_PREVIEW)).toBeInTheDocument();
  });

  it('يعرض `messagePreview` وحدها ولا زر «عرض النص الكامل» عندما تكون المعاينة كاملة', () => {
    render(<AlertRow alert={pendingAlert()} />);

    expect(screen.getByText(PENDING_PREVIEW)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'عرض النص الكامل' })).not.toBeInTheDocument();
  });

  it('يعرض «عرض النص الكامل» عند `previewTruncated` فقط', () => {
    render(<AlertRow alert={makeHeadAlertSummary({ previewTruncated: true })} />);

    expect(screen.getByRole('button', { name: 'عرض النص الكامل' })).toHaveAttribute(
      'aria-expanded',
      'false',
    );
  });

  it('عرض الصادر: عدّاد صفري يظهر «مقروء من الجميع» بدل «غير مقروء: 0»', () => {
    const sent = { ...pendingAlert(), isRead: null, recipientCount: 1, unreadCount: 0 };
    const { rerender } = render(<AlertRow alert={sent} sentView />);
    expect(screen.getByText('مقروء من الجميع')).toBeInTheDocument();
    expect(screen.queryByText(/غير مقروء/)).not.toBeInTheDocument();

    rerender(<AlertRow alert={{ ...sent, unreadCount: 1 }} sentView />);
    expect(screen.getByText(/غير مقروء/)).toBeInTheDocument();
    expect(screen.queryByText('مقروء من الجميع')).not.toBeInTheDocument();
  });

  it('العرض الافتراضي (مستلمات): العدّاد الصفري يبقى عدّادًا بلا شارة مقروء', () => {
    render(<AlertRow alert={{ ...pendingAlert(), recipientCount: 2, unreadCount: 0 }} />);
    expect(screen.getByText(/غير مقروء/)).toBeInTheDocument();
    expect(screen.queryByText('مقروء من الجميع')).not.toBeInTheDocument();
  });

  it('نقطة القراءة: حمراء لغير المقروء في «مستلمة»، ولا تُرسم إطلاقًا في «مرسلة»', () => {
    // «مرسلة» بلا نقطة أصلًا (لا شفافة تحجز فراغًا): `isRead` حالة المستلم وهي
    // `null` للمُرسل، فرسمها حمراء يقرأ «غير مقروء» كاذبًا على تنبيه أرسله هو.
    const unread = { ...pendingAlert(), isRead: false };
    const sent = { ...pendingAlert(), isRead: null, recipientCount: 3, unreadCount: 2 };

    const receivedView = render(<AlertRow alert={unread} />);
    expect(receivedView.container.querySelector('.bg-red-500')).not.toBeNull();
    receivedView.unmount();

    const sentView = render(<AlertRow alert={sent} sentView />);
    expect(sentView.container.querySelector('.bg-red-500')).toBeNull();
    expect(sentView.container.querySelector('.bg-gray-300')).toBeNull();
    // حالة المستلمين تُعلن بالعدّاد وحده (ولا نقطة).
    expect(screen.getByText(/غير مقروء/)).toBeInTheDocument();
  });

  it('يعرض تسمية النوع من الكتالوج للرمز المعروف، ولا يخترع تسمية للرمز المجهول', () => {
    const { unmount } = render(<AlertRow alert={pendingAlert()} />);
    expect(screen.getByText('ملفات دائرة بانتظار التحديث')).toBeInTheDocument();
    unmount();

    // رمز لم يُزامَن بعد: لا تسمية عامة تُوهم المستخدم بنوع معروف.
    render(<AlertRow alert={makeHeadAlertSummary({ kind: 'kind_added_later' })} />);
    expect(screen.queryByText('تنبيه')).not.toBeInTheDocument();
  });

  it('الرمز المجهول لا يمنع فتح الوجهة الاحتياطية للملف المرتبط', () => {
    render(
      <AlertRow
        alert={makeHeadAlertSummary({ kind: 'kind_added_later', messagePreview: 'مرجع قديم', documentId: 5 })}
      />,
    );

    expect(screen.getByRole('link', { name: 'مرجع قديم' })).toHaveAttribute('href', '/documents/5');
  });
});

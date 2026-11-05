import { describe, expect, it } from 'vitest';
import {
  ALERT_KINDS,
  UNKNOWN_ALERT_KIND,
  alertKindLabel,
  alertRoute,
  isKnownAlertKind,
  resolveAlertKind,
} from './alertKinds';
import { makeHeadAlertSummary } from '../../test/factories';

describe('alertKinds — كتالوج أنواع التنبيهات', () => {
  it('يغطي الرموز المعتمدة الـ40 + الرمز المحجوز `legacy` (41 مدخلًا، بلا تكرار)', () => {
    const keys = Object.keys(ALERT_KINDS);
    expect(keys).toHaveLength(41);
    expect(new Set(keys).size).toBe(41);
    expect(keys).toContain('legacy');
    expect(keys).not.toContain('unknown_kind');
  });

  it('تسمية كل رمز غير فارغة وموجودة فعلًا', () => {
    for (const [kind, definition] of Object.entries(ALERT_KINDS)) {
      expect(definition.label.trim(), `التسمية مفقودة للرمز ${kind}`).not.toBe('');
    }
  });

  it('تطابق تسميات عيّنة من العائلات مع `HeadAlertKindCatalog.ToLabel` الخلفي', () => {
    expect(alertKindLabel('delegation_pending_approval')).toBe('بانتظار اعتماد الإنابة');
    expect(alertKindLabel('circuit_pending_documents')).toBe('ملفات دائرة بانتظار التحديث');
    expect(alertKindLabel('appeal_assigned_to_lawyer')).toBe('إسناد استئناف');
    expect(alertKindLabel('review_letter_reply')).toBe('رد على كتاب المطالعة');
    expect(alertKindLabel('manual_to_lawyer')).toBe('رسالة خاصة');
    expect(alertKindLabel('legacy')).toBe('تنبيه سابق');
  });

  it('الرمز المجهول يعود إلى تعريف احتياطي بدل الرمي أو عرض الرمز الخام', () => {
    expect(isKnownAlertKind('kind_added_later')).toBe(false);
    expect(resolveAlertKind('kind_added_later')).toBe(UNKNOWN_ALERT_KIND);
    expect(alertKindLabel('kind_added_later')).toBe('تنبيه');
    expect(resolveAlertKind(null)).toBe(UNKNOWN_ALERT_KIND);
    expect(resolveAlertKind(undefined)).toBe(UNKNOWN_ALERT_KIND);
  });

  it('التوجيه من الكتالوج: إعادة القيد تفتح المعلقات حتى مع وجود `documentId`', () => {
    expect(alertRoute(makeHeadAlertSummary({ kind: 'circuit_pending_documents', documentId: 7 }))).toBe(
      '/pending-registrations',
    );
  });

  it('التوجيه من الكتالوج: كتب المطالعة والاستئناف والملف اليدوي', () => {
    expect(alertRoute(makeHeadAlertSummary({ kind: 'review_letter_reply', reviewLetterId: 4 }))).toBe('/reviews/4');
    expect(alertRoute(makeHeadAlertSummary({ kind: 'appeal_assigned_to_lawyer', appealId: 9 }))).toBe('/appeals/9');
    expect(alertRoute(makeHeadAlertSummary({ kind: 'manual_to_document', documentId: 12 }))).toBe('/documents/12');
  });

  it('سلسلة الاحتياط للمعرّفات تعمل للرمز المجهول وللأنواع النصية', () => {
    // نوع مجهول لكن الصف يحمل مرجعًا حقيقيًا: لا نُفقد الرابط.
    expect(
      alertRoute(
        makeHeadAlertSummary({ kind: 'kind_added_later', reviewLetterId: 3, appealId: 8, documentId: 5 }),
      ),
    ).toBe('/reviews/3');
    // نوع نصي (تعميم) بلا مرجع: لا وجهة ولا رابط كاذب.
    expect(alertRoute(makeHeadAlertSummary({ kind: 'manual_to_branch' }))).toBeNull();
    // نوع نصي يحمل ملفًا مرتبطًا: يُفتح الملف.
    expect(alertRoute(makeHeadAlertSummary({ kind: 'manual_to_branch', documentId: 21 }))).toBe('/documents/21');
  });

  it('الأولوية: الكتالوج قبل المعرّفات، ثم مطالعة ← استئناف ← ملف', () => {
    expect(
      alertRoute(
        makeHeadAlertSummary({ kind: 'appeal_final_to_base_lawyer', appealId: 2, documentId: 30 }),
      ),
    ).toBe('/appeals/2');
    expect(
      alertRoute(makeHeadAlertSummary({ kind: 'kind_added_later', appealId: 8, documentId: 5 })),
    ).toBe('/appeals/8');
    expect(alertRoute(makeHeadAlertSummary({ kind: 'kind_added_later', documentId: 5 }))).toBe('/documents/5');
  });
});

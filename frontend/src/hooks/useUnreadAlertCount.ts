import { api } from '../api/client';
import { useCancellableRequest, type CancellableRequestResult } from './useCancellableRequest';

/**
 * النطاق الشخصي الموحّد لعدّاد غير المقروء: `user` يعني «غير المقروء لي» لكل الأدوار.
 * حُسم 2026-11-02 بعد اعتراض المالك في الاستخدام الحيّ: `received` للرئيس/الشعبة
 * كان يجمع غير المقروء لدى كل مستلمي التنبيهات، فيبقى الرقم بعد قراءة صاحب
 * الحساب لكل تنبيهاته. المجموع التجميعي يبقى ظاهرًا في عدّاد كل صف داخل «مرسلة».
 */
export const ALERTS_UNREAD_SCOPE = 'user' as const;

export interface UnreadAlertCount {
  count: number;
}

/**
 * عدّاد التنبيهات غير المقروءة للفاعل نفسه — hook واحد لصفحات اللوحة والحساب
 * والتنبيهات بدل تكرار الجلب في كل صفحة.
 *
 * يحافظ على العقد نفسه `CancellableRequestResult` لأن `AccountPage` يقرأ `error`
 * و`AlertsPage` يستخدم `setData` لإنقاص العدّاد فور تعليم المقروء. تفعيل الجلب
 * يبقى مسؤولية كل صفحة (`enabled`) لأن شروطه تختلف بحسب الدور والفرع.
 */
export function useUnreadAlertCount(enabled: boolean): CancellableRequestResult<UnreadAlertCount> {
  return useCancellableRequest<UnreadAlertCount>(
    (signal) =>
      api
        .get('/alerts/unread-count', { signal, params: { scope: ALERTS_UNREAD_SCOPE } })
        .then((r) => r.data),
    [ALERTS_UNREAD_SCOPE],
    { enabled },
  );
}

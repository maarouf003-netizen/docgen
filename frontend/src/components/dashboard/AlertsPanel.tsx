import type { ReactNode } from 'react';
import type { HeadAlertSummaryDto } from '../../types';
import { AlertRow } from './AlertRow';

export interface AlertsPanelProps {
  /** شارة العدّاد بجانب العنوان (حمراء للمحامي، خضراء للإجمالي عند الرئيس). */
  badge: ReactNode;
  /** سطر توضيحي تحت العنوان — يحدد دلالة الشارة (نحو «غير المقروء في تنبيهاتك» للرئيس). */
  subtitle?: ReactNode;
  /** الطرف الأيمن للترويسة («الأحدث أولاً» للمحامي، زر الإصدار للرئيس). */
  headerExtra?: ReactNode;
  /** نموذج الإصدار — للرئيس فقط، يُعرض بين الترويسة وكتلة الخطأ. */
  form?: ReactNode;
  /** تذييل أسفل القائمة — سطر «المزيد» عند تجاوز `totalCount` حدود الصفحة الواحدة. */
  footer?: ReactNode;
  /** نص الخطأ (يُدمج خطأ الإجراء مع خطأ الاستعلام في اللوحة). */
  error: string;
  /** تحميل الصفحة الأولى: تُعرض حالة تحميل بدل «لا توجد تنبيهات» الكاذبة. */
  isLoading?: boolean;
  alerts: HeadAlertSummaryDto[];
  /** تعليم المقروء — للمحامي فقط (غيابه يُخفي الزر كما في صف الرئيس). */
  onMarkRead?: (a: HeadAlertSummaryDto) => void;
  markingKey?: string | null;
  /** عرض الصادر (تبويب «أرسلتها»): يُظهر شارة «مقروء من الجميع» بدل العدّاد الصفري. */
  sentView?: boolean;
}

/**
 * لوحة «تنبيهات رئيس القسم» المشتركة (محامٍ/رئيس): نفس الهيكل والنصوص
 * والأصناف — الاختلاف كله في الشارة والطرف الأيمن والنموذج وزر القراءة.
 * الصفوف من عقد الملخص (`HeadAlertSummaryDto`): معاينة فقط، والنص الكامل عند الطلب.
 */
export function AlertsPanel({
  badge,
  subtitle,
  headerExtra,
  form,
  footer,
  error,
  isLoading = false,
  alerts,
  onMarkRead,
  markingKey,
  sentView = false,
}: AlertsPanelProps) {
  // جسم اللوحة بحالاته الثلاث: تحميل الصفحة الأولى، فراغ مشروع، أو القائمة.
  // و«الفراغ» لا يُعلن تحت خطأ: الفشل لا يعني أن للمستخدم صفر تنبيهات، ورسالة
  // «لا توجد تنبيهات» تحته تناقض اللافتة وتُخفي أن الخلل في الجلب لا في البيانات.
  let body: ReactNode;
  if (alerts.length === 0 && isLoading) {
    body = (
      <div className="p-10 text-center">
        <p className="text-gray-500 text-sm" role="status">
          جارٍ تحميل التنبيهات…
        </p>
      </div>
    );
  } else if (alerts.length === 0) {
    body = error ? null : (
      <div className="p-10 text-center">
        <p className="text-gray-400 text-sm">لا توجد تنبيهات حالياً</p>
      </div>
    );
  } else {
    body = (
      <ul className="divide-y divide-gray-100 max-h-[420px] overflow-y-auto">
        {alerts.map((a) => (
          <AlertRow key={a.id} alert={a} onMarkRead={onMarkRead} markingKey={markingKey} sentView={sentView} />
        ))}
      </ul>
    );
  }

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
      <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2 min-w-0">
          <span className="w-2 h-2 shrink-0 rounded-full bg-red-500" aria-hidden="true" />
          <div className="min-w-0">
            <h3 className="font-bold text-gray-900">تنبيهات رئيس القسم</h3>
            {subtitle ? <p className="text-[11px] text-gray-500">{subtitle}</p> : null}
          </div>
          {badge}
        </div>
        {headerExtra}
      </div>

      {form}

      {error ? (
        <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100" role="alert">
          <p className="text-red-700 text-sm">{error}</p>
        </div>
      ) : null}

      {body}

      {footer}
    </div>
  );
}

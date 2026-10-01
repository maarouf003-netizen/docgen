import type { ReactNode } from 'react';
import type { HeadAlertDto } from '../../types';
import { AlertRow } from './AlertRow';

export interface AlertsPanelProps {
  /** شارة العدّاد بجانب العنوان (حمراء للمحامي، خضراء للإجمالي عند الرئيس). */
  badge: ReactNode;
  /** الطرف الأيمن للترويسة («الأحدث أولاً» للمحامي، زر الإصدار للرئيس). */
  headerExtra?: ReactNode;
  /** نموذج الإصدار — للرئيس فقط، يُعرض بين الترويسة وكتلة الخطأ. */
  form?: ReactNode;
  /** نص الخطأ (يُدمج خطأ الإجراء مع خطأ الاستعلام في اللوحة). */
  error: string;
  alerts: HeadAlertDto[];
  /** تعليم المقروء — للمحامي فقط (غيابه يُخفي الزر كما في صف الرئيس). */
  onMarkRead?: (a: HeadAlertDto) => void;
  markingKey?: string | null;
}

/**
 * لوحة «تنبيهات رئيس القسم» المشتركة (محامٍ/رئيس): نفس الهيكل والنصوص
 * والأصناف — الاختلاف كله في الشارة والطرف الأيمن والنموذج وزر القراءة.
 */
export function AlertsPanel({
  badge,
  headerExtra,
  form,
  error,
  alerts,
  onMarkRead,
  markingKey,
}: AlertsPanelProps) {
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
      <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
        <div className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full bg-red-500" aria-hidden="true" />
          <h3 className="font-bold text-gray-900">تنبيهات رئيس القسم</h3>
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

      {alerts.length === 0 ? (
        <div className="p-10 text-center">
          <p className="text-gray-400 text-sm">لا توجد تنبيهات حالياً</p>
        </div>
      ) : (
        <ul className="divide-y divide-gray-100 max-h-[420px] overflow-y-auto">
          {alerts.map((a) => (
            <AlertRow key={a.id} alert={a} onMarkRead={onMarkRead} markingKey={markingKey} />
          ))}
        </ul>
      )}
    </div>
  );
}

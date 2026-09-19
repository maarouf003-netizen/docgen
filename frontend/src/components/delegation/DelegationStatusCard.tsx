import type { DocumentResponse } from '../../types';
import { getDocumentBadge } from '../../utils/documentStatus';
import { SectionCard } from '../view/SectionCard';
import { DelegationActivityStrip } from './DelegationActivityStrip';

/**
 * بطاقة «حالة الإنابة» (L2/و3): تُعرض للملف المناب فقط (حين تكون له إنابة واردة) تحت
 * «بيانات السند التنفيذي»، وتجمع شارة الحالة الحالية للمناب (متداول/تريث/مسترد/منفذ...)
 * مع شريط «حالة الإنابة» (تنبيهات المرآة) — وهو التوثيق الوحيد لحالة المسار بتمامها.
 */
export function DelegationStatusCard({
  doc,
  delegationId,
}: {
  doc: DocumentResponse;
  delegationId: number;
}) {
  const badge = getDocumentBadge(doc);
  return (
    <SectionCard title="حالة الإنابة">
      <div className="flex items-start justify-between gap-2 flex-wrap">
        <p className="text-sm text-gray-700 min-w-0">
          حالة الملف المناب الحالية تبعًا لمسار الإنابة الوارد عليه.
        </p>
        <span className={`rounded-full px-3 py-1 text-xs font-medium whitespace-nowrap shrink-0 ${badge.cls}`}>
          {badge.text}
        </span>
      </div>
      <DelegationActivityStrip delegationId={delegationId} />
    </SectionCard>
  );
}
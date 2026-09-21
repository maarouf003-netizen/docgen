import type { AssetDto, DelegationDto } from '../../types';
import { getDocumentBadge } from '../../utils/documentStatus';
import {
  DELEGATION_STATUS_REGISTERED,
  isDelegationPending,
  withCourtPrefix,
} from '../../utils/delegationStatus';
import { SectionCard } from '../view/SectionCard';
import { DelegationDetails } from './DelegationDetails';

/** شارة حالة الملف المناب في سطر التشعبات (L11 — تحمل «مسترد»/«تريث»/«منفذ»...) عبر targetExecStatus. */
function targetStatusBadge(d: DelegationDto): { text: string; cls: string } | null {
  if (d.targetDocumentId == null) return null;
  return getDocumentBadge({
    execStatus: d.targetExecStatus ?? '',
    execSubStatus: '',
    isDraft: false,
    generalEntitySide: 'applicant',
  });
}

/**
 * شرح «بانتظار الإتمام» في المنيب (المصدر فقط — هذه البطاقة لا تظهر في المناب):
 * «بانتظار الإتمام — سُجّل الملف المناب أصولًا برقم أساس {N} دائرة تنفيذ {C}».
 * يظهر للمسجلة أصولًا ذات رقم مناب معروف (رقم وسنة معًا) ومناب غير نهائي فقط؛
 * المعلّقة/المحالة بلا مناب بعد، والمسجلة بمناب نهائي (مسترد/مشطوب/منفذ إنابة —
 * targetTerminal) بلا شرح لأن «الانتظار» انتهى (A2).
 */
function registrationExplanation(d: DelegationDto): string | null {
  if (d.status !== DELEGATION_STATUS_REGISTERED) return null;
  if (d.targetTerminal) return null;
  if (!d.targetFileNumber || !d.targetFileYear) return null;
  const targetNumber = `${d.targetFileNumber}/${d.targetFileYear}`;
  const court = withCourtPrefix(d.delegatedCourt);
  if (!court) return null;
  return `بانتظار الإتمام — سُجّل الملف المناب أصولًا برقم أساس ${targetNumber} ${court}`;
}

/**
 * بطاقة «تشعبات الملف» (في الملف المنيب): كل إناباته الصادرة، مع إمكانية تسطير إنابة
 * جديدة (للمحامي المالك على ملف متداول) وتعديل/حذف المعلّقة من محامي المنيب.
 */
export function DelegationsCard({
  delegations,
  canCreate,
  currentUserId,
  onCreate,
  onEdit,
  onDelete,
  sourceAssets,
  delegationsLoading,
}: {
  delegations: DelegationDto[];
  canCreate: boolean;
  /** معرف المحامي الحالي: به تُقيَّد أزرار تعديل/حذف بمحامي المنيب المالك (الخلفية تراقب أيضًا). */
  currentUserId?: number;
  onCreate: () => void;
  onEdit: (d: DelegationDto) => void;
  onDelete: (d: DelegationDto) => void;
  /** أصول الملف المنيب الحالية (سياق المنيب فقط) — تُكشف بها اللقطات اليتيمة (C2). */
  sourceAssets?: AssetDto[];
  /**
   * تحميل الإنابات الأولى جارٍ (بلا بيانات بعد) — يُعطَّل زر التسطير ويُعرض هيكل
   * تحميل بدل نص الفراغ الكاذب (E1). إعادة الجلب ببيانات حاضرة لا تعطّل.
   */
  delegationsLoading?: boolean;
}) {
  return (
    <SectionCard
      title="تشعبات الملف"
     
      actions={
        canCreate && (
          <button
            type="button"
            onClick={onCreate}
            disabled={delegationsLoading}
            aria-disabled={delegationsLoading}
            className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
          >
            {delegationsLoading ? 'جارٍ التحميل…' : 'تسطير إنابة'}
          </button>
        )
      }
    >

      {delegationsLoading ? (
        <p className="text-gray-400 text-sm motion-safe:animate-pulse">جارٍ تحميل الإنابات…</p>
      ) : delegations.length === 0 ? (
        <p className="text-gray-400 text-sm">لا توجد إنابات مسجلة لهذا الملف</p>
      ) : (
        <ul className="divide-y divide-gray-100">
          {delegations.map((d) => {
            const manageable = isDelegationPending(d.status) && d.createdById === currentUserId;
            return (
              <li key={d.id} className="py-3 first:pt-0 last:pb-0">
                <DelegationDetails d={d} sourceAssets={sourceAssets} />
                {(() => {
                  const explanation = registrationExplanation(d);
                  return explanation == null ? null : (
                    <p className="mt-2 text-xs font-medium text-violet-900 bg-violet-50 border border-violet-200 rounded-lg px-3 py-2 break-words">
                      {explanation}
                    </p>
                  );
                })()}
                {(() => {
                  const badge = targetStatusBadge(d);
                  return badge == null ? null : (
                    <p className="flex items-center gap-2 mt-2 text-xs text-gray-500">
                      <span>حالة الملف المناب</span>
                      <span className={`rounded-full px-2 py-0.5 text-xs whitespace-nowrap ${badge.cls}`}>
                        {badge.text}
                      </span>
                    </p>
                  );
                })()}
                {manageable && (
                  <div className="flex gap-2 mt-3">
                    <button
                      type="button"
                      onClick={() => onEdit(d)}
                      className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 text-xs min-h-11"
                    >
                      تعديل
                    </button>
                    <button
                      type="button"
                      onClick={() => onDelete(d)}
                      className="text-red-700 hover:bg-red-50 rounded-lg px-3 py-2 text-xs min-h-11"
                    >
                      حذف
                    </button>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </SectionCard>
  );
}

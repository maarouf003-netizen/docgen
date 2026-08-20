import type { DelegationDto } from '../../types';
import { isDelegationPending } from '../../utils/delegationStatus';
import { SectionCard } from '../view/SectionCard';
import { DelegationDetails } from './DelegationDetails';

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
}: {
  delegations: DelegationDto[];
  canCreate: boolean;
  /** معرف المحامي الحالي: به تُقيَّد أزرار تعديل/حذف بمحامي المنيب المالك (الخلفية تراقب أيضًا). */
  currentUserId?: number;
  onCreate: () => void;
  onEdit: (d: DelegationDto) => void;
  onDelete: (d: DelegationDto) => void;
}) {
  return (
    <SectionCard
      title="تشعبات الملف"
     
      actions={
        canCreate && (
          <button
            type="button"
            onClick={onCreate}
            className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
          >
            تسطير إنابة
          </button>
        )
      }
    >

      {delegations.length === 0 ? (
        <p className="text-gray-400 text-sm">لا توجد إنابات مسجلة لهذا الملف</p>
      ) : (
        <ul className="divide-y divide-gray-100">
          {delegations.map((d) => {
            const manageable = isDelegationPending(d.status) && d.createdById === currentUserId;
            return (
              <li key={d.id} className="py-3 first:pt-0 last:pb-0">
                <DelegationDetails d={d} />
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

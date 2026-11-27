import { formatEntityCoverage, isEntryPendingReview } from '../../utils/entityRegistry';
import type { PublicEntityEntryDto } from '../../types';
import {
  AbolishBranchPanel,
  MergeBranchesPanel,
  RenameBranchPanel,
  UnifyBranchesPanel,
} from './branchManagement/BranchManagementPanels';
import { ParentBranchCard } from './branchManagement/ParentBranchCard';
import { useBranchManagement } from './branchManagement/useBranchManagement';

interface BranchManagementModalProps {
  groupId: number;
  groupName: string;
  onClose: () => void;
  onCommitted: (summary: string) => void;
}

/**
 * إدارة فروع جهة عامة لرئيس القسم (محافظته فقط):
 * تعديل تسمية / دمج / إلغاء / توحيد تسميات — كلها تمر بمعاينة موحّدة قبل الاعتماد،
 * مع بطاقة «الجهة الأم» للقراءة فقط واقتراح تعديلها (سحب ذاتي ما دام معلّقًا).
 * الحالة والقيادة في `branchManagement/useBranchManagement.ts`، واللوحات وبطاقة الأم
 * في `branchManagement/*`.
 */
export function BranchManagementModal({ groupId, groupName, onClose, onCommitted }: BranchManagementModalProps) {
  const m = useBranchManagement({ groupId, groupName, onCommitted });

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overscroll-contain"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={`إدارة فروع: ${groupName}`}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[85vh] flex flex-col overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div className="min-w-0">
            <h3 className="text-lg font-bold text-gray-800">إدارة فروع الجهة</h3>
            <p className="text-xs text-gray-500 mt-0.5 break-words">
              «{groupName}» — محافظتك فقط · تعديل / دمج / إلغاء / توحيد
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 shrink-0 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="overflow-y-auto p-5 grow overscroll-contain">
          {m.loading && <p className="text-sm text-gray-500">جارِ تحميل الفروع…</p>}
          {m.error && <p role="alert" className="text-red-600 text-sm mb-3">{m.error}</p>}
          {m.success && <p role="status" className="text-emerald-700 bg-emerald-50 border border-emerald-100 rounded-lg p-2 text-sm mb-3">{m.success}</p>}
          {!m.loading && m.suggestionError && (
            <p role="alert" className="text-amber-800 bg-amber-50 border border-amber-200 rounded-lg p-2 text-sm mb-3">
              تعذّر تحميل اقتراح تعديل الجهة الأم: {m.suggestionError}
            </p>
          )}

          {!m.loading && !m.error && m.entries.length === 0 && (
            <p className="text-sm text-gray-400 text-center py-8">لا توجد فروع نشطة في محافظتك لهذه الجهة</p>
          )}

          {!m.loading && m.entries.length > 0 && (
            <>
              <ParentBranchCard m={m} />

              {/* قائمة الفروع */}
              <div className="divide-y divide-gray-100 border border-gray-100 rounded-xl overflow-hidden mb-5">
                {m.activeEntries.map((e: PublicEntityEntryDto) => (
                  <div key={e.id} className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div className="min-w-0">
                      <div className="font-medium text-gray-800 break-words">{e.branchName}</div>
                      <div className="text-xs text-gray-500 mt-0.5">
                        {formatEntityCoverage(e)}
                        {e.coverageLabel && <span className="mr-2">{e.governorate}</span>}
                      </div>
                      {isEntryPendingReview(e) && <span className="inline-block mt-1 bg-amber-100 text-amber-800 rounded-full px-2 py-0.5 text-xs">بانتظار المراجعة</span>}
                    </div>
                    <div className="flex flex-wrap gap-2 shrink-0">
                      <button
                        onClick={() => m.startEdit(e)}
                        className="border border-sky-200 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
                      >
                        تعديل التسمية
                      </button>
                      <button
                        onClick={() => {
                          m.setAbolishId(e.id);
                          m.setAbolishTarget('');
                          m.setError('');
                          m.setSuccess('');
                          m.clearPreview();
                        }}
                        className="border border-red-200 text-red-700 hover:bg-red-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
                      >
                        إلغاء
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              <RenameBranchPanel m={m} />
              <MergeBranchesPanel m={m} />
              <AbolishBranchPanel m={m} />
              <UnifyBranchesPanel m={m} />
            </>
          )}
        </div>

        <div className="px-5 py-4 border-t border-gray-100 flex justify-end">
          <button
            onClick={onClose}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            إغلاق
          </button>
        </div>
      </div>
    </div>
  );
}

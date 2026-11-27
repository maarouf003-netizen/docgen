import {
  CITATION_FORMULA_OPTIONS,
  ENTITY_TYPE_OPTIONS,
  formatEntityCoverage,
  isEntryPendingReview,
} from '../../../utils/entityRegistry';
import type { CitationFormula, PublicEntityType } from '../../../types';
import type { BranchManagementModel } from './useBranchManagement';

/**
 * بطاقة «الجهة الأم» للقراءة فقط + اقتراح تعديلها (وسحب الاقتراح المعلّق ذاتيًا).
 * لا تُعرض أزرار تعديل/إلغاء الفروع هنا — تلك للفروع فقط.
 */
export function ParentBranchCard({ m }: { m: BranchManagementModel }) {
  const parent = m.parent;
  if (!parent) return null;

  return (
    <div className="bg-violet-50 border border-violet-200 rounded-xl p-4 mb-5">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-bold text-gray-800 break-words">{parent.canonicalName}</span>
            <span className="inline-block bg-violet-700 text-white rounded-full px-2 py-0.5 text-xs">الجهة الأم</span>
            <span className="inline-block bg-violet-100 text-violet-700 rounded-full px-2 py-0.5 text-xs">قراءة فقط</span>
          </div>
          <p className="text-xs text-gray-500 mt-1 break-words">
            {formatEntityCoverage(parent)}
            {parent.coverageLabel && <span className="mr-2 text-violet-700">{parent.governorate}</span>}
          </p>
          {m.pendingSuggestion && (
            <p className="mt-2 flex flex-wrap items-center gap-2 text-xs">
              <span className="inline-block bg-amber-100 text-amber-800 rounded-full px-2 py-0.5">
                اقتراحك «{parent.canonicalName}» قيد المراجعة
              </span>
            </p>
          )}
        </div>
        <div className="flex flex-wrap gap-2 shrink-0">
          {m.pendingSuggestion ? (
            <>
              {isEntryPendingReview(parent) && (
                <span className="inline-block bg-amber-100 text-amber-800 rounded-full px-2 py-0.5 text-xs self-center">بانتظار المراجعة</span>
              )}
              <button
                onClick={m.withdrawSuggestion}
                disabled={m.busy}
                className="border border-red-200 text-red-700 hover:bg-red-50 disabled:opacity-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
              >
                سحب الاقتراح
              </button>
            </>
          ) : (
            <button
              onClick={() => m.openPropose(parent)}
              className="bg-violet-700 hover:bg-violet-600 text-white rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-violet-500"
            >
              اقتراح تعديل الجهة الأم
            </button>
          )}
        </div>
      </div>

      {/* نموذج اقتراح تعديل الجهة الأم */}
      {m.proposeEntry && !m.pendingSuggestion && (
        <div className="mt-4 bg-white border border-violet-200 rounded-xl p-4">
          <h4 className="font-bold text-sm text-gray-800 mb-3">اقتراح تعديل بيانات الجهة الأم</h4>
          <p className="text-xs text-gray-500 mb-3">
            يُسجَّل اقتراحك للإدارة لمراجعته — لا يُعدَّل اسم الجهة فورًا. بادر بتعيين ما تراه مطلوبًا فقط.
          </p>
          <div className="grid sm:grid-cols-2 gap-3">
            <div className="sm:col-span-2">
              <label htmlFor="bmp-name" className="block text-xs font-medium text-gray-600 mb-1">الاسم المعتمد المقترح</label>
              <input
                id="bmp-name"
                value={m.pCanonical}
                onChange={(e) => m.setPCanonical(e.target.value)}
                placeholder="مثال: المديرية العامة للمصارف…"
                autoComplete="off"
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500"
              />
            </div>
            <div>
              <label htmlFor="bmp-type" className="block text-xs font-medium text-gray-600 mb-1">نوع الجهة المقترح</label>
              <select
                id="bmp-type"
                value={m.pType}
                onChange={(e) => m.setPType(e.target.value as PublicEntityType | '')}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-violet-500"
              >
                <option value="">بدون تغيير…</option>
                {ENTITY_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="bmp-citation" className="block text-xs font-medium text-gray-600 mb-1">صيغة ممثلها المقترحة</label>
              <select
                id="bmp-citation"
                value={m.pCitation}
                onChange={(e) => m.setPCitation(e.target.value as CitationFormula | '')}
                className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-violet-500"
              >
                <option value="">بدون تغيير…</option>
                {CITATION_FORMULA_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </div>
            <div className="sm:col-span-2">
              <label htmlFor="bmp-reason" className="block text-xs font-medium text-gray-600 mb-1">
                سبب الاقتراح <span className="text-red-600">*</span>
              </label>
              <textarea
                id="bmp-reason"
                value={m.pReason}
                onChange={(e) => m.setPReason(e.target.value)}
                rows={2}
                placeholder="مثال: تغيّر التسمية بموجب النظام الداخلي الجديد…"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500"
              />
            </div>
          </div>
          <div className="flex gap-2 mt-3">
            <button
              onClick={m.suggestParentEdit}
              disabled={m.busy}
              className="bg-violet-700 hover:bg-violet-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-violet-500"
            >
              {m.busy ? 'جارِ الإرسال…' : 'إرسال الاقتراح للإدارة'}
            </button>
            <button
              onClick={() => m.setProposeEntry(null)}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              تراجع
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

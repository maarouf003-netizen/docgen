import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { isEntryPendingReview } from '../../utils/entityRegistry';
import type { PublicEntityEntryDto } from '../../types';

interface BranchManagementModalProps {
  groupId: number;
  groupName: string;
  onClose: () => void;
  onCommitted: (summary: string) => void;
}

/**
 * إدارة فروع جهة عامة لرئيس القسم (محافظته فقط):
 * توحيد تسمية الفرع (تعديل branchName)، دمج فرعين، إلغاء فرع.
 * كل العمليات تمر عبر نقاط النهاية القائمة مع فرض نطاق المحافظة في الخدمة.
 */
export function BranchManagementModal({ groupId, groupName, onClose, onCommitted }: BranchManagementModalProps) {
  const [entries, setEntries] = useState<PublicEntityEntryDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [editingId, setEditingId] = useState<number | null>(null);
  const [editBranch, setEditBranch] = useState('');
  const [editCoverage, setEditCoverage] = useState('');
  const [editShowCoverage, setEditShowCoverage] = useState(false);
  const [savingEdit, setSavingEdit] = useState(false);

  const [mergeSource, setMergeSource] = useState<number | ''>('');
  const [mergeTarget, setMergeTarget] = useState<number | ''>('');
  const [merging, setMerging] = useState(false);

  const [abolishId, setAbolishId] = useState<number | null>(null);
  const [abolishing, setAbolishing] = useState(false);

  const reload = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const res = await api.get<PublicEntityEntryDto[]>(`/entity-registry/groups/${groupId}/entries`);
      setEntries(res.data ?? []);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [groupId]);

  useEffect(() => {
    reload();
  }, [reload]);

  const startEdit = (e: PublicEntityEntryDto) => {
    setEditingId(e.id);
    setEditBranch(e.branchName);
    setEditShowCoverage(!!e.coverageLabel);
    setEditCoverage(e.coverageLabel ?? '');
    setError('');
    setSuccess('');
  };

  const saveEdit = async () => {
    if (editingId === null || !editBranch.trim()) {
      setError('اسم الفرع مطلوب');
      return;
    }
    setSavingEdit(true);
    setError('');
    try {
      await api.put(`/entity-registry/${editingId}`, {
        branchName: editBranch.trim(),
        coverageLabel: editShowCoverage && editCoverage.trim() ? editCoverage.trim() : null,
      });
      setSuccess(`تم تحديث الفرع «${editBranch.trim()}»`);
      setEditingId(null);
      await reload();
      onCommitted(`تم تحديث فرع في «${groupName}»`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSavingEdit(false);
    }
  };

  const doMerge = async () => {
    if (mergeSource === '' || mergeTarget === '' || mergeSource === mergeTarget) {
      setError('اختر فرعًا مصدرًا وفرعًا هدفًا مختلفين');
      return;
    }
    setMerging(true);
    setError('');
    try {
      await api.post(`/entity-registry/${mergeSource}/move`, { targetEntryId: mergeTarget });
      setSuccess('تم دمج الفرع بنجاح — انتقلت روابط الملفات');
      setMergeSource('');
      setMergeTarget('');
      await reload();
      onCommitted(`تم دمج فرعين في «${groupName}»`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setMerging(false);
    }
  };

  const doAbolish = async () => {
    if (abolishId === null) return;
    setAbolishing(true);
    setError('');
    try {
      await api.put(`/entity-registry/${abolishId}`, { isActive: false });
      setSuccess('تم إلغاء الفرع');
      setAbolishId(null);
      await reload();
      onCommitted(`تم إلغاء فرع في «${groupName}»`);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setAbolishing(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={`إدارة فروع: ${groupName}`}
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[85vh] flex flex-col overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div>
            <h3 className="text-lg font-bold text-gray-800">إدارة فروع الجهة</h3>
            <p className="text-xs text-gray-500 mt-0.5">
              «{groupName}» — محافظتك فقط · توحيد / دمج / إلغاء
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 rounded-lg"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="overflow-y-auto p-5 grow overscroll-contain">
          {loading && <p className="text-sm text-gray-500">جارِ تحميل الفروع…</p>}
          {error && <p role="alert" className="text-red-600 text-sm mb-3">{error}</p>}
          {success && <p role="status" className="text-emerald-700 bg-emerald-50 border border-emerald-100 rounded-lg p-2 text-sm mb-3">{success}</p>}

          {!loading && entries.length === 0 && (
            <p className="text-sm text-gray-400 text-center py-8">لا توجد فروع نشطة في محافظتك لهذه الجهة</p>
          )}

          {!loading && entries.length > 0 && (
            <>
              <div className="divide-y divide-gray-100 border border-gray-100 rounded-xl overflow-hidden mb-5">
                {entries.map((e) => (
                  <div key={e.id} className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                    <div className="min-w-0">
                      <div className="font-medium text-gray-800 break-words">{e.branchName}</div>
                      <div className="text-xs text-gray-500 mt-0.5">
                        {e.governorate} · {e.citationFormula === 'add-to-position' ? 'إضافة لمنصبه' : 'إضافة لوظيفته'}
                        {e.coverageLabel && <span className="mr-2 bg-sky-50 text-sky-700 rounded-full px-2 py-0.5">{e.coverageLabel}</span>}
                      </div>
                      {isEntryPendingReview(e) && <span className="inline-block mt-1 bg-amber-100 text-amber-800 rounded-full px-2 py-0.5 text-xs">بانتظار المراجعة</span>}
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <button
                        onClick={() => startEdit(e)}
                        className="border border-sky-200 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-sky-500"
                      >
                        تعديل
                      </button>
                      <button
                        onClick={() => setAbolishId(e.id)}
                        className="border border-red-200 text-red-700 hover:bg-red-50 rounded-lg px-3 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
                      >
                        إلغاء
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              {/* تعديل فرع */}
              {editingId !== null && (
                <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 mb-4">
                  <h4 className="font-bold text-sm text-gray-800 mb-3">تعديل الفرع</h4>
                  <label htmlFor="bm-branch" className="block text-xs font-medium text-gray-600 mb-1">اسم الفرع</label>
                  <input
                    id="bm-branch"
                    value={editBranch}
                    onChange={(e) => setEditBranch(e.target.value)}
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                  <label className="inline-flex items-center gap-2 text-sm cursor-pointer mt-3 min-h-11">
                    <input
                      type="checkbox"
                      checked={editShowCoverage}
                      onChange={(ev) => { setEditShowCoverage(ev.target.checked); if (!ev.target.checked) setEditCoverage(''); }}
                      className="h-4 w-4"
                    />
                    تغطية أكثر من محافظة
                  </label>
                  {editShowCoverage && (
                    <input
                      value={editCoverage}
                      onChange={(e) => setEditCoverage(e.target.value)}
                      placeholder="مثال: دمشق وريفها"
                      maxLength={150}
                      className="mt-2 w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  )}
                  <div className="flex gap-2 mt-3">
                    <button
                      onClick={saveEdit}
                      disabled={savingEdit}
                      className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
                    >
                      {savingEdit ? 'جارِ الحفظ…' : 'حفظ'}
                    </button>
                    <button
                      onClick={() => setEditingId(null)}
                      className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                    >
                      إلغاء
                    </button>
                  </div>
                </div>
              )}

              {/* دمج فرعين */}
              <div className="bg-amber-50 border border-amber-200 rounded-xl p-4">
                <h4 className="font-bold text-sm text-gray-800 mb-2">دمج فرعين</h4>
                <p className="text-xs text-gray-500 mb-3">انقل روابط فرع مصدر إلى فرع هدف ضمن نفس الجهة — يُلغى المصدر.</p>
                <div className="grid sm:grid-cols-2 gap-3">
                  <div>
                    <label htmlFor="bm-src" className="block text-xs font-medium text-gray-600 mb-1">الفرع المصدر (سيُلغى)</label>
                    <select
                      id="bm-src"
                      value={mergeSource}
                      onChange={(e) => setMergeSource(e.target.value ? Number(e.target.value) : '')}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-500"
                    >
                      <option value="">اختر المصدر…</option>
                      {entries.map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
                    </select>
                  </div>
                  <div>
                    <label htmlFor="bm-tgt" className="block text-xs font-medium text-gray-600 mb-1">الفرع الهدف (يبقى)</label>
                    <select
                      id="bm-tgt"
                      value={mergeTarget}
                      onChange={(e) => setMergeTarget(e.target.value ? Number(e.target.value) : '')}
                      className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-amber-500"
                    >
                      <option value="">اختر الهدف…</option>
                      {entries.filter((e) => e.id !== mergeSource).map((e) => <option key={e.id} value={e.id}>{e.branchName} — {e.governorate}</option>)}
                    </select>
                  </div>
                </div>
                <button
                  onClick={doMerge}
                  disabled={merging || mergeSource === '' || mergeTarget === ''}
                  className="mt-3 bg-amber-700 hover:bg-amber-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-amber-500"
                >
                  {merging ? 'جارِ الدمج…' : 'دمج الفرعين'}
                </button>
              </div>

              {/* تأكيد إلغاء */}
              {abolishId !== null && (
                <div className="mt-4 bg-red-50 border border-red-200 rounded-xl p-4">
                  <p className="text-sm text-red-800 mb-3">هل أنت متأكد من إلغاء هذا الفرع؟ سيُعطّل ولن يظهر للربط.</p>
                  <div className="flex gap-2">
                    <button
                      onClick={doAbolish}
                      disabled={abolishing}
                      className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
                    >
                      {abolishing ? 'جارِ الإلغاء…' : 'تأكيد الإلغاء'}
                    </button>
                    <button
                      onClick={() => setAbolishId(null)}
                      className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
                    >
                      تراجع
                    </button>
                  </div>
                </div>
              )}
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

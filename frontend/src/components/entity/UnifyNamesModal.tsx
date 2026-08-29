import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import type {
  AbsorbedGroupUnifyPreviewDto,
  PublicEntityGroupDto,
  PublicEntityGroupListResponse,
  UnifyPreviewResponse,
} from '../../types';

interface UnifyNamesModalProps {
  onClose: () => void;
  onCommitted: (summary: string) => void;
  initialGroupId?: number;
}

/**
 * نافذة توحيد التسمية N←1 (المدير/المشرف — بلا هجرة ملفات):
 * تنقل قيود المجموعات الممتصة إلى الهوية الهدف وتعطّل المجموعات الممتصة.
 * لا تحفظ الأسماء القديمة كأسماء بديلة، لكنها تُسجّل في سجل التغييرات.
 */
export function UnifyNamesModal({ onClose, onCommitted, initialGroupId }: UnifyNamesModalProps) {
  const [groups, setGroups] = useState<PublicEntityGroupDto[]>([]);
  const [targetId, setTargetId] = useState<number | ''>(initialGroupId ?? '');
  const [absorbedIds, setAbsorbedIds] = useState<Set<number>>(new Set());
  const [query, setQuery] = useState('');
  const [preview, setPreview] = useState<UnifyPreviewResponse | null>(null);
  const [loadingGroups, setLoadingGroups] = useState(true);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [error, setError] = useState('');
  const [decreeKind, setDecreeKind] = useState('');
  const [decreeNumber, setDecreeNumber] = useState('');
  const [decreeDate, setDecreeDate] = useState('');

  useEffect(() => {
    let active = true;
    setLoadingGroups(true);
    api
      .get<PublicEntityGroupListResponse>('/entity-registry/groups', {
        params: { perPage: 100, q: query.trim() || undefined },
      })
      .then((res) => {
        if (active) setGroups(res.data.items ?? []);
      })
      .catch((err) => {
        if (active) setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (active) setLoadingGroups(false);
      });
    return () => {
      active = false;
    };
  }, [query]);

  // مزامنة initialGroupId عند تغيّره أو عند تحميل المجموعات
  useEffect(() => {
    if (initialGroupId != null && groups.length > 0) {
      const exists = groups.some((g) => g.groupId === initialGroupId);
      if (exists) setTargetId(initialGroupId);
    }
  }, [initialGroupId, groups]);

  const toggleAbsorbed = (id: number) => {
    setAbsorbedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
    setPreview(null);
    setError('');
  };

  const loadPreview = async () => {
    if (targetId === '' || absorbedIds.size === 0) {
      setError('اختر الهوية الهدف والهويات المراد توحيدها');
      return;
    }
    setLoadingPreview(true);
    setError('');
    try {
      const res = await api.post<UnifyPreviewResponse>('/entity-registry/groups/unify-preview', {
        targetGroupId: targetId,
        absorbedGroupIds: [...absorbedIds],
      });
      setPreview(res.data);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoadingPreview(false);
    }
  };

  const commit = async () => {
    if (!preview || targetId === '' || absorbedIds.size === 0) return;
    setCommitting(true);
    setError('');
    try {
      const res = await api.post('/entity-registry/groups/unify', {
        targetGroupId: targetId,
        absorbedGroupIds: [...absorbedIds],
        decreeKind: decreeKind.trim() || null,
        decreeNumber: decreeNumber.trim() || null,
        decreeDate: normalizeArabicDigits(decreeDate).trim() || null,
      });
      const r = res.data as { groupsUnified: number; entriesMoved: number; canonicalName: string };
      onCommitted(
        `تم توحيد ${r.groupsUnified} هويات في «${r.canonicalName}» — ${r.entriesMoved} قيدًا نُقل`,
      );
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setCommitting(false);
    }
  };

  const filteredForTarget = groups.filter((g) => !absorbedIds.has(g.groupId));
  const filteredForAbsorbed = groups.filter((g) => targetId === '' || g.groupId !== targetId);

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="توحيد تسمية جهات عامة"
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[85vh] flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div>
            <h3 className="text-lg font-bold text-gray-800">توحيد تسمية جهات عامة</h3>
            <p className="text-xs text-gray-500 mt-0.5">
              وحّد عدة هويات متشابهة في هوية واحدة — نقل القيود دون هجرة ملفات.
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
          {/* Search */}
          <div className="mb-4">
            <label htmlFor="unify-search" className="sr-only">بحث باسم الجهة</label>
            <input
              id="unify-search"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                setPreview(null);
              }}
              placeholder="بحث باسم الهوية…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>

          {loadingGroups && <p className="text-sm text-gray-500">جارِ تحميل الهويات…</p>}
          {!loadingGroups && error && !preview && (
            <p role="alert" className="text-red-600 text-sm mb-3">{error}</p>
          )}

          {!loadingGroups && groups.length > 0 && (
            <div className="grid sm:grid-cols-2 gap-4 mb-4">
              <div>
                <label htmlFor="unify-target" className="block text-xs font-medium text-gray-600 mb-1">
                  الهوية الهدف (يبقى اسمها)
                </label>
                <select
                  id="unify-target"
                  value={targetId}
                  onChange={(e) => {
                    setTargetId(e.target.value ? Number(e.target.value) : '');
                    setPreview(null);
                    setError('');
                  }}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  <option value="">اختر الهوية الهدف…</option>
                  {filteredForTarget.map((g) => (
                    <option key={g.groupId} value={g.groupId}>
                      {g.canonicalName} — {g.entryCount} قيد{g.governorates.length > 0 ? ` · ${g.governorates.join('، ')}` : ''}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <span className="block text-xs font-medium text-gray-600 mb-1">
                  الهويات المراد توحيدها (تُنقل قيودها للهدف)
                </span>
                <div className="max-h-40 overflow-y-auto border border-gray-200 rounded-lg p-2 overscroll-contain">
                  {filteredForAbsorbed.map((g) => (
                    <label key={g.groupId} className="flex items-center gap-2 py-1.5 cursor-pointer text-sm hover:bg-gray-50 rounded px-1">
                      <input
                        type="checkbox"
                        checked={absorbedIds.has(g.groupId)}
                        onChange={() => toggleAbsorbed(g.groupId)}
                        className="h-4 w-4 text-emerald-600 focus:ring-emerald-500"
                      />
                      <span className="truncate">
                        {g.canonicalName}
                        <span className="text-xs text-gray-400 mr-1">({g.entryCount} قيد{g.governorates.length > 0 ? ` · ${g.governorates.join('، ')}` : ''})</span>
                      </span>
                    </label>
                  ))}
                  {filteredForAbsorbed.length === 0 && (
                    <p className="text-xs text-gray-400 text-center py-2">لا توجد هويات متاحة</p>
                  )}
                </div>
              </div>
            </div>
          )}

          {targetId !== '' && absorbedIds.size > 0 && !preview && (
            <button
              onClick={loadPreview}
              disabled={loadingPreview}
              className="mb-4 bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              {loadingPreview ? 'جارِ المعاينة…' : 'معاينة التوحيد'}
            </button>
          )}

          {error && preview === null && (
            <p role="alert" className="text-red-600 text-sm mb-3">{error}</p>
          )}

          {preview && (
            <div className="bg-emerald-50 border border-emerald-200 rounded-lg p-4 text-sm">
              <h4 className="font-bold text-gray-800 mb-2">معاينة توحيد التسمية</h4>
              <p className="text-gray-600 mb-2">
                سيتم توحيد <strong>{preview.absorbedGroups.length}</strong> هويات في{' '}
                <strong>«{preview.targetName}»</strong> —{' '}
                <strong>{preview.totalEntriesToMove}</strong> قيدًا سيُنقل.
              </p>
              <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-lg p-2 mb-3">
                تنبيه: الأسماء القديمة للهويات الممتصة لن تُحفظ كأسماء بديلة، وسيُسجَّل التوحيد في سجل التغييرات فقط. لن تُهاجر روابط الملفات.
              </p>
              {preview.warnings.length > 0 && (
                <ul className="list-disc pr-5 text-amber-700 text-xs mb-2">
                  {preview.warnings.map((w, i) => <li key={i}>{w}</li>)}
                </ul>
              )}
              <ul className="divide-y divide-emerald-100">
                {preview.absorbedGroups.map((ag: AbsorbedGroupUnifyPreviewDto) => (
                  <li key={ag.groupId} className="py-2 flex items-center justify-between gap-2">
                    <span className="font-medium text-gray-800 truncate">{ag.name}</span>
                    <span className="text-xs text-gray-500 whitespace-nowrap tabular-nums">
                      {ag.entryCount} قيد{ag.governorates.length > 0 ? ` · ${ag.governorates.join('، ')}` : ''}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {preview && (
            <div className="mt-4 border-t border-gray-100 pt-4">
              <h4 className="text-sm font-bold text-gray-700 mb-2">المرسوم (اختياري — للتعديلات العامة)</h4>
              <div className="grid sm:grid-cols-3 gap-3">
                <div>
                  <label htmlFor="unify-decree-kind" className="block text-xs font-medium text-gray-600 mb-1">نوع المرسوم</label>
                  <input
                    id="unify-decree-kind"
                    value={decreeKind}
                    onChange={(e) => setDecreeKind(e.target.value)}
                    placeholder="مثال: مرسوم تشريعي…"
                    autoComplete="off"
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                </div>
                <div>
                  <label htmlFor="unify-decree-number" className="block text-xs font-medium text-gray-600 mb-1">رقم المرسوم</label>
                  <input
                    id="unify-decree-number"
                    value={decreeNumber}
                    onChange={(e) => setDecreeNumber(e.target.value)}
                    placeholder="مثال: 123…"
                    autoComplete="off"
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                </div>
                <div>
                  <label htmlFor="unify-decree-date" className="block text-xs font-medium text-gray-600 mb-1">تاريخ المرسوم</label>
                  <input
                    id="unify-decree-date"
                    type="text"
                    value={decreeDate}
                    onChange={(e) => setDecreeDate(e.target.value)}
                    placeholder="مثال: 1/8/2026"
                    autoComplete="off"
                    className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="px-5 py-4 border-t border-gray-100 flex flex-wrap items-center justify-between gap-3">
          {error && preview !== null && <p role="alert" className="text-sm text-red-600">{error}</p>}
          <div className="flex gap-2 ms-auto">
            <button
              onClick={commit}
              disabled={!preview || committing}
              className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              {committing ? 'جارِ التوحيد…' : 'تأكيد التوحيد'}
            </button>
            <button
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              إغلاق
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

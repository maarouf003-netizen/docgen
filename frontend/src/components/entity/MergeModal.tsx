import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import type {
  AbsorbedGroupPreviewDto,
  MergePreviewResponse,
  PublicEntityEntryDto,
  PublicEntityListResponse,
} from '../../types';

interface MergeModalProps {
  onClose: () => void;
  onCommitted: (summary: string) => void;
}

/**
 * أداة الدمج N←1 (د5 §4): تعرض معاينة الدمج ثم تعتمده مع ترحيل الروابط وإضافة الأسماء البديلة.
 */
export function MergeModal({ onClose, onCommitted }: MergeModalProps) {
  const [entries, setEntries] = useState<PublicEntityEntryDto[]>([]);
  const [survivorId, setSurvivorId] = useState<number | ''>('');
  const [absorbedIds, setAbsorbedIds] = useState<Set<number>>(new Set());
  const [preview, setPreview] = useState<MergePreviewResponse | null>(null);
  const [loadingEntries, setLoadingEntries] = useState(true);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    api
      .get<PublicEntityListResponse>('/entity-registry', {
        params: { status: 'final', perPage: 200, includePending: true },
      })
      .then((res) => {
        if (active) setEntries(res.data.items ?? []);
      })
      .catch((err) => {
        if (active) setError(getApiErrorMessage(err));
      })
      .finally(() => {
        if (active) setLoadingEntries(false);
      });
    return () => { active = false; };
  }, []);

  const toggleAbsorbed = (id: number) => {
    setAbsorbedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
    setPreview(null);
    setMessage('');
  };

  const loadPreview = async () => {
    if (survivorId === '' || absorbedIds.size === 0) {
      setError('اختر الهوية الناجية والقيود المُهمَلة');
      return;
    }
    setLoadingPreview(true);
    setError('');
    setMessage('');
    try {
      const res = await api.post<MergePreviewResponse>('/entity-registry/merge-preview', {
        survivorGroupId: survivorId,
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
    if (!preview || survivorId === '' || absorbedIds.size === 0) return;
    setCommitting(true);
    setError('');
    try {
      const res = await api.post('/entity-registry/merge-commit', {
        survivorGroupId: survivorId,
        absorbedGroupIds: [...absorbedIds],
        unifyTexts: false,
      });
      const r = res.data as { absorbedGroupsCount: number; entriesMigrated: number; totalAffectedDocuments: number };
      onCommitted(
        `تم الدمج: ${r.absorbedGroupsCount} هويات في «${preview.survivorName}» — ${r.entriesMigrated} قيد، ${r.totalAffectedDocuments} ملفًا متأثرًا`,
      );
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setCommitting(false);
    }
  };

  const formatCoverage = (e: PublicEntityEntryDto) => e.coverageLabel ?? e.governorate;

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="دمج جهات عامة"
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[85vh] flex flex-col overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div>
            <h3 className="text-lg font-bold text-gray-800">دمج جهات عامة</h3>
            <p className="text-xs text-gray-500 mt-0.5">
              ادمج عدة هويات أم في هوية واحدة — ترحيل روابط الملفات تلقائيًا.
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="overflow-y-auto p-5 grow overscroll-contain">
          {loadingEntries && <p className="text-sm text-gray-500">جارِ تحميل السجل…</p>}
          {!loadingEntries && error && !preview && (
            <p role="alert" className="text-red-600 text-sm mb-3">{error}</p>
          )}

          {!loadingEntries && entries.length > 0 && (
            <div className="grid sm:grid-cols-2 gap-4 mb-4">
              <div>
                <label htmlFor="merge-survivor" className="block text-xs font-medium text-gray-600 mb-1">
                  الهوية الأم الناجية (تبقى)
                </label>
                <select
                  id="merge-survivor"
                  value={survivorId}
                  onChange={(e) => {
                    setSurvivorId(e.target.value ? Number(e.target.value) : '');
                    setPreview(null);
                    setMessage('');
                  }}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  <option value="">اختر الهوية الناجية…</option>
                  {entries
                    .filter((e) => !absorbedIds.has(e.groupId))
                    .map((e) => (
                      <option key={e.groupId} value={e.groupId}>
                        {e.canonicalName} — {formatCoverage(e)}
                      </option>
                    ))}
                </select>
              </div>
              <div>
                <span className="block text-xs font-medium text-gray-600 mb-1">
                  القيود المُهمَلة (تحتدمج في الناجية)
                </span>
                <div className="max-h-40 overflow-y-auto border border-gray-200 rounded-lg p-2">
                  {entries
                    .filter((e) => survivorId === '' || e.groupId !== survivorId)
                    .map((e) => (
                      <label key={`${e.groupId}-${e.id}`} className="flex items-center gap-2 py-1 cursor-pointer text-sm">
                        <input
                          type="checkbox"
                          checked={absorbedIds.has(e.groupId)}
                          onChange={() => toggleAbsorbed(e.groupId)}
                        />
                        <span className="truncate">{e.canonicalName} — {formatCoverage(e)}/{e.branchName}</span>
                      </label>
                    ))}
                </div>
              </div>
            </div>
          )}

          {survivorId !== '' && absorbedIds.size > 0 && !preview && (
            <button
              onClick={loadPreview}
              disabled={loadingPreview}
              className="mb-4 bg-sky-700 hover:bg-sky-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {loadingPreview ? 'جارِ المعاينة…' : 'معاينة الدمج'}
            </button>
          )}

          {error && preview === null && (
            <p role="alert" className="text-red-600 text-sm mb-3">{error}</p>
          )}

          {preview && (
            <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 text-sm">
              <h4 className="font-bold text-gray-800 mb-2">معاينة الدمج</h4>
              <p className="text-gray-600 mb-2">
                سيتم امتصاص{' '}
                <strong>{preview.absorbedGroups.length}</strong> هويات أم في{' '}
                <strong>«{preview.survivorName}»</strong>
                {' '}—{' '}
                <strong>{preview.totalAffectedDocuments}</strong> ملفًا متأثرًا.
              </p>
              {preview.warnings.length > 0 && (
                <ul className="list-disc pr-5 text-amber-700 text-xs mb-2">
                  {preview.warnings.map((w, i) => <li key={i}>{w}</li>)}
                </ul>
              )}
              <ul className="divide-y divide-amber-100">
                {preview.absorbedGroups.map((ag: AbsorbedGroupPreviewDto) => (
                  <li key={ag.groupId} className="py-2">
                    <span className="font-medium text-gray-800">{ag.name}</span>
                    <span className="text-xs text-gray-400 mr-2 tabular-nums">
                      ({ag.totalDocuments} ملفًا، {ag.entries.length} قيد)
                    </span>
                    {ag.aliases.length > 0 && (
                      <span className="text-xs text-gray-400 block">
                        أسماء بديلة: {ag.aliases.join('، ')}
                      </span>
                    )}
                    <ul className="pr-4 text-xs text-gray-500 mt-1">
                      {ag.entries.map((ae) => (
                        <li key={ae.entryId}>
                          {ae.governorate}/{ae.branchName} →{' '}
                          {ae.conflictsWithSurvivor ? (
                            <span className="text-amber-600">القيد الافتراضي</span>
                          ) : (
                            <span className="text-emerald-600">مطابق</span>
                          )}
                          {' '}({ae.documentCount} ملفًا)
                        </li>
                      ))}
                    </ul>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>

        <div className="px-5 py-4 border-t border-gray-100 flex flex-wrap items-center justify-between gap-3">
          {message && <p role="status" className="text-sm text-emerald-700">{message}</p>}
          {error && preview !== null && <p role="alert" className="text-sm text-red-600">{error}</p>}
          <div className="flex gap-2 ms-auto">
            <button
              onClick={commit}
              disabled={!preview || committing}
              className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {committing ? 'جارِ الدمج…' : 'تأكيد الدمج'}
            </button>
            <button
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              إغلاق
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

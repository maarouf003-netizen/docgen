import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { GOVERNORATES } from '../../utils/governorate';
import type {
  ImportCommitItemRequest,
  ImportPreviewItemDto,
  ImportPreviewResponse,
} from '../../types';

interface ImportModalProps {
  onClose: () => void;
  onCommitted: (summary: string) => void;
}

/**
 * أداة الاستيراد التاريخي (د12): تعرض النصوص المتمايزة بعد التطبيع مع عدّادات
 * ملفاتها، وتسمح بتعديل الاسم المعتمد واختيار المحافظة قبل الاعتماد الجماعي.
 */
export function ImportModal({ onClose, onCommitted }: ImportModalProps) {
  const [preview, setPreview] = useState<ImportPreviewItemDto[] | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [canonicals, setCanonicals] = useState<Record<string, string>>({});
  const [governorates, setGovernorates] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [committing, setCommitting] = useState(false);
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;
    api
      .post<ImportPreviewResponse>('/entity-registry/import-preview')
      .then((res) => {
        if (!active) return;
        const items = res.data.items ?? [];
        setPreview(items);
        setSelected(new Set(items.map((i) => i.normalizedName)));
        setCanonicals(
          Object.fromEntries(items.map((i) => [i.normalizedName, i.suggestedCanonicalName])),
        );
        setGovernorates(
          Object.fromEntries(items.map((i) => [i.normalizedName, i.governorates[0] ?? 'دمشق'])),
        );
      })
      .catch((err) => {
        if (active) setMessage(getApiErrorMessage(err));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, []);

  const toggle = (norm: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(norm)) next.delete(norm);
      else next.add(norm);
      return next;
    });
  };

  const commit = async () => {
    if (!preview) return;
    const items: ImportCommitItemRequest[] = preview
      .filter((p) => selected.has(p.normalizedName))
      .map((p) => ({
        normalizedName: p.normalizedName,
        canonicalName: (canonicals[p.normalizedName] ?? p.suggestedCanonicalName).trim(),
        entityType: 'ministry',
        governorate: governorates[p.normalizedName] ?? p.governorates[0] ?? 'دمشق',
        branchName: 'الفرع الرئيسي',
        addVariantsAsAliases: true,
      }));
    if (items.length === 0) {
      setMessage('حدّد نصًا واحدًا على الأقل للاعتماد');
      return;
    }

    setCommitting(true);
    setMessage('');
    try {
      const res = await api.post<{ groupsCreated: number; entriesCreated: number; aliasesAdded: number }>(
        '/entity-registry/import-commit',
        { items },
      );
      const r = res.data;
      onCommitted(
        `تم الاستيراد: ${r.entriesCreated} قيدًا نهائيًا (${r.groupsCreated} هوية، ${r.aliasesAdded} اسمًا بديلًا)`,
      );
    } catch (err) {
      setMessage(getApiErrorMessage(err));
    } finally {
      setCommitting(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="استيراد النصوص التاريخية"
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-3xl max-h-[85vh] flex flex-col overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div>
            <h3 className="text-lg font-bold text-gray-800">استيراد النصوص التاريخية</h3>
            <p className="text-xs text-gray-500 mt-0.5">
              كل كتابات الجهة المتشابهة تجتمع تحت قيد واحد نهائي مباشرة.
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
          {loading && <p className="text-sm text-gray-500">جارِ جمع النصوص…</p>}
          {!loading && message && preview === null && (
            <p role="alert" className="text-red-600 text-sm">{message}</p>
          )}
          {preview !== null && preview.length === 0 && !loading && (
            <p className="text-sm text-gray-400 py-8 text-center">
              لا توجد نصوص تاريخية جديدة — كل الكتابات مسجلة في السجل.
            </p>
          )}
          {preview !== null && preview.length > 0 && (
            <ul className="divide-y divide-gray-100">
              {preview.map((item) => (
                <li key={item.normalizedName} className="py-3 flex flex-col gap-2">
                  <label className="flex items-start gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={selected.has(item.normalizedName)}
                      onChange={() => toggle(item.normalizedName)}
                      aria-label={`اعتماد ${item.suggestedCanonicalName}`}
                      className="mt-1"
                    />
                    <span className="grow min-w-0">
                      <span className="block font-medium text-gray-800 break-words">
                        {item.suggestedCanonicalName}
                        {' '}
                        <span className="text-xs font-normal text-gray-400 tabular-nums">
                          ({item.totalDocuments} ملفًا)
                        </span>
                      </span>
                      <span className="block text-xs text-gray-400 truncate" title={item.variants.map((v) => v.text).join('، ')}>
                        كتابات: {item.variants.map((v) => v.text).join('، ')}
                      </span>
                    </span>
                  </label>
                  {selected.has(item.normalizedName) && (
                    <div className="grid sm:grid-cols-2 gap-3 pr-6">
                      <div>
                        <label htmlFor={`imp-name-${item.normalizedName}`} className="sr-only">الاسم المعتمد</label>
                        <input
                          id={`imp-name-${item.normalizedName}`}
                          value={canonicals[item.normalizedName] ?? item.suggestedCanonicalName}
                          onChange={(e) =>
                            setCanonicals((prev) => ({ ...prev, [item.normalizedName]: e.target.value }))}
                          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                        />
                      </div>
                      <div>
                        <label htmlFor={`imp-gov-${item.normalizedName}`} className="sr-only">المحافظة</label>
                        <select
                          id={`imp-gov-${item.normalizedName}`}
                          value={governorates[item.normalizedName] ?? item.governorates[0] ?? 'دمشق'}
                          onChange={(e) =>
                            setGovernorates((prev) => ({ ...prev, [item.normalizedName]: e.target.value }))}
                          className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                        >
                          {(item.governorates.length > 0 ? item.governorates : ['دمشق']).map((g) => (
                            <option key={g} value={g}>{g}</option>
                          ))}
                          {GOVERNORATES.filter((g) => !(item.governorates ?? []).includes(g)).map((g) => (
                            <option key={g} value={g}>{g}</option>
                          ))}
                        </select>
                      </div>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="px-5 py-4 border-t border-gray-100 flex flex-wrap items-center justify-between gap-3">
          {message && preview !== null && <p role="status" className="text-sm text-emerald-700">{message}</p>}
          <div className="flex gap-2 ms-auto">
            <button
              onClick={commit}
              disabled={committing || loading || !preview || preview.length === 0}
              className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
            >
              {committing ? 'جارِ الاعتماد…' : `اعتماد المحدد (${selected.size})`}
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

import { useEffect, useRef, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import RichTextEditor from '../RichTextEditor';
import type {
  CorrespondenceDto,
  CorrespondenceImportance,
  CorrespondenceTargetDto,
} from '../../types';
import {
  CORRESPONDENCE_IMPORTANCE_LABELS,
  correspondenceRoleLabel,
} from './correspondenceDisplay';

/**
 * نافذة تسطير مراسلة لطرف معيَّن بالاسم:
 * - من قائمة المراسلات: مراسلة عامة غير مرتبطة بملف (documentId فارغ).
 * - من تفاصيل ملف: المراسلة مرتبطة بذلك الملف حصرًا.
 * «حفظ وإرسال» تولّد الرقم والتاريخ تلقائيًا في الخلفية، والأهمية ثابتة بعده.
 */
export default function CreateCorrespondenceModal({
  documentId,
  documentTitle,
  portal = false,
  onClose,
  onCreated,
}: {
  documentId?: number | null;
  documentTitle?: string;
  portal?: boolean;
  onClose: () => void;
  onCreated?: (letter: CorrespondenceDto) => void;
}) {
  const base = portal ? '/portal/correspondence' : '/correspondence';
  const [bodyHtml, setBodyHtml] = useState('');
  const [importance, setImportance] = useState<CorrespondenceImportance>('normal');
  const [query, setQuery] = useState('');
  const [targets, setTargets] = useState<CorrespondenceTargetDto[]>([]);
  const [selectedTarget, setSelectedTarget] = useState<CorrespondenceTargetDto | null>(null);
  const [targetsLoading, setTargetsLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
const searchTimer = useRef<number | undefined>(undefined);
  const searchSeq = useRef(0);

  // بحث المستلمين بالاسم مع تأخير منعًا لإغراق الخادم بكل ضغطة.
  // عند وجود ملف تُقيَّد الأهلية بالخلفية تلقائيًا: مندوب←محامو الملف، محامٍ/رئيس←مناديب النطاق.
  useEffect(() => {
    window.clearTimeout(searchTimer.current);
    // إبطال أي استجابة معلّقة من مصطلح/ملف سابق حتى لا تكتسح نتائج أحدث.
    searchSeq.current += 1;
    const term = query.trim();
    if (!term) {
      setTargets([]);
      setTargetsLoading(false);
      return undefined;
    }
    setTargetsLoading(true);
    const seq = searchSeq.current;
    searchTimer.current = window.setTimeout(() => {
      const params: Record<string, unknown> = { q: term };
      if (documentId) params.documentId = documentId;
      api
        .get<CorrespondenceTargetDto[]>(`${base}/targets`, { params })
        .then((r) => {
          if (seq === searchSeq.current)
            setTargets(Array.isArray(r.data) ? r.data : []);
        })
        .catch(() => {
          if (seq === searchSeq.current) setTargets([]);
        })
        .finally(() => {
          if (seq === searchSeq.current) setTargetsLoading(false);
        });
    }, 300);
    return () => window.clearTimeout(searchTimer.current);
  }, [query, base, documentId]);

  const submit = async () => {
    if (!selectedTarget) {
      setError('حدّد الطرف المستلم بالاسم أولًا');
      return;
    }
    setSaving(true);
    setError('');
    try {
      const response = await api.post<CorrespondenceDto>(base, {
        documentId: documentId ?? null,
        targetUserId: selectedTarget.userId,
        importance,
        bodyHtml,
      });
      onCreated?.(response.data);
      onClose();
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label="تسطير مراسلة"
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto overscroll-contain">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 sticky top-0 bg-white">
          <h3 className="text-lg font-bold text-gray-800">تسطير مراسلة</h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 rounded"
            aria-label="إغلاق"
          >
            ×
          </button>
        </div>

        <div className="px-5 py-4">
          {documentTitle ? (
            <div className="rounded-lg bg-gray-50 border border-gray-200 px-3 py-2 mb-4 text-sm text-gray-700">
              ستُربط هذه المراسلة بالملف: <span className="font-medium">{documentTitle}</span>
            </div>
          ) : (
            <div className="rounded-lg bg-emerald-50 border border-emerald-200 px-3 py-2 mb-4 text-sm text-emerald-800">
              مراسلة عامة غير مرتبطة بملف
            </div>
          )}

          <label htmlFor="correspondence-target" className="block text-sm font-medium text-gray-700 mb-1.5">
            الطرف المستلم (بالاسم)
          </label>
          <input
            id="correspondence-target"
            name="correspondence-target"
            type="search"
            autoComplete="off"
            value={selectedTarget ? selectedTarget.fullName : query}
            onChange={(e) => {
              setSelectedTarget(null);
              setQuery(e.target.value);
            }}
            placeholder="ابحث باسم المحامي أو رئيس القسم أو المندوب…"
            className="w-full min-w-0 border border-gray-300 rounded-lg px-3 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          />
          {targetsLoading && <p className="text-xs text-gray-500 mt-1">جارِ البحث…</p>}
          {!selectedTarget && !targetsLoading && query.trim() && targets.length === 0 && (
            <p className="text-xs text-gray-500 mt-1">
              {documentId
                ? 'لا يوجد مستلم مؤهل لهذه المراسلة — المؤهلون: مندوب←محامو الملف، محامٍ/رئيس←مناديب نطاق الملف'
                : 'لا توجد نتائج مطابقة للبحث'}
            </p>
          )}
          {!selectedTarget && targets.length > 0 && (
            <ul
              className="mt-1 max-h-44 overflow-y-auto border border-gray-200 rounded-lg divide-y divide-gray-100"
              role="listbox"
              aria-label="نتائج البحث عن المستلم"
            >
              {targets.map((t) => (
                <li key={t.userId}>
                  <button
                    type="button"
                    role="option"
                    aria-selected="false"
                    onClick={() => {
                      setSelectedTarget(t);
                      setTargets([]);
                    }}
                    className="w-full text-right px-3 py-2.5 text-sm hover:bg-emerald-50 min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-emerald-500"
                  >
                    <span className="font-medium text-gray-800">{t.fullName}</span>
                    <span className="text-xs text-gray-500">
                      {' '}
                      — {correspondenceRoleLabel(t.role)}
                      {t.branchName ? ` — ${t.branchName}` : ''}
                      {!t.branchName && t.governorate ? ` — ${t.governorate}` : ''}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          {selectedTarget && (
            <p className="text-xs text-emerald-700 mt-1">
              المستلم: <span className="font-medium">{selectedTarget.fullName}</span>
              {' — '}
              {correspondenceRoleLabel(selectedTarget.role)}
              <button
                type="button"
                onClick={() => {
                  setSelectedTarget(null);
                  setQuery('');
                }}
                className="underline hover:no-underline mr-2 min-h-11 px-1"
              >
                تغيير
              </button>
            </p>
          )}

          <label htmlFor="correspondence-importance" className="block text-sm font-medium text-gray-700 mb-1.5 mt-4">
            الأهمية (ثابتة بعد الإرسال)
          </label>
          <select
            id="correspondence-importance"
            name="correspondence-importance"
            value={importance}
            onChange={(e) => setImportance(e.target.value as CorrespondenceImportance)}
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            {(Object.keys(CORRESPONDENCE_IMPORTANCE_LABELS) as CorrespondenceImportance[]).map((key) => (
              <option key={key} value={key}>
                {CORRESPONDENCE_IMPORTANCE_LABELS[key]}
              </option>
            ))}
          </select>
          {importance === 'urgent' && (
            <p className="text-xs text-red-600 mt-1">المراسلة العاجلة تُظهر جرسًا للطرف المستلم حتى يؤكد مشاهدتها.</p>
          )}

          <label htmlFor="correspondence-body" className="block text-sm font-medium text-gray-700 mb-1.5 mt-4">
            نص المراسلة
          </label>
          <RichTextEditor value={bodyHtml} onChange={setBodyHtml} placeholder="اكتب نص المراسلة…" />

          {error && (
            <p className="text-red-600 text-sm mt-3" role="alert">
              {error}
            </p>
          )}

          <div className="mt-5 flex flex-wrap gap-2">
            <button
              onClick={submit}
              disabled={saving}
              className="bg-[#800000] hover:bg-[#9e0e0e] disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-[#800000]"
            >
              {saving ? 'جارِ الحفظ والإرسال…' : 'حفظ وإرسال'}
            </button>
            <button
              onClick={onClose}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              إلغاء
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

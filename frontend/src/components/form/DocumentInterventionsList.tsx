import { useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { useFocusTrap } from '../../hooks/useFocusTrap';
import type { DocumentInterventionDto } from '../../types';
import { formatDate } from '../../utils/dates';

/**
 * قائمة المتدخلين المحفوظين داخل صفحة التعديل: عرض مختصر + تعديل + حذف بتأكيد.
 * لا تُعرض شيئًا عند غياب المتدخلين؛ الإضافة من زر «إضافة متدخل» في سطر الصفة.
 */
export function DocumentInterventionsList({
  documentId,
  interventions,
  onChanged,
  onEdit,
}: {
  documentId: number;
  interventions: DocumentInterventionDto[];
  onChanged: (next: DocumentInterventionDto[]) => void;
  onEdit: (intervention: DocumentInterventionDto) => void;
}) {
  const [deleteTarget, setDeleteTarget] = useState<DocumentInterventionDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const confirmRef = useFocusTrap<HTMLDivElement>();

  if (interventions.length === 0 && !deleteTarget && !error) return null;

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setBusy(true);
    setError('');
    try {
      await api.delete(`/documents/${documentId}/interventions/${deleteTarget.id}`);
      onChanged(interventions.filter((i) => i.id !== deleteTarget.id));
      setDeleteTarget(null);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const openDelete = (intervention: DocumentInterventionDto) => {
    setError('');
    setDeleteTarget(intervention);
  };

  const closeDelete = () => {
    setDeleteTarget(null);
    setError('');
  };

  return (
    <div className="mt-3">
      {interventions.length > 0 && (
        <>
          <h4 className="text-xs font-bold text-gray-600 mb-2">المتدخلون المحفوظون</h4>
          <ul className="space-y-2">
            {interventions.map((intervention) => (
              <li
                key={intervention.id}
                className="rounded-lg border border-gray-200 bg-white px-3 py-2 flex flex-wrap items-start justify-between gap-2"
              >
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-gray-800 break-words">
                    {intervention.displayName || '—'}
                    <span className="ms-2 text-[11px] font-normal text-gray-500">
                      ({intervention.natureLabel})
                    </span>
                  </p>
                  {(intervention.receiptDate || intervention.acceptanceDate) && (
                    <p className="text-xs text-gray-500 mt-0.5">
                      {intervention.receiptDate
                        ? `ورود الطلب: ${formatDate(intervention.receiptDate)}`
                        : ''}
                      {intervention.acceptanceDate
                        ? `${intervention.receiptDate ? ' — ' : ''}القبول: ${formatDate(intervention.acceptanceDate)}`
                        : ''}
                    </p>
                  )}
                  {intervention.subject && (
                    <p className="text-xs text-gray-500 mt-0.5 line-clamp-2 break-words">
                      {intervention.subject}
                    </p>
                  )}
                </div>
                <div className="flex items-center gap-1 shrink-0">
                  <button
                    type="button"
                    onClick={() => onEdit(intervention)}
                    className="min-h-11 px-3 rounded-lg border border-emerald-200 text-emerald-800 text-xs hover:bg-emerald-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
                    aria-label={`تعديل المتدخل ${intervention.displayName}`}
                  >
                    تعديل
                  </button>
                  <button
                    type="button"
                    onClick={() => openDelete(intervention)}
                    className="min-h-11 px-3 rounded-lg border border-red-200 text-red-700 text-xs hover:bg-red-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
                    aria-label={`حذف المتدخل ${intervention.displayName}`}
                  >
                    حذف
                  </button>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}

      {deleteTarget && (
        <div
          className="fixed inset-0 bg-black/50 z-[60] flex items-center justify-center p-4"
          dir="rtl"
          role="dialog"
          aria-modal="true"
          aria-label="تأكيد حذف المتدخل"
        >
          <div ref={confirmRef} className="bg-white rounded-xl shadow-xl w-full max-w-sm p-5">
            <h4 className="text-base font-bold text-gray-800 mb-2">حذف المتدخل</h4>
            <p className="text-sm text-gray-600 mb-4">
              هل تريد حذف المتدخل «{deleteTarget.displayName || '—'}»؟ سيُحذف معه سطر «طلب تدخل»
              من وقوعات الملف، ويبقى أثر العملية في سجل التدقيق.
            </p>
            {error && (
              <div
                role="alert"
                className="mb-3 rounded-lg bg-red-50 border border-red-200 text-red-800 text-sm px-3 py-2"
              >
                {error}
              </div>
            )}
            <div className="flex flex-col-reverse sm:flex-row sm:justify-end gap-2">
              <button
                type="button"
                onClick={closeDelete}
                disabled={busy}
                className="min-h-11 px-4 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-60"
              >
                إلغاء
              </button>
              <button
                type="button"
                onClick={confirmDelete}
                disabled={busy}
                className="min-h-11 px-4 rounded-lg bg-red-700 hover:bg-red-600 text-white text-sm font-bold disabled:opacity-60 focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
              >
                {busy ? 'جارِ الحذف…' : 'حذف'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

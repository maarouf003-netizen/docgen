import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import type { DelegationDto } from '../types';
import { DelegationDetails } from '../components/delegation/DelegationDetails';
import AssignDelegationModal from '../components/delegation/AssignDelegationModal';

/**
 * نافذة رئيس القسم «طلبات الإنابة والاستئنافات والمطالعات»: طلبات الإنابة المعلّقة لفرعه
 * (الإنابات الداخلية من فرعه والإنابات الخارجية المنابة إليه من محافظات أخرى) — يعتمدها
 * باختيار المحامي المختص، فيُنشأ الملف المناب تلقائيًا ويُشعَر المحامي بتنبييه.
 */
export default function DelegationRequests() {
  const [delegations, setDelegations] = useState<DelegationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [assignTarget, setAssignTarget] = useState<DelegationDto | null>(null);
  const [successMessage, setSuccessMessage] = useState('');

  const load = useCallback(() => {
    setLoading(true);
    setLoadError('');
    api
      .get<DelegationDto[]>('/delegations/pending')
      .then((r) => setDelegations(Array.isArray(r.data) ? r.data : []))
      .catch((err) => setLoadError(getApiErrorMessage(err)))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const handleAssigned = (lawyerName: string) => {
    setAssignTarget(null);
    setSuccessMessage(`تم اعتماد الإنابة وتكليف المحامي ${lawyerName || 'المختص'}`);
    load();
  };

  return (
    <div className="max-w-4xl mx-auto">
      <div className="flex items-center justify-between gap-2 flex-wrap mb-6">
        <h2 className="text-xl sm:text-2xl font-bold text-gray-900">طلبات الإنابة</h2>
        {delegations.length > 0 && (
          <span className="text-xs bg-amber-100 text-amber-800 rounded-full px-3 py-1 font-medium">
            {delegations.length} {delegations.length === 1 ? 'طلب معلّق' : 'طلبات معلّقة'}
          </span>
        )}
      </div>

      {successMessage && (
        <div
          className="bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg px-4 py-3 text-sm mb-4"
          role="status"
        >
          {successMessage}
        </div>
      )}

      {loadError && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4" role="alert">
          <p>{loadError}</p>
          <button
            type="button"
            onClick={load}
            className="mt-2 text-red-800 underline underline-offset-2 min-h-11"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      {loading ? (
        <p className="text-gray-500">جارِ التحميل...</p>
      ) : delegations.length === 0 ? (
        <div className="bg-white rounded-xl shadow p-10 text-center">
          <p className="text-gray-400">لا توجد طلبات إنابة معلّقة لفرعك</p>
        </div>
      ) : (
        <ul className="space-y-4">
          {delegations.map((d) => (
            <li key={d.id} className="bg-white rounded-xl shadow p-5">
              <div className="flex items-start justify-between gap-3 flex-wrap mb-2">
                <p className="font-medium text-gray-800 text-sm min-w-0 break-words">
                  {d.sourceDocumentLabel || `ملف رقم ${d.sourceDocumentId}`}
                </p>
                <span className="text-xs text-gray-400 shrink-0">
                  {d.createdByName ? `سطرها: ${d.createdByName}` : ''}
                </span>
              </div>
              <DelegationDetails d={d} />
              <div className="mt-4 pt-3 border-t border-gray-100 flex justify-end">
                <button
                  type="button"
                  onClick={() => {
                    setSuccessMessage('');
                    setAssignTarget(d);
                  }}
                  className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                >
                  اعتماد واختيار محامٍ
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {assignTarget && (
        <AssignDelegationModal
          delegation={assignTarget}
          onClose={() => setAssignTarget(null)}
          onAssigned={handleAssigned}
        />
      )}
    </div>
  );
}

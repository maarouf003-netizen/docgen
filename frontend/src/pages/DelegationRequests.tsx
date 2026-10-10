import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import type { DelegationDto, SectionDto } from '../types';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { DelegationDetails } from '../components/delegation/DelegationDetails';
import AssignDelegationModal from '../components/delegation/AssignDelegationModal';

/**
 * نافذة الرئيس «طلبات الإنابة»: طلبات الإنابة المعلّقة لنطاقه
 * (الداخلية لدوائره والخارجية المنابة إليه) — يعتمدها باختيار المحامي
 * المختص، فيُنشأ الملف المناب تلقائيًا ويُشعَر المحامي بتنبيهه.
 * رئيس القسم يوجّه الخارجية لشعبة (`توجيه للشعبة`) ويتراجع قبل الإسناد،
 * ويرفض الدائرة الخطأ برسالة تُعيد المحامي للتصحيح (§7.3–§7.4).
 */
export default function DelegationRequests() {
  const { user } = useAuth();
  const isHead = user?.role === 'head';
  const [assignTarget, setAssignTarget] = useState<DelegationDto | null>(null);
  const [successMessage, setSuccessMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [rejectedOnly, setRejectedOnly] = useState(false);
  const [sections, setSections] = useState<SectionDto[]>([]);
  const [redirectTarget, setRedirectTarget] = useState<DelegationDto | null>(null);
  const [redirectSectionId, setRedirectSectionId] = useState<number | ''>('');
  const [rejectTarget, setRejectTarget] = useState<DelegationDto | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [busyId, setBusyId] = useState<number | null>(null);

  const requestsQuery = useCancellableRequest<DelegationDto[]>(
    (signal) =>
      api
        .get<DelegationDto[]>('/delegations/pending', { signal, params: rejectedOnly ? { rejectedOnly: true } : {} })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [rejectedOnly],
  );

  useEffect(() => {
    if (!isHead || user?.branchId == null) return;
    api
      .get<SectionDto[]>('/sections', { params: { branchId: user.branchId } })
      .then((r) => setSections(r.data ?? []))
      .catch(() => setSections([]));
  }, [isHead, user?.branchId]);

  const delegations = requestsQuery.data ?? [];
  const loading = requestsQuery.isLoading;
  const loadError = requestsQuery.error;

  const handleAssigned = (lawyerName: string) => {
    setAssignTarget(null);
    setSuccessMessage(`تم اعتماد الإنابة وتكليف المحامي ${lawyerName || 'المختص'}`);
    requestsQuery.refetch();
  };

  const runAction = async (d: DelegationDto, path: 'redirect' | 'recall-redirect' | 'reject', body: unknown) => {
    setBusyId(d.id);
    setActionError('');
    try {
      await api.post(`/delegations/${d.id}/${path}`, body ?? {});
      setRedirectTarget(null);
      setRejectTarget(null);
      setRejectReason('');
      requestsQuery.refetch();
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="max-w-4xl mx-auto">
      <div className="flex items-center justify-between gap-2 flex-wrap mb-6">
        <h2 className="text-xl sm:text-2xl font-bold text-gray-900">طلبات الإنابة</h2>
        <div className="flex items-center gap-2 flex-wrap">
          <button
            type="button"
            onClick={() => setRejectedOnly((v) => !v)}
            aria-pressed={rejectedOnly}
            className={`min-h-11 rounded-lg px-4 py-2 text-sm border focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 ${
              rejectedOnly ? 'bg-amber-100 border-amber-300 text-amber-900' : 'border-gray-300 text-gray-700 hover:bg-gray-50'
            }`}
          >
            {rejectedOnly ? 'كل المعلّقة' : 'مرفوض بانتظار التصحيح'}
          </button>
          {delegations.length > 0 && (
            <span className="text-xs bg-amber-100 text-amber-800 rounded-full px-3 py-1 font-medium tabular-nums">
              {delegations.length} {delegations.length === 1 ? 'طلب معلّق' : 'طلبات معلّقة'}
            </span>
          )}
        </div>
      </div>

      {successMessage && (
        <div
          className="bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg px-4 py-3 text-sm mb-4"
          role="status"
        >
          {successMessage}
        </div>
      )}

      {actionError && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4" role="alert">
          {actionError}
        </div>
      )}

      {loadError && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg px-4 py-3 text-sm mb-4" role="alert">
          <p>{loadError}</p>
          <button
            type="button"
            onClick={requestsQuery.refetch}
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
              {d.redirectedToSectionName && (
                <p className="text-xs text-indigo-700 mt-1">موجَّه لشعبة {d.redirectedToSectionName}</p>
              )}
              {d.rejectReason && (
                <p className="text-xs text-red-700 mt-1 break-words">مرفوض: {d.rejectReason}</p>
              )}
              <div className="mt-4 pt-3 border-t border-gray-100 flex justify-end gap-2 flex-wrap">
                {isHead && d.isExternal && !d.redirectedToSectionId && d.assignedLawyerId == null && (
                  <button
                    type="button"
                    onClick={() => { setRedirectTarget(d); setRedirectSectionId(''); }}
                    disabled={busyId === d.id}
                    className="border border-indigo-200 text-indigo-700 hover:bg-indigo-50 rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
                  >
                    توجيه للشعبة
                  </button>
                )}
                {isHead && d.redirectedToSectionId != null && d.assignedLawyerId == null && (
                  <button
                    type="button"
                    onClick={() => runAction(d, 'recall-redirect', {})}
                    disabled={busyId === d.id}
                    className="border border-gray-300 text-gray-700 hover:bg-gray-50 rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
                  >
                    {busyId === d.id ? 'جارِ...' : 'تراجع عن التوجيه'}
                  </button>
                )}
                {d.assignedLawyerId == null && (
                  <button
                    type="button"
                    onClick={() => { setRejectTarget(d); setRejectReason(d.rejectReason ?? ''); }}
                    disabled={busyId === d.id}
                    className="border border-red-200 text-red-700 hover:bg-red-50 rounded-lg px-4 py-2 text-sm min-h-11 disabled:opacity-50"
                  >
                    رفض
                  </button>
                )}
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

      {redirectTarget && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" dir="rtl" role="dialog" aria-modal="true" aria-label="توجيه الإنابة لشعبة">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-5 space-y-3">
            <h3 className="text-lg font-bold text-gray-800">توجيه الإنابة للشعبة</h3>
            <p className="text-xs text-gray-500">الإنابة من اختصاص الشعبة — يرجى التفضل بالاطلاع والإسناد.</p>
            <label htmlFor="redirect-section" className="block text-xs font-bold text-gray-600">الشعبة</label>
            <select
              id="redirect-section"
              value={redirectSectionId}
              onChange={(e) => setRedirectSectionId(e.target.value ? Number(e.target.value) : '')}
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              <option value="">اختر الشعبة...</option>
              {sections.map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setRedirectTarget(null)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm min-h-11"
              >
                إلغاء
              </button>
              <button
                type="button"
                disabled={redirectSectionId === '' || busyId === redirectTarget.id}
                onClick={() => runAction(redirectTarget, 'redirect', { sectionId: redirectSectionId })}
                className="bg-indigo-700 hover:bg-indigo-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {busyId === redirectTarget.id ? 'جارِ التوجيه...' : 'توجيه'}
              </button>
            </div>
          </div>
        </div>
      )}

      {rejectTarget && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4" dir="rtl" role="dialog" aria-modal="true" aria-label="رفض الإنابة">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-5 space-y-3">
            <h3 className="text-lg font-bold text-gray-800">رفض الإنابة — الدائرة ليست ضمن نطاقك</h3>
            <label htmlFor="reject-reason" className="block text-xs font-bold text-gray-600">سبب الرفض (يُعرض للمحامي للتصحيح)</label>
            <input
              id="reject-reason"
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="مثال: الدائرة المنابة غير صحيحة…"
              autoComplete="off"
              className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500"
            />
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setRejectTarget(null)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm min-h-11"
              >
                إلغاء
              </button>
              <button
                type="button"
                disabled={busyId === rejectTarget.id || !rejectReason.trim()}
                onClick={() => runAction(rejectTarget, 'reject', { reason: rejectReason.trim() })}
                className="bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {busyId === rejectTarget.id ? 'جارِ الرفض...' : 'رفض وإعادة للمحامي'}
              </button>
            </div>
          </div>
        </div>
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

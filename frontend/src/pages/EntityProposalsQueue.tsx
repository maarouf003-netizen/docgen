import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import {
  citationFormulaLabel,
  entityTypeLabel,
} from '../utils/entityRegistry';
import type { PublicEntityProposalDto } from '../types';

/**
 * قائمة انتظار اقتراحات الجهات الجديدة (د4): رئيس القسم يرى اقتراحات محافظته
 * حصرًا ويعتمدها أو يرفضها بسبب معلن. الاقتراح المعتمد ينشئ قيدًا نهائيًا فورًا.
 */
export default function EntityProposalsQueue() {
  const { user } = useAuth();

  const [proposals, setProposals] = useState<PublicEntityProposalDto[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState('');
  const [actionError, setActionError] = useState('');
  const [busyId, setBusyId] = useState<number | null>(null);
  const [successMsg, setSuccessMsg] = useState('');

  // نافذة الرفض
  const [rejecting, setRejecting] = useState<PublicEntityProposalDto | null>(null);
  const [reason, setReason] = useState('');
  const [rejectSaving, setRejectSaving] = useState(false);
  const [rejectError, setRejectError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setListError('');
    try {
      const res = await api.get<PublicEntityProposalDto[]>('/entity-registry/proposals/pending');
      setProposals(Array.isArray(res.data) ? res.data : []);
    } catch (err) {
      setListError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const approve = async (proposal: PublicEntityProposalDto) => {
    setBusyId(proposal.id);
    setActionError('');
    try {
      await api.post(`/entity-registry/proposals/${proposal.id}/approve`);
      setSuccessMsg(`تم اعتماد «${proposal.proposedName}» وإنشاء قيدها النهائي`);
      await load();
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  };

  const submitReject = async () => {
    if (!rejecting) return;
    if (!reason.trim()) {
      setRejectError('سبب الرفض مطلوب');
      return;
    }

    setRejectSaving(true);
    setRejectError('');
    try {
      await api.post(`/entity-registry/proposals/${rejecting.id}/reject`, { reason: reason.trim() });
      setSuccessMsg(`تم رفض اقتراح «${rejecting.proposedName}»`);
      setRejecting(null);
      setReason('');
      await load();
    } catch (err) {
      setRejectError(getApiErrorMessage(err));
    } finally {
      setRejectSaving(false);
    }
  };

  return (
    <div className="max-w-3xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">اقتراحات جهات بانتظار الاعتماد</h2>

      {user?.role === 'head' && (
        <p className="text-sm text-gray-500 mb-4">
          ترى هنا مقترحات محافظة فرعك فقط؛ بقية المحافظات لدى أصحابها.
        </p>
      )}

      {successMsg && (
        <p role="status" className="mb-4 bg-emerald-50 border border-emerald-100 text-emerald-800 rounded-lg p-3 text-sm">
          {successMsg}
        </p>
      )}
      {(actionError || listError) && (
        <div role="alert" className="mb-4 bg-red-50 border border-red-100 rounded-lg p-3 text-sm text-red-700 flex items-center justify-between gap-3">
          <span>{actionError || listError}</span>
          <button onClick={() => void load()} className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 min-h-11">
            إعادة المحاولة
          </button>
        </div>
      )}

      <ul className="space-y-3">
        {loading && <li className="bg-white rounded-xl shadow p-5 text-gray-500 text-sm">جارِ التحميل…</li>}

        {!loading && proposals !== null && proposals.length === 0 && (
          <li className="bg-white rounded-xl shadow p-10 text-center text-gray-400 text-sm">لا توجد اقتراحات معلّقة</li>
        )}

        {!loading && (proposals ?? []).map((p) => (
          <li key={p.id} className="bg-white rounded-xl shadow p-5">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <h3 className="font-bold text-gray-800 break-words">{p.proposedName}</h3>
                <p className="text-sm text-gray-600 mt-1">
                  {entityTypeLabel(p.entityType)} · {p.governorate} / {p.branchName}
                </p>
                <p className="text-xs text-gray-500 mt-0.5">
                  صيغة الممثل القانوني: {citationFormulaLabel(p.citationFormula)}
                </p>
                {p.proposedByName && (
                  <p className="text-xs text-gray-400 mt-0.5">قُدِّر من: {p.proposedByName}</p>
                )}
              </div>
              <span className="inline-block rounded-full bg-amber-100 text-amber-800 px-2 py-0.5 text-xs whitespace-nowrap">
                بانتظار الاعتماد
              </span>
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <button
                onClick={() => void approve(p)}
                disabled={busyId === p.id}
                className="bg-emerald-800 hover:bg-emerald-700 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {busyId === p.id ? 'جارِ الاعتماد…' : 'اعتماد وإنشاء القيد'}
              </button>
              <button
                onClick={() => { setRejecting(p); setReason(''); setRejectError(''); }}
                className="border border-red-200 text-red-600 hover:bg-red-50 rounded-lg px-4 py-2 text-sm min-h-11"
              >
                رفض…
              </button>
            </div>
          </li>
        ))}
      </ul>

      {rejecting && (
        <div
          className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
          dir="rtl"
          role="dialog"
          aria-modal="true"
          aria-label={`رفض اقتراح: ${rejecting.proposedName}`}
          style={{ overscrollBehavior: 'contain' }}
        >
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-5">
            <h3 className="text-lg font-bold text-gray-800 mb-1">رفض الاقتراح</h3>
            <p className="text-sm text-gray-500 mb-4 break-words">{rejecting.proposedName}</p>

            <label htmlFor="reject-reason" className="block text-xs font-medium text-gray-600 mb-1">سبب الرفض</label>
            <textarea
              id="reject-reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
              placeholder="يظهر السبب للمحامي مقدم الاقتراح…"
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
            {rejectError && <p role="alert" className="text-red-600 text-sm mt-2">{rejectError}</p>}

            <div className="mt-4 flex flex-wrap gap-2 justify-end">
              <button
                onClick={submitReject}
                disabled={rejectSaving}
                className="bg-red-600 hover:bg-red-500 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {rejectSaving ? 'جارِ الحفظ…' : 'تأكيد الرفض'}
              </button>
              <button
                onClick={() => setRejecting(null)}
                className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
              >
                إلغاء
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

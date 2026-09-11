import { useCallback, useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../../api/client';
import { normalizeArabicDigits } from '../../utils/arabicDigits';
import { formatDateTime } from '../../utils/dates';
import { entityTypeLabel } from '../../utils/entityRegistry';
import type {
  ParentEditSuggestionDto,
  ParentEditSuggestionListResponse,
  ParentEditSuggestionStatus,
  RenameGroupResponse,
  ReviewParentEditSuggestionRequest,
} from '../../types';

const fCount = new Intl.NumberFormat('ar-EG');

const STATUS_FILTERS: Array<{ value: '' | ParentEditSuggestionStatus; label: string }> = [
  { value: '', label: 'الكل' },
  { value: 'pending', label: 'قيد المراجعة' },
  { value: 'approved', label: 'مقبول' },
  { value: 'rejected', label: 'مرفوض' },
  { value: 'withdrawn', label: 'مسحوب' },
];

const STATUS_BADGE: Record<ParentEditSuggestionStatus, string> = {
  pending: 'bg-amber-100 text-amber-800',
  approved: 'bg-emerald-100 text-emerald-800',
  rejected: 'bg-red-100 text-red-800',
  withdrawn: 'bg-gray-100 text-gray-600',
};

const STATUS_LABEL: Record<ParentEditSuggestionStatus, string> = {
  pending: 'قيد المراجعة',
  approved: 'مقبول',
  rejected: 'مرفوض',
  withdrawn: 'مسحوب',
};

const DECREE_KINDS = ['قرار', 'قانون', 'مرسوم'];

/**
 * تبويب «اقتراحات الأم» — للمدير/المشرف:
 * قبول اقتراح تعديل الجهة الأم (اسم مقترح → عبر شاشة rename بمرسوم إلزامي ثم تعليم approved)
 * أو رفده مع سبب إلزامي.
 */
export default function ParentSuggestionsTab() {
  const [status, setStatus] = useState<'' | ParentEditSuggestionStatus>('');
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<ParentEditSuggestionDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const [rejecting, setRejecting] = useState<ParentEditSuggestionDto | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [rejectingBusy, setRejectingBusy] = useState(false);
  const [rejectError, setRejectError] = useState('');

  const [approving, setApproving] = useState<ParentEditSuggestionDto | null>(null);
  const [apprBusy, setApprBusy] = useState(false);
  const [apprError, setApprError] = useState('');
  // حقول المرسوم عند اقتراح اسم معتمد جديد (تُنفَّذ عبر rename بمرسوم إلزامي ثم approved).
  const [decreeKind, setDecreeKind] = useState('');
  const [decreeNumber, setDecreeNumber] = useState('');
  const [decreeDate, setDecreeDate] = useState('');

  const perPage = 10;

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const res = await api.get<ParentEditSuggestionListResponse>('/entity-registry/parent-edit-suggestions', {
        params: { status: status || undefined, page, perPage },
      });
      setItems(res.data?.items ?? []);
      setTotal(res.data?.total ?? 0);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [status, page, perPage]);

  useEffect(() => {
    void load();
  }, [load]);

  const pages = Math.max(1, Math.ceil(total / perPage));

  const needsRename = (s: ParentEditSuggestionDto) =>
    !!s.proposedCanonicalName && s.proposedCanonicalName.trim() !== s.canonicalName;

  // إعادة قراءة الاقتراح الحي من الخادم (المرجع الوحيد المصوت للاسم الحالي) بعد فشل rename،
  // لتقرير ما إذا كانت التسمية المقترحة طُبِّقت فعلًا في محاولة سابقة (فشل review بعد نجاح rename)
  // دون الاعتماد على نص رسالة الخطأ — مصدر الحقيقة هو الخادم لا واجهة الاتصال.
  const fetchSuggestionLive = useCallback(
    async (suggestionId: number): Promise<ParentEditSuggestionDto | null> => {
      const res = await api.get<ParentEditSuggestionListResponse>('/entity-registry/parent-edit-suggestions', {
        params: { status: status || undefined, page, perPage },
      });
      return res.data?.items.find((s) => s.id === suggestionId) ?? null;
    },
    [status, page, perPage],
  );

  const openApprove = (s: ParentEditSuggestionDto) => {
    setApproving(s);
    setApprError('');
    setApprBusy(false);
    setDecreeKind('قرار');
    setDecreeNumber('');
    setDecreeDate('');
  };

  const openReject = (s: ParentEditSuggestionDto) => {
    setRejecting(s);
    setRejectReason('');
    setRejectError('');
    setRejectingBusy(false);
  };

  const confirmApprove = async () => {
    if (!approving) return;
    setApprBusy(true);
    setApprError('');
    try {
      let appliedName = '';
      // تُنفَّذ إعادة التسمية بمرسوم فقط عندما يكون للاقتراح اسم مقترح مختلف عن الاسم الحالي.
      // عند الفشل متعدد الخطوات (نجاح rename ثم فشل review)، تُقرأ نسخة حية من الخادم: إن كان
      // الاسم المقترح مطابقًا فعلًا للاسم الحالي فالتسمية طُبِّقت في محاولة سابقة ويُستأنف بقبول
      // الاقتراح مباشرة (تدفق idempotent لا يعلّق السجل في حالة pending أبدًا).
      if (needsRename(approving)) {
        if (!decreeKind.trim() || !decreeNumber.trim() || !normalizeArabicDigits(decreeDate).trim()) {
          setApprError('بيانات المرجع (القرار/الرقم/التاريخ) مطلوبة لتطبيق التسمية المقترحة');
          setApprBusy(false);
          return;
        }
        try {
          const rename = await api.post<RenameGroupResponse>(
            `/entity-registry/groups/${approving.groupId}/rename`,
            {
              groupId: approving.groupId,
              newCanonicalName: approving.proposedCanonicalName!.trim(),
              decreeKind: decreeKind.trim(),
              decreeNumber: decreeNumber.trim(),
              decreeDate: normalizeArabicDigits(decreeDate).trim(),
            },
          );
          appliedName = rename.data?.newCanonicalName ?? approving.proposedCanonicalName!.trim();
        } catch (renameErr) {
          const live = await fetchSuggestionLive(approving.id);
          // الاسم لا يزال مختلفًا بعد الفشل → فشل حقيقي للـ rename (لا مكابرة على الحالة).
          if (!live || needsRename(live)) throw renameErr;
          appliedName = live.canonicalName;
        }
      }
      const review: ReviewParentEditSuggestionRequest = { status: 'approved' };
      await api.post(`/entity-registry/parent-edit-suggestions/${approving.id}/review`, review);
      setApproving(null);
      setSuccess(
        appliedName
          ? `قُبل الاقتراح — طُبِّقت التسمية «${appliedName}» على الجهة الأم بمرسوم`
          : 'قُبل الاقتراح — حالته الآن «مقبول»',
      );
      await load();
    } catch (err) {
      setApprError(getApiErrorMessage(err));
    } finally {
      setApprBusy(false);
    }
  };

  const confirmReject = async () => {
    if (!rejecting) return;
    if (!rejectReason.trim()) {
      setRejectError('سبب الرفض مطلوب');
      return;
    }
    setRejectingBusy(true);
    setRejectError('');
    try {
      const review: ReviewParentEditSuggestionRequest = {
        status: 'rejected',
        reviewReason: rejectReason.trim(),
      };
      await api.post(`/entity-registry/parent-edit-suggestions/${rejecting.id}/review`, review);
      setRejecting(null);
      setSuccess('رُفض الاقتراح مع تسجيل السبب');
      await load();
    } catch (err) {
      setRejectError(getApiErrorMessage(err));
    } finally {
      setRejectingBusy(false);
    }
  };

  return (
    <div>
      <p className="text-sm text-gray-600 mb-4">
        اقتراحات رؤساء الأقسام لتعديل «الجهة الأم» المُخزَّنة. قبول اقتراح اسم معتمد جديد يمر عبر إعادة
        التسمية بمرسوم إلزامي ثم يُعلَّم الاقتراح «مقبولًا».
      </p>

      {success && (
        <p role="status" className="mb-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg p-3 text-sm">
          {success}
        </p>
      )}
      {error && (
        <div role="alert" className="mb-4 bg-red-50 border border-red-100 rounded-lg p-3 text-sm text-red-700 flex items-center justify-between gap-3">
          <span>{error}</span>
          <button onClick={() => void load()} className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 min-h-11">
            إعادة المحاولة
          </button>
        </div>
      )}

      {/* فلاتر الحالة */}
      <div className="flex flex-wrap gap-2 mb-4" role="group" aria-label="تصفية بحالة الاقتراح">
        {STATUS_FILTERS.map((s) => (
          <button
            key={s.value}
            onClick={() => {
              setStatus(s.value);
              setPage(1);
            }}
            aria-pressed={status === s.value}
            className={`rounded-full px-4 py-1.5 text-xs min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500 ${
              status === s.value
                ? 'bg-emerald-700 text-white'
                : 'bg-white border border-gray-300 text-gray-700 hover:bg-emerald-50'
            }`}
          >
            {s.label}
          </button>
        ))}
      </div>

      {loading && <p className="text-sm text-gray-500">جارِ التحميل…</p>}

      {!loading && items.length === 0 && (
        <p className="bg-white rounded-xl shadow p-10 text-center text-gray-400 text-sm">
          لا توجد اقتراحات بهذه الحالة
        </p>
      )}

      <ul className="space-y-3">
        {items.map((s) => (
          <li key={s.id} className="bg-white rounded-xl shadow p-5">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <h3 className="font-bold text-gray-800 break-words">{s.canonicalName}</h3>
                <p className="text-xs text-gray-500 mt-0.5">
                  {entityTypeLabel(s.entityType)} · اقترحه: {s.createdByName} · {formatDateTime(s.createdAtUtc)}
                </p>
              </div>
              <span className={`inline-block rounded-full px-2 py-0.5 text-xs whitespace-nowrap ${STATUS_BADGE[s.status]}`}>
                {STATUS_LABEL[s.status]}
              </span>
            </div>

            <dl className="mt-3 space-y-1.5 text-sm">
              {s.proposedCanonicalName && s.proposedCanonicalName.trim() !== s.canonicalName && (
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <dt className="text-gray-500 text-xs">الاسم المقترح:</dt>
                  <dd className="font-medium text-gray-800 break-words">{s.proposedCanonicalName}</dd>
                </div>
              )}
              {s.proposedEntityType && (
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <dt className="text-gray-500 text-xs">نوع الجهة المقترح:</dt>
                  <dd className="text-gray-800 break-words">{entityTypeLabel(s.proposedEntityType)}</dd>
                </div>
              )}
              {s.proposedCitationFormula && (
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <dt className="text-gray-500 text-xs">صيغة الممثل المقترحة:</dt>
                  <dd className="text-gray-800 break-words">
                    {s.proposedCitationFormula === 'add-to-position' ? 'إضافة لمنصبه' : 'إضافة لوظيفته'}
                  </dd>
                </div>
              )}
              <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                <dt className="text-gray-500 text-xs">السبب:</dt>
                <dd className="text-gray-800 break-words">{s.reason}</dd>
              </div>
              {s.reviewReason && (
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <dt className="text-gray-500 text-xs">سبب الرفض:</dt>
                  <dd className="text-red-700 break-words">{s.reviewReason}</dd>
                </div>
              )}
            </dl>

            {s.status === 'pending' && (
              <div className="mt-4 flex flex-wrap gap-2">
                <button
                  onClick={() => openApprove(s)}
                  className="bg-emerald-700 hover:bg-emerald-600 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
                >
                  قبول
                </button>
                <button
                  onClick={() => openReject(s)}
                  className="border border-red-200 text-red-700 hover:bg-red-50 rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
                >
                  رفض
                </button>
              </div>
            )}
          </li>
        ))}
      </ul>

      {/* ترقيم */}
      {!loading && pages > 1 && (
        <nav aria-label="ترقيم الاقتراحات" className="mt-4 flex items-center justify-center gap-2">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-40 min-h-11"
          >
            السابق
          </button>
          <span className="text-sm text-gray-600 tabular-nums">
            صفحة {fCount.format(page)} من {fCount.format(pages)}
          </span>
          <button
            onClick={() => setPage((p) => Math.min(pages, p + 1))}
            disabled={page >= pages}
            className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-40 min-h-11"
          >
            التالي
          </button>
        </nav>
      )}

      {/* نافذة قبول */}
      {approving && (
        <ModalShell title="قبول اقتراح تعديل الجهة الأم" onClose={() => setApproving(null)}>
          {needsRename(approving) ? (
            <div className="space-y-4">
              <p className="text-xs text-gray-600 leading-relaxed">
                سيُعاد تسمية الجهة الأم إلى «<span className="font-medium">{approving.proposedCanonicalName!.trim()}</span>»
                عبر إعادة التسمية بمرسوم (يطبّق التسمية على كل الملفات) ثم يُعلَّم الاقتراح «مقبولًا».
              </p>
              <div>
                <label htmlFor="ps-decree-kind" className="block text-xs font-medium text-gray-600 mb-1">نوع المرجع</label>
                <select
                  id="ps-decree-kind"
                  value={decreeKind}
                  onChange={(e) => setDecreeKind(e.target.value)}
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                >
                  {DECREE_KINDS.map((k) => (
                    <option key={k} value={k}>{k}</option>
                  ))}
                </select>
              </div>
              <div>
                <label htmlFor="ps-decree-number" className="block text-xs font-medium text-gray-600 mb-1">رقم المرجع</label>
                <input
                  id="ps-decree-number"
                  value={decreeNumber}
                  onChange={(e) => setDecreeNumber(e.target.value)}
                  autoComplete="off"
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
              <div>
                <label htmlFor="ps-decree-date" className="block text-xs font-medium text-gray-600 mb-1">تاريخ المرجع</label>
                <input
                  id="ps-decree-date"
                  type="text"
                  value={decreeDate}
                  onChange={(e) => setDecreeDate(e.target.value)}
                  placeholder="مثال: 1/8/2026"
                  autoComplete="off"
                  className="w-full min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
              </div>
            </div>
          ) : (
            <p className="text-xs text-gray-600 leading-relaxed">
              هذا الاقتراح لا يتضمن اسمًا معتمدًا جديدًا (تغيير النوع/الصيغة فقط) — يُعلَّم «مقبولًا» دون تطبيق
              كتابة مباشر؛ تُطبَّق البيانات المقترحة من تبويب «تعديل جهة عامة».
            </p>
          )}

          {apprError && <p role="alert" className="text-red-600 text-sm mt-3">{apprError}</p>}
          <div className="mt-5 flex flex-wrap gap-2 justify-end">
            <button
              onClick={confirmApprove}
              disabled={apprBusy}
              className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-emerald-500"
            >
              {apprBusy ? 'جارِ التنفيذ…' : 'تأكيد القبول'}
            </button>
            <button
              onClick={() => setApproving(null)}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              تراجع
            </button>
          </div>
        </ModalShell>
      )}

      {/* نافذة رفض */}
      {rejecting && (
        <ModalShell title="رفض اقتراح تعديل الجهة الأم" onClose={() => setRejecting(null)}>
          <label htmlFor="ps-reject-reason" className="block text-xs font-medium text-gray-600 mb-1">
            سبب الرفض <span className="text-red-600">*</span>
          </label>
          <textarea
            id="ps-reject-reason"
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
            rows={3}
            placeholder="مثال: التسمية المقترحة تتعارض مع الهوية المعتمدة…"
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
          />
          {rejectError && <p role="alert" className="text-red-600 text-sm mt-2">{rejectError}</p>}
          <div className="mt-5 flex flex-wrap gap-2 justify-end">
            <button
              onClick={confirmReject}
              disabled={rejectingBusy}
              className="bg-red-700 hover:bg-red-600 disabled:opacity-50 text-white rounded-lg px-4 py-2 text-sm min-h-11 focus-visible:ring-2 focus-visible:ring-red-500"
            >
              {rejectingBusy ? 'جارِ التنفيذ…' : 'تأكيد الرفض'}
            </button>
            <button
              onClick={() => setRejecting(null)}
              className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11"
            >
              تراجع
            </button>
          </div>
        </ModalShell>
      )}
    </div>
  );
}

/** غلاف نافذة حوار مشترك (تأكيد قبول/رفض اقتراح). */
function ModalShell({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  return (
    <div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      style={{ overscrollBehavior: 'contain' }}
    >
      <div className="bg-white rounded-xl shadow-xl w-full max-w-md max-h-[85vh] overflow-y-auto overscroll-contain p-5">
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-lg font-bold text-gray-800">{title}</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl leading-none px-2 min-h-11" aria-label="إغلاق">×</button>
        </div>
        {children}
      </div>
    </div>
  );
}
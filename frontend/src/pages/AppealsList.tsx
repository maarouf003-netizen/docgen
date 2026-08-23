import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { useIsMobile } from '../hooks/useMediaQuery';
import { formatDate } from '../utils/dates';
import {
  APPEAL_STATUS_DECIDED,
  APPEAL_STATUS_PENDING,
  APPEAL_STATUS_STRUCK_OFF,
  appealOutcomeCls,
  appealOutcomeLabel,
  appealStatusBadge,
} from '../utils/appealStatus';
import type { AppealDto } from '../types';
import AppealRegistrationModal from '../components/appeal/AppealRegistrationModal';
import DecideAppealModal from '../components/appeal/DecideAppealModal';
import StrikeAppealModal from '../components/appeal/StrikeAppealModal';
import AppealRotationModal from '../components/appeal/AppealRotationModal';
import AssignAppealModal from '../components/appeal/AssignAppealModal';
import TransferAllAppealsModal from '../components/appeal/TransferAllAppealsModal';

const STATUS_FILTERS: Array<{ value: string; label: string }> = [
  { value: '', label: 'كل الحالات' },
  { value: APPEAL_STATUS_PENDING, label: 'منظور' },
  { value: APPEAL_STATUS_DECIDED, label: 'محسوم' },
  { value: APPEAL_STATUS_STRUCK_OFF, label: 'مشطوب' },
];

function truncate(text: string | undefined | null, max = 70): string {
  const t = (text ?? '').trim();
  if (!t) return '—';
  return t.length > max ? `${t.slice(0, max)}…` : t;
}

function appellantsText(a: AppealDto): string {
  return (a.appellants ?? []).map((p) => p.name).join('، ') || '—';
}

function firstAppellee(a: AppealDto): string {
  return a.appellees?.[0]?.name ?? '—';
}

/** صفحة «الاستئنافات»: جدول مكتبي بالأعمدة المعتمدة وبطاقات جوال، بنطاق رؤية الدور. */
export default function AppealsList() {
  const { user } = useAuth();
  const isMobile = useIsMobile();
  const role = user?.role;
  const hasFullAccess = role === 'manager' || role === 'admin';
  const isHead = role === 'head';
  const canSeeAssigned = hasFullAccess || isHead;

  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);

  const listQuery = useCancellableRequest<{ items: AppealDto[]; totalCount: number; totalPages: number }>(
    (signal) =>
      api
        .get('/appeals', { signal, params: { q: query, status, page, perPage: 20 } })
        .then((r) => r.data),
    [query, status, page],
  );

  const items = listQuery.data?.items ?? [];
  const total = listQuery.data?.totalCount ?? 0;
  const totalPages = listQuery.data?.totalPages ?? 1;

  // نافذة الإجراءات المفتوحة على استئناف محدد (نموذج واحد في كل مرة).
  const [registrationTarget, setRegistrationTarget] = useState<AppealDto | null>(null);
  const [decideTarget, setDecideTarget] = useState<AppealDto | null>(null);
  const [strikeTarget, setStrikeTarget] = useState<AppealDto | null>(null);
  const [rotationTarget, setRotationTarget] = useState<AppealDto | null>(null);
  const [assignTarget, setAssignTarget] = useState<{ appeal: AppealDto; mode: 'assign' | 'transfer' } | null>(null);
  const [transferAllOpen, setTransferAllOpen] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const refresh = () => listQuery.refetch();

  const isFollower = (a: AppealDto) => role === 'lawyer' && a.assignedLawyerId === user?.id;
  const isCreator = (a: AppealDto) => role === 'lawyer' && a.createdById === user?.id;
  const canRotateAppeal = (a: AppealDto) => isFollower(a) || isCreator(a);
  const canAssign = (a: AppealDto) => isHead && a.status === APPEAL_STATUS_PENDING;

  const openAssign = (a: AppealDto) =>
    setAssignTarget({ appeal: a, mode: a.assignedLawyerId ? 'transfer' : 'assign' });

  /** خلايا الأفعال المشتركة بين الجدول والبطاقات. */
  function Actions({ a }: { a: AppealDto }) {
    return (
      <div className="flex flex-wrap gap-1.5">
        <Link
          to={`/appeals/${a.id}`}
          className="border border-gray-300 hover:bg-gray-50 rounded-lg px-3 py-2 min-h-11 inline-flex items-center text-xs text-gray-700"
        >
          التفاصيل
        </Link>
        {isFollower(a) && (
          <>
            {a.direction === 'appellants' && (
              <button
                type="button"
                onClick={() => setRegistrationTarget(a)}
                className="bg-sky-800 hover:bg-sky-700 text-white rounded-lg px-3 py-2 min-h-11 inline-flex items-center text-xs"
              >
                تعديل القيد
              </button>
            )}
            {a.status === APPEAL_STATUS_PENDING && (
              <>
                <button
                  type="button"
                  onClick={() => setDecideTarget(a)}
                  className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-3 py-2 min-h-11 inline-flex items-center text-xs"
                >
                  حسم
                </button>
                <button
                  type="button"
                  onClick={() => setStrikeTarget(a)}
                  className="text-red-700 hover:bg-red-50 border border-red-200 rounded-lg px-3 py-2 min-h-11 inline-flex items-center text-xs"
                >
                  مشطوب
                </button>
              </>
            )}
          </>
        )}
        {canRotateAppeal(a) && (
          <button
            type="button"
            onClick={() => setRotationTarget(a)}
            className="border border-gray-300 hover:bg-gray-50 rounded-lg px-3 py-2 min-h-11 inline-flex items-center text-xs text-gray-700"
            aria-label={`تدوير رقم الأساس الاستئنافي للاستئناف رقم ${a.id}`}
          >
            تدوير
          </button>
        )}
        {/* الإسناد/النقل لرئيس القسم حصرًا — الخلفية ترفض غيره. */}
        {isHead && (
          <button
            type="button"
            onClick={() => openAssign(a)}
            disabled={!canAssign(a)}
            title={a.status !== APPEAL_STATUS_PENDING ? 'متاح للاستئنافات المنظورة فقط' : undefined}
            className="bg-sky-800 hover:bg-sky-700 disabled:opacity-40 text-white rounded-lg px-3 py-2 min-h-11 inline-flex items-center text-xs"
          >
            {a.assignedLawyerId ? 'نقل المحامي' : 'إسناد لمحامٍ'}
          </button>
        )}
      </div>
    );
  }

  function BaseNumberCell({ a }: { a: AppealDto }) {
    const badge = appealStatusBadge(a.status);
    const number = a.currentBaseNumber ?? a.appealBaseNumber;
    return (
      <div className="flex flex-col gap-1 items-start">
        {number ? (
          canRotateAppeal(a) ? (
            <button
              type="button"
              onClick={() => setRotationTarget(a)}
              aria-label={`رقم الأساس الاستئنافي ${number} — فتح نافذة التدوير`}
              className={`tabular-nums font-medium underline-offset-2 hover:underline min-h-11 inline-flex items-center ${
                a.needsRotation ? 'text-red-600 font-bold' : 'text-gray-800'
              }`}
            >
              {number}
            </button>
          ) : (
            <span className={`tabular-nums ${a.needsRotation ? 'text-red-600 font-bold' : 'text-gray-800'}`}>
              {number}
            </span>
          )
        ) : (
          <span className="text-gray-400">—</span>
        )}
        <span className={`rounded-full px-2 py-0.5 text-[11px] ${badge.cls}`}>{badge.text}</span>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto">
      <div className="flex items-center justify-between gap-3 flex-wrap mb-4">
        <h2 className="text-xl md:text-2xl font-bold text-red-800">الاستئنافات</h2>
        {isHead && (
          <button
            type="button"
            onClick={() => setTransferAllOpen(true)}
            className="border border-gray-300 hover:bg-gray-50 rounded-lg px-4 py-2 text-sm text-gray-700 min-h-11"
          >
            نقل استئنافات محامٍ
          </button>
        )}
      </div>

      {/* البحث وفلتر الحالة */}
      <div className="flex flex-col sm:flex-row gap-2 sm:items-center mb-4">
        <label htmlFor="appeals-search" className="sr-only">بحث في الاستئنافات</label>
        <input
          id="appeals-search"
          type="search"
          value={query}
          onChange={(e) => { setQuery(e.target.value); setPage(1); }}
          placeholder="بحث باسم المستأنف أو المستأنف عليهم أو رقم الأساس…"
          autoComplete="off"
          className="w-full border border-gray-300 rounded-lg px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 min-h-11 bg-white"
        />
        <label htmlFor="appeals-status" className="sr-only">فلتر الحالة</label>
        <select
          id="appeals-status"
          value={status}
          onChange={(e) => { setStatus(e.target.value); setPage(1); }}
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white text-gray-800 min-h-11 focus:outline-none focus:ring-2 focus:ring-emerald-500"
        >
          {STATUS_FILTERS.map((s) => (
            <option key={s.value} value={s.value}>{s.label}</option>
          ))}
        </select>
      </div>

      {listQuery.error && (
        <div role="alert" className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-sm text-red-700 mb-4 flex items-center justify-between gap-3 flex-wrap">
          <span>تعذر تحميل الاستئنافات — تفقّد الاتصال وأعد المحاولة.</span>
          <button
            type="button"
            onClick={refresh}
            className="min-h-11 px-4 rounded-lg border border-red-300 hover:bg-red-100 text-red-900 font-medium"
          >
            إعادة المحاولة
          </button>
        </div>
      )}{notice && (
        <div role="status" className="bg-emerald-50 border border-emerald-200 rounded-xl px-4 py-3 text-sm text-emerald-800 mb-4">
          {notice}
        </div>
      )}

      {!listQuery.isLoading && !listQuery.error && items.length === 0 && (
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 px-6 py-10 text-center text-gray-500">
          لا توجد استئنافات مطابقة.
        </div>
      )}

      {/* الجدول المكتبي */}
      {!isMobile && !listQuery.error && items.length > 0 && (
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 overflow-x-auto">
          <table className="w-full text-right text-sm">
            <caption className="sr-only">قائمة الاستئنافات</caption>
            <thead>
              <tr className="border-b border-gray-200 text-gray-600">
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">رقم الأساس الاستئنافي</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">نوع الاستئناف</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">المحكمة الناظرة</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">المستأنف</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">المستأنف عليهم</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">رقم الملف التنفيذي</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">ملخص قرار رئيس التنفيذ</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">رقم قرار الحسم</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">تاريخ قرار الحسم</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">ملخص منطوق القرار</th>
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">نتيجة الاستئناف</th>
                {canSeeAssigned && <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">المحامي المختص</th>}
                <th scope="col" className="px-3 py-3 font-medium whitespace-nowrap">إجراءات</th>
              </tr>
            </thead>
            <tbody>
              {items.map((a) => (
                <tr key={a.id} className="border-b border-gray-50 align-top hover:bg-gray-50/60">
                  <td className="px-3 py-3"><BaseNumberCell a={a} /></td>
                  <td className="px-3 py-3 text-gray-800">{a.appealTypeLabel || '—'}</td>
                  <td className="px-3 py-3 text-gray-800">{a.appellateCourt || '—'}</td>
                  <td className="px-3 py-3 text-gray-800 max-w-[180px] break-words">{appellantsText(a)}</td>
                  <td className="px-3 py-3 text-gray-800 max-w-[160px] break-words">{firstAppellee(a)}</td>
                  <td className="px-3 py-3 text-gray-800 whitespace-nowrap">
                    {[a.fileNumber, a.fileType, a.fileYear].filter(Boolean).join(' / ') || '—'}
                    {a.court && <span className="block text-xs text-gray-500 mt-0.5">{a.court}</span>}
                  </td>
                  <td className="px-3 py-3 text-gray-600 max-w-[200px]">{truncate(a.appealedDecisionSummary || a.appealedDecisionText)}</td>
                  <td className="px-3 py-3 text-gray-800 tabular-nums">{a.decisionNumber || '—'}</td>
                  <td className="px-3 py-3 text-gray-800 whitespace-nowrap tabular-nums">{formatDate(a.decisionDate, '—')}</td>
                  <td className="px-3 py-3 text-gray-600 max-w-[200px]" dir="auto">{truncate(a.decisionRuling)}</td>
                  <td className="px-3 py-3">
                    <span className={appealOutcomeCls(a.outcome)}>{appealOutcomeLabel(a.outcome)}</span>
                  </td>
                  {canSeeAssigned && (
                    <td className="px-3 py-3 text-gray-800">{a.assignedLawyerName || <span className="text-gray-400">—</span>}</td>
                  )}
                  <td className="px-3 py-3"><Actions a={a} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* بطاقات الجوال */}
      {isMobile && !listQuery.error && items.length > 0 && (
        <ul className="space-y-3">
          {items.map((a) => {
            const badge = appealStatusBadge(a.status);
            return (
              <li key={a.id} className="bg-white rounded-xl shadow border border-gray-100 p-4 space-y-2">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-1.5">
                    <span className={`rounded-full px-2 py-0.5 text-[11px] ${badge.cls}`}>{badge.text}</span>
                    <span className={`text-xs font-semibold ${appealOutcomeCls(a.outcome)}`}>
                      {appealOutcomeLabel(a.outcome) !== '—' ? appealOutcomeLabel(a.outcome) : ''}
                    </span>
                  </div>
                  <span className={`tabular-nums text-sm ${a.needsRotation ? 'text-red-600 font-bold' : 'text-gray-800'}`}>
                    {a.currentBaseNumber ?? a.appealBaseNumber ?? '—'}
                  </span>
                </div>
                <p className="text-gray-900 font-medium break-words">{appellantsText(a)}</p>
                <p className="text-xs text-gray-600">ضد: {firstAppellee(a)}</p>
                <p className="text-xs text-gray-600">
                  {[a.fileNumber, a.fileType, a.fileYear].filter(Boolean).join(' / ') || '—'}
                  {a.court ? ` · ${a.court}` : ''}
                </p>
                {a.appellateCourt && <p className="text-xs text-gray-600">المحكمة الناظرة: {a.appellateCourt}</p>}
                {canSeeAssigned && (
                  <p className="text-xs text-gray-600">المحامي المختص: {a.assignedLawyerName || '—'}</p>
                )}
                {(a.decisionNumber || a.decisionRuling) && (
                  <p className="text-xs text-gray-600 break-words">
                    الحسم: {a.decisionNumber || '—'}
                    {a.decisionDate ? ` · ${formatDate(a.decisionDate)}` : ''}
                    {a.decisionRuling ? ` — ${truncate(a.decisionRuling, 60)}` : ''}
                  </p>
                )}
                <div className="pt-1 border-t border-gray-100"><Actions a={a} /></div>
              </li>
            );
          })}
        </ul>
      )}

      {/* الترقيم */}
      {totalPages > 1 && (
        <nav aria-label="ترقيم الاستئنافات" className="flex items-center justify-center gap-3 mt-5">
          <button
            type="button"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 disabled:opacity-40"
          >
            السابق
          </button>
          <span className="text-sm text-gray-600 tabular-nums">
            صفحة {page} من {totalPages} ({total} استئناف)
          </span>
          <button
            type="button"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
            className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 min-h-11 disabled:opacity-40"
          >
            التالي
          </button>
        </nav>
      )}

      {/* نوافذ الإجراءات */}
      {registrationTarget && (
        <AppealRegistrationModal
          appeal={registrationTarget}
          onClose={() => setRegistrationTarget(null)}
          onSaved={() => { setRegistrationTarget(null); setNotice('تم تحديث قيد الاستئناف.'); refresh(); }}
        />
      )}
      {decideTarget && (
        <DecideAppealModal
          appeal={decideTarget}
          onClose={() => setDecideTarget(null)}
          onSaved={() => { setDecideTarget(null); setNotice('تم حسم الاستئناف وإشعار محامي الملف الأساس.'); refresh(); }}
        />
      )}
      {strikeTarget && (
        <StrikeAppealModal
          appeal={strikeTarget}
          onClose={() => setStrikeTarget(null)}
          onSaved={() => { setStrikeTarget(null); setNotice('تم شطب الاستئناف.'); refresh(); }}
        />
      )}
      {rotationTarget && (
        <AppealRotationModal
          appeal={rotationTarget}
          onClose={() => setRotationTarget(null)}
          onSaved={() => { setRotationTarget(null); setNotice('تم تحديث رقم الأساس الاستئنافي.'); refresh(); }}
        />
      )}
      {assignTarget && (
        <AssignAppealModal
          appeal={assignTarget.appeal}
          mode={assignTarget.mode}
          onClose={() => setAssignTarget(null)}
          onDone={(name) => {
            setAssignTarget(null);
            setNotice(`تمت العملية وسيصل تنبيه إلى ${name}.`);
            refresh();
          }}
        />
      )}
      {transferAllOpen && (
        <TransferAllAppealsModal
          onClose={() => setTransferAllOpen(false)}
          onTransferred={(count) => {
            setTransferAllOpen(false);
            setNotice(`تم نقل ${count} استئنافًا.`);
            refresh();
          }}
        />
      )}
    </div>
  );
}

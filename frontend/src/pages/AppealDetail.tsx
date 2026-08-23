import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { sanitizeRichText } from '../utils/richText';
import { appealStatusBadge } from '../utils/appealStatus';
import AppealDetailsBody from '../components/appeal/AppealDetailsBody';
import AppealActionsModal from '../components/appeal/AppealActionsModal';
import AppealRegistrationModal from '../components/appeal/AppealRegistrationModal';
import DecideAppealModal from '../components/appeal/DecideAppealModal';
import StrikeAppealModal from '../components/appeal/StrikeAppealModal';
import AssignAppealModal from '../components/appeal/AssignAppealModal';
import type { AppealDto, DocumentResponse } from '../types';

/**
 * صفحة تفاصيل الاستئناف للمتابع: بطاقة تفاصيل الاستئناف كاملة، وبطاقة إجراءات
 * وملاحظات الاستئناف (الإدخال الجديد)، وبطاقة إجراءات وملاحظات الملف الأساس
 * للقراءة فقط — مع أزرار القيد والحسم والشطب للمحامي المتابع.
 */
export default function AppealDetail() {
  const { id } = useParams();
  const { user } = useAuth();

  const appealQuery = useCancellableRequest<AppealDto | null>(
    (signal) =>
      api
        .get<AppealDto>(`/appeals/${id}`, { signal })
        .then((r) => r.data ?? null),
    [id],
    { enabled: Boolean(id) },
  );
  const docQuery = useCancellableRequest<DocumentResponse | null>(
    (signal) =>
      api
        .get<DocumentResponse>(`/documents/${appealQuery.data?.documentId}`, { signal })
        .then((r) => r.data ?? null),
    [appealQuery.data?.documentId],
    { enabled: Boolean(appealQuery.data?.documentId) },
  );

  const [actionsOpen, setActionsOpen] = useState(false);
  const [registrationOpen, setRegistrationOpen] = useState(false);
  const [decideOpen, setDecideOpen] = useState(false);
  const [strikeOpen, setStrikeOpen] = useState(false);
  const [assignOpen, setAssignOpen] = useState<'assign' | 'transfer' | null>(null);

  const appeal = appealQuery.data ?? null;
  const doc = docQuery.data ?? null;
  const isFollower = user?.role === 'lawyer' && appeal?.assignedLawyerId === user.id;
  const isHead = user?.role === 'head';

  if (appealQuery.error) {
    return <div role="alert" className="max-w-3xl mx-auto text-red-600">{appealQuery.error}</div>;
  }
  if (!appeal) return <div className="text-gray-500">جارِ التحميل...</div>;

  const badge = appealStatusBadge(appeal.status);
  const baseActions = doc?.executionActions ?? [];

  return (
    <div className="max-w-5xl mx-auto">
      <div className="sticky top-0 z-30 bg-white/95 backdrop-blur border-b border-gray-200 rounded-b-xl shadow-sm px-4 py-3 mb-5">
        <div className="flex items-center justify-between gap-3 flex-wrap">
          <h2 className="text-xl md:text-2xl font-bold text-gray-800 flex items-center gap-2 flex-wrap min-w-0">
            <span className={`rounded-full px-3 py-1 text-sm ${badge.cls}`}>{badge.text}</span>
            <span className="min-w-0 truncate">استئناف قرار رئيس التنفيذ — {appeal.documentLabel || `ملف #${appeal.documentId}`}</span>
          </h2>
          <div className="flex gap-2 flex-wrap">
            {isFollower && (
              <>
                {appeal.direction === 'appellants' && (
                  <button
                    type="button"
                    onClick={() => setRegistrationOpen(true)}
                    disabled={appeal.status !== 'pending'}
                    title={appeal.status !== 'pending' ? 'متاح للاستئنافات المنظورة فقط' : undefined}
                    className="bg-sky-800 hover:bg-sky-700 disabled:opacity-40 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                  >
                    تعديل القيد
                  </button>
                )}
                {appeal.status === 'pending' && (
                  <>
                    <button
                      type="button"
                      onClick={() => setDecideOpen(true)}
                      className="bg-emerald-800 hover:bg-emerald-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
                    >
                      حسم
                    </button>
                    <button
                      type="button"
                      onClick={() => setStrikeOpen(true)}
                      className="text-red-700 hover:bg-red-50 border border-red-200 rounded-lg px-4 py-2 text-sm min-h-11"
                    >
                      مشطوب
                    </button>
                  </>
                )}
              </>
            )}
            {isHead && appeal.status === 'pending' && (
              <button
                type="button"
                onClick={() => setAssignOpen(appeal.assignedLawyerId ? 'transfer' : 'assign')}
                className="bg-sky-800 hover:bg-sky-700 text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                {appeal.assignedLawyerId ? 'نقل المحامي' : 'إسناد لمحامٍ'}
              </button>
            )}
            <Link to="/appeals" className="border border-gray-300 rounded-lg px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 inline-flex items-center min-h-11">
              عودة للاستئنافات
            </Link>
          </div>
        </div>

        <dl className="mt-3 flex flex-wrap gap-2 text-sm">
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
            <dt className="text-xs text-gray-500 font-medium">رقم الملف</dt>
            <dd className="text-gray-800 font-semibold tabular-nums">{appeal.fileNumber || '—'}</dd>
          </div>
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
            <dt className="text-xs text-gray-500 font-medium">الدائرة</dt>
            <dd className="text-gray-800 font-semibold">{appeal.court || '—'}</dd>
          </div>
          <div className="inline-flex items-baseline gap-1.5 rounded-lg bg-gray-50 border border-gray-200 px-3 py-1.5">
            <dt className="text-xs text-gray-500 font-medium">المتابع</dt>
            <dd className="text-gray-800 font-semibold">{appeal.assignedLawyerName || 'بانتظار الإسناد'}</dd>
          </div>
          <Link
            to={`/documents/${appeal.documentId}`}
            className="inline-flex items-center rounded-lg bg-emerald-50 border border-emerald-200 px-3 py-1.5 text-xs text-emerald-800 hover:bg-emerald-100"
          >
            فتح الملف الأساسي
          </Link>
        </dl>
      </div>

      <div className="grid lg:grid-cols-2 gap-5">
        {/* بطاقة تفاصيل الاستئناف الكاملة */}
        <section aria-label="تفاصيل الاستئناف" className="lg:col-span-2 bg-white rounded-xl border shadow-sm px-5 py-4">
          <h3 className="text-lg font-bold text-emerald-800 mb-3">تفاصيل الاستئناف</h3>
          <AppealDetailsBody appeal={appeal} />
        </section>

        {/* بطاقة إجراءات وملاحظات الاستئناف */}
        <section aria-label="إجراءات وملاحظات الاستئناف" className="bg-white rounded-xl border shadow-sm px-5 py-4">
          <div className="flex items-center justify-between gap-3 mb-3 flex-wrap">
            <h3 className="text-lg font-bold text-emerald-800">إجراءات وملاحظات الاستئناف</h3>
            {isFollower && (
              <button
                type="button"
                onClick={() => setActionsOpen(true)}
                className="bg-[#800000] hover:bg-[#9e0e0e] text-white rounded-lg px-4 py-2 text-sm min-h-11"
              >
                إدخال ملاحظات وإجراءات جديدة
              </button>
            )}
          </div>
          <p className="text-sm text-gray-500">
            قائمة مستقلة عن الملف الأساس{isFollower ? ' — تفتح في نافذة كاملة للإضافة والتعديل.' : ' — للقراءة.'}
          </p>
        </section>

        {/* بطاقة إجراءات وملاحظات الملف الأساس — قراءة فقط */}
        <section aria-label="إجراءات وملاحظات الملف الأساسي" className="bg-white rounded-xl border shadow-sm px-5 py-4">
          <h3 className="text-lg font-bold text-emerald-800 mb-3">إجراءات وملاحظات الملف الأساسي</h3>
          {!doc ? (
            <p className="text-gray-500 text-sm">جارِ التحميل...</p>
          ) : baseActions.length === 0 ? (
            <p className="text-gray-400 text-sm">لا توجد إجراءات على الملف الأساس.</p>
          ) : (
            <ul className="space-y-3 max-h-80 overflow-y-auto pl-1" role="list">
              {baseActions.map((a) => (
                <li key={a.id} className="border-b border-gray-100 last:border-0 pb-2 last:pb-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span
                      className={`text-[11px] px-2 py-0.5 rounded-full ${
                        a.type === 'note' ? 'bg-sky-100 text-sky-800' : 'bg-emerald-100 text-emerald-800'
                      }`}
                    >
                      {a.type === 'note' ? 'ملاحظة' : 'إجراء'}
                    </span>
                    <span className="text-emerald-600 text-sm tabular-nums">{a.actionDate || '—'}</span>
                    {a.createdByName && <span className="text-gray-400 text-xs">· {a.createdByName}</span>}
                  </div>
                  <div
                    dir="auto"
                    className="text-sm text-gray-800 mt-1 [&_ul]:list-disc [&_ul]:pr-5 [&_ol]:list-decimal [&_ol]:pr-5 break-words line-clamp-4"
                    dangerouslySetInnerHTML={{ __html: sanitizeRichText(a.text) }}
                  />
                </li>
              ))}
            </ul>
          )}
          <p className="text-xs text-gray-400 mt-2">للقراءة فقط — الإدخال من صفحة الملف الأساس.</p>
        </section>
      </div>

      {actionsOpen && appeal && (
        <AppealActionsModal
          appealId={appeal.id}
          onClose={() => setActionsOpen(false)}
          onChanged={appealQuery.refetch}
        />
      )}
      {registrationOpen && appeal && (
        <AppealRegistrationModal
          appeal={appeal}
          onClose={() => setRegistrationOpen(false)}
          onSaved={() => { setRegistrationOpen(false); appealQuery.refetch(); }}
        />
      )}
      {decideOpen && appeal && (
        <DecideAppealModal
          appeal={appeal}
          onClose={() => setDecideOpen(false)}
          onSaved={() => { setDecideOpen(false); appealQuery.refetch(); }}
        />
      )}
      {strikeOpen && appeal && (
        <StrikeAppealModal
          appeal={appeal}
          onClose={() => setStrikeOpen(false)}
          onSaved={() => { setStrikeOpen(false); appealQuery.refetch(); }}
        />
      )}
      {assignOpen && appeal && (
        <AssignAppealModal
          appeal={appeal}
          mode={assignOpen}
          onClose={() => setAssignOpen(null)}
          onDone={() => { setAssignOpen(null); appealQuery.refetch(); }}
        />
      )}
    </div>
  );
}

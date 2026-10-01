import { useState } from 'react';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { ROLE_LABELS } from '../auth/roleLabels';
import { ChangePasswordForm } from '../components/ChangePasswordForm';
import { SuggestionDialog } from '../components/SuggestionDialog';
import { formatNumber } from '../components/dashboard/dashboardFormat';
import type { AppSuggestionDto, HeadAlertDto, ManagerStatsDto, PersonalReminderDto } from '../types';

/**
 * الحساب الشخصي (`/account` — محامٍ ورئيس قسم):
 * بطاقة الملف + تغيير كلمة المرور + تسجيل الخروج + ملخص إحصائي +
 * اقتراحات التطوير وسجلها.
 *
 * الملخص حسب الدور (الاستعلامات المحامية `403` لغيره — تُغلق صراحةً):
 * - المحامي: ملفاتي هذه السنة + تذكيراتي النشطة + تنبيهات غير مقروءة.
 * - رئيس القسم: ملفات الفرع هذه السنة (`/stats/manager` — فرعه إجباري خلفيًا) +
 *   مجموع مستلمي تنبيهات الفرع غير القارئين (مجموع `unreadCount` عبر التنبيهات —
 *   أزواج (تنبيه × مستلم) لا أشخاصًا مميزين، وأبدًا `isRead` فهو `null` في عرض الرئيس) + اقتراحاتي.
 */
export default function AccountPage() {
  const { user, logout } = useAuth();
  const userReady = Boolean(user);
  const isHead = user?.role === 'head';
  const [dialogOpen, setDialogOpen] = useState(false);
  const [sentFlash, setSentFlash] = useState(false);

  const statsQuery = useCancellableRequest<ManagerStatsDto>(
    (signal) => api.get(isHead ? '/stats/manager' : '/stats/me', { signal }).then((r) => r.data),
    [isHead],
    { enabled: userReady },
  );
  const personalQuery = useCancellableRequest<PersonalReminderDto[]>(
    (signal) => api.get('/personal-reminders', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
    { enabled: userReady && !isHead },
  );
  const unreadQuery = useCancellableRequest<{ count: number }>(
    (signal) => api.get('/alerts/unread-count', { signal }).then((r) => r.data),
    [],
    { enabled: userReady && !isHead },
  );
  const headAlertsQuery = useCancellableRequest<HeadAlertDto[]>(
    (signal) => api.get('/alerts', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
    { enabled: userReady && isHead },
  );
  const suggestionsQuery = useCancellableRequest<AppSuggestionDto[]>(
    (signal) => api.get('/app-suggestions', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
    { enabled: userReady },
  );

  const suggestions = suggestionsQuery.data ?? [];
  const initials = (user?.fullName ?? '').trim().slice(0, 2) || '؟';
  const branchUnread = (headAlertsQuery.data ?? []).reduce(
    (sum, a) => sum + Math.max(0, Number(a.unreadCount) || 0),
    0,
  );
  const summaryError = isHead
    ? (statsQuery.error ?? headAlertsQuery.error)
    : (statsQuery.error ?? personalQuery.error ?? unreadQuery.error);

  return (
    <div className="max-w-3xl mx-auto">
      <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6 text-balance">الحساب الشخصي</h2>

      {sentFlash ? (
        <p role="status" className="text-sm text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-xl px-3 py-2 mb-4">
          تم إرسال اقتراحك إلى المشرف — شكرًا لك.
        </p>
      ) : null}

      <div className="grid grid-cols-1 gap-4">
        <section aria-label="الملف الشخصي" className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5">
          <div className="flex items-center gap-3 min-w-0">
            <span
              className="shrink-0 w-14 h-14 rounded-full bg-emerald-700 text-white inline-flex items-center justify-center text-xl font-bold"
              aria-hidden="true"
            >
              {initials}
            </span>
            <div className="min-w-0">
              <p className="font-bold text-gray-900 truncate">{user?.fullName}</p>
              <p className="text-sm text-gray-500 truncate" dir="ltr">
                {user?.username}
              </p>
              <p className="text-xs text-gray-500 mt-0.5">
                {user?.role ? ROLE_LABELS[user.role] : ''} — {user?.branchName || 'كل الفروع'}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={logout}
            className="mt-4 w-full sm:w-auto min-h-11 px-4 rounded-lg border border-gray-200 text-sm hover:bg-gray-50 focus-visible:ring-2 focus-visible:ring-emerald-600"
          >
            تسجيل الخروج
          </button>
        </section>

        <section aria-label="تغيير كلمة المرور" className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5">
          <h3 className="font-bold text-gray-900 mb-3">تغيير كلمة المرور</h3>
          <ChangePasswordForm />
        </section>

        <section aria-label="ملخص إحصائي" className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5">
          <h3 className="font-bold text-gray-900 mb-3">ملخص سريع</h3>
          {summaryError ? (
            <p role="alert" className="text-red-700 text-sm">
              {summaryError}
            </p>
          ) : (
            <dl className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-center">
              <div className="rounded-xl bg-gray-50 px-2 py-3">
                <dt className="text-xs text-gray-500 mb-1">{isHead ? 'ملفات الفرع هذه السنة' : 'ملفاتي هذه السنة'}</dt>
                <dd className="text-xl font-bold text-gray-900 tabular-nums" dir="ltr">
                  {statsQuery.data ? formatNumber(statsQuery.data.totalFiles) : '…'}
                </dd>
              </div>
              {isHead ? (
                <div className="rounded-xl bg-gray-50 px-2 py-3">
                  <dt className="text-xs text-gray-500 mb-1">مجموع مستلمي تنبيهات الفرع غير القارئين</dt>
                  <dd className="text-xl font-bold text-gray-900 tabular-nums" dir="ltr">
                    {headAlertsQuery.data ? formatNumber(branchUnread) : '…'}
                  </dd>
                </div>
              ) : (
                <div className="rounded-xl bg-gray-50 px-2 py-3">
                  <dt className="text-xs text-gray-500 mb-1">تذكيراتي النشطة</dt>
                  <dd className="text-xl font-bold text-gray-900 tabular-nums" dir="ltr">
                    {personalQuery.data ? formatNumber(personalQuery.data.length) : '…'}
                  </dd>
                </div>
              )}
              {isHead ? (
                <div className="rounded-xl bg-gray-50 px-2 py-3">
                  <dt className="text-xs text-gray-500 mb-1">اقتراحاتي</dt>
                  <dd className="text-xl font-bold text-gray-900 tabular-nums" dir="ltr">
                    {suggestionsQuery.data ? formatNumber(suggestions.length) : '…'}
                  </dd>
                </div>
              ) : (
                <div className="rounded-xl bg-gray-50 px-2 py-3">
                  <dt className="text-xs text-gray-500 mb-1">تنبيهات غير مقروءة</dt>
                  <dd className="text-xl font-bold text-gray-900 tabular-nums" dir="ltr">
                    {unreadQuery.data ? formatNumber(Number(unreadQuery.data.count) || 0) : '…'}
                  </dd>
                </div>
              )}
            </dl>
          )}
        </section>

        <section aria-label="اقتراحات التطوير" className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5">
          <div className="flex items-center justify-between gap-3 mb-3">
            <h3 className="font-bold text-gray-900">اقتراحات التطوير</h3>
            <button
              type="button"
              onClick={() => setDialogOpen(true)}
              className="min-h-11 px-4 rounded-lg bg-emerald-700 hover:bg-emerald-600 text-white text-sm font-medium focus-visible:ring-2 focus-visible:ring-emerald-600"
            >
              إرسال اقتراح لتطوير التطبيق
            </button>
          </div>
          <h4 className="text-sm font-medium text-gray-700 mb-2">اقتراحاتي</h4>
          {suggestionsQuery.error ? (
            <p role="alert" className="text-red-700 text-sm">
              {suggestionsQuery.error}
            </p>
          ) : suggestions.length === 0 ? (
            <p className="text-gray-400 text-sm">لا اقتراحات بعد — رأيك يطوّر التطبيق.</p>
          ) : (
            <ul className="divide-y divide-gray-100">
              {suggestions.map((s) => (
                <li key={s.id} className="py-2.5">
                  <p className="text-sm text-gray-800 break-words">{s.message}</p>
                  <p className="flex flex-wrap items-center gap-x-2 gap-y-0.5 mt-1 text-xs text-gray-400">
                    <span className="tabular-nums" dir="ltr">
                      {s.createdAt.slice(0, 10)}
                    </span>
                    <span
                      className={`rounded-full px-2 py-0.5 font-medium ${
                        s.isRead ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-800'
                      }`}
                    >
                      {s.isRead ? 'مقروء' : 'بانتظار القراءة'}
                    </span>
                  </p>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      {dialogOpen ? (
        <SuggestionDialog
          onClose={() => setDialogOpen(false)}
          onSent={() => {
            suggestionsQuery.refetch();
            setSentFlash(true);
          }}
        />
      ) : null}
    </div>
  );
}

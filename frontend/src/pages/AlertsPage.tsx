import { useMemo, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useAlertsFeed } from '../hooks/useAlertsFeed';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import type { HeadAlertSummaryDto, HeadAlertTargetType, LawyerListItem } from '../types';
import { AlertList } from '../components/dashboard/AlertList';
import { AlertDetailsSheet } from '../components/dashboard/AlertDetailsSheet';
import { AlertsFeedFooter } from '../components/dashboard/AlertsFeedFooter';
import { CreateAlertForm, type AlertFormError } from '../components/dashboard/CreateAlertForm';
import { RecipientsModal } from '../components/dashboard/RecipientsModal';

/** مرشّح الأرشفة — القيم التي يقبلها الخادم حرفيًا (`ParseArchiveFilter`). */
type ArchiveFilter = 'active' | 'all' | 'only';

type AlertsTab = 'received' | 'sent';

const ARCHIVE_OPTIONS: ReadonlyArray<{ value: ArchiveFilter; label: string }> = [
  { value: 'active', label: 'نشط' },
  { value: 'all', label: 'الكل' },
  { value: 'only', label: 'المؤرشف' },
];

/**
 * صفحة التنبيهات `/alerts` — النطاق الكامل للتنبيهات خارج لوحة التحكم.
 *
 * **لماذا صفحة؟** لوحة التحكم كانت تحمل قائمة كاملة داخل بطاقة (`AlertsPanel`)
 * بنفس وظائف الصفحة (تبويبان، ترقيم، نص كامل، إصدار)؛ فتضاعف السطح وتضاعفت
 * نقاط الفشل. الصفحة هي السطح الوحيد الآن، واللوحة تُبقي مدخلًا واحدًا:
 * بطاقة «التنبيهات» بشارة غير المقروء.
 *
 * الأدوار (مطابقة الخلفية):
 * - المحامي: «مستلمة» فقط (`/alerts`)، وتعليم المقروء متاح.
 * - رئيس القسم/الشعبة: «مستلمة» + «مرسلة» + إصدار تنبيه، وتعليم المقروء في
 *   «مستلمة» فقط (في «مرسلة» `isRead` حالة المستلم لا المُرسل).
 * - المدير/مشرف النظام: «مستلمة» إشرافية للقراءة فقط.
 * - رئيس/شعبة بلا فرع: حالة صريحة بلا طلبات محكومة بالفشل (`D11`).
 */
export default function AlertsPage() {
  const { user } = useAuth();
  const isLawyer = user?.role === 'lawyer';
  const isManager = user?.role === 'manager' || user?.role === 'admin';
  const isHeadOrSubHead = user?.role === 'head' || user?.role === 'subhead';
  const userReady = Boolean(user);

  // نطاق رئيس القسم/الشعبة يشترط **الفرع** فقط (`RequireBranch` خلفيًا) لا الشعبة.
  const headScopeEnabled = userReady && isHeadOrSubHead && (user?.branchId ?? null) != null;
  const sentEnabled = headScopeEnabled;
  const receivedEnabled = userReady && (isLawyer || isManager || headScopeEnabled);
  const canMarkRead = isLawyer || headScopeEnabled;

  const [tab, setTab] = useState<AlertsTab>('received');
  const [archive, setArchive] = useState<ArchiveFilter>('active');
  const activeTab: AlertsTab = tab === 'sent' && !sentEnabled ? 'received' : tab;

  // المرشّح في الرابط نفسه: تغيّره يُطلق جلبًا جديدًا ويُعيد الترقيم إلى الأولى
  // (الحالة مفتاحية بالنطاق داخل `useAlertsFeed`).
  const receivedUrl = `/alerts?archived=${archive}`;
  const sentUrl = `/alerts/sent?archived=${archive}`;
  const receivedFeed = useAlertsFeed(receivedUrl, { enabled: receivedEnabled });
  const sentFeed = useAlertsFeed(sentUrl, { enabled: sentEnabled });

  const activeFeed = activeTab === 'received' ? receivedFeed : sentFeed;

  // شارة غير المقروء بنطاق **شخصي موحّد** (`user`) لكل الأدوار — «غير المقروء لي».
  // حُسم 2026-11-02 بعد اعتراض المالك: `received` للرئيس/الشعبة كان يجمع غير المقروء
  // لدى كل مستلمي التنبيهات، فيبقى رقمًا واحدًا بعد قراءة صاحب الحساب لكل تنبيهاته.
  const unreadScope = 'user';
  const unreadQuery = useCancellableRequest<{ count: number }>(
    (signal) =>
      api.get('/alerts/unread-count', { signal, params: { scope: unreadScope } }).then((r) => r.data),
    [unreadScope],
    { enabled: userReady && (isLawyer || headScopeEnabled) },
  );
  const unreadCount = Math.max(0, Number(unreadQuery.data?.count) || 0);

  const [markingKey, setMarkingKey] = useState<string | null>(null);
  const [listError, setListError] = useState('');

  const [detailsId, setDetailsId] = useState<number | null>(null);
  const [recipientsId, setRecipientsId] = useState<number | null>(null);

  // نموذج الإصدار — للرئيس/الشعبة فقط، ونقل من لوحة التحكم بلا تغيير سلوك.
  const [showForm, setShowForm] = useState(false);
  const [targetType, setTargetType] = useState<HeadAlertTargetType>('branch');
  const [lawyerId, setLawyerId] = useState('');
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<AlertFormError>({ field: null, text: '' });
  const [flash, setFlash] = useState('');

  const branchLawyersQuery = useCancellableRequest<LawyerListItem[]>(
    (signal) =>
      api
        .get('/users/lawyers', { signal, params: { mode: 'branch' } })
        .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [headScopeEnabled],
    { enabled: headScopeEnabled },
  );
  const branchLawyers = useMemo(() => branchLawyersQuery.data ?? [], [branchLawyersQuery.data]);

  const markAlertRead = async (alert: HeadAlertSummaryDto) => {
    setMarkingKey(String(alert.id));
    setListError('');
    try {
      await api.patch(`/alerts/${alert.id}/read`);
      receivedFeed.updateItems((prev) =>
        prev.map((row) => {
          if (row.id !== alert.id || row.isRead) return row;
          // عدّاد الصف عند الرئيس/الشعبة = غير المقروء من مستلمي الصف (`D6`)؛
          // تعليمه مقروءًا يُنقصه بمقدار واحد. صفوف المحامي (`unreadCount = null`) بلا عدّاد.
          return {
            ...row,
            isRead: true,
            unreadCount: row.unreadCount == null ? row.unreadCount : Math.max(0, row.unreadCount - 1),
          };
        }),
      );
      unreadQuery.setData((prev) => (prev ? { count: Math.max(0, prev.count - 1) } : prev));
    } catch (err) {
      setListError(getApiErrorMessage(err));
    } finally {
      setMarkingKey(null);
    }
  };

  const submitAlert = async (event: FormEvent) => {
    event.preventDefault();
    if (!message.trim()) {
      setFormError({ field: 'message', text: 'نص التنبيه مطلوب' });
      document.getElementById('alert-message')?.focus();
      return;
    }
    let targetLawyerId: number | null = null;
    if (targetType === 'lawyer') {
      targetLawyerId = lawyerId ? Number(lawyerId) : null;
      if (!targetLawyerId) {
        setFormError({ field: 'lawyer', text: 'اختر المحامي المستلم' });
        document.getElementById('alert-lawyer')?.focus();
        return;
      }
    }

    setSubmitting(true);
    setFormError({ field: null, text: '' });
    try {
      // عقد `POST /alerts` = `201` بجسم الملخص (D3) — النوع مرآة السلك لا `HeadAlertDto`.
      await api.post<HeadAlertSummaryDto>('/alerts', {
        targetType,
        documentId: null,
        targetLawyerId,
        message: message.trim(),
      });
      setShowForm(false);
      setMessage('');
      setLawyerId('');
      setFlash('تم إصدار التنبيه — تجده في تبويب «مرسلة»');
      sentFeed.reload();
    } catch (err) {
      setFormError({ field: null, text: getApiErrorMessage(err) });
    } finally {
      setSubmitting(false);
    }
  };

  const header = (
    <div className="mb-4">
      <h1 className="text-xl font-bold text-gray-900">التنبيهات</h1>
      <p className="mt-1 text-sm text-gray-500">
        {isManager
          ? 'قائمة إشرافية للقراءة فقط عبر الفروع.'
          : isHeadOrSubHead
            ? 'ما استلمته في فرعك وما أصدرته، مع النص الكامل عند الطلب.'
            : 'تنبيهاتك غير المقروءة والمقروءة، مع النص الكامل عند الطلب.'}
      </p>
    </div>
  );

  // رئيس/شعبة بلا فرع: النطاق مرفوض خلفيًا (`D11`) ⇒ حالة صريحة بلا طلبات فاشلة.
  if (isHeadOrSubHead && !headScopeEnabled) {
    return (
      <div className="mx-auto max-w-3xl">
        {header}
        <div className="rounded-2xl border border-gray-100 bg-white p-6 text-center shadow-sm">
          <p className="text-sm text-gray-600">
            حسابك غير مرتبط بفرع، فلا يمكن عرض تنبيهات القسم. راجع مدير النظام لربط حسابك بفرع.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl">
      {header}

      {flash ? (
        <p
          role="status"
          className="mb-4 rounded-xl border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700"
        >
          {flash}
        </p>
      ) : null}

      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        {sentEnabled ? (
          <div className="flex gap-2" role="tablist" aria-label="تبويبات التنبيهات">
            {(['received', 'sent'] as const).map((value) => (
              <button
                key={value}
                type="button"
                role="tab"
                id={`alerts-tab-${value}`}
                aria-selected={activeTab === value}
                aria-controls={`alerts-panel-${value}`}
                onClick={() => setTab(value)}
                className={`min-h-11 rounded-lg px-4 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 ${
                  activeTab === value
                    ? 'bg-emerald-800 text-white'
                    : 'border border-gray-300 text-gray-700 hover:bg-gray-50'
                }`}
              >
                {value === 'received' ? 'مستلمة' : 'مرسلة'}
                {value === 'received' && unreadCount > 0 ? (
                  <span className="ms-1.5 inline-flex min-w-5 items-center justify-center rounded-full bg-red-600 px-1 text-[11px] font-bold text-white tabular-nums">
                    {unreadCount > 99 ? '+99' : unreadCount}
                    <span className="sr-only"> غير مقروء</span>
                  </span>
                ) : null}
              </button>
            ))}
          </div>
        ) : (
          <span className="text-sm text-gray-500">
            {/* المدير/المشرف: حالة القراءة غير منطبقة (`isRead = null` — ليس مستلمًا)،
                فلا يُعرض «لا تنبيهات غير مقروءة» كاذبًا ولا رقم عدّاد (§18:3045). */}
            {isManager ? (
              'قراءة إشرافية عبر الفروع'
            ) : unreadCount > 0 ? (
              <>
                <span className="tabular-nums">{unreadCount}</span> غير مقروء
              </>
            ) : (
              'لا تنبيهات غير مقروءة'
            )}
          </span>
        )}

        {headScopeEnabled ? (
          <button
            type="button"
            onClick={() => setShowForm((value) => !value)}
            className="min-h-11 rounded-lg bg-emerald-800 px-4 text-sm font-medium text-white hover:bg-emerald-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            {showForm ? 'إلغاء' : '+ إصدار تنبيه'}
          </button>
        ) : null}
      </div>

      {showForm && headScopeEnabled ? (
        <div className="mb-4 rounded-2xl border border-gray-100 bg-white shadow-sm">
          <CreateAlertForm
            targetType={targetType}
            onTargetTypeChange={setTargetType}
            lawyers={branchLawyers}
            lawyerId={lawyerId}
            onLawyerIdChange={setLawyerId}
            message={message}
            onMessageChange={setMessage}
            submitting={submitting}
            error={formError}
            onSubmit={submitAlert}
            onCancel={() => setShowForm(false)}
          />
        </div>
      ) : null}

      <div className="mb-3 flex flex-wrap items-center gap-2" role="group" aria-label="تصفية الأرشفة">
        <span className="text-xs text-gray-500">الأرشيف:</span>
        {ARCHIVE_OPTIONS.map((option) => (
          <button
            key={option.value}
            type="button"
            aria-pressed={archive === option.value}
            onClick={() => setArchive(option.value)}
            className={`min-h-11 rounded-lg px-3 text-xs font-medium focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 ${
              archive === option.value
                ? 'bg-gray-900 text-white'
                : 'border border-gray-300 text-gray-700 hover:bg-gray-50'
            }`}
          >
            {option.label}
          </button>
        ))}
      </div>

      <div
        role="tabpanel"
        id={`alerts-panel-${activeTab}`}
        aria-labelledby={`alerts-tab-${activeTab}`}
        className="overflow-hidden rounded-2xl border border-gray-100 bg-white shadow-sm"
      >
        {listError || activeFeed.error ? (
          <div className="border-b border-red-100 bg-red-50 px-4 py-2.5 sm:px-5" role="alert">
            <p className="text-sm text-red-700">{listError || activeFeed.error}</p>
          </div>
        ) : null}

        <AlertList
          alerts={activeFeed.items}
          isLoading={activeFeed.isLoading}
          error={activeFeed.error}
          sentView={activeTab === 'sent'}
          onMarkRead={canMarkRead && activeTab === 'received' ? markAlertRead : undefined}
          markingKey={activeTab === 'received' ? markingKey : null}
          onOpen={(alert) => setDetailsId(alert.id)}
          onOpenRecipients={
            // المستلمون: للمُصدِر في «مرسلة»، وللمدير/المشرف في قائمته الإشرافية
            // (لا تبويب «مرسلة» له، والخادم يمنحه `GET /{id}/recipients` — D3).
            activeTab === 'sent' || isManager ? (alert) => setRecipientsId(alert.id) : undefined
          }
          emptyText={
            archive === 'only'
              ? 'لا توجد تنبيهات مؤرشفة في هذا النطاق'
              : archive === 'all'
                ? 'لا توجد تنبيهات في هذا النطاق'
                : 'لا توجد تنبيهات نشطة في هذا النطاق'
          }
        />

        <AlertsFeedFooter feed={activeFeed} />
      </div>

      <AlertDetailsSheet alertId={detailsId} onClose={() => setDetailsId(null)} />
      <RecipientsModal alertId={recipientsId} onClose={() => setRecipientsId(null)} />
    </div>
  );
}

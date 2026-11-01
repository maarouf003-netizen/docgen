import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useAlertsFeed } from '../hooks/useAlertsFeed';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { useReminderCancellation } from '../hooks/useReminderCancellation';
import type {
  AppealReminderDto,
  ExecutionCircuitDto,
  HeadAlertSummaryDto,
  HeadAlertTargetType,
  LawyerListItem,
  PendingRegistrationDto,
  PersonalReminderDto,
  ReminderDto,
} from '../types';
import { AlertsPanel } from '../components/dashboard/AlertsPanel';
import { AlertsFeedFooter } from '../components/dashboard/AlertsFeedFooter';
import { CreateAlertForm, type AlertFormError } from '../components/dashboard/CreateAlertForm';
import { DayRemindersModal } from '../components/dashboard/DayRemindersModal';
import { GreetingHeader } from '../components/dashboard/GreetingHeader';
import { HeadIconRow } from '../components/dashboard/HeadIconRow';
import { LawyerCalendar } from '../components/dashboard/LawyerCalendar';
import { LawyerIconRow } from '../components/dashboard/LawyerIconRow';
import { ManagerIconRow } from '../components/dashboard/ManagerIconRow';
import { isDueTodayOrOverdue } from '../components/dashboard/dashboardFormat';
import { currentWeekRange, dayKeyOf, expandPersonalReminder, groupRemindersByDay, hasPendingOccurrenceOnOrBefore, parseDayKey } from '../components/dashboard/personalReminders';
import { CORRESPONDENCE_UNSEEN_EVENT } from '../components/correspondence/correspondenceDisplay';
import { REVIEWS_UNSEEN_EVENT } from '../components/review/reviewDisplay';
import { useBadgeCount } from '../hooks/useBadgeCount';
import { ReminderList } from '../components/dashboard/ReminderList';

export default function Dashboard() {
  const { user } = useAuth();
  const isLawyer = user?.role === 'lawyer';
  const isManager = user?.role === 'manager' || user?.role === 'admin';
  const isHeadOrSubHead = user?.role === 'head' || user?.role === 'subhead';
  const isSubHead = user?.role === 'subhead';

  const [alertsError, setAlertsError] = useState('');
  const [markingKey, setMarkingKey] = useState<string | null>(null);

  const [showAlertForm, setShowAlertForm] = useState(false);
  const [alertTargetType, setAlertTargetType] = useState<HeadAlertTargetType>('branch');
  const [alertLawyerId, setAlertLawyerId] = useState('');
  const [alertMessage, setAlertMessage] = useState('');
  const [alertSubmitting, setAlertSubmitting] = useState(false);
  const [alertFormError, setAlertFormError] = useState<AlertFormError>({ field: null, text: '' });

  const userReady = Boolean(user);
  // رئيس بلا فرع (ورئيس شعبة برمز بلا شعبة): عدّادات شاراته مرفوضة (`400`/`403`) أو صفرية خلفيًا —
  // لا تُطلق ولا تُستطلع أصلًا، وتبقى الشارات صفرًا.
  const headBadgesEnabled =
    userReady && isHeadOrSubHead && (user?.branchId ?? null) != null && (isSubHead ? (user?.sectionId ?? null) != null : true);

  // التذكيرات خاصة بالمحامي فقط؛ لا تُجلب لرئيس القسم ولا للمدير/المشرف.
  const remindersQuery = useCancellableRequest<ReminderDto[]>(
    (signal) => api.get('/reminders', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [isLawyer],
    { enabled: userReady && !isManager && isLawyer },
  );

  // تذكيرات إجراءات الاستئنافات التي يتابعها المحامي — تُدمج في بطاقة التذكيرات نفسها.
  const appealRemindersQuery = useCancellableRequest<AppealReminderDto[]>(
    (signal) => api.get('/appeals/reminders', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [isLawyer],
    { enabled: userReady && isLawyer },
  );

  // التذكيرات الشخصية الحرة — تُدمج في التقويم ونافذة اليوم.
  const personalQuery = useCancellableRequest<PersonalReminderDto[]>(
    (signal) => api.get('/personal-reminders', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [isLawyer],
    { enabled: userReady && isLawyer },
  );

  const { cancellingKey, actionError, cancelReminder, cancelAppealReminder } =
    useReminderCancellation(remindersQuery, appealRemindersQuery);

  // نطاق التنبيهات لرئيس القسم/الشعبة يشترط **الفرع** فقط (`RequireBranch` خلفيًا)،
  // لا الشعبة — فاشتراط الشعبة هنا كان يحجب قائمة مشروعة ويُبقي 403 لرئيس بلا فرع.
  const headAlertsScopeEnabled = userReady && isHeadOrSubHead && (user?.branchId ?? null) != null;

  // قوائم التنبيهات مرقّمة (`PagedResult<HeadAlertSummaryDto>`) — معاينة فقط بلا نص كامل،
  // و`pageSize=100` صريح مع «تحميل المزيد» فلا قصّ صامت فوق الصفحة الواحدة.
  const receivedFeed = useAlertsFeed('/alerts', {
    enabled: userReady && !isManager && (!isHeadOrSubHead || headAlertsScopeEnabled),
  });

  // صندوق الصادر («أرسلتها» — قراءة فقط): ما أصدره الرئيس في فرعه، منفصل عن
  // المستلَمات حتى لا يختلط المُرسَل بالمستلَم ولا يُطالب بتعليم مقروء.
  const [alertsTab, setAlertsTab] = useState<'received' | 'sent'>('received');
  const [sentFlash, setSentFlash] = useState('');
  const sentFeed = useAlertsFeed('/alerts/sent', { enabled: headAlertsScopeEnabled });

  // شارة غير المقروء بنطاق صريح (`D6`/`D7`): المحامي صفوفه الشخصية (`user`) بلا قيد
  // فرع، والرئيس/الشعبة ما استلمه في فرعه (`received`) — القاعدة نفسها في `AccountPage`
  // فلا تُنتج الشاشتان رقمين مختلفين لنفس الدور.
  const unreadScope = isHeadOrSubHead ? 'received' : 'user';
  const unreadQuery = useCancellableRequest<{ count: number }>(
    (signal) =>
      api
        .get('/alerts/unread-count', { signal, params: { scope: unreadScope } })
        .then((r) => r.data),
    [unreadScope],
    { enabled: userReady && !isManager && (isLawyer || headAlertsScopeEnabled) },
  );

  const branchLawyersQuery = useCancellableRequest<LawyerListItem[]>(
    // قائمة التنبيهات: كل محامي النطاق (الجدد بلا ملفات مشمولون — `mode=branch`).
    (signal) => api.get('/users/lawyers', { signal, params: { mode: 'branch' } }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [isHeadOrSubHead],
    { enabled: userReady && isHeadOrSubHead },
  );

  // عدّاد بطاقة سجل الجهات: جهات أدخلها المحامون وبانتظار مراجعة رئيس القسم —
  // endpoint عدّ خفيف (`{ count }`) بدل تنزيل القائمة كاملة كل دقيقة.
  const entityPending = useBadgeCount('/entity-registry/pending-review-count', {
    enabled: headBadgesEnabled,
    intervalMs: 60_000,
    shape: 'count',
  });

  // عدّادا بطاقتي المطالعات/المراسلات للمحامي: جلب واحد عند التركيب + تحديث
  // فور حدث المشاهدة — بلا استطلاع دوري مكرر (الاستطلاع الدوري في `Layout` وحده).
  const unseenRepliesQuery = useCancellableRequest<{ count: number }>(
    (signal) => api.get('/review-letters/unseen-replies-count', { signal }).then((r) => r.data),
    [isLawyer],
    { enabled: userReady && isLawyer },
  );
  const urgentQuery = useCancellableRequest<{ count: number }>(
    (signal) => api.get('/correspondence/urgent-unseen-count', { signal }).then((r) => r.data),
    [isLawyer],
    { enabled: userReady && isLawyer },
  );
  const { refetch: refetchReplies } = unseenRepliesQuery;
  const { refetch: refetchUrgent } = urgentQuery;
  useEffect(() => {
    if (!isLawyer) return;
    window.addEventListener(REVIEWS_UNSEEN_EVENT, refetchReplies);
    window.addEventListener(CORRESPONDENCE_UNSEEN_EVENT, refetchUrgent);
    return () => {
      window.removeEventListener(REVIEWS_UNSEEN_EVENT, refetchReplies);
      window.removeEventListener(CORRESPONDENCE_UNSEEN_EVENT, refetchUrgent);
    };
  }, [isLawyer, refetchReplies, refetchUrgent]);

  // عدّادات بطاقات رئيس القسم: جلب عند التركيب + استطلاع كل دقيقة (إيقاع الأجراس
  // السابقة) + تحديث حدثي للمراسلات — بلا استطلاع مكرر في `Layout` له.
  const reviewsPending = useBadgeCount('/review-letters/pending-count', {
    enabled: headBadgesEnabled,
    intervalMs: 60_000,
    shape: 'count',
  });
  const headUrgentCorrespondence = useBadgeCount('/correspondence/urgent-unseen-count', {
    enabled: headBadgesEnabled,
    intervalMs: 60_000,
    shape: 'count',
    eventName: CORRESPONDENCE_UNSEEN_EVENT,
  });
  const delegationsPending = useBadgeCount('/delegations/pending-count', {
    enabled: headBadgesEnabled,
    intervalMs: 60_000,
    shape: 'count',
  });

  // شارة معلقات الدوائر لرئيس القسم (B44): مجموع pendingCount من قائمة الإدارة —
  // تُجلب مرة عند التركيب (بلا استطلاع: التحديث عبر صفحة الإدارة نفسها).
  const circuitsQuery = useCancellableRequest<ExecutionCircuitDto[]>(
    (signal) => api.get('/execution-circuits/mine', { signal }).then((r) => (Array.isArray(r?.data) ? r.data : [])),
    [isHeadOrSubHead],
    { enabled: headBadgesEnabled },
  );
  const circuitsPending = useMemo(() => {
    if (!isHeadOrSubHead) return 0;
    return (circuitsQuery.data ?? []).reduce((n, c) => n + Math.max(0, Number(c.pendingCount) || 0), 0);
  }, [isHeadOrSubHead, circuitsQuery.data]);

  // مدخل معلقات المحامي (B19): عدد ملفاته المحالة بانتظار تحديث بياناتها — بطاقة «ملفات معلقة».
  // تُحجب البطاقة بعد نجاح الجلب والصفر المؤكد فقط؛ وتبقى أثناء التحميل وعند الخطأ (fail-open).
  const pendingRegistrationsQuery = useCancellableRequest<PendingRegistrationDto[]>(
    (signal) => api.get('/documents/my-pending-registrations', { signal }).then((r) => (Array.isArray(r?.data) ? r.data : [])),
    [isLawyer],
    { enabled: userReady && isLawyer },
  );
  const pendingRegistrations = isLawyer ? (pendingRegistrationsQuery.data ?? []).length : 0;
  const showReferredFiles =
    pendingRegistrationsQuery.isLoading ||
    pendingRegistrationsQuery.error != null ||
    pendingRegistrations > 0;
  const reminders = useMemo(() => remindersQuery.data ?? [], [remindersQuery.data]);
  const appealReminders = useMemo(() => appealRemindersQuery.data ?? [], [appealRemindersQuery.data]);
  const personalReminders = useMemo(() => personalQuery.data ?? [], [personalQuery.data]);
  // التبويب الظاهر وحده يحدد المعروض والإجمالي وتذييل «المزيد».
  const activeFeed = alertsTab === 'received' ? receivedFeed : sentFeed;
  const branchLawyers = useMemo(() => branchLawyersQuery.data ?? [], [branchLawyersQuery.data]);
  const unreadCount = Math.max(0, Number(unreadQuery.data?.count) || 0);
  // شارة لوحة الرئيس: غير المقروء بنطاقه (`received` — `D6`/`D7`) في تبويب «مستلمة»،
  // وإجمالي ما أصدره في فرعه في «أرسلتها». الإجمالي لا يُعرض قبل أن يُعرَف: أثناء التحميل
  // أو بعد فشل الصادر لا شارة «0» كاذبة. ورقم الصادر وحده بلا سياق للقارئ الصوتي،
  // فيسبقه نص مخفي (`sr-only`) يسمّيه — لا `aria-label` على `span` عام (لا يُنطق أصلًا).
  const headBadge =
    alertsTab === 'received'
      ? unreadCount > 0
        ? { text: `${unreadCount} غير مقروء`, srPrefix: '' }
        : null
      : sentFeed.isLoading || sentFeed.error
        ? null
        : { text: String(sentFeed.totalCount), srPrefix: 'إجمالي الصادر: ' };
  const unseenReplies = isLawyer ? Math.max(0, Number(unseenRepliesQuery.data?.count) || 0) : 0;
  const urgentCorrespondence = isLawyer ? Math.max(0, Number(urgentQuery.data?.count) || 0) : 0;
  // بطاقة التذكيرات في اللوحة: الأسبوع الحالي فقط (من الأحد إلى السبت) —
  // ملف/استئناف بتواريخها، والشخصي بتكراراته غير المنجزة (الكل في `/calendar`).
  const weekRange = useMemo(() => currentWeekRange(), []);
  const inWeekFileReminders = useMemo(() => {
    if (!isLawyer) return { reminders: [] as ReminderDto[], appealReminders: [] as AppealReminderDto[] };
    const inWeek = (dueDate: string) => {
      const key = dayKeyOf(dueDate);
      return key !== null && key >= weekRange.fromKey && key <= weekRange.toKey;
    };
    return {
      reminders: reminders.filter((r) => inWeek(r.dueDate)),
      appealReminders: appealReminders.filter((r) => inWeek(r.dueDate)),
    };
  }, [isLawyer, reminders, appealReminders, weekRange]);
  const inWeekPersonalCount = useMemo(() => {
    if (!isLawyer) return 0;
    return personalReminders.reduce(
      (n, p) => n + expandPersonalReminder(p, weekRange.fromDate, weekRange.toDate).length,
      0,
    );
  }, [isLawyer, personalReminders, weekRange]);
  const totalReminderCount = inWeekFileReminders.reminders.length + inWeekFileReminders.appealReminders.length + inWeekPersonalCount;
  // جرس التقويم: تذكير اليوم أو متأخر فقط (المعيار المعتمد) — مشتق محليًا من دمج
  // التذكيرات الثلاثة (ملف + استئناف + شخصي بتكراراته غير المنجزة).
  const calendarAlerts = useMemo(() => {
    if (!isLawyer) return 0;
    const today = new Date();
    const fileAlerts = [...reminders, ...appealReminders].filter((r) => isDueTodayOrOverdue(r.dueDate)).length;
    const personalAlerts = personalReminders.filter((p) => hasPendingOccurrenceOnOrBefore(p, today)).length;
    return fileAlerts + personalAlerts;
  }, [isLawyer, reminders, appealReminders, personalReminders]);

  const [selectedDay, setSelectedDay] = useState<string | null>(null);
  const dayOccurrences = useMemo(() => {
    if (!isLawyer || !selectedDay) return [];
    const day = parseDayKey(selectedDay);
    if (!day) return [];
    return groupRemindersByDay([...reminders, ...appealReminders], personalReminders, day, day).get(selectedDay) ?? [];
  }, [isLawyer, selectedDay, reminders, appealReminders, personalReminders]);

  const refreshReminders = () => {
    remindersQuery.refetch();
    appealRemindersQuery.refetch();
    personalQuery.refetch();
  };

  const markAlertRead = async (a: HeadAlertSummaryDto) => {
    const key = String(a.id);
    setMarkingKey(key);
    setAlertsError('');
    try {
      await api.patch(`/alerts/${a.id}/read`);
      receivedFeed.updateItems((prev) =>
        prev.map((x) => {
          if (x.id !== a.id || x.isRead) return x;
          // عدّاد الصف عند الرئيس/الشعبة = غير المقروء من مستلمي هذا الصف (`D6`)،
          // وتعليمه مقروءًا يُنقصه بمقدار واحد — وإلا بقي «غير مقروء: 2 / 2» بعد أن
          // قرأه المستخدم نفسه، حتى إعادة التركيب. صفوف المحامي (`unreadCount = null`)
          // بلا عدّاد فلا تُمس.
          return {
            ...x,
            isRead: true,
            unreadCount: x.unreadCount == null ? x.unreadCount : Math.max(0, x.unreadCount - 1),
          };
        }),
      );
      unreadQuery.setData((prev) => (prev ? { count: Math.max(0, prev.count - 1) } : prev));
    } catch (err) {
      setAlertsError(getApiErrorMessage(err));
    } finally {
      setMarkingKey(null);
    }
  };

  const submitAlert = async (e: FormEvent) => {
    e.preventDefault();
    if (!alertMessage.trim()) {
      setAlertFormError({ field: 'message', text: 'نص التنبيه مطلوب' });
      // تركيز أول حقل خاطئ عند الإرسال (قاعدة AGENTS.md) — عبر المعرف الثابت
      // لأن الحقل داخل `CreateAlertForm` ولا مرجع مباشر له هنا.
      document.getElementById('alert-message')?.focus();
      return;
    }
    let targetLawyerId: number | null = null;
    if (alertTargetType === 'lawyer') {
      targetLawyerId = alertLawyerId ? Number(alertLawyerId) : null;
      if (!targetLawyerId) {
        setAlertFormError({ field: 'lawyer', text: 'اختر المحامي المستلم' });
        document.getElementById('alert-lawyer')?.focus();
        return;
      }
    }

    setAlertSubmitting(true);
    setAlertFormError({ field: null, text: '' });
    try {
      // عقد `POST /alerts` = `201` بجسم الملخص (D3) — النوع مرآة السلك لا `HeadAlertDto`.
      await api.post<HeadAlertSummaryDto>('/alerts', {
        targetType: alertTargetType,
        documentId: null,
        targetLawyerId,
        message: alertMessage.trim(),
      });
      // بلا إلحاق تفاؤلي عمدًا: القائمة مستلَمات فقط، والمُرسَل في تبويب «أرسلتها».
      setShowAlertForm(false);
      setAlertMessage('');
      setAlertLawyerId('');
      setSentFlash('تم إصدار التنبيه — تجده في تبويب «أرسلتها»');
      sentFeed.reload();
    } catch (err) {
      // خطأ الإرسال الخادمي بلا حقل مخالف — يُعلن وحده دون تعليم أي حقل.
      setAlertFormError({ field: null, text: getApiErrorMessage(err) });
    } finally {
      setAlertSubmitting(false);
    }
  };

  if (isManager) {
    // المدير/مشرف النظام: نفس نمط المحامي ورئيس القسم — ترحيب + صف أيقونات،
    // والإحصائيات تُفتح من بطاقة «الإحصائيات» في `/stats` (لا عرض مباشر في اللوحة).
    const isAdmin = user?.role === 'admin';
    return (
      <div className="max-w-7xl mx-auto">
        <GreetingHeader fullName={user?.fullName} />
        <div className="mt-4">
          <ManagerIconRow isAdmin={isAdmin} />
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto">
      {isLawyer ? (
        <>
          <GreetingHeader fullName={user?.fullName} />
          <div className="mt-4">
            <LawyerIconRow
              counts={{ unseenReplies, urgentCorrespondence, calendarAlerts, pendingRegistrations, alertUnread: unreadCount }}
              showReferredFiles={showReferredFiles}
              pendingState={
                pendingRegistrationsQuery.isLoading
                  ? 'loading'
                  : pendingRegistrationsQuery.error != null
                    ? 'error'
                    : undefined
              }
            />
          </div>

          <section aria-label="التقويم" id="lawyer-calendar" className="scroll-mt-4 mt-8">
            <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5">
              <div className="flex items-center gap-2 mb-4">
                <span className="w-2 h-2 rounded-full bg-violet-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">التقويم</h3>
              </div>
              <LawyerCalendar
                reminders={[...reminders, ...appealReminders]}
                personal={personalReminders}
                weekStartsOn="sunday"
                selectedDay={selectedDay}
                onSelectDay={setSelectedDay}
              />
            </div>
          </section>

          {selectedDay ? (
            <DayRemindersModal
              dayKey={selectedDay}
              occurrences={dayOccurrences}
              onClose={() => setSelectedDay(null)}
              onChanged={refreshReminders}
            />
          ) : null}
        </>
      ) : (
        <>
          <GreetingHeader fullName={user?.fullName} />
          <div className="mt-4">
            <HeadIconRow
              counts={{
                reviewsPending,
                urgentCorrespondence: headUrgentCorrespondence,
                delegationsPending,
                entityPending,
                circuitsPending,
                alertUnread: unreadCount,
              }}
              scopeLabel={isSubHead ? 'مؤشرات الشعبة' : 'مؤشرات الفرع'}
              isSubHead={isSubHead}
            />
          </div>
        </>
      )}

      {isLawyer ? (
        <>
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-amber-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">التذكيرات</h3>
                <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium tabular-nums">
                  {totalReminderCount}
                </span>
              </div>
              <span className="text-xs text-gray-400">الأسبوع الحالي</span>
            </div>

            {actionError || remindersQuery.error || appealRemindersQuery.error ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{actionError || remindersQuery.error || appealRemindersQuery.error}</p>
              </div>
            ) : null}

            {totalReminderCount === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تذكيرات هذا الأسبوع</p>
              </div>
            ) : (
              <>
                {inWeekFileReminders.reminders.length + inWeekFileReminders.appealReminders.length > 0 ? (
                  <ReminderList
                    reminders={inWeekFileReminders.reminders}
                    appealReminders={inWeekFileReminders.appealReminders}
                    onCancel={cancelReminder}
                    onCancelAppeal={cancelAppealReminder}
                    cancellingKey={cancellingKey}
                  />
                ) : null}
                {inWeekPersonalCount > 0 ? (
                  <Link
                    to="/calendar"
                    className={`block px-4 sm:px-5 py-3 text-sm text-emerald-800 hover:bg-emerald-50 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-emerald-600 min-h-11 ${
                      inWeekFileReminders.reminders.length + inWeekFileReminders.appealReminders.length > 0
                        ? 'border-t border-gray-100'
                        : ''
                    }`}
                  >
                    تذكيرات شخصية هذا الأسبوع ({inWeekPersonalCount}) — عرض في التقويم ←
                  </Link>
                ) : null}
              </>
            )}
          </div>

          <AlertsPanel
            badge={
              unreadCount > 0 ? (
                <span className="shrink-0 whitespace-nowrap text-xs bg-red-100 text-red-800 rounded-full px-2 py-0.5 font-medium tabular-nums">
                  {unreadCount} غير مقروء
                </span>
              ) : null
            }
            headerExtra={<span className="text-xs text-gray-400">الأحدث أولاً</span>}
            footer={<AlertsFeedFooter feed={receivedFeed} />}
            error={alertsError || receivedFeed.error || ''}
            isLoading={receivedFeed.isLoading}
            alerts={receivedFeed.items}
            onMarkRead={markAlertRead}
            markingKey={markingKey}
          />
        </>
      ) : !headAlertsScopeEnabled ? (
        // رئيس/شعبة بلا فرع: النطاق مرفوض خلفيًا (`403` — `D11`) فلا نُطلق طلبات
        // محكومة بالفشل ولا نُظهر قائمة فارغة كاذبة — بل حالة صريحة قابلة للمعالجة.
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 mt-8 text-center">
          <p className="text-sm text-gray-600">
            حسابك غير مرتبط بفرع، فلا يمكن عرض تنبيهات القسم. راجع مدير النظام لربط حسابك بفرع.
          </p>
        </div>
      ) : (
        <>
          {sentFlash ? (
            <p role="status" className="text-sm text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-xl px-3 py-2 mb-4">
              {sentFlash}
            </p>
          ) : null}
          <div className="flex gap-2 mb-4" role="tablist" aria-label="تبويبات التنبيهات">
            {(['received', 'sent'] as const).map((t) => (
              <button
                key={t}
                type="button"
                role="tab"
                id={`alerts-tab-${t}`}
                aria-selected={alertsTab === t}
                aria-controls={`alerts-panel-${t}`}
                onClick={() => setAlertsTab(t)}
                className={`min-h-11 rounded-lg px-4 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 ${
                  alertsTab === t ? 'bg-emerald-800 text-white' : 'border border-gray-300 text-gray-700 hover:bg-gray-50'
                }`}
              >
                {t === 'received' ? 'مستلمة' : 'أرسلتها'}
              </button>
            ))}
          </div>
          {/* اللوحة الحاوية للتبويب الظاهر: `aria-controls` في الأزرار يشير إلى
              `alerts-panel-{tab}`، واللوحة تُسمّى بتبويبها (النمط نفسه في
              `DocumentView`/`PortalFileDetail`). */}
          <div
            role="tabpanel"
            id={`alerts-panel-${alertsTab}`}
            aria-labelledby={`alerts-tab-${alertsTab}`}
          >
            <AlertsPanel
              badge={
                headBadge === null ? null : (
                  <span className="shrink-0 whitespace-nowrap text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium tabular-nums">
                    {headBadge.srPrefix ? <span className="sr-only">{headBadge.srPrefix}</span> : null}
                    {headBadge.text}
                  </span>
                )
              }
              // دلالة الشارة صريحة: `received` مجموع غير المقروء في تنبيهات القسم
              // (أزواج تنبيه × مستلم) لا صفوف الرئيس الشخصية — فلا تُقرأ «غير مقروءة لي».
              subtitle={alertsTab === 'received' ? 'غير المقروء في تنبيهاتك' : null}
              headerExtra={
                <button
                  type="button"
                  onClick={() => setShowAlertForm((v) => !v)}
                  className="min-h-11 px-4 rounded-lg bg-emerald-800 hover:bg-emerald-700 text-white text-sm font-medium"
                >
                  {showAlertForm ? 'إلغاء' : '+ إصدار تنبيه'}
                </button>
              }
              form={
                showAlertForm ? (
                  <CreateAlertForm
                    targetType={alertTargetType}
                    onTargetTypeChange={setAlertTargetType}
                    lawyers={branchLawyers}
                    lawyerId={alertLawyerId}
                    onLawyerIdChange={setAlertLawyerId}
                    message={alertMessage}
                    onMessageChange={setAlertMessage}
                    submitting={alertSubmitting}
                    error={alertFormError}
                    onSubmit={submitAlert}
                    onCancel={() => setShowAlertForm(false)}
                  />
                ) : null
              }
              error={alertsError || activeFeed.error || ''}
              footer={<AlertsFeedFooter feed={activeFeed} />}
              isLoading={activeFeed.isLoading}
              alerts={activeFeed.items}
              // تعليم المقروء **للمستلم** في تبويب «مستلمة» وحده: `isRead` حالة استلام
              // المستخدم نفسه (وللرئيس/الشعبة تنبيهات نطاق `head` تستلمها فعليًا)،
              // والخلفية تسمح لهم بـ`PATCH /alerts/{id}/read`. وفي «أرسلتها» لا يُمرَّر
              // أصلًا (`isRead = null` للمُرسل، وكان الزر سيظهر لأن `!null` صحيح).
              onMarkRead={alertsTab === 'received' ? markAlertRead : undefined}
              markingKey={alertsTab === 'received' ? markingKey : null}
              sentView={alertsTab === 'sent'}
            />
          </div>
        </>
      )}
    </div>
  );
}

import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { useReminderCancellation } from '../hooks/useReminderCancellation';
import type {
  AppealReminderDto,
  BranchDto,
  ExecutionCircuitDto,
  HeadAlertDto,
  HeadAlertTargetType,
  LawyerListItem,
  ManagerLawyerStatDto,
  ManagerStatsDto,
  MonthlyStatDto,
  PendingRegistrationDto,
  PersonalReminderDto,
  ReminderDto,
  StatsPeriod,
} from '../types';
import { AlertsPanel } from '../components/dashboard/AlertsPanel';
import { CreateAlertForm, type AlertFormError } from '../components/dashboard/CreateAlertForm';
import { DayRemindersModal } from '../components/dashboard/DayRemindersModal';
import { GreetingHeader } from '../components/dashboard/GreetingHeader';
import { HeadIconRow } from '../components/dashboard/HeadIconRow';
import { LawyerCalendar } from '../components/dashboard/LawyerCalendar';
import { LawyerIconRow } from '../components/dashboard/LawyerIconRow';
import {
  isDueTodayOrOverdue,
  mostRecentSelection,
  previousSelection,
} from '../components/dashboard/dashboardFormat';
import { currentWeekRange, dayKeyOf, expandPersonalReminder, groupRemindersByDay, hasPendingOccurrenceOnOrBefore, parseDayKey } from '../components/dashboard/personalReminders';
import { CORRESPONDENCE_UNSEEN_EVENT } from '../components/correspondence/correspondenceDisplay';
import { REVIEWS_UNSEEN_EVENT } from '../components/review/reviewDisplay';
import { useBadgeCount } from '../hooks/useBadgeCount';
import type { PeriodSelection } from '../components/dashboard/dashboardTypes';
import { ManagerStatsSection } from '../components/dashboard/ManagerStatsSection';
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
  const [period, setPeriod] = useState<StatsPeriod>('yearly');
  const [selection, setSelection] = useState<PeriodSelection | null>(null);
  const [branchId, setBranchId] = useState<number | null>(null);

  const userReady = Boolean(user);
  // رئيس بلا فرع (ورئيس شعبة برمز بلا شعبة): عدّادات شاراته مرفوضة (`400`/`403`) أو صفرية خلفيًا —
  // لا تُطلق ولا تُستطلع أصلًا، وتبقى الشارات صفرًا.
  const headBadgesEnabled =
    userReady && isHeadOrSubHead && (user?.branchId ?? null) != null && (isSubHead ? (user?.sectionId ?? null) != null : true);

  const branchesQuery = useCancellableRequest<BranchDto[]>(
    (signal) => api.get('/branches', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [isManager],
    { enabled: userReady && isManager },
  );

  // التذكيرات خاصة بالمحامي فقط؛ لا تُجلب لرئيس القسم.
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

  const alertsQuery = useCancellableRequest<HeadAlertDto[]>(
    (signal) => api.get('/alerts', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
    { enabled: userReady && !isManager },
  );

  const unreadQuery = useCancellableRequest<{ count: number }>(
    (signal) => api.get('/alerts/unread-count', { signal }).then((r) => r.data),
    [isLawyer],
    { enabled: userReady && !isManager && isLawyer },
  );

  const branchLawyersQuery = useCancellableRequest<LawyerListItem[]>(
    (signal) => api.get('/users/lawyers', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
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

  const availableQuery = useCancellableRequest<MonthlyStatDto[]>(
    (signal) => {
      const params: Record<string, unknown> = {};
      if (isManager && branchId) params.branchId = branchId;
      return api
        .get('/stats/periods', { params, signal })
        .then((r) => (Array.isArray(r.data) ? r.data : []));
    },
    [isManager, branchId],
    // الإحصائيات في صفحة `/stats` للمدير/المشرف — اللوحة لا تجلب الفترات لغيرهما.
    { enabled: userReady && isManager },
  );

  const statsQuery = useCancellableRequest<ManagerStatsDto>((signal) => {
    const params: Record<string, unknown> = { period };
    if (selection) {
      params.year = selection.year;
      if (selection.month != null) params.month = selection.month;
      if (selection.quarter != null) params.quarter = selection.quarter;
    }
    if (isManager && branchId) params.branchId = branchId;
    return api.get<ManagerStatsDto>('/stats/manager', { params, signal }).then((r) => r.data);
  }, [isManager, period, selection, branchId], { enabled: userReady && isManager });

  // إحصائيات الفترة السابقة للدلتا (المدير/المشرف فقط — اللوحة لا تجلبها لغيرهما).
  const prevSelection = useMemo(() => previousSelection(selection, period), [selection, period]);
  const prevStatsQuery = useCancellableRequest<ManagerStatsDto | null>((signal) => {
    if (!prevSelection) return Promise.resolve(null);
    const params: Record<string, unknown> = { period, year: prevSelection.year };
    if (prevSelection.month != null) params.month = prevSelection.month;
    if (prevSelection.quarter != null) params.quarter = prevSelection.quarter;
    if (branchId) params.branchId = branchId;
    return api.get<ManagerStatsDto>('/stats/manager', { params, signal }).then((r) => r.data);
  }, [isManager, period, prevSelection, branchId], { enabled: userReady && isManager && prevSelection != null });

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

  const lawyersBranch = isManager ? branchId : (user?.branchId ?? null);
  const lawyerStatsQuery = useCancellableRequest<ManagerLawyerStatDto[]>((signal) => {
    const params: Record<string, unknown> = { period };
    if (selection) {
      params.year = selection.year;
      if (selection.month != null) params.month = selection.month;
      if (selection.quarter != null) params.quarter = selection.quarter;
    }
    if (isManager && branchId) params.branchId = branchId;
    return api
      .get('/stats/manager/lawyers', { params: { ...params, branchId: lawyersBranch }, signal })
      .then((r) => (Array.isArray(r.data) ? r.data : []));
  }, [isLawyer, isManager, period, selection, branchId, lawyersBranch], { enabled: userReady && isManager && lawyersBranch != null });

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
  const alerts = useMemo(() => alertsQuery.data ?? [], [alertsQuery.data]);
  const branches = useMemo(() => branchesQuery.data ?? [], [branchesQuery.data]);
  const branchLawyers = useMemo(() => branchLawyersQuery.data ?? [], [branchLawyersQuery.data]);
  const available = useMemo(() => availableQuery.data ?? [], [availableQuery.data]);
  const managerStats = statsQuery.data;
  const lawyerStats = useMemo(
    () => (lawyersBranch != null ? (lawyerStatsQuery.data ?? []) : []),
    [lawyersBranch, lawyerStatsQuery.data],
  );
  const unreadCount = Math.max(0, Number(unreadQuery.data?.count) || 0);
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

  const markAlertRead = async (a: HeadAlertDto) => {
    const key = String(a.id);
    setMarkingKey(key);
    setAlertsError('');
    try {
      await api.patch(`/alerts/${a.id}/read`);
      alertsQuery.setData((prev) => (prev ?? []).map((x) => (x.id === a.id ? { ...x, isRead: true } : x)));
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
      const { data } = await api.post<HeadAlertDto>('/alerts', {
        targetType: alertTargetType,
        documentId: null,
        targetLawyerId,
        message: alertMessage.trim(),
      });
      alertsQuery.setData((prev) => [data, ...(prev ?? [])]);
      setShowAlertForm(false);
      setAlertMessage('');
      setAlertLawyerId('');
    } catch (err) {
      // خطأ الإرسال الخادمي بلا حقل مخالف — يُعلن وحده دون تعليم أي حقل.
      setAlertFormError({ field: null, text: getApiErrorMessage(err) });
    } finally {
      setAlertSubmitting(false);
    }
  };

  useEffect(() => {
    const recent = mostRecentSelection(available, period);
    setSelection(recent);
  }, [available, period]);

  if (isManager) {
    return (
      <div className="max-w-7xl mx-auto">
        <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6">لوحة التحكم</h2>
        <ManagerStatsSection
          period={period}
          onPeriodChange={setPeriod}
          availablePeriods={available}
          selection={selection}
          onSelectionChange={setSelection}
          branches={branches}
          branchId={branchId}
          onBranchChange={setBranchId}
          stats={managerStats}
          prevStats={prevStatsQuery.data ?? null}
          lawyers={lawyerStats}
          lawyersError={lawyerStatsQuery.error ?? ''}
          error={statsQuery.error ?? ''}
        />
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
              counts={{ unseenReplies, urgentCorrespondence, calendarAlerts, pendingRegistrations }}
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
              }}
              scopeLabel={isSubHead ? 'مؤشرات الشعبة' : 'مؤشرات الفرع'}
              hideAudit={isSubHead}
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
                <span className="text-xs bg-red-100 text-red-800 rounded-full px-2 py-0.5 font-medium">
                  {unreadCount} غير مقروء
                </span>
              ) : null
            }
            headerExtra={<span className="text-xs text-gray-400">الأحدث أولاً</span>}
            error={alertsError || alertsQuery.error || ''}
            alerts={alerts}
            onMarkRead={markAlertRead}
            markingKey={markingKey}
          />
        </>
      ) : (
        <>
          <AlertsPanel
            badge={
              <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium">
                {alerts.length}
              </span>
            }
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
            error={alertsError || alertsQuery.error || ''}
            alerts={alerts}
          />
        </>
      )}
    </div>
  );
}

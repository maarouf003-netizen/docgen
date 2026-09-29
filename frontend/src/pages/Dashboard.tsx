import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { useReminderCancellation } from '../hooks/useReminderCancellation';
import type {
  AppealReminderDto,
  BranchDto,
  HeadAlertDto,
  HeadAlertTargetType,
  LawyerListItem,
  ManagerLawyerStatDto,
  ManagerStatsDto,
  MonthlyStatDto,
  PersonalReminderDto,
  PublicEntityEntryDto,
  ReminderDto,
  StatsPeriod,
} from '../types';
import { AlertRow } from '../components/dashboard/AlertRow';
import { CreateAlertForm } from '../components/dashboard/CreateAlertForm';
import { DayRemindersModal } from '../components/dashboard/DayRemindersModal';
import { GreetingHeader } from '../components/dashboard/GreetingHeader';
import { LawyerCalendar } from '../components/dashboard/LawyerCalendar';
import { LawyerIconRow } from '../components/dashboard/LawyerIconRow';
import {
  isDueTodayOrOverdue,
  mostRecentSelection,
} from '../components/dashboard/dashboardFormat';
import { currentWeekRange, dayKeyOf, expandPersonalReminder, groupRemindersByDay, hasPendingOccurrenceOnOrBefore, parseDayKey } from '../components/dashboard/personalReminders';
import { CORRESPONDENCE_UNSEEN_EVENT } from '../components/correspondence/correspondenceDisplay';
import { REVIEWS_UNSEEN_EVENT } from '../components/review/reviewDisplay';
import type { PeriodSelection } from '../components/dashboard/dashboardTypes';
import { ManagerStatsSection } from '../components/dashboard/ManagerStatsSection';
import { ReminderList } from '../components/dashboard/ReminderList';
import { formatEntityCoverage } from '../utils/entityRegistry';

export default function Dashboard() {
  const { user } = useAuth();
  const isLawyer = user?.role === 'lawyer';
  const isManager = user?.role === 'manager' || user?.role === 'admin';
  const isHead = user?.role === 'head';

  const [alertsError, setAlertsError] = useState('');
  const [markingKey, setMarkingKey] = useState<string | null>(null);

  const [showAlertForm, setShowAlertForm] = useState(false);
  const [alertTargetType, setAlertTargetType] = useState<HeadAlertTargetType>('branch');
  const [alertLawyerId, setAlertLawyerId] = useState('');
  const [alertMessage, setAlertMessage] = useState('');
  const [alertSubmitting, setAlertSubmitting] = useState(false);
  const [alertFormError, setAlertFormError] = useState('');
  const [period, setPeriod] = useState<StatsPeriod>('yearly');
  const [selection, setSelection] = useState<PeriodSelection | null>(null);
  const [branchId, setBranchId] = useState<number | null>(null);

  const userReady = Boolean(user);

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
    [],
    { enabled: isHead },
  );

  // جهات أدخلها المحامون وبانتظار مراجعة رئيس القسم (نموذج الحوكمة الجديد).
  const entityReviewQuery = useCancellableRequest<PublicEntityEntryDto[]>(
    (signal) => api
      .get('/entity-registry/pending-review', { signal })
      .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [isHead],
    { enabled: userReady && isHead },
  );
  const entityReview = entityReviewQuery.data ?? [];

  const availableQuery = useCancellableRequest<MonthlyStatDto[]>(
    (signal) => {
      const params: Record<string, unknown> = {};
      if (isManager && branchId) params.branchId = branchId;
      return api
        .get('/stats/periods', { params, signal })
        .then((r) => (Array.isArray(r.data) ? r.data : []));
    },
    [isManager, branchId],
    // الإحصائيات في صفحة `/stats` للمحامي — اللوحة لا تجلب الفترات له.
    { enabled: userReady && !isLawyer },
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
  }, [isManager, period, selection, branchId], { enabled: userReady && !isLawyer });

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

  const lawyersBranch = isManager ? branchId : (user?.branchId ?? null);  const lawyerStatsQuery = useCancellableRequest<ManagerLawyerStatDto[]>((signal) => {
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
  }, [isLawyer, isManager, period, selection, branchId, lawyersBranch], { enabled: userReady && lawyersBranch != null });

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
      setAlertFormError('نص التنبيه مطلوب');
      return;
    }
    let targetLawyerId: number | null = null;
    if (alertTargetType === 'lawyer') {
      targetLawyerId = alertLawyerId ? Number(alertLawyerId) : null;
      if (!targetLawyerId) {
        setAlertFormError('اختر المحامي المستلم');
        return;
      }
    }

    setAlertSubmitting(true);
    setAlertFormError('');
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
      setAlertFormError(getApiErrorMessage(err));
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
          lawyers={lawyerStats}
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
              counts={{ unseenReplies, urgentCorrespondence, calendarAlerts }}
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
          <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6">لوحة التحكم</h2>

          <ManagerStatsSection
            period={period}
            onPeriodChange={setPeriod}
            availablePeriods={available}
            selection={selection}
            onSelectionChange={setSelection}
            branches={branches}
            branchId={user?.branchId ?? null}
            onBranchChange={() => {}}
            showBranchSelect={false}
            showLawyerTable={!isLawyer}
            stats={managerStats}
            lawyers={lawyerStats}
            error={statsQuery.error ?? ''}
            appealsStats={isLawyer ? (managerStats?.appeals ?? null) : null}
          />
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

          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-red-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">تنبيهات رئيس القسم</h3>
                {unreadCount > 0 ? (
                  <span className="text-xs bg-red-100 text-red-800 rounded-full px-2 py-0.5 font-medium">
                    {unreadCount} غير مقروء
                  </span>
                ) : null}
              </div>
              <span className="text-xs text-gray-400">الأحدث أولاً</span>
            </div>

            {(alertsError || alertsQuery.error) ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{alertsError || alertsQuery.error}</p>
              </div>
            ) : null}

            {alerts.length === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تنبيهات حالياً</p>
              </div>
            ) : (
              <ul className="divide-y divide-gray-100 max-h-[420px] overflow-y-auto">
                {alerts.map((a) => (
                  <AlertRow key={a.id} alert={a} onMarkRead={markAlertRead} markingKey={markingKey} />
                ))}
              </ul>
            )}
          </div>
        </>
      ) : (
        <>
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-red-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">تنبيهات رئيس القسم</h3>
                <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium">
                  {alerts.length}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setShowAlertForm((v) => !v)}
                className="min-h-11 px-4 rounded-lg bg-emerald-800 hover:bg-emerald-700 text-white text-sm font-medium"
              >
                {showAlertForm ? 'إلغاء' : '+ إصدار تنبيه'}
              </button>
            </div>

            {showAlertForm ? (
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
            ) : null}

            {(alertsError || alertsQuery.error) ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{alertsError || alertsQuery.error}</p>
              </div>
            ) : null}

            {alerts.length === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تنبيهات حالياً</p>
              </div>
            ) : (
              <ul className="divide-y divide-gray-100 max-h-[420px] overflow-y-auto">
                {alerts.map((a) => (
                  <AlertRow key={a.id} alert={a} />
                ))}
              </ul>
            )}
          </div>

          {isHead && (
            <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
              <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
                <div className="flex items-center gap-2">
                  <span className="w-2 h-2 rounded-full bg-amber-500" aria-hidden="true" />
                  <h3 className="font-bold text-gray-900">مراجعة سجل الجهات العامة</h3>
                  <span
                    className={`text-xs rounded-full px-2 py-0.5 font-medium ${
                      entityReview.length > 0
                        ? 'bg-amber-100 text-amber-800'
                        : 'bg-emerald-100 text-emerald-800'
                    }`}
                  >
                    {entityReview.length}
                  </span>
                </div>
                <Link to="/entities/review" className="text-sm text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 min-h-11">
                  مراجعة السجل…
                </Link>
              </div>
              {entityReviewQuery.error ? (
                <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                  <p className="text-red-700 text-sm">{entityReviewQuery.error}</p>
                </div>
              ) : entityReview.length === 0 ? (
                <div className="p-6 text-center">
                  <p className="text-gray-400 text-sm">لا توجد جهات بانتظار المراجعة</p>
                </div>
              ) : (
                <ul className="divide-y divide-gray-100">
                  {entityReview.slice(0, 5).map((e) => (
                    <li key={e.id} className="px-4 sm:px-5 py-3">
                      <p className="font-medium text-gray-800 break-words">{e.canonicalName}</p>
                      <p className="text-xs text-gray-500 mt-0.5 tabular-nums">
                        {formatEntityCoverage(e)} / {e.branchName} · أدخلها {e.createdByName || 'محامٍ'}
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </>
      )}
    </div>
  );
}

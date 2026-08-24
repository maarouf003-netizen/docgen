import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import type {
  AppealReminderDto,
  BranchDto,
  HeadAlertDto,
  HeadAlertTargetType,
  LawyerListItem,
  ManagerLawyerStatDto,
  ManagerStatsDto,
  MonthlyStatDto,
  PublicEntityProposalDto,
  ReminderDto,
  StatsPeriod,
} from '../types';
import { AlertRow } from '../components/dashboard/AlertRow';
import { CreateAlertForm } from '../components/dashboard/CreateAlertForm';
import { mostRecentSelection } from '../components/dashboard/dashboardFormat';
import type { PeriodSelection } from '../components/dashboard/dashboardTypes';
import { ManagerStatsSection } from '../components/dashboard/ManagerStatsSection';
import { ReminderList } from '../components/dashboard/ReminderList';

export default function Dashboard() {
  const { user } = useAuth();
  const isLawyer = user?.role === 'lawyer';
  const isManager = user?.role === 'manager' || user?.role === 'admin';
  const isHead = user?.role === 'head';

  const [cancellingKey, setCancellingKey] = useState<string | null>(null);
  const [actionError, setActionError] = useState('');

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

  // اقتراحات الجهات الجديدة بانتظار اعتماد رئيس القسم (د4).
  const entityProposalsQuery = useCancellableRequest<PublicEntityProposalDto[]>(
    (signal) => api
      .get('/entity-registry/proposals/pending', { signal })
      .then((r) => (Array.isArray(r.data) ? r.data : [])),
    [isHead],
    { enabled: userReady && isHead },
  );
  const entityProposals = entityProposalsQuery.data ?? [];

  const availableQuery = useCancellableRequest<MonthlyStatDto[]>(
    (signal) => {
      const params: Record<string, unknown> = {};
      if (isManager && branchId) params.branchId = branchId;
      return api
        .get('/stats/periods', { params, signal })
        .then((r) => (Array.isArray(r.data) ? r.data : []));
    },
    [isManager, branchId],
    { enabled: userReady },
  );

  const statsQuery = useCancellableRequest<ManagerStatsDto>((signal) => {
    const params: Record<string, unknown> = { period };
    if (selection) {
      params.year = selection.year;
      if (selection.month != null) params.month = selection.month;
      if (selection.quarter != null) params.quarter = selection.quarter;
    }
    const url = isLawyer ? '/stats/me' : '/stats/manager';
    if (!isLawyer && isManager && branchId) params.branchId = branchId;
    return api.get<ManagerStatsDto>(url, { params, signal }).then((r) => r.data);
  }, [isLawyer, isManager, period, selection, branchId], { enabled: userReady });

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
  }, [isLawyer, isManager, period, selection, branchId, lawyersBranch], { enabled: userReady && lawyersBranch != null });

  const reminders = useMemo(() => remindersQuery.data ?? [], [remindersQuery.data]);
  const appealReminders = useMemo(() => appealRemindersQuery.data ?? [], [appealRemindersQuery.data]);
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

  const cancelReminder = async (r: ReminderDto) => {
    const key = String(r.actionId);
    setCancellingKey(key);
    setActionError('');
    try {
      await api.delete(`/documents/${r.documentId}/actions/${r.actionId}/reminder`);
      remindersQuery.setData((prev) => (prev ?? []).filter((x) => x.actionId !== r.actionId));
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setCancellingKey(null);
    }
  };

  const cancelAppealReminder = async (r: AppealReminderDto) => {
    const key = `appeal-${r.actionId}`;
    setCancellingKey(key);
    setActionError('');
    try {
      await api.delete(`/appeals/${r.appealId}/actions/${r.actionId}/reminder`);
      appealRemindersQuery.setData((prev) => (prev ?? []).filter((x) => x.actionId !== r.actionId));
    } catch (err) {
      setActionError(getApiErrorMessage(err));
    } finally {
      setCancellingKey(null);
    }
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

      {isLawyer ? (
        <>
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-amber-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">التذكيرات</h3>
                <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium">
                  {reminders.length + appealReminders.length}
                </span>
              </div>
              <span className="text-xs text-gray-400">الأقرب أولاً</span>
            </div>

            {actionError || remindersQuery.error || appealRemindersQuery.error ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{actionError || remindersQuery.error || appealRemindersQuery.error}</p>
              </div>
            ) : null}

            {reminders.length === 0 && appealReminders.length === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تذكيرات حالياً</p>
              </div>
            ) : (
              <ReminderList
                reminders={reminders}
                appealReminders={appealReminders}
                onCancel={cancelReminder}
                onCancelAppeal={cancelAppealReminder}
                cancellingKey={cancellingKey}
              />
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
                  <h3 className="font-bold text-gray-900">اقتراحات الجهات العامة</h3>
                  <span
                    className={`text-xs rounded-full px-2 py-0.5 font-medium ${
                      entityProposals.length > 0
                        ? 'bg-amber-100 text-amber-800'
                        : 'bg-emerald-100 text-emerald-800'
                    }`}
                  >
                    {entityProposals.length}
                  </span>
                </div>
                <Link to="/entities/proposals" className="text-sm text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 min-h-11">
                  إدارة الاقتراحات…
                </Link>
              </div>
              {entityProposalsQuery.error ? (
                <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                  <p className="text-red-700 text-sm">{entityProposalsQuery.error}</p>
                </div>
              ) : entityProposals.length === 0 ? (
                <div className="p-6 text-center">
                  <p className="text-gray-400 text-sm">لا توجد اقتراحات بانتظار الاعتماد</p>
                </div>
              ) : (
                <ul className="divide-y divide-gray-100">
                  {entityProposals.slice(0, 5).map((p) => (
                    <li key={p.id} className="px-4 sm:px-5 py-3">
                      <p className="font-medium text-gray-800 break-words">{p.proposedName}</p>
                      <p className="text-xs text-gray-500 mt-0.5 tabular-nums">
                        {p.governorate} / {p.branchName} · من {p.proposedByName || 'محامٍ'}
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

import { useEffect, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import type {
  BranchDto,
  HeadAlertDto,
  HeadAlertTargetType,
  LawyerListItem,
  ManagerLawyerStatDto,
  ManagerStatsDto,
  MonthlyStatDto,
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

  const [reminders, setReminders] = useState<ReminderDto[]>([]);
  const [cancellingKey, setCancellingKey] = useState<string | null>(null);
  const [actionError, setActionError] = useState('');

  const [alerts, setAlerts] = useState<HeadAlertDto[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [alertsError, setAlertsError] = useState('');
  const [markingKey, setMarkingKey] = useState<string | null>(null);

  const [showAlertForm, setShowAlertForm] = useState(false);
  const [alertTargetType, setAlertTargetType] = useState<HeadAlertTargetType>('branch');
  const [alertLawyerId, setAlertLawyerId] = useState('');
  const [alertMessage, setAlertMessage] = useState('');
  const [alertSubmitting, setAlertSubmitting] = useState(false);
  const [alertFormError, setAlertFormError] = useState('');
  const [branchLawyers, setBranchLawyers] = useState<LawyerListItem[]>([]);

  const [period, setPeriod] = useState<StatsPeriod>('yearly');
  const [available, setAvailable] = useState<MonthlyStatDto[]>([]);
  const [selection, setSelection] = useState<PeriodSelection | null>(null);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [branchId, setBranchId] = useState<number | null>(null);
  const [managerStats, setManagerStats] = useState<ManagerStatsDto | null>(null);
  const [lawyerStats, setLawyerStats] = useState<ManagerLawyerStatDto[]>([]);
  const [managerError, setManagerError] = useState('');

  const cancelReminder = async (r: ReminderDto) => {
    const key = String(r.actionId);
    setCancellingKey(key);
    setActionError('');
    try {
      await api.delete(`/documents/${r.documentId}/actions/${r.actionId}/reminder`);
      setReminders((prev) => prev.filter((x) => x.actionId !== r.actionId));
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
      setAlerts((prev) => prev.map((x) => (x.id === a.id ? { ...x, isRead: true } : x)));
      setUnreadCount((c) => Math.max(0, c - 1));
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
      setAlerts((prev) => [data, ...prev]);
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
    if (!user) return;

    if (isManager) {
      api
        .get<BranchDto[]>('/branches')
        .then((r) => setBranches(Array.isArray(r.data) ? r.data : []))
        .catch(() => {});
      return;
    }

    // التذكيرات خاصة بالمحامي فقط؛ لا تُجلب لرئيس القسم.
    if (isLawyer) {
      api
        .get<ReminderDto[]>('/reminders')
        .then((r) => setReminders(Array.isArray(r.data) ? r.data : []))
        .catch(() => {});
    }
  }, [isLawyer, isManager, user]);

  useEffect(() => {
    if (!user || isManager) return;

    api
      .get<HeadAlertDto[]>('/alerts')
      .then((r) => setAlerts(Array.isArray(r.data) ? r.data : []))
      .catch(() => setAlerts([]));

    if (isLawyer) {
      api
        .get<{ count: number }>('/alerts/unread-count')
        .then((r) => setUnreadCount(Number(r.data.count) || 0))
        .catch(() => setUnreadCount(0));
    }
  }, [isLawyer, isManager, user]);

  useEffect(() => {
    if (!isHead) return;

    api
      .get<LawyerListItem[]>('/users/lawyers')
      .then((r) => setBranchLawyers(Array.isArray(r.data) ? r.data : []))
      .catch(() => setBranchLawyers([]));
  }, [isHead]);

  useEffect(() => {
    if (!user) return;

    const params: Record<string, unknown> = {};
    if (isManager && branchId) params.branchId = branchId;
    api
      .get<MonthlyStatDto[]>('/stats/periods', { params })
      .then((r) => setAvailable(Array.isArray(r.data) ? r.data : []))
      .catch(() => setAvailable([]));
  }, [isManager, branchId, user]);

  useEffect(() => {
    const recent = mostRecentSelection(available, period);
    setSelection(recent);
  }, [available, period]);

  useEffect(() => {
    if (!user) return;

    setManagerError('');
    const params: Record<string, unknown> = { period };
    if (selection) {
      params.year = selection.year;
      if (selection.month != null) params.month = selection.month;
      if (selection.quarter != null) params.quarter = selection.quarter;
    }

    if (isLawyer) {
      api
        .get<ManagerStatsDto>('/stats/me', { params })
        .then((r) => setManagerStats(r.data))
        .catch((err) => setManagerError(getApiErrorMessage(err)));
      return;
    }

    if (isManager && branchId) params.branchId = branchId;
    api
      .get<ManagerStatsDto>('/stats/manager', { params })
      .then((r) => setManagerStats(r.data))
      .catch((err) => setManagerError(getApiErrorMessage(err)));

    const lawyersBranch = isManager ? branchId : (user?.branchId ?? null);
    if (lawyersBranch) {
      api
        .get<ManagerLawyerStatDto[]>('/stats/manager/lawyers', {
          params: { ...params, branchId: lawyersBranch },
        })
        .then((r) => setLawyerStats(Array.isArray(r.data) ? r.data : []))
        .catch(() => setLawyerStats([]));
    } else {
      setLawyerStats([]);
    }
  }, [isLawyer, isManager, period, selection, branchId, user]);

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
          error={managerError}
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
        error={managerError}
      />

      {isLawyer ? (
        <>
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 flex flex-col overflow-hidden mt-8">
            <div className="flex items-center justify-between gap-3 px-4 sm:px-5 py-4 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <span className="w-2 h-2 rounded-full bg-amber-500" aria-hidden="true" />
                <h3 className="font-bold text-gray-900">التذكيرات</h3>
                <span className="text-xs bg-emerald-100 text-emerald-800 rounded-full px-2 py-0.5 font-medium">
                  {reminders.length}
                </span>
              </div>
              <span className="text-xs text-gray-400">الأقرب أولاً</span>
            </div>

            {actionError ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{actionError}</p>
              </div>
            ) : null}

            {reminders.length === 0 ? (
              <div className="p-10 text-center">
                <p className="text-gray-400 text-sm">لا توجد تذكيرات حالياً</p>
              </div>
            ) : (
              <ReminderList reminders={reminders} onCancel={cancelReminder} cancellingKey={cancellingKey} />
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

            {alertsError ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{alertsError}</p>
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

            {alertsError ? (
              <div className="px-4 sm:px-5 py-2.5 bg-red-50 border-b border-red-100">
                <p className="text-red-700 text-sm">{alertsError}</p>
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
        </>
      )}
    </div>
  );
}

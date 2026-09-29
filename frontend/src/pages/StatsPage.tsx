import { useEffect, useMemo, useState } from 'react';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { LawyerStatsSection } from '../components/dashboard/LawyerStatsSection';
import { mostRecentSelection, previousSelection } from '../components/dashboard/dashboardFormat';
import type { PeriodSelection } from '../components/dashboard/dashboardTypes';
import type { ManagerStatsDto, MonthlyStatDto, StatsPeriod } from '../types';

/**
 * صفحة إحصائيات المحامي (`/stats`): قسم الإحصائيات الكامل الذي كان في اللوحة —
 * البطل ضمن-الفترة + الدلتا + الاتجاه + التفاصيل خلف التوسيع + روابط التعمّق.
 */
export default function StatsPage() {
  const { user } = useAuth();
  const userReady = Boolean(user);
  const [period, setPeriod] = useState<StatsPeriod>('yearly');
  const [selection, setSelection] = useState<PeriodSelection | null>(null);

  const availableQuery = useCancellableRequest<MonthlyStatDto[]>(
    (signal) =>
      api.get('/stats/periods', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [],
    { enabled: userReady },
  );
  const available = useMemo(() => availableQuery.data ?? [], [availableQuery.data]);

  const statsQuery = useCancellableRequest<ManagerStatsDto>((signal) => {
    const params: Record<string, unknown> = { period };
    if (selection) {
      params.year = selection.year;
      if (selection.month != null) params.month = selection.month;
      if (selection.quarter != null) params.quarter = selection.quarter;
    }
    return api.get<ManagerStatsDto>('/stats/me', { params, signal }).then((r) => r.data);
  }, [period, selection], { enabled: userReady });

  const prevSelection = useMemo(() => previousSelection(selection, period), [selection, period]);
  const prevStatsQuery = useCancellableRequest<ManagerStatsDto | null>((signal) => {
    if (!prevSelection) return Promise.resolve(null);
    const params: Record<string, unknown> = { period, year: prevSelection.year };
    if (prevSelection.month != null) params.month = prevSelection.month;
    if (prevSelection.quarter != null) params.quarter = prevSelection.quarter;
    return api.get<ManagerStatsDto>('/stats/me', { params, signal }).then((r) => r.data);
  }, [period, prevSelection], { enabled: userReady && prevSelection != null });

  useEffect(() => {
    setSelection(mostRecentSelection(available, period));
  }, [available, period]);

  return (
    <div className="max-w-7xl mx-auto">
      <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6 text-balance">الإحصائيات</h2>
      <LawyerStatsSection
        period={period}
        onPeriodChange={setPeriod}
        availablePeriods={available}
        selection={selection}
        onSelectionChange={setSelection}
        stats={statsQuery.data ?? null}
        prevStats={prevStatsQuery.data ?? null}
        appealsStats={statsQuery.data?.appeals ?? null}
        error={statsQuery.error ?? ''}
      />
    </div>
  );
}

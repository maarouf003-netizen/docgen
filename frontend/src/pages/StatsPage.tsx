import { useEffect, useMemo, useState } from 'react';
import { api } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { useCancellableRequest } from '../hooks/useCancellableRequest';
import { LawyerStatsSection } from '../components/dashboard/LawyerStatsSection';
import { LawyersTable } from '../components/dashboard/LawyersTable';
import { mostRecentSelection, previousSelection } from '../components/dashboard/dashboardFormat';
import type { PeriodSelection } from '../components/dashboard/dashboardTypes';
import type { ManagerLawyerStatDto, ManagerStatsDto, MonthlyStatDto, StatsPeriod } from '../types';

/**
 * صفحة الإحصائيات (`/stats`) حسب الدور:
 * - المحامي: إحصائياته الشخصية (`/stats/me`) بروابط التعمّق.
 * - رئيس القسم/الشعبة: إحصائيات نطاقه (`/stats/manager` — النطاق إجباري خلفيًا:
 *   القسم لدوائره، والشعبة لدوائرها — قرار §2.27)
 *   بلا روابط تعمّق وبلا استئنافات (`null` تلقائيًا)، مع جدول محامي النطاق.
 * (المدير/المشرف: إحصائياتهما في اللوحة، لا يصلان هنا — الحارس في `App.tsx`.)
 */
export default function StatsPage() {
  const { user } = useAuth();
  const userReady = Boolean(user);
  const isHeadOrSubHead = user?.role === 'head' || user?.role === 'subhead';
  const isSubHead = user?.role === 'subhead';
  const [period, setPeriod] = useState<StatsPeriod>('yearly');
  const [selection, setSelection] = useState<PeriodSelection | null>(null);
  // رئيس بلا فرع (ورئيس شعبة برمز بلا شعبة): حالة محرّمة تُرفض عند الدخول أصلًا — فإن وُجدت (دفاع عمقي)
  // لا تُطلق طلباته ويُخفى القسم، فتبقى رسالة تعيين الفرع الوحيدة في الجدول أدناه.
  const headMissingBranch =
    isHeadOrSubHead && ((user?.branchId ?? null) == null || (isSubHead && (user?.sectionId ?? null) == null));

  const availableQuery = useCancellableRequest<MonthlyStatDto[]>(
    (signal) =>
      api.get('/stats/periods', { signal }).then((r) => (Array.isArray(r.data) ? r.data : [])),
    [isHeadOrSubHead],
    { enabled: userReady && !headMissingBranch },
  );
  const available = useMemo(() => availableQuery.data ?? [], [availableQuery.data]);

  // رئيس القسم/الشعبة يُحصر بنطاقه خلفيًا — لا وسيط `branchId` إطلاقًا.
  // رئيس بلا فرع: لا إحصائيات له أصلًا — لا تُطلق طلباته، ويُخفى القسم
  // فتبقى رسالة تعيين الفرع الوحيدة في الجدول أدناه (بلا تكديس رسائل).
  const statsEndpoint = isHeadOrSubHead ? '/stats/manager' : '/stats/me';
  const noBranchMessage = 'لا يوجد فرع مرتبط بحسابك — تواصل مع المشرف لتعيين فرعك';
  const statsQuery = useCancellableRequest<ManagerStatsDto>((signal) => {
    const params: Record<string, unknown> = { period };
    if (selection) {
      params.year = selection.year;
      if (selection.month != null) params.month = selection.month;
      if (selection.quarter != null) params.quarter = selection.quarter;
    }
    return api.get<ManagerStatsDto>(statsEndpoint, { params, signal }).then((r) => r.data);
  }, [period, selection, statsEndpoint], { enabled: userReady && !headMissingBranch });

  const prevSelection = useMemo(() => previousSelection(selection, period), [selection, period]);
  const prevStatsQuery = useCancellableRequest<ManagerStatsDto | null>((signal) => {
    if (!prevSelection) return Promise.resolve(null);
    const params: Record<string, unknown> = { period, year: prevSelection.year };
    if (prevSelection.month != null) params.month = prevSelection.month;
    if (prevSelection.quarter != null) params.quarter = prevSelection.quarter;
    return api.get<ManagerStatsDto>(statsEndpoint, { params, signal }).then((r) => r.data);
  }, [period, prevSelection, statsEndpoint], { enabled: userReady && !headMissingBranch && prevSelection != null });

  // جدول محامي النطاق لرئيس القسم/الشعبة فقط (نطاقه من الرمز — بلا وسائط).
  const lawyersQuery = useCancellableRequest<ManagerLawyerStatDto[]>((signal) => {
    const params: Record<string, unknown> = { period };
    if (selection) {
      params.year = selection.year;
      if (selection.month != null) params.month = selection.month;
      if (selection.quarter != null) params.quarter = selection.quarter;
    }
    return api
      .get('/stats/manager/lawyers', { params, signal })
      .then((r) => (Array.isArray(r.data) ? r.data : []));
  }, [period, selection], { enabled: userReady && isHeadOrSubHead && !headMissingBranch });
  const lawyers = useMemo(() => lawyersQuery.data ?? [], [lawyersQuery.data]);

  useEffect(() => {
    setSelection(mostRecentSelection(available, period));
  }, [available, period]);

  return (
    <div className="max-w-7xl mx-auto">
      <h2 className="text-xl sm:text-2xl font-bold text-gray-900 mb-6 text-balance">الإحصائيات</h2>
      {headMissingBranch ? null : (
        <LawyerStatsSection
          period={period}
          onPeriodChange={setPeriod}
          availablePeriods={available}
          selection={selection}
          onSelectionChange={setSelection}
          stats={statsQuery.data ?? null}
          prevStats={prevStatsQuery.data ?? null}
          appealsStats={isHeadOrSubHead ? null : (statsQuery.data?.appeals ?? null)}
          error={statsQuery.error ?? ''}
          showDrillLinks={!isHeadOrSubHead}
          sectionLabel={isHeadOrSubHead ? (isSubHead ? 'إحصائيات الشعبة' : 'إحصائيات القسم') : 'إحصائيات المحامي'}
          sectionId={isHeadOrSubHead ? 'branch-stats' : 'lawyer-stats'}
        />
      )}
      {isHeadOrSubHead ? (
        <div className="mt-3 sm:mt-4">
          <LawyersTable
            showTable
            branchId={headMissingBranch ? null : (user?.branchId ?? null)}
            lawyers={lawyers}
            error={lawyersQuery.error ?? ''}
            noBranchMessage={noBranchMessage}
          />
        </div>
      ) : null}
    </div>
  );
}

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, getApiErrorMessage, getDownloadErrorMessage } from '../api/client';
import { useAuth } from '../auth/useAuth';
import { isHeadOrSubHeadRole } from '../auth/roles';
import SpinnerIcon from '../components/SpinnerIcon';
import { downloadBlob } from '../utils/download';
import type { BranchDto, CircuitStatsDto } from '../types';

/**
 * صفحة «إحصائيات الدوائر» للمدير/المشرف (منتقي فرع + جدول الدوائر)
 * ورئيس القسم/الشعبة (نطاقه فقط — بلا منتقي إذ يُتجاهل اختياره خادميًا).
 * لكل دائرة: ملفات × محامون نشطون × معلقات × الشعبة المالكة (§12).
 *
 * `P6-01b`: أول شريحة `TanStack Query` — القائمة والإحصاءات استعلامان مخزَّنان
 * بمفتاحيهما (`branches`، و`circuit-stats` + الفرع) بدل `useEffect` يدوي، مع
 * `placeholderData` لإبقاء الصفوف السابقة أثناء تبديل الفرع (سلوك الشاشة القائم).
 */
export default function CircuitStatsPage() {
  const { user } = useAuth();
  const isHeadOrSubHead = isHeadOrSubHeadRole(user?.role);
  const isSubHead = user?.role === 'subhead';
  const [branchId, setBranchId] = useState<number | ''>('');
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState('');

  const branchesQuery = useQuery({
    queryKey: ['branches'],
    queryFn: async () => {
      const r = await api.get<BranchDto[]>('/branches');
      return Array.isArray(r?.data) ? r.data : [];
    },
    enabled: !isHeadOrSubHead,
  });
  // رئيس القسم/الشعبة لا يرى منتقي الفروع أصلًا (نطاقه خادمي)، فلا نعرض بياناته.
  const branches = isHeadOrSubHead ? [] : (branchesQuery.data ?? []);

  const statsQuery = useQuery({
    queryKey: ['circuit-stats', branchId],
    queryFn: async () => {
      const r = await api.get<CircuitStatsDto[]>('/execution-circuits/stats', {
        params: branchId === '' ? {} : { branchId },
      });
      return r.data ?? [];
    },
    // إبقاء صفوف الفرع السابق أثناء جلب الجديد كما كان (`setLoading` يخفيها فقط).
    placeholderData: (previous) => previous,
  });
  const rows = statsQuery.data ?? [];
  const loading = statsQuery.isFetching;
  const error = statsQuery.error ? getApiErrorMessage(statsQuery.error) : '';

  const fmt = (n: number) => new Intl.NumberFormat('ar-SY').format(n);

  const exportStats = async () => {
    if (exporting) return;
    setExporting(true);
    setExportError('');
    try {
      const r = await api.get('/execution-circuits/stats/export', {
        params: branchId === '' ? {} : { branchId },
        responseType: 'blob',
      });
      downloadBlob(r.data as Blob, 'إحصاءات الدوائر.xlsx');
    } catch (err) {
      // أخطاء التنزيل تصل `Blob` (حتى JSON الخطأ) فيُستخرج نصها أولًا —
      // وإلا ضاعت رسالة الخادم الدقيقة (كـ429 «تصدير قيد التنفيذ»).
      setExportError(await getDownloadErrorMessage(err));
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="max-w-6xl mx-auto">
      <div className="flex items-center justify-between gap-3 flex-wrap mb-6">
        <h2 className="text-2xl font-bold text-gray-800 text-balance">إحصائيات الدوائر</h2>
        <button
          type="button"
          onClick={exportStats}
          disabled={exporting || loading}
          className="min-h-11 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-40 inline-flex items-center gap-2"
        >
          {exporting ? (
            <>
              <SpinnerIcon />
              جارِ التصدير…
            </>
          ) : (
            'تصدير Excel'
          )}
        </button>
      </div>
      {exporting && (
        <p role="status" className="text-xs text-gray-500 mb-4">
          التصدير الكبير قد يستغرق دقائق — لا تغلق الصفحة
        </p>
      )}
      {!isHeadOrSubHead && (
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div>
          <label htmlFor="stats-branch" className="block text-xs font-bold text-gray-600 mb-1">
            الفرع
          </label>
          <select
            id="stats-branch"
            value={branchId}
            onChange={(e) => {
              setBranchId(e.target.value === '' ? '' : Number(e.target.value));
              setExportError('');
            }}
            className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500"
          >
            <option value="">كل الفروع</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </div>
      </div>
      )}
      {isHeadOrSubHead && (
        <p className="text-sm text-gray-500 mb-4">
          {isSubHead ? 'إحصائيات دوائر شعبتك' : 'إحصائيات دوائر قسمك'}
        </p>
      )}
      {(error || exportError) && (
        <div className="bg-red-50 text-red-700 border border-red-200 rounded-lg p-3 mb-4" role="alert">
          {error || exportError}
        </div>
      )}
      {loading ? (
        <p className="text-gray-500 py-8 text-center">جارِ التحميل…</p>
      ) : rows.length === 0 ? (
        <p className="text-gray-500 py-8 text-center">لا توجد دوائر</p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 text-gray-600">
                <th className="px-4 py-3 text-start font-bold">الدائرة</th>
                <th className="px-4 py-3 text-start font-bold">الفرع</th>
                <th className="px-4 py-3 text-start font-bold">الشعبة</th>
                <th className="px-4 py-3 text-start font-bold tabular-nums">الملفات</th>
                <th className="px-4 py-3 text-start font-bold tabular-nums">المحامون النشطون</th>
                <th className="px-4 py-3 text-start font-bold tabular-nums">المعلقات</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.circuitId} className="border-t border-gray-100">
                  <td className="px-4 py-3 font-medium text-gray-800 break-words">
                    {r.circuitName}
                    {!r.isActive && <span className="ms-2 text-xs text-gray-500">(معطلة)</span>}
                  </td>
                  <td className="px-4 py-3">{r.circuitId === 0 && r.branchName == null ? 'كل الفروع' : (r.branchName ?? '—')}</td>
                  <td className="px-4 py-3 break-words">{r.sectionName ?? 'القسم'}</td>
                  <td className="px-4 py-3 tabular-nums">{fmt(r.fileCount)}</td>
                  <td className="px-4 py-3 tabular-nums">{fmt(r.lawyerCount)}</td>
                  <td className="px-4 py-3 tabular-nums">{fmt(r.pendingCount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

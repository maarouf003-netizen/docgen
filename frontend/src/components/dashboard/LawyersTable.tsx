import type { ManagerLawyerStatDto } from '../../types';
import { MONTHS } from './dashboardFormat';

/**
 * جدول «إحصائيات محامي الفرع» — مستخرج من `ManagerStatsSection` بلا تغيير
 * بصري: يُعرض مع `branchId`، ورسالة «اختر فرعًا» بدونه، وحاشية الوسم معلنة.
 */
export function LawyersTable({
  showTable,
  branchId,
  lawyers,
  error = '',
  noBranchMessage = 'اختر فرعًا لعرض إحصائيات محامي الفرع',
}: {
  showTable: boolean;
  branchId: number | null;
  lawyers: ManagerLawyerStatDto[];
  /** خطأ جلب الجدول — يُعرض بدل العبارة المضللة «لا يوجد محامون». */
  error?: string;
  /** رسالة غياب الفرع — للرئيس بلا فرع رسالة مختلفة (لا يملك منتقي فرع). */
  noBranchMessage?: string;
}) {
  // مجموع ملفات جدول المحامين المحسوبة بتاريخ إدخالها (وسم المصدر) — يُعرض معلنًا.
  const lawyerFallbackTotal = showTable
    ? lawyers.reduce(
        (sum, l) => sum + l.points.reduce((s, p) => s + (p.fromCreatedAtCount ?? 0), 0),
        0,
      )
    : 0;

  if (showTable && branchId) {
    if (error) {
      return (
        <p role="alert" className="text-sm text-red-700 mb-6">
          {error}
        </p>
      );
    }
    return (
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-4 sm:p-5">
        <h3 className="font-bold text-gray-800 mb-4">إحصائيات محامي الفرع</h3>
        {lawyers.length === 0 ? (
          <p className="text-gray-400 text-sm">لا يوجد محامون في هذا الفرع</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead>
                <tr className="text-gray-500 border-b border-gray-100">
                  <th className="text-right font-medium py-2.5 px-3">المحامي</th>
                  <th className="text-right font-medium py-2.5 px-3">المجموع</th>
                  {lawyers[0]?.points.map((p) => (
                    <th key={`${p.year}-${p.month}`} className="text-right font-medium py-2.5 px-3">
                      {MONTHS[p.month - 1]}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {lawyers.map((l) => (
                  <tr key={l.lawyerId}>
                    <td className="py-2.5 px-3 font-medium text-gray-800 whitespace-nowrap">{l.lawyerName}</td>
                    <td className="py-2.5 px-3 text-gray-900 tabular-nums">{l.totalCount}</td>
                    {l.points.map((p) => (
                      <td key={`${p.year}-${p.month}`} className="py-2.5 px-3 text-gray-600 tabular-nums">
                        {p.count}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {lawyerFallbackTotal > 0 ? (
          <p className="text-xs text-amber-700 mt-3">
            منها <span className="font-bold tabular-nums" dir="ltr">({lawyerFallbackTotal})</span> محسوبة
            بتاريخ الإدخال لغياب تاريخ قيدها أو تعذّر تحليله
          </p>
        ) : null}
      </div>
    );
  }

  if (showTable) {
    return <p className="text-sm text-gray-400 mb-6">{noBranchMessage}</p>;
  }

  return null;
}

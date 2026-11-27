import { useIsMobile } from '../../hooks/useMediaQuery';
import type { BranchDto } from '../../types';

function statusBadge(b: BranchDto) {
  return (
    <span
      className={`inline-block rounded-full px-2 py-0.5 text-xs ${
        (b.isActive ?? true) ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'
      }`}
    >
      {(b.isActive ?? true) ? 'مفعّل' : 'موقوف'}
    </span>
  );
}

function usageLine(b: BranchDto) {
  return `${b.userCount ?? 0} مستخدم · ${b.documentCount ?? 0} مستند`;
}

/**
 * عرض الفروع: جدول على المكتبي وبطاقات على الجوال (نفس نمط `DocumentsList`)،
 * مع الحالة والاستخدام وزر التعديل. لا منطق بيانات هنا.
 */
export function BranchesTable({
  branches,
  loading,
  onEdit,
}: {
  branches: BranchDto[];
  loading: boolean;
  onEdit: (b: BranchDto) => void;
}) {
  const isMobile = useIsMobile();

  return (
    <div className="bg-white rounded-xl shadow overflow-hidden">
      {!isMobile && (
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-gray-600">
            <tr className="text-right">
              <th className="px-4 py-3">الاسم</th>
              <th className="px-4 py-3">الكود</th>
              <th className="px-4 py-3">المحافظة</th>
              <th className="px-4 py-3">العنوان</th>
              <th className="px-4 py-3">الاستخدام</th>
              <th className="px-4 py-3">الحالة</th>
              <th className="px-4 py-3">إجراء</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {branches.map((b) => (
              <tr key={b.id} className="hover:bg-gray-50">
                <td className="px-4 py-3 font-medium text-gray-800">{b.name}</td>
                <td className="px-4 py-3">{b.code}</td>
                <td className="px-4 py-3 text-gray-500">{b.governorate || '—'}</td>
                <td className="px-4 py-3 text-gray-500">{b.address || '—'}</td>
                <td className="px-4 py-3 text-gray-500">{usageLine(b)}</td>
                <td className="px-4 py-3">{statusBadge(b)}</td>
                <td className="px-4 py-3">
                  <button
                    onClick={() => onEdit(b)}
                    className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11"
                  >
                    تعديل
                  </button>
                </td>
              </tr>
            ))}
            {!loading && branches.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-gray-400">لا توجد فروع</td>
              </tr>
            )}
          </tbody>
        </table>
      )}

      {isMobile && (
        <div className="divide-y divide-gray-100">
          {!loading && branches.length === 0 && (
            <div className="px-4 py-8 text-center text-gray-400">لا توجد فروع</div>
          )}
          {branches.map((b) => (
            <div key={b.id} className="p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="font-bold text-gray-800">
                  {b.name} <span className="text-gray-400 font-normal">({b.code})</span>
                </div>
                {statusBadge(b)}
              </div>
              <div className="text-sm text-gray-600 mt-1">
                {b.governorate ? `${b.governorate} · ` : ''}{b.address || '—'} · {usageLine(b)}
              </div>
              <button
                onClick={() => onEdit(b)}
                className="mt-3 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 text-xs min-h-11"
              >
                تعديل
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

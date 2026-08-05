import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { api, getApiErrorMessage } from '../api/client';

interface AuditLogDto {
  id: number;
  timestamp: string;
  userName: string | null;
  actionType: string | null;
  details: string | null;
  documentId: number | null;
  documentType: string | null;
}

interface Paged<T> {
  items: T[];
  page: number;
  perPage: number;
  totalCount: number;
}

const ACTION_LABELS: Record<string, string> = {
  login: 'تسجيل دخول',
  login_failed: 'محاولة دخول فاشلة',
  login_locked: 'قفل الحساب',
  change_password: 'تغيير كلمة مرور',
  create: 'إنشاء مستند',
  update: 'تعديل مستند',
  delete: 'حذف مستند',
  restore: 'استعادة مستند',
  status: 'تغيير حالة',
  action: 'إجراء/ملاحظة',
  transfer: 'نقل ملف',
  create_user: 'إنشاء مستخدم',
  update_user: 'تعديل مستخدم',
};

export default function AuditLogs() {
  const [rows, setRows] = useState<AuditLogDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [userName, setUserName] = useState('');
  const [actionType, setActionType] = useState('');
  const [error, setError] = useState('');
  const perPage = 20;

  const load = useCallback(() => {
    const params = new URLSearchParams({ page: String(page), perPage: String(perPage) });
    if (userName) params.set('userName', userName);
    if (actionType) params.set('actionType', actionType);

    api
      .get<Paged<AuditLogDto>>(`/audit-logs?${params.toString()}`)
      .then((r) => {
        setRows(r.data.items);
        setTotal(r.data.totalCount);
      })
      .catch((err) => setError(getApiErrorMessage(err)));
  }, [page, userName, actionType]);

  useEffect(() => {
    load();
  }, [load]);

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setPage(1);
  };

  const pages = Math.max(1, Math.ceil(total / perPage));

  return (
    <div className="max-w-5xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">سجل التدقيق</h2>

      <form onSubmit={submit} className="bg-white rounded-xl shadow p-4 mb-6 flex flex-wrap gap-3 items-end">
        <div className="flex flex-col">
          <label className="text-sm text-gray-600 mb-1">اسم المستخدم</label>
          <input
            value={userName}
            onChange={(e) => setUserName(e.target.value)}
            className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none"
          />
        </div>
        <div className="flex flex-col">
          <label className="text-sm text-gray-600 mb-1">نوع الحدث</label>
          <select
            value={actionType}
            onChange={(e) => setActionType(e.target.value)}
            className="min-h-11 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none"
          >
            <option value="">الكل</option>
            {Object.entries(ACTION_LABELS).map(([k, v]) => (
              <option key={k} value={k}>{v}</option>
            ))}
          </select>
        </div>
        <button
          type="submit"
          className="bg-emerald-800 hover:bg-emerald-700 text-white text-sm font-bold rounded-lg px-4 py-2 min-h-11"
        >
          بحث
        </button>
      </form>

      {error && <div className="text-red-600 mb-4">{error}</div>}

      <div className="bg-white rounded-xl shadow overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-gray-600">
            <tr className="text-right">
              <th className="px-4 py-3">الوقت</th>
              <th className="px-4 py-3">المستخدم</th>
              <th className="px-4 py-3">الحدث</th>
              <th className="px-4 py-3">التفاصيل</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {rows.map((r) => (
              <tr key={r.id} className="hover:bg-gray-50">
                <td className="px-4 py-3 whitespace-nowrap">
                  {new Date(r.timestamp).toLocaleString('ar-SY')}
                </td>
                <td className="px-4 py-3">{r.userName}</td>
                <td className="px-4 py-3">
                  {r.actionType ? ACTION_LABELS[r.actionType] ?? r.actionType : ''}
                </td>
                <td className="px-4 py-3 text-gray-600">{r.details}</td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-gray-400">لا توجد سجلات</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between mt-4 text-sm text-gray-600">
        <span>إجمالي السجلات: {total}</span>
        <div className="flex gap-2">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="border border-gray-300 rounded-lg px-3 py-1.5 disabled:opacity-40 min-h-11"
          >
            السابق
          </button>
          <span className="px-3 py-1.5">صفحة {page} من {pages}</span>
          <button
            onClick={() => setPage((p) => Math.min(pages, p + 1))}
            disabled={page >= pages}
            className="border border-gray-300 rounded-lg px-3 py-1.5 disabled:opacity-40 min-h-11"
          >
            التالي
          </button>
        </div>
      </div>
    </div>
  );
}

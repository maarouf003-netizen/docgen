import { api } from '../api/client';
import { useCancellableRequest } from '../hooks/useCancellableRequest';

interface UserActivityDto {
  username: string;
  fullName: string;
  documentCount: number;
  viewCount: number;
}

export default function UsersActivity() {
  const activityQuery = useCancellableRequest<UserActivityDto[]>(
    (signal) => api.get('/users/activity', { signal }).then((r) => r.data),
    [],
  );
  const rows = activityQuery.data ?? [];
  const error = activityQuery.error;

  if (error) return <div role="alert" className="text-red-600 mb-6">{error}</div>;

  return (
    <div className="max-w-4xl mx-auto">
      <h2 className="text-2xl font-bold text-gray-800 mb-6">نشاط المستخدمين</h2>
      <div className="bg-white rounded-xl shadow overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[36rem] text-sm">
            <thead className="bg-gray-50 text-gray-600">
              <tr className="text-right">
                <th scope="col" className="px-4 py-3">الاسم</th>
                <th scope="col" className="px-4 py-3">اسم المستخدم</th>
                <th scope="col" className="px-4 py-3">عدد المستندات</th>
                <th scope="col" className="px-4 py-3">إجمالي المشاهدات</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {rows.map((r) => (
                <tr key={r.username} className="hover:bg-gray-50">
                  <td className="px-4 py-3">{r.fullName}</td>
                  <td className="px-4 py-3 whitespace-nowrap">{r.username}</td>
                  <td className="px-4 py-3 tabular-nums whitespace-nowrap">{r.documentCount}</td>
                  <td className="px-4 py-3 tabular-nums whitespace-nowrap">{r.viewCount}</td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-gray-400">لا توجد بيانات</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

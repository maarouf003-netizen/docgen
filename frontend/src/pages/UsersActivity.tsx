import { useEffect, useState } from 'react';
import { api, getApiErrorMessage } from '../api/client';

interface UserActivityDto {
  username: string;
  fullName: string;
  documentCount: number;
  viewCount: number;
}

export default function UsersActivity() {
  const [rows, setRows] = useState<UserActivityDto[]>([]);
  const [error, setError] = useState('');

  useEffect(() => {
    api
      .get<UserActivityDto[]>('/users/activity')
      .then((r) => setRows(r.data))
      .catch((err) => setError(getApiErrorMessage(err)));
  }, []);

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

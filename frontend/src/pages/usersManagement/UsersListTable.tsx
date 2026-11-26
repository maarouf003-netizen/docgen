import { ROLE_LABELS } from '../../auth/roleLabels';
import type { UserListItem } from '../../types';

interface UsersListTableProps {
  users: UserListItem[];
  loading: boolean;
  isMobile: boolean;
  isManager: boolean;
  onEdit: (user: UserListItem) => void;
}

/** جدول المستخدمين (مكتبي) وبطاقاتهم (جوال) — منقول حرفيًا من `UsersManagement`. */
export function UsersListTable({ users, loading, isMobile, isManager, onEdit }: UsersListTableProps) {
  return (
    <div className="bg-white rounded-xl shadow overflow-hidden">
      {!isMobile && (
        <table className="w-full text-sm">
          <thead className="bg-gray-50 text-gray-600">
            <tr className="text-right">
              <th className="px-4 py-3">الاسم</th>
              <th className="px-4 py-3">اسم المستخدم</th>
              <th className="px-4 py-3">الدور</th>
              <th className="px-4 py-3">الفرع</th>
              <th className="px-4 py-3">الشعبة</th>
              <th className="px-4 py-3">الحالة</th>
              <th className="px-4 py-3">إجراء</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {users.map((u) => (
              <tr key={u.id} className="hover:bg-gray-50">
                <td className="px-4 py-3 font-medium text-gray-800">{u.fullName}</td>
                <td className="px-4 py-3">{u.username}</td>
                <td className="px-4 py-3">{ROLE_LABELS[u.role] ?? u.role}</td>
                <td className="px-4 py-3">{u.branchName || '—'}</td>
                <td className="px-4 py-3 break-words">{u.sectionName ?? '—'}</td>
                <td className="px-4 py-3">
                  <span
                    className={`inline-block rounded-full px-2 py-0.5 text-xs ${
                      u.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'
                    }`}
                  >
                    {u.isActive ? 'مفعّل' : 'موقوف'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  {isManager && u.role === 'admin' ? (
                    <span className="text-xs text-gray-400">مشرف</span>
                  ) : (
                    <button
                      onClick={() => onEdit(u)}
                      className="text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-1.5 text-xs min-h-11"
                    >
                      تعديل
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {!loading && users.length === 0 && (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-gray-400">لا يوجد مستخدمون</td>
              </tr>
            )}
          </tbody>
        </table>
      )}

      {isMobile && (
        <div className="divide-y divide-gray-100">
          {!loading && users.length === 0 && (
            <div className="px-4 py-8 text-center text-gray-400">لا يوجد مستخدمون</div>
          )}
          {users.map((u) => (
            <div key={u.id} className="p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="font-bold text-gray-800">{u.fullName}</div>
                <span
                  className={`rounded-full px-2 py-0.5 text-xs ${
                    u.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-200 text-gray-600'
                  }`}
                >
                  {u.isActive ? 'مفعّل' : 'موقوف'}
                </span>
              </div>
              <div className="text-sm text-gray-600 mt-1">
                {u.username} · {ROLE_LABELS[u.role] ?? u.role}
                {u.branchName ? <span className="text-gray-400"> · {u.branchName}</span> : null}
                {u.sectionName ? <span className="text-gray-400"> · {u.sectionName}</span> : null}
              </div>
              {isManager && u.role === 'admin' ? (
                <span className="mt-3 inline-block text-xs text-gray-400">مشرف</span>
              ) : (
                <button
                  onClick={() => onEdit(u)}
                  className="mt-3 text-sky-700 hover:bg-sky-50 rounded-lg px-3 py-2 text-xs min-h-11"
                >
                  تعديل
                </button>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

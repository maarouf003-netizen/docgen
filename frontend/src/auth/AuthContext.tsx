import { useEffect, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import type { LoginBranchSelectionResponse, LoginResponse, UserDto } from '../types';
import { AuthContext } from './auth-context';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  // الجلسة Cookie مصادقة HttpOnly يرسلها المتصفح تلقائيًا؛ يُسترجَع المستخدم دائمًا
  // من /auth/me دون أي تخزين محلي للتوكن. 401 تعني عدم وجود جلسة = غير مسجّل دخول.
  useEffect(() => {
    api
      .get<UserDto>('/auth/me')
      .then((res) => setUser(res.data))
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  const login = async (
    username: string,
    password: string,
    branchId?: number | null,
  ): Promise<LoginResponse | LoginBranchSelectionResponse> => {
    const res = await api.post<LoginResponse | LoginBranchSelectionResponse>('/auth/login', {
      username,
      password,
      branchId,
    });
    if (res.data && 'requiresBranchSelection' in res.data) return res.data;
    const loginRes = res.data as LoginResponse;
    setUser(loginRes.user);
    return loginRes;
  };

  const logout = () => {
    // حذف Cookie الجلسة خادميًا ثم العودة لصفحة الدخول (الملاحة بإعادة تحميل تنظّف الحالة).
    void api.post('/auth/logout').finally(() => {
      setUser(null);
      window.location.href = '/login?logged_out=1';
    });
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        loading,
        login,
        logout,
        hasFullAccess: user?.role === 'manager' || user?.role === 'admin',
        isHead: user?.role === 'head',
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

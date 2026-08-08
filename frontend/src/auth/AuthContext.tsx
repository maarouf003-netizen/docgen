import { useEffect, useState, type ReactNode } from 'react';
import { api, setToken, getToken } from '../api/client';
import type { LoginBranchSelectionResponse, LoginResponse, UserDto } from '../types';
import { AuthContext } from './auth-context';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = getToken();
    if (!token) {
      setLoading(false);
      return;
    }
    api
      .get<UserDto>('/auth/me')
      .then((res) => setUser(res.data))
      .catch(() => setToken(null))
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
    setToken(loginRes.token);
    setUser(loginRes.user);
    return loginRes;
  };

  const logout = () => {
    setToken(null);
    setUser(null);
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

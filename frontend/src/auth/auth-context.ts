import { createContext } from 'react';
import type { LoginBranchSelectionResponse, LoginResponse, UserDto } from '../types';

export interface AuthContextValue {
  user: UserDto | null;
  loading: boolean;
  login: (
    username: string,
    password: string,
    branchId?: number | null,
  ) => Promise<LoginResponse | LoginBranchSelectionResponse>;
  logout: () => void;
  hasFullAccess: boolean;
  isHead: boolean;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

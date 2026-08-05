import { createContext } from 'react';
import type { UserDto } from '../types';

export interface AuthContextValue {
  user: UserDto | null;
  loading: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
  hasFullAccess: boolean;
  isHead: boolean;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

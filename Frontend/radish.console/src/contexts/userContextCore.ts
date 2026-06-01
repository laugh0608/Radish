import { createContext } from 'react';
import type { UserInfo } from '../types/user';

export interface UserContextValue {
  user: UserInfo | null;
  loading: boolean;
  refreshUser: () => Promise<void>;
}

export const UserContext = createContext<UserContextValue | undefined>(undefined);

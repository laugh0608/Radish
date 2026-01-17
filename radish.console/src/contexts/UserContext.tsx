import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import { log } from '@/utils/logger';
import { userApi } from '../api/user';
import { tokenService } from '../services/tokenService';
import type { UserInfo } from '../types/user';

interface UserContextValue {
  user: UserInfo | null;
  loading: boolean;
  refreshUser: () => Promise<void>;
}

const UserContext = createContext<UserContextValue | undefined>(undefined);

export interface UserProviderProps {
  children: ReactNode;
}

/**
 * 用户信息提供者
 *
 * 管理当前登录用户的信息
 */
export function UserProvider({ children }: UserProviderProps) {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [loading, setLoading] = useState(true);

  const refreshUser = async () => {
    const token = tokenService.getAccessToken();
    if (!token) {
      setUser(null);
      setLoading(false);
      return;
    }

    try {
      const response = await userApi.getCurrentUser();
      if (response.ok && response.data) {
        setUser(response.data);
      } else {
        setUser(null);
      }
    } catch (error) {
      log.error('UserContext', 'Failed to fetch user info:', error);
      setUser(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refreshUser();
  }, []);

  return (
    <UserContext.Provider value={{ user, loading, refreshUser }}>
      {children}
    </UserContext.Provider>
  );
}

/**
 * 使用用户信息 Hook
 */
export function useUser() {
  const context = useContext(UserContext);
  if (context === undefined) {
    throw new Error('useUser must be used within a UserProvider');
  }
  return context;
}

import { useContext } from 'react';
import { UserContext } from '../contexts/userContextCore';

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

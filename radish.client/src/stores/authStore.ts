import { create } from 'zustand';
import { tokenService } from '@/services/tokenService';
import { useUserStore } from './userStore';
import { useNotificationStore } from './notificationStore';
import { log } from '@/utils/logger';

interface AuthStore {
  /** 是否已认证 */
  isAuthenticated: boolean;

  /** 是否正在检查认证状态 */
  isCheckingAuth: boolean;

  /** 检查认证状态（从 Token 和用户信息判断） */
  checkAuthStatus: () => Promise<void>;

  /** 登出（清除所有状态） */
  logout: () => Promise<void>;

  /** 设置认证状态 */
  setAuthenticated: (value: boolean) => void;
}

export const useAuthStore = create<AuthStore>((set, get) => ({
  isAuthenticated: false,
  isCheckingAuth: false,

  checkAuthStatus: async () => {
    set({ isCheckingAuth: true });

    try {
      // 检查是否有有效的 Token
      const token = await tokenService.getValidAccessToken();
      const hasValidToken = !!token;

      // 检查是否有用户信息
      const userStore = useUserStore.getState();
      const hasUserInfo = userStore.isAuthenticated();

      // 只有同时满足两个条件才认为已认证
      const isAuthenticated = hasValidToken && hasUserInfo;

      log.debug('AuthStore', '认证状态检查:', {
        hasValidToken,
        hasUserInfo,
        isAuthenticated
      });

      set({ isAuthenticated });
    } catch (error) {
      log.error('AuthStore', '检查认证状态失败:', error);
      set({ isAuthenticated: false });
    } finally {
      set({ isCheckingAuth: false });
    }
  },

  logout: async () => {
    log.info('AuthStore', '执行登出');

    // 1. 清除 Token
    tokenService.clearTokens();

    // 2. 清除用户信息
    const userStore = useUserStore.getState();
    userStore.clearUser();

    // 3. 清除通知
    const notificationStore = useNotificationStore.getState();
    notificationStore.clearRecentNotifications();
    notificationStore.setUnreadCount(0);

    // 4. 更新认证状态
    set({ isAuthenticated: false });

    log.info('AuthStore', '登出完成');
  },

  setAuthenticated: (value: boolean) => {
    log.debug('AuthStore', '设置认证状态:', value);
    set({ isAuthenticated: value });
  }
}));

import { create } from 'zustand';
import type { UserInfo } from '@/desktop/types';

interface UserStore extends UserInfo {
  /** 设置用户信息 */
  setUser: (user: UserInfo) => void;

  /** 清除用户信息 */
  clearUser: () => void;

  /** 是否已登录 */
  isAuthenticated: () => boolean;
}

const defaultUser: UserInfo = {
  userId: 0,
  userName: '',
  tenantId: 0,
  roles: []
};

export const useUserStore = create<UserStore>((set, get) => ({
  ...defaultUser,

  setUser: (user: UserInfo) => {
    set({
      userId: user.userId,
      userName: user.userName,
      tenantId: user.tenantId,
      roles: user.roles || []
    });
  },

  clearUser: () => {
    set(defaultUser);
  },

  isAuthenticated: () => {
    const { userId } = get();
    return userId > 0;
  }
}));

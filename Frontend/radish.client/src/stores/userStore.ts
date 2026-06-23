import { create } from 'zustand';
import type { UserInfo } from '@/desktop/types';
import { normalizePositiveLongIdKey } from '@/utils/longId';

interface UserStore extends UserInfo {
  /** 设置用户信息 */
  setUser: (user: UserInfo) => void;

  /** 清除用户信息 */
  clearUser: () => void;

  /** 是否已登录 */
  isAuthenticated: () => boolean;
}

const defaultUser: UserInfo = {
  userId: '',
  userName: '',
  displayHandle: '',
  publicId: '',
  publicIndex: '',
  nickname: '',
  tenantId: '0',
  roles: [],
  permissions: []
};

export const useUserStore = create<UserStore>((set, get) => ({
  ...defaultUser,

  setUser: (user: UserInfo) => {
    const current = get();
    set({
      userId: user.userId,
      userName: user.userName,
      displayHandle: user.displayHandle ?? current.displayHandle,
      publicId: user.publicId ?? current.publicId,
      publicIndex: user.publicIndex ?? current.publicIndex,
      nickname: user.nickname ?? current.nickname,
      tenantId: user.tenantId,
      roles: user.roles || [],
      permissions: user.permissions || [],
      avatarUrl: user.avatarUrl,
      avatarThumbnailUrl: user.avatarThumbnailUrl
    });
  },

  clearUser: () => {
    set(defaultUser);
  },

  isAuthenticated: () => {
    const { userId } = get();
    return normalizePositiveLongIdKey(userId) !== null;
  }
}));

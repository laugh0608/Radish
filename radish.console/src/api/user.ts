import { apiGet } from '@radish/ui';
import type { ParsedApiResponse } from '@radish/ui';
import type { UserInfo } from '../types/user';

/**
 * 用户 API
 */
export const userApi = {
  /**
   * 获取当前登录用户信息
   */
  async getCurrentUser(): Promise<ParsedApiResponse<UserInfo>> {
    return apiGet<UserInfo>(
      '/api/v1/User/GetUserByHttpContext',
      { withAuth: true }
    );
  },
};

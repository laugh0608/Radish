import { apiGet, apiPost } from '@radish/http';
import type { ParsedApiResponse } from '@radish/http';
import type { UserInfo } from '../types/user';

/**
 * 用户 API
 */
export const userApi = {
  /**
   * 获取当前登录用户信息
   */
  async getCurrentUser(): Promise<ParsedApiResponse<UserInfo>> {
    const response = await apiGet<any>(
      '/api/v1/User/GetUserByHttpContext',
      { withAuth: true }
    );

    // 处理后端返回的 CurrentUserVo 结构，映射字段名
    if (response.ok && response.data) {
      const backendData = response.data;
      const mappedData: UserInfo = {
        voUserId: backendData.voUserId || backendData.VoUserId || 0,
        voUserName: backendData.voUserName || backendData.VoUserName || '',
        voTenantId: backendData.voTenantId || backendData.VoTenantId || 0,
        voAvatarUrl: backendData.voAvatarUrl || backendData.VoAvatarUrl,
        voAvatarThumbnailUrl: backendData.voAvatarThumbnailUrl || backendData.VoAvatarThumbnailUrl,
        roles: backendData.roles || ['Admin'], // 默认角色，实际应该从后端获取
      };

      return {
        ...response,
        data: mappedData
      };
    }

    return response as ParsedApiResponse<UserInfo>;
  },

  /**
   * 设置当前用户头像
   * @param attachmentId 附件ID（字符串类型的雪花ID，"0"表示清空头像）
   */
  async setMyAvatar(attachmentId: string): Promise<ParsedApiResponse<null>> {
    return await apiPost<null>(
      '/api/v1/User/SetMyAvatar',
      { AttachmentId: attachmentId },
      { withAuth: true }
    );
  },
};

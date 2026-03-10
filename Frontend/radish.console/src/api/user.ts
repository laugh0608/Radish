import { apiGet, apiPost } from '@radish/http';
import type { ParsedApiResponse } from '@radish/http';
import type { UserInfo } from '../types/user';

function toNumber(value: unknown): number {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }

  return 0;
}

function toRoles(value: unknown): string[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const roles = value
    .filter((item): item is string => typeof item === 'string')
    .map((item) => item.trim())
    .filter((item) => item.length > 0);

  return roles.length > 0 ? roles : undefined;
}

function toPermissions(value: unknown): string[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const permissions = value
    .filter((item): item is string => typeof item === 'string')
    .map((item) => item.trim())
    .filter((item) => item.length > 0);

  return permissions.length > 0 ? permissions : undefined;
}

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
      const resolvedRoles = toRoles(
        backendData.voRoles
        ?? backendData.VoRoles
        ?? backendData.roles
        ?? backendData.Roles
      );
      const resolvedPermissions = toPermissions(
        backendData.voPermissions
        ?? backendData.VoPermissions
        ?? backendData.permissions
        ?? backendData.Permissions
      );
      const mappedData: UserInfo = {
        voUserId: toNumber(backendData.voUserId ?? backendData.VoUserId),
        voUserName: backendData.voUserName || backendData.VoUserName || '',
        voTenantId: toNumber(backendData.voTenantId ?? backendData.VoTenantId),
        voAvatarUrl: backendData.voAvatarUrl || backendData.VoAvatarUrl,
        voAvatarThumbnailUrl: backendData.voAvatarThumbnailUrl || backendData.VoAvatarThumbnailUrl,
        roles: resolvedRoles,
        permissions: resolvedPermissions,
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

import { apiGet, apiPost } from '@radish/http';
import type { ParsedApiResponse } from '@radish/http';
import type { UserInfo } from '../types/user';

export interface MyProfileInfo {
  voUserId: number;
  voUserName: string;
  voUserEmail: string;
  voRealName: string;
  voSex: number;
  voAge: number;
  voBirth?: string | null;
  voAddress: string;
  voCreateTime: string;
  voAvatarAttachmentId?: number | string | null;
  voAvatarUrl?: string | null;
  voAvatarThumbnailUrl?: string | null;
}

export interface UpdateMyProfileRequest {
  userName?: string;
  userEmail?: string;
  realName?: string;
  sex?: number;
  age?: number;
  birth?: string | null;
  address?: string;
}

export interface UserTimePreferenceVo {
  voUserId: number;
  voTimeZoneId: string;
  voIsCustomized: boolean;
  voSystemDefaultTimeZoneId: string;
  voDisplayFormat: string;
  voModifyTime?: string | null;
}

export interface ChangeMyLoginPasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

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

function mapMyProfile(raw: any): MyProfileInfo {
  return {
    voUserId: toNumber(raw.voUserId ?? raw.VoUserId),
    voUserName: raw.voUserName ?? raw.VoUserName ?? '',
    voUserEmail: raw.voUserEmail ?? raw.VoUserEmail ?? '',
    voRealName: raw.voRealName ?? raw.VoRealName ?? '',
    voSex: toNumber(raw.voSex ?? raw.VoSex),
    voAge: toNumber(raw.voAge ?? raw.VoAge),
    voBirth: raw.voBirth ?? raw.VoBirth ?? null,
    voAddress: raw.voAddress ?? raw.VoAddress ?? '',
    voCreateTime: raw.voCreateTime ?? raw.VoCreateTime ?? '',
    voAvatarAttachmentId: raw.voAvatarAttachmentId ?? raw.VoAvatarAttachmentId,
    voAvatarUrl: raw.voAvatarUrl ?? raw.VoAvatarUrl,
    voAvatarThumbnailUrl: raw.voAvatarThumbnailUrl ?? raw.VoAvatarThumbnailUrl,
  };
}

function mapTimePreference(raw: any): UserTimePreferenceVo {
  return {
    voUserId: toNumber(raw.voUserId ?? raw.VoUserId),
    voTimeZoneId: raw.voTimeZoneId ?? raw.VoTimeZoneId ?? '',
    voIsCustomized: raw.voIsCustomized ?? raw.VoIsCustomized ?? false,
    voSystemDefaultTimeZoneId: raw.voSystemDefaultTimeZoneId ?? raw.VoSystemDefaultTimeZoneId ?? 'Asia/Shanghai',
    voDisplayFormat: raw.voDisplayFormat ?? raw.VoDisplayFormat ?? 'yyyy-MM-dd HH:mm:ss',
    voModifyTime: raw.voModifyTime ?? raw.VoModifyTime ?? null,
  };
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
   * 获取当前用户个人资料
   */
  async getMyProfile(): Promise<ParsedApiResponse<MyProfileInfo>> {
    const response = await apiGet<any>('/api/v1/User/GetMyProfile', { withAuth: true });

    if (response.ok && response.data) {
      return {
        ...response,
        data: mapMyProfile(response.data),
      };
    }

    return response as ParsedApiResponse<MyProfileInfo>;
  },

  /**
   * 更新当前用户个人资料
   */
  async updateMyProfile(request: UpdateMyProfileRequest): Promise<ParsedApiResponse<null>> {
    return await apiPost<null>('/api/v1/User/UpdateMyProfile', request, { withAuth: true });
  },

  /**
   * 获取当前用户时区偏好
   */
  async getMyTimePreference(): Promise<ParsedApiResponse<UserTimePreferenceVo>> {
    const response = await apiGet<any>('/api/v1/User/GetMyTimePreference', { withAuth: true });

    if (response.ok && response.data) {
      return {
        ...response,
        data: mapTimePreference(response.data),
      };
    }

    return response as ParsedApiResponse<UserTimePreferenceVo>;
  },

  /**
   * 更新当前用户时区偏好
   */
  async updateMyTimePreference(timeZoneId: string): Promise<ParsedApiResponse<UserTimePreferenceVo>> {
    const response = await apiPost<any>(
      '/api/v1/User/UpdateMyTimePreference',
      { timeZoneId },
      { withAuth: true }
    );

    if (response.ok && response.data) {
      return {
        ...response,
        data: mapTimePreference(response.data),
      };
    }

    return response as ParsedApiResponse<UserTimePreferenceVo>;
  },

  /**
   * 修改当前用户登录密码
   */
  async changeMyLoginPassword(request: ChangeMyLoginPasswordRequest): Promise<ParsedApiResponse<null>> {
    return await apiPost<null>('/api/v1/User/ChangeMyLoginPassword', request, { withAuth: true });
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

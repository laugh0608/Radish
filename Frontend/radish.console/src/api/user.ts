import { apiGet, apiPost } from '@radish/http';
import type { ParsedApiResponse } from '@radish/http';
import type { UserInfo } from '../types/user';

type ApiRecord = Record<string, unknown>;

export interface MyProfileInfo {
  voUserId: string;
  voPublicId?: string | null;
  voPublicIndex?: string | number | null;
  voDisplayName?: string | null;
  voDisplayHandle?: string | null;
  voUserName: string;
  voUserEmail: string;
  voSex: number;
  voAge: number;
  voBirth?: string | null;
  voAddress: string;
  voCreateTime: string;
  voAvatarAttachmentId?: string | null;
  voAvatarUrl?: string | null;
  voAvatarThumbnailUrl?: string | null;
}

export interface UpdateMyProfileRequest {
  userName?: string;
  userEmail?: string;
  sex?: number;
  age?: number;
  birth?: string | null;
  address?: string;
}

export interface UserTimePreferenceVo {
  voUserId: string;
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

function toStringValue(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback;
}

function toIdString(value: unknown): string {
  if (typeof value === 'string') {
    return value;
  }

  if (typeof value === 'number' && Number.isFinite(value)) {
    return String(value);
  }

  return '';
}

function toOptionalString(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function toNullableString(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

function toBoolean(value: unknown): boolean {
  return typeof value === 'boolean' ? value : false;
}

function toAttachmentId(value: unknown): string | null {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return String(value);
  }

  if (typeof value === 'string') {
    return value;
  }

  return null;
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

function isApiRecord(value: unknown): value is ApiRecord {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}

function toParsedResponse<T>(response: ParsedApiResponse<unknown>, data?: T): ParsedApiResponse<T> {
  return {
    ok: response.ok,
    data,
    message: response.message,
    code: response.code,
    statusCode: response.statusCode,
  };
}

function mapMyProfile(raw: ApiRecord): MyProfileInfo {
  return {
    voUserId: toIdString(raw.voUserId ?? raw.VoUserId),
    voPublicId: toNullableString(raw.voPublicId ?? raw.VoPublicId),
    voPublicIndex: toIdString(raw.voPublicIndex ?? raw.VoPublicIndex),
    voDisplayName: toNullableString(raw.voDisplayName ?? raw.VoDisplayName),
    voDisplayHandle: toNullableString(raw.voDisplayHandle ?? raw.VoDisplayHandle),
    voUserName: toStringValue(raw.voUserName ?? raw.VoUserName),
    voUserEmail: toStringValue(raw.voUserEmail ?? raw.VoUserEmail),
    voSex: toNumber(raw.voSex ?? raw.VoSex),
    voAge: toNumber(raw.voAge ?? raw.VoAge),
    voBirth: toNullableString(raw.voBirth ?? raw.VoBirth),
    voAddress: toStringValue(raw.voAddress ?? raw.VoAddress),
    voCreateTime: toStringValue(raw.voCreateTime ?? raw.VoCreateTime),
    voAvatarAttachmentId: toAttachmentId(raw.voAvatarAttachmentId ?? raw.VoAvatarAttachmentId),
    voAvatarUrl: toNullableString(raw.voAvatarUrl ?? raw.VoAvatarUrl),
    voAvatarThumbnailUrl: toNullableString(raw.voAvatarThumbnailUrl ?? raw.VoAvatarThumbnailUrl),
  };
}

function mapTimePreference(raw: ApiRecord): UserTimePreferenceVo {
  return {
    voUserId: toIdString(raw.voUserId ?? raw.VoUserId),
    voTimeZoneId: toStringValue(raw.voTimeZoneId ?? raw.VoTimeZoneId),
    voIsCustomized: toBoolean(raw.voIsCustomized ?? raw.VoIsCustomized),
    voSystemDefaultTimeZoneId: toStringValue(raw.voSystemDefaultTimeZoneId ?? raw.VoSystemDefaultTimeZoneId, 'Asia/Shanghai'),
    voDisplayFormat: toStringValue(raw.voDisplayFormat ?? raw.VoDisplayFormat, 'yyyy-MM-dd HH:mm:ss'),
    voModifyTime: toNullableString(raw.voModifyTime ?? raw.VoModifyTime),
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
    const response = await apiGet<ApiRecord>(
      '/api/v1/User/GetUserByHttpContext',
      { withAuth: true }
    );

    // 处理后端返回的 CurrentUserVo 结构，映射字段名
    if (response.ok && isApiRecord(response.data)) {
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
        voUserId: toIdString(backendData.voUserId ?? backendData.VoUserId),
        voDisplayName: toOptionalString(backendData.voDisplayName ?? backendData.VoDisplayName),
        voDisplayHandle: toOptionalString(backendData.voDisplayHandle ?? backendData.VoDisplayHandle),
        voUserName: toStringValue(backendData.voUserName ?? backendData.VoUserName),
        voTenantId: toIdString(backendData.voTenantId ?? backendData.VoTenantId),
        voAvatarUrl: toOptionalString(backendData.voAvatarUrl ?? backendData.VoAvatarUrl),
        voAvatarThumbnailUrl: toOptionalString(backendData.voAvatarThumbnailUrl ?? backendData.VoAvatarThumbnailUrl),
        roles: resolvedRoles,
        permissions: resolvedPermissions,
      };

      return toParsedResponse(response, mappedData);
    }

    return toParsedResponse(response);
  },

  /**
   * 获取当前用户个人资料
   */
  async getMyProfile(): Promise<ParsedApiResponse<MyProfileInfo>> {
    const response = await apiGet<ApiRecord>('/api/v1/User/GetMyProfile', { withAuth: true });

    if (response.ok && isApiRecord(response.data)) {
      return toParsedResponse(response, mapMyProfile(response.data));
    }

    return toParsedResponse(response);
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
    const response = await apiGet<ApiRecord>('/api/v1/User/GetMyTimePreference', { withAuth: true });

    if (response.ok && isApiRecord(response.data)) {
      return toParsedResponse(response, mapTimePreference(response.data));
    }

    return toParsedResponse(response);
  },

  /**
   * 更新当前用户时区偏好
   */
  async updateMyTimePreference(timeZoneId: string): Promise<ParsedApiResponse<UserTimePreferenceVo>> {
    const response = await apiPost<ApiRecord>(
      '/api/v1/User/UpdateMyTimePreference',
      { timeZoneId },
      { withAuth: true }
    );

    if (response.ok && isApiRecord(response.data)) {
      return toParsedResponse(response, mapTimePreference(response.data));
    }

    return toParsedResponse(response);
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

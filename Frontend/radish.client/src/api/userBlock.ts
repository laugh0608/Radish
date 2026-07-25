import {
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
  type ParsedApiResponse,
  type UserBlockMutationVo,
  type UserBlockPageVo,
} from '@radish/http';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

async function ensureOk<T>(
  request: Promise<ParsedApiResponse<T>>,
  fallbackMessage: string,
): Promise<T> {
  const response = await request;
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(
      response.messageKey ? response : { ...response, message: undefined },
      fallbackMessage,
    );
  }

  return response.data;
}

export async function blockUser(
  targetUserPublicId: string,
  operationKey: string,
  t: TFunction,
): Promise<UserBlockMutationVo> {
  return await ensureOk(
    apiPost<UserBlockMutationVo>(
      '/api/v1/UserBlock/Block',
      { targetUserPublicId, operationKey },
      { withAuth: true },
    ),
    t('userBlock.error.blockFailed'),
  );
}

export async function unblockUser(
  targetUserPublicId: string,
  operationKey: string,
  t: TFunction,
): Promise<UserBlockMutationVo> {
  return await ensureOk(
    apiPost<UserBlockMutationVo>(
      '/api/v1/UserBlock/Unblock',
      { targetUserPublicId, operationKey },
      { withAuth: true },
    ),
    t('userBlock.error.unblockFailed'),
  );
}

export async function getMyBlockedUsers(
  pageIndex: number,
  pageSize: number,
  t: TFunction,
): Promise<UserBlockPageVo> {
  const query = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });
  return await ensureOk(
    apiGet<UserBlockPageVo>(`/api/v1/UserBlock/GetMine?${query}`, { withAuth: true }),
    t('userBlock.error.listFailed'),
  );
}

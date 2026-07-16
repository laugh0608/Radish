/**
 * 附件上传相关的 API 调用
 */

import {
  ApiResponseError,
  apiDelete,
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
  getApiClientConfig,
  parseApiResponseWithI18n,
  type ApiClientConfig,
  type ApiResponse,
  type ParsedApiResponse,
} from '@radish/http';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

class UploadTransportError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'UploadTransportError';
  }
}

function toTranslationOptions(messageArguments?: readonly unknown[]): Record<number, unknown> {
  return Object.fromEntries(
    (messageArguments ?? []).map((value, index) => [index, value]),
  );
}

function resolveTranslation(
  key: string,
  t?: TFunction,
  messageArguments?: readonly unknown[],
  configuredTranslator = getApiClientConfig().translateMessage,
): string | undefined {
  const configuredMessage = configuredTranslator?.(key, messageArguments);
  if (configuredMessage && configuredMessage !== key) {
    return configuredMessage;
  }

  if (!t) {
    return undefined;
  }

  const callerMessage = t(key, toTranslationOptions(messageArguments));
  return typeof callerMessage === 'string' && callerMessage !== key
    ? callerMessage
    : undefined;
}

function resolveFallbackMessage(key: string, t?: TFunction): string {
  return resolveTranslation(key, t) ?? key;
}

function localizeResponse<T>(
  response: ParsedApiResponse<T>,
  t?: TFunction,
): ParsedApiResponse<T> {
  if (!response.messageKey) {
    return response;
  }

  const localizedMessage = resolveTranslation(
    response.messageKey,
    t,
    response.messageArguments,
  );
  return localizedMessage
    ? { ...response, message: localizedMessage }
    : response;
}

function requireResponseData<T>(
  response: ParsedApiResponse<T>,
  fallbackKey: string,
  t?: TFunction,
): T {
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(
      localizeResponse(response, t),
      resolveFallbackMessage(fallbackKey, t),
    );
  }

  return response.data;
}

function requireSuccessfulResponse(
  response: ParsedApiResponse<unknown>,
  fallbackKey: string,
  t?: TFunction,
): void {
  if (!response.ok) {
    throw createApiResponseError(
      localizeResponse(response, t),
      resolveFallbackMessage(fallbackKey, t),
    );
  }
}

function isApiResponseEnvelope<T>(value: unknown): value is ApiResponse<T> {
  return typeof value === 'object'
    && value !== null
    && typeof (value as ApiResponse<T>).isSuccess === 'boolean';
}

function getXhrStatus(xhr: XMLHttpRequest): number | undefined {
  return xhr.status > 0 ? xhr.status : undefined;
}

function getXhrTraceId(xhr: XMLHttpRequest): string | undefined {
  return xhr.getResponseHeader('x-correlation-id')?.trim() || undefined;
}

function parseXhrApiResponse<T>(
  xhr: XMLHttpRequest,
  t: TFunction,
  config: Readonly<ApiClientConfig>,
): ParsedApiResponse<T> | null {
  let json: unknown;
  try {
    json = JSON.parse(xhr.responseText) as unknown;
  } catch {
    return null;
  }

  if (!isApiResponseEnvelope<T>(json)) {
    return null;
  }

  const parsed = parseApiResponseWithI18n<T>(json, (key, messageArguments) => (
    resolveTranslation(key, t, messageArguments, config.translateMessage) ?? key
  ));

  return {
    ...parsed,
    statusCode: parsed.statusCode ?? getXhrStatus(xhr),
    httpStatus: getXhrStatus(xhr),
    traceId: parsed.traceId || getXhrTraceId(xhr),
    message: !json.messageInfo && !json.messageKey ? undefined : parsed.message,
  };
}

function createUnparsedXhrError(
  xhr: XMLHttpRequest,
  fallbackMessage: string,
): ApiResponseError {
  const status = getXhrStatus(xhr);
  return createApiResponseError({
    ok: false,
    statusCode: status,
    httpStatus: status,
    traceId: getXhrTraceId(xhr),
  }, fallbackMessage);
}

function uploadFormData<T>(options: {
  path: string;
  formData: FormData;
  onProgress?: (progress: number) => void;
  t: TFunction;
  failureKey: string;
  signal?: AbortSignal;
}): Promise<T> {
  const { path, formData, onProgress, t, failureKey, signal } = options;
  const config = getApiClientConfig();
  const url = `${getApiBaseUrl()}${path}`;
  const invalidResponseMessage = resolveTranslation(
    'attachment.api.invalidResponse',
    t,
    undefined,
    config.translateMessage,
  ) ?? 'attachment.api.invalidResponse';
  const networkErrorMessage = resolveTranslation(
    'attachment.api.networkError',
    t,
    undefined,
    config.translateMessage,
  ) ?? 'attachment.api.networkError';
  const cancelledMessage = resolveTranslation(
    'attachment.api.uploadCancelled',
    t,
    undefined,
    config.translateMessage,
  ) ?? 'attachment.api.uploadCancelled';
  const failureMessage = resolveTranslation(
    failureKey,
    t,
    undefined,
    config.translateMessage,
  ) ?? failureKey;

  return new Promise<T>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    let settled = false;

    const cleanup = () => {
      signal?.removeEventListener('abort', handleSignalAbort);
    };
    const resolveOnce = (value: T) => {
      if (settled) return;
      settled = true;
      cleanup();
      resolve(value);
    };
    const rejectOnce = (error: Error) => {
      if (settled) return;
      settled = true;
      cleanup();
      reject(error);
    };
    function handleSignalAbort() {
      xhr.abort();
    }

    if (signal?.aborted) {
      rejectOnce(new UploadTransportError(cancelledMessage));
      return;
    }

    signal?.addEventListener('abort', handleSignalAbort, { once: true });

    if (onProgress) {
      xhr.upload.addEventListener('progress', (event) => {
        if (event.lengthComputable) {
          onProgress(Math.round((event.loaded / event.total) * 100));
        }
      });
    }

    xhr.addEventListener('load', () => {
      if (xhr.status === 0) {
        rejectOnce(new UploadTransportError(networkErrorMessage));
        return;
      }

      const parsed = parseXhrApiResponse<T>(xhr, t, config);
      if (!parsed) {
        rejectOnce(createUnparsedXhrError(xhr, invalidResponseMessage));
        return;
      }

      if (xhr.status >= 200 && xhr.status < 300 && parsed.ok && parsed.data !== undefined) {
        resolveOnce(parsed.data);
        return;
      }

      rejectOnce(createApiResponseError(
        xhr.status >= 200 && xhr.status < 300 ? parsed : { ...parsed, ok: false },
        failureMessage,
      ));
    });

    xhr.addEventListener('error', () => {
      rejectOnce(new UploadTransportError(networkErrorMessage));
    });

    xhr.addEventListener('timeout', () => {
      rejectOnce(new UploadTransportError(networkErrorMessage));
    });

    xhr.addEventListener('abort', () => {
      rejectOnce(new UploadTransportError(cancelledMessage));
    });

    xhr.open('POST', url);
    xhr.timeout = config.timeout;
    xhr.setRequestHeader('Accept', 'application/json');

    const language = config.getLanguage?.()?.trim();
    if (language) {
      xhr.setRequestHeader('Accept-Language', language);
    }

    const token = config.getToken?.();
    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    xhr.send(formData);
  });
}

/**
 * 附件信息（匹配后端 AttachmentVo）
 */
export interface AttachmentInfo {
  /**
   * 附件 ID
   */
  voId: string;

  /**
   * 原始文件名
   */
  voOriginalName: string;

  /**
   * 文件扩展名
   */
  voExtension: string;

  /**
   * 文件大小（字节）
   */
  voFileSize: number | string;

  /**
   * 文件大小（格式化后，如 1.5MB）
   */
  voFileSizeFormatted?: string;

  /**
   * MIME 类型
   */
  voMimeType: string;

  /**
   * 存储类型（Local/MinIO/OSS）
   */
  voStorageType?: string;

  /**
   * 访问 URL
   */
  voUrl: string;

  /**
   * 缩略图 URL（仅图片）
   */
  voThumbnailUrl?: string;

  /**
   * 上传者 ID
   */
  voUploaderId?: string;

  /**
   * 上传者名称
   */
  voUploaderName?: string;

  /**
   * 业务类型
   */
  voBusinessType?: string;

  /**
   * 业务 ID
   */
  voBusinessId?: string | null;

  /**
   * 是否公开
   */
  voIsPublic?: boolean;

  /**
   * 下载次数
   */
  voDownloadCount?: number;

  /**
   * 内容审核状态（Pending/Pass/Reject）
   */
  voAuditStatus?: string | null;

  /**
   * 备注
   */
  voRemark?: string;

  /**
   * 创建时间
   */
  voCreateTime?: string;
}

export type MyAttachmentBusinessType = 'All' | 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document';

export interface MyAttachmentItem {
  voId: string;
  voOriginalName: string;
  voExtension?: string;
  voFileSize: number;
  voFileSizeFormatted?: string;
  voMimeType: string;
  voUrl: string;
  voThumbnailUrl?: string | null;
  voBusinessType?: string;
  voCreateTime: string;
}

export interface AttachmentPageModel<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}

/**
 * 图片上传选项
 */
export interface ImageUploadOptions {
  /**
   * 文件对象
   */
  file: File;

  /**
   * 业务类型
   * @default "General"
   */
  businessType?: 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document' | 'Wiki' | 'Chat';

  /**
   * 是否生成缩略图
   * @default true
   */
  generateThumbnail?: boolean;

  /**
   * 是否生成多尺寸
   * @default false
   */
  generateMultipleSizes?: boolean;

  /**
   * 是否添加水印
   * @default false
   */
  addWatermark?: boolean;

  /**
   * 水印文本
   * @default "Radish"
   */
  watermarkText?: string;

  /**
   * 是否移除 EXIF 信息
   * @default true
   */
  removeExif?: boolean;

  /**
   * 上传进度回调
   */
  onProgress?: (progress: number) => void;

  /**
   * 取消当前上传请求。
   */
  signal?: AbortSignal;
}

/**
 * 文档上传选项
 */
export interface DocumentUploadOptions {
  /**
   * 文件对象
   */
  file: File;

  /**
   * 业务类型
   * @default "Document"
   */
  businessType?: 'General' | 'Post' | 'Comment' | 'Document' | 'Wiki' | 'Chat';

  /**
   * 上传进度回调
   */
  onProgress?: (progress: number) => void;

  /**
   * 取消当前上传请求。
   */
  signal?: AbortSignal;
}

/**
 * 上传图片
 * @param options 上传选项
 * @param t i18n 翻译函数
 * @returns 附件信息
 */
export async function uploadImage(
  options: ImageUploadOptions,
  t: TFunction
): Promise<AttachmentInfo> {
  const failureKey = 'attachment.api.imageUploadFailed';
  const {
    file,
    businessType = 'General',
    generateThumbnail = true,
    generateMultipleSizes = false,
    addWatermark = false,
    watermarkText = 'Radish',
    removeExif = true,
    onProgress,
    signal,
  } = options;

  const formData = new FormData();
  formData.append('file', file);
  formData.append('businessType', businessType);
  formData.append('generateThumbnail', generateThumbnail.toString());
  formData.append('generateMultipleSizes', generateMultipleSizes.toString());
  formData.append('addWatermark', addWatermark.toString());
  formData.append('watermarkText', watermarkText);
  formData.append('removeExif', removeExif.toString());

  return uploadFormData<AttachmentInfo>({
    path: '/api/v1/Attachment/UploadImage',
    formData,
    onProgress,
    t,
    failureKey,
    signal,
  });
}

/**
 * 上传文档
 * @param options 上传选项
 * @param t i18n 翻译函数
 * @returns 附件信息
 */
export async function uploadDocument(
  options: DocumentUploadOptions,
  t: TFunction
): Promise<AttachmentInfo> {
  const failureKey = 'attachment.api.documentUploadFailed';
  const {
    file,
    businessType = 'Document',
    onProgress,
    signal,
  } = options;

  const formData = new FormData();
  formData.append('file', file);
  formData.append('businessType', businessType);

  return uploadFormData<AttachmentInfo>({
    path: '/api/v1/Attachment/UploadDocument',
    formData,
    onProgress,
    t,
    failureKey,
    signal,
  });
}

/**
 * 根据 ID 获取附件信息
 * @param id 附件 ID
 * @param t i18n 翻译函数
 * @returns 附件信息
 */
export async function getAttachmentById(
  id: string,
  t: TFunction
): Promise<AttachmentInfo> {
  const response = await apiGet<AttachmentInfo>(
    `/api/v1/Attachment/GetById/${encodeURIComponent(id)}`,
    { withAuth: false },
  );
  return requireResponseData(response, 'attachment.api.loadFailed', t);
}

/**
 * 根据业务类型和 ID 获取附件列表
 * @param businessType 业务类型
 * @param businessId 业务 ID
 * @param t i18n 翻译函数
 * @returns 附件列表
 */
export async function getAttachmentsByBusiness(
  businessType: string,
  businessId: string,
  t: TFunction
): Promise<AttachmentInfo[]> {
  const response = await apiGet<AttachmentInfo[]>(
    `/api/v1/Attachment/GetByBusiness?businessType=${encodeURIComponent(businessType)}&businessId=${encodeURIComponent(businessId)}`,
    { withAuth: false },
  );
  return requireResponseData(response, 'attachment.api.listLoadFailed', t);
}

export async function getMyAttachments(options: {
  pageIndex?: number;
  pageSize?: number;
  businessType?: MyAttachmentBusinessType;
  keyword?: string;
} = {}): Promise<AttachmentPageModel<MyAttachmentItem>> {
  const params = new URLSearchParams({
    pageIndex: String(options.pageIndex ?? 1),
    pageSize: String(options.pageSize ?? 10),
  });

  if (options.businessType && options.businessType !== 'All') {
    params.set('businessType', options.businessType);
  }

  const keyword = options.keyword?.trim();
  if (keyword) {
    params.set('keyword', keyword);
  }

  const response = await apiGet<AttachmentPageModel<MyAttachmentItem>>(
    `/api/v1/Attachment/GetMyAttachments?${params.toString()}`,
    { withAuth: true }
  );
  return requireResponseData(response, 'profile.attachments.loadFailed');
}

/**
 * 删除附件
 * @param id 附件 ID
 * @param t i18n 翻译函数
 */
export async function deleteAttachment(
  id: string,
  t: TFunction
): Promise<void> {
  const response = await apiDelete<void>(
    `/api/v1/Attachment/Delete/${encodeURIComponent(id)}`,
    { withAuth: true },
  );
  requireSuccessfulResponse(response, 'attachment.api.deleteFailed', t);
}

/**
 * 批量删除附件
 * @param ids 附件 ID 列表
 * @param t i18n 翻译函数
 */
export async function deleteAttachmentsBatch(
  ids: string[],
  t: TFunction
): Promise<void> {
  const response = await apiPost<void>(
    '/api/v1/Attachment/DeleteBatch',
    ids,
    { withAuth: true },
  );
  requireSuccessfulResponse(response, 'attachment.api.batchDeleteFailed', t);
}

/**
 * 设置或清空当前用户头像。
 * @param attachmentId Avatar 附件 ID；null 表示清空。
 * @param t i18n 翻译函数
 * @param signal 取消当前请求
 */
export async function setMyAvatar(
  attachmentId: string | null,
  t: TFunction,
  signal?: AbortSignal,
): Promise<void> {
  const response = await apiPost<void>(
    '/api/v1/User/SetMyAvatar',
    { attachmentId: attachmentId ?? '0' },
    { withAuth: true, signal },
  );
  requireSuccessfulResponse(
    response,
    attachmentId ? 'profile.avatar.setAvatarFailed' : 'profile.avatar.removeAvatarFailed',
    t,
  );
}

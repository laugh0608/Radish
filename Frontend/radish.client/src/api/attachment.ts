import { log } from '@/utils/logger';

/**
 * 附件上传相关的 API 调用
 */

import {
  ApiResponseError,
  apiDelete,
  apiGet,
  apiPost,
  apiPut,
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

/**
 * 延迟函数（用于重试）
 */
function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

const recoverableUploadStatuses = new Set([500, 502, 503, 504]);

class UploadTransportError extends Error {
  readonly retryable: boolean;

  constructor(message: string, retryable: boolean) {
    super(message);
    this.name = 'UploadTransportError';
    this.retryable = retryable;
  }
}

function resolveTranslation(
  key: string,
  t?: TFunction,
  configuredTranslator = getApiClientConfig().translateMessage,
): string | undefined {
  const configuredMessage = configuredTranslator?.(key);
  if (configuredMessage && configuredMessage !== key) {
    return configuredMessage;
  }

  if (!t) {
    return undefined;
  }

  const callerMessage = t(key);
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

  const localizedMessage = resolveTranslation(response.messageKey, t);
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

function isRetryableUploadError(error: Error): boolean {
  if (error instanceof UploadTransportError) {
    return error.retryable;
  }

  if (!(error instanceof ApiResponseError)) {
    return false;
  }

  const status = error.httpStatus ?? error.statusCode;
  return status !== undefined && recoverableUploadStatuses.has(status);
}

/**
 * 带重试的上传函数
 * @param uploadFn 上传函数
 * @param maxRetries 最大重试次数（默认 3）
 * @param baseDelay 基础延迟时间（毫秒，默认 1000）
 * @returns 上传结果
 */
async function uploadWithRetry<T>(
  uploadFn: () => Promise<T>,
  exhaustedFallbackMessage: string,
  maxRetries: number = 3,
  baseDelay: number = 1000
): Promise<T> {
  let lastError: Error | null = null;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await uploadFn();
    } catch (error) {
      lastError = error instanceof Error ? error : new Error(exhaustedFallbackMessage);

      if (!isRetryableUploadError(lastError)) {
        throw lastError;
      }

      // 如果是最后一次尝试，直接抛出错误
      if (attempt === maxRetries) {
        break;
      }

      // 指数退避：1s, 2s, 4s
      const delayTime = baseDelay * Math.pow(2, attempt);
      log.warn('Attachment upload failed; scheduling a retry.', {
        attempt: attempt + 1,
        maxRetries,
        delayMs: delayTime,
        message: lastError.message,
      });
      await delay(delayTime);
    }
  }

  throw lastError ?? new Error(exhaustedFallbackMessage);
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

  const parsed = parseApiResponseWithI18n<T>(json, (key) => (
    resolveTranslation(key, t, config.translateMessage) ?? key
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
}): Promise<T> {
  const { path, formData, onProgress, t, failureKey } = options;
  const config = getApiClientConfig();
  const url = `${getApiBaseUrl()}${path}`;
  const invalidResponseMessage = resolveTranslation(
    'attachment.api.invalidResponse',
    t,
    config.translateMessage,
  ) ?? 'attachment.api.invalidResponse';
  const networkErrorMessage = resolveTranslation(
    'attachment.api.networkError',
    t,
    config.translateMessage,
  ) ?? 'attachment.api.networkError';
  const cancelledMessage = resolveTranslation(
    'attachment.api.uploadCancelled',
    t,
    config.translateMessage,
  ) ?? 'attachment.api.uploadCancelled';
  const failureMessage = resolveTranslation(failureKey, t, config.translateMessage) ?? failureKey;

  return new Promise<T>((resolve, reject) => {
    const xhr = new XMLHttpRequest();

    if (onProgress) {
      xhr.upload.addEventListener('progress', (event) => {
        if (event.lengthComputable) {
          onProgress(Math.round((event.loaded / event.total) * 100));
        }
      });
    }

    xhr.addEventListener('load', () => {
      if (xhr.status === 0) {
        reject(new UploadTransportError(networkErrorMessage, true));
        return;
      }

      const parsed = parseXhrApiResponse<T>(xhr, t, config);
      if (!parsed) {
        reject(createUnparsedXhrError(xhr, invalidResponseMessage));
        return;
      }

      if (xhr.status >= 200 && xhr.status < 300 && parsed.ok && parsed.data !== undefined) {
        resolve(parsed.data);
        return;
      }

      reject(createApiResponseError(
        xhr.status >= 200 && xhr.status < 300 ? parsed : { ...parsed, ok: false },
        failureMessage,
      ));
    });

    xhr.addEventListener('error', () => {
      reject(new UploadTransportError(networkErrorMessage, true));
    });

    xhr.addEventListener('timeout', () => {
      reject(new UploadTransportError(networkErrorMessage, true));
    });

    xhr.addEventListener('abort', () => {
      reject(new UploadTransportError(cancelledMessage, false));
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
   * 业务 ID（可选，稍后可通过 UpdateBusinessAssociation 更新）
   */
  businessId?: string;

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
  businessType?: 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document' | 'Wiki' | 'Chat';

  /**
   * 业务 ID（可选）
   */
  businessId?: string;

  /**
   * 上传进度回调
   */
  onProgress?: (progress: number) => void;
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
  return uploadWithRetry(async () => {
    const {
      file,
      businessType = 'General',
      businessId,
      generateThumbnail = true,
      generateMultipleSizes = false,
      addWatermark = false,
      watermarkText = 'Radish',
      removeExif = true,
      onProgress
    } = options;

    const formData = new FormData();
    formData.append('file', file);
    formData.append('businessType', businessType);
    if (businessId) {
      formData.append('businessId', businessId.toString());
    }
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
    });
  }, resolveFallbackMessage(failureKey, t));
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
  return uploadWithRetry(async () => {
    const {
      file,
      businessType = 'Document',
      businessId,
      onProgress
    } = options;

    const formData = new FormData();
    formData.append('file', file);
    formData.append('businessType', businessType);
    if (businessId) {
      formData.append('businessId', businessId.toString());
    }

    return uploadFormData<AttachmentInfo>({
      path: '/api/v1/Attachment/UploadDocument',
      formData,
      onProgress,
      t,
      failureKey,
    });
  }, resolveFallbackMessage(failureKey, t));
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
 * 更新附件的业务关联
 * @param id 附件 ID
 * @param businessType 业务类型
 * @param businessId 业务 ID
 * @param t i18n 翻译函数
 */
export async function updateAttachmentBusinessAssociation(
  id: string,
  businessType: string,
  businessId: string,
  t: TFunction
): Promise<void> {
  const response = await apiPut<void>(
    `/api/v1/Attachment/UpdateBusinessAssociation/${encodeURIComponent(id)}?businessType=${encodeURIComponent(businessType)}&businessId=${encodeURIComponent(businessId)}`,
    undefined,
    { withAuth: true },
  );
  requireSuccessfulResponse(response, 'attachment.api.updateAssociationFailed', t);
}

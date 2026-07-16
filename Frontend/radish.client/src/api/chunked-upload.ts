/**
 * 分片上传相关的 API 调用
 */

import {
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
  getApiClientConfig,
  parseApiResponseWithI18n,
  type ApiResponse,
} from '@radish/http';
import type { AttachmentInfo } from '@/api/attachment';
import { getApiBaseUrl } from '@/config/env';
import { normalizePositiveLongIdKey } from '@/utils/longId';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 上传会话信息
 */
export interface UploadSession {
  /**
   * 会话 ID
   */
  sessionId: string;

  /**
   * 原始文件名
   */
  fileName: string;

  /**
   * 文件总大小（字节）
   */
  totalSize: number;

  /**
   * 分片大小（字节）
   */
  chunkSize: number;

  /**
   * 总分片数
   */
  totalChunks: number;

  /**
   * 已上传分片数
   */
  uploadedChunks: number;

  /**
   * 已上传分片索引列表
   */
  uploadedChunkIndexes: number[];

  /**
   * 上传进度（0-100）
   */
  progress: number;

  /**
   * 会话状态（Uploading/Completed/Failed/Cancelled）
   */
  status: string;

  /**
   * 最终附件 ID（完成后）
   */
  attachmentId?: string;

  /**
   * 过期时间
   */
  expiresAt: string;

  /**
   * 创建时间
   */
  createTime: string;
}

interface UploadSessionVoResponse {
  voSessionId: string;
  voFileName: string;
  voTotalSize: number | string;
  voChunkSize: number;
  voTotalChunks: number;
  voUploadedChunks: number;
  voUploadedChunkIndexes: number[];
  voProgress: number;
  voStatus: string;
  voAttachmentId?: string | number | null;
  voExpiresAt: string;
  voCreateTime: string;
}

export interface ChunkedUploadAttachmentInfo {
  attachmentId: string;
  fileName: string;
  fileSize: number;
  accessUrl: string;
}

function mapUploadSession(response: UploadSessionVoResponse): UploadSession {
  const totalSize = typeof response.voTotalSize === 'number'
    ? response.voTotalSize
    : Number(response.voTotalSize);
  if (
    typeof response.voSessionId !== 'string'
    || !Number.isSafeInteger(totalSize)
    || totalSize <= 0
    || !Array.isArray(response.voUploadedChunkIndexes)
    || !response.voUploadedChunkIndexes.every(Number.isSafeInteger)
  ) {
    throw new Error('分片上传会话响应格式无效');
  }

  const attachmentId = response.voAttachmentId == null
    ? undefined
    : normalizePositiveLongIdKey(response.voAttachmentId) ?? undefined;
  return {
    sessionId: response.voSessionId,
    fileName: response.voFileName,
    totalSize,
    chunkSize: response.voChunkSize,
    totalChunks: response.voTotalChunks,
    uploadedChunks: response.voUploadedChunks,
    uploadedChunkIndexes: response.voUploadedChunkIndexes,
    progress: response.voProgress,
    status: response.voStatus,
    attachmentId,
    expiresAt: response.voExpiresAt,
    createTime: response.voCreateTime,
  };
}

function mapMergedAttachment(response: AttachmentInfo): ChunkedUploadAttachmentInfo {
  const attachmentId = normalizePositiveLongIdKey(response.voId);
  const fileSize = typeof response.voFileSize === 'number'
    ? response.voFileSize
    : Number(response.voFileSize);
  if (!attachmentId || !Number.isSafeInteger(fileSize) || fileSize < 0) {
    throw new Error('分片上传附件响应格式无效');
  }

  return {
    attachmentId,
    fileName: response.voOriginalName,
    fileSize,
    accessUrl: response.voUrl,
  };
}

/**
 * 创建上传会话选项
 */
export interface CreateSessionOptions {
  /**
   * 原始文件名
   */
  fileName: string;

  /**
   * 文件总大小（字节）
   */
  totalSize: number;

  /**
   * 文件 MIME 类型
   */
  mimeType?: string;

  /**
   * 分片大小（字节，默认 2MB）
   */
  chunkSize?: number;

  /**
   * 业务类型
   */
  businessType?: 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document';

}

/**
 * 合并分片选项
 */
export interface MergeChunksOptions {
  /**
   * 会话 ID
   */
  sessionId: string;

  /**
   * 是否生成缩略图
   */
  generateThumbnail?: boolean;

  /**
   * 是否生成多尺寸
   */
  generateMultipleSizes?: boolean;

  /**
   * 是否添加水印
   */
  addWatermark?: boolean;

  /**
   * 水印文本
   */
  watermarkText?: string;

  /**
   * 是否移除 EXIF
   */
  removeExif?: boolean;
}

/**
 * 创建上传会话
 * @param options 创建选项
 * @returns 上传会话信息
 */
export async function createSession(
  options: CreateSessionOptions
): Promise<UploadSession> {
  const {
    fileName,
    totalSize,
    mimeType,
    chunkSize = 2 * 1024 * 1024, // 默认 2MB
    businessType = 'General'
  } = options;

  const response = await apiPost<UploadSessionVoResponse>(
    '/api/v1/ChunkedUpload/CreateSession',
    {
      fileName,
      totalSize,
      mimeType,
      chunkSize,
      businessType
    },
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '创建上传会话失败');
  }

  return mapUploadSession(response.data);
}

/**
 * 上传分片
 * @param sessionId 会话 ID
 * @param chunkIndex 分片索引
 * @param chunkBlob 分片数据
 * @param onProgress 上传进度回调
 * @returns 更新后的上传会话信息
 *
 * 注意：此方法使用 XMLHttpRequest 而非统一 API 客户端，
 * 因为需要支持上传进度回调功能
 */
export async function uploadChunk(
  sessionId: string,
  chunkIndex: number,
  chunkBlob: Blob,
  onProgress?: (progress: number) => void,
  signal?: AbortSignal,
): Promise<UploadSession> {
  const config = getApiClientConfig();
  const url = `${config.baseUrl}/api/v1/ChunkedUpload/UploadChunk`;

  const formData = new FormData();
  formData.append('sessionId', sessionId);
  formData.append('chunkIndex', chunkIndex.toString());
  formData.append('chunkData', chunkBlob, `chunk_${chunkIndex}`);

  // 使用 XMLHttpRequest 以支持上传进度
  return new Promise<UploadSession>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    let settled = false;

    const cleanup = () => signal?.removeEventListener('abort', handleSignalAbort);
    const resolveOnce = (value: UploadSession) => {
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
      rejectOnce(new Error('上传已取消'));
      return;
    }

    signal?.addEventListener('abort', handleSignalAbort, { once: true });

    // 上传进度
    if (onProgress) {
      xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable) {
          const progress = Math.round((e.loaded / e.total) * 100);
          onProgress(progress);
        }
      });
    }

    // 上传完成
    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const json = JSON.parse(xhr.responseText) as ApiResponse<UploadSessionVoResponse>;
          if (typeof json?.isSuccess !== 'boolean') {
            rejectOnce(new Error('解析响应失败'));
            return;
          }

          const parsed = parseApiResponseWithI18n(json, (key, messageArguments) => (
            config.translateMessage?.(key, messageArguments) ?? key
          ));
          parsed.httpStatus = xhr.status;
          parsed.traceId ||= xhr.getResponseHeader('x-correlation-id') || undefined;
          if (parsed.ok && parsed.data) {
            resolveOnce(mapUploadSession(parsed.data));
          } else {
            rejectOnce(createApiResponseError(parsed, '上传分片失败'));
          }
        } catch {
          rejectOnce(new Error('解析响应失败'));
        }
      } else {
        try {
          const json = JSON.parse(xhr.responseText) as ApiResponse<UploadSessionVoResponse>;
          const parsed = parseApiResponseWithI18n(json, (key, messageArguments) => (
            config.translateMessage?.(key, messageArguments) ?? key
          ));
          parsed.httpStatus = xhr.status;
          parsed.traceId ||= xhr.getResponseHeader('x-correlation-id') || undefined;
          rejectOnce(createApiResponseError(parsed, `上传分片失败: HTTP ${xhr.status}`));
        } catch {
          rejectOnce(new Error(`上传分片失败: HTTP ${xhr.status}`));
        }
      }
    });

    // 上传错误
    xhr.addEventListener('error', () => {
      rejectOnce(new Error('网络错误'));
    });

    // 请求超时
    xhr.addEventListener('timeout', () => {
      rejectOnce(new Error('上传分片超时'));
    });

    // 上传取消
    xhr.addEventListener('abort', () => {
      rejectOnce(new Error('上传已取消'));
    });

    // 发送请求
    xhr.open('POST', url);
    xhr.timeout = config.timeout;
    xhr.setRequestHeader('Accept', 'application/json');

    const language = config.getLanguage?.()?.trim();
    if (language) {
      xhr.setRequestHeader('Accept-Language', language);
    }

    // 添加认证头（从统一配置获取 token）
    const token = config.getToken?.();
    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    xhr.send(formData);
  });
}

/**
 * 获取上传会话信息
 * @param sessionId 会话 ID
 * @returns 上传会话信息
 */
export async function getSession(
  sessionId: string
): Promise<UploadSession> {
  const response = await apiGet<UploadSessionVoResponse>(
    `/api/v1/ChunkedUpload/GetSession?sessionId=${encodeURIComponent(sessionId)}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取上传会话失败');
  }

  return mapUploadSession(response.data);
}

/**
 * 合并分片
 * @param options 合并选项
 * @returns 附件信息
 */
export async function mergeChunks(
  options: MergeChunksOptions
): Promise<ChunkedUploadAttachmentInfo> {
  const {
    sessionId,
    generateThumbnail = true,
    generateMultipleSizes = false,
    addWatermark = false,
    watermarkText = 'Radish',
    removeExif = true
  } = options;

  const response = await apiPost<AttachmentInfo>(
    '/api/v1/ChunkedUpload/MergeChunks',
    {
      sessionId,
      generateThumbnail,
      generateMultipleSizes,
      addWatermark,
      watermarkText,
      removeExif
    },
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '合并分片失败');
  }

  return mapMergedAttachment(response.data);
}

/**
 * 取消上传会话
 * @param sessionId 会话 ID
 */
export async function cancelSession(
  sessionId: string
): Promise<void> {
  const response = await apiPost<void>(
    '/api/v1/ChunkedUpload/CancelSession',
    sessionId,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '取消上传会话失败');
  }
}

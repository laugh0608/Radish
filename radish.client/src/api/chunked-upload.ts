/**
 * 分片上传相关的 API 调用
 */

import { parseApiResponse, type ApiResponse } from '@radish/ui';
import type { AttachmentInfo } from '@/api/attachment';

const defaultApiBase = 'https://localhost:5000';

/**
 * 获取 API Base URL
 */
function getApiBaseUrl(): string {
  const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
  return (configured ?? defaultApiBase).replace(/\/$/, '');
}

/**
 * 带认证的 fetch 封装
 */
interface ApiFetchOptions extends RequestInit {
  withAuth?: boolean;
}

function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
  const { withAuth, headers, ...rest } = options;

  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    ...headers
  };

  if (withAuth && typeof window !== 'undefined') {
    const token = window.localStorage.getItem('access_token');
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  return fetch(input, {
    ...rest,
    headers: finalHeaders
  });
}

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
  attachmentId?: number;

  /**
   * 过期时间
   */
  expiresAt: string;

  /**
   * 创建时间
   */
  createTime: string;
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

  /**
   * 业务 ID（可选）
   */
  businessId?: number | string;
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
 * @param t i18n 翻译函数
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
    businessType = 'General',
    businessId
  } = options;

  const url = `${getApiBaseUrl()}/api/v1/ChunkedUpload/CreateSession`;

  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true,
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      fileName,
      totalSize,
      mimeType,
      chunkSize,
      businessType,
      businessId
    })
  });

  if (!response.ok) {
    throw new Error(`创建上传会话失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UploadSession>;
  const parsed = parseApiResponse<UploadSession>(json);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '创建上传会话失败');
  }

  return parsed.data;
}

/**
 * 上传分片
 * @param sessionId 会话 ID
 * @param chunkIndex 分片索引
 * @param chunkBlob 分片数据
 * @param onProgress 上传进度回调
 * @param t i18n 翻译函数
 * @returns 更新后的上传会话信息
 */
export async function uploadChunk(
  sessionId: string,
  chunkIndex: number,
  chunkBlob: Blob,
  onProgress?: (progress: number) => void
): Promise<UploadSession> {
  const url = `${getApiBaseUrl()}/api/v1/ChunkedUpload/UploadChunk`;

  const formData = new FormData();
  formData.append('sessionId', sessionId);
  formData.append('chunkIndex', chunkIndex.toString());
  formData.append('chunkData', chunkBlob, `chunk_${chunkIndex}`);

  // 使用 XMLHttpRequest 以支持上传进度
  return new Promise<UploadSession>((resolve, reject) => {
    const xhr = new XMLHttpRequest();

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
          const json = JSON.parse(xhr.responseText) as ApiResponse<UploadSession>;
          const parsed = parseApiResponse<UploadSession>(json);

          if (!parsed.ok || !parsed.data) {
            reject(new Error(parsed.message || '上传分片失败'));
          } else {
            resolve(parsed.data);
          }
        } catch (error) {
          reject(new Error('解析响应失败'));
        }
      } else {
        reject(new Error(`上传分片失败: HTTP ${xhr.status}`));
      }
    });

    // 上传错误
    xhr.addEventListener('error', () => {
      reject(new Error('网络错误'));
    });

    // 上传取消
    xhr.addEventListener('abort', () => {
      reject(new Error('上传已取消'));
    });

    // 发送请求
    xhr.open('POST', url);

    // 添加认证头
    const token = window.localStorage.getItem('access_token');
    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    xhr.send(formData);
  });
}

/**
 * 获取上传会话信息
 * @param sessionId 会话 ID
 * @param t i18n 翻译函数
 * @returns 上传会话信息
 */
export async function getSession(
  sessionId: string
): Promise<UploadSession> {
  const url = `${getApiBaseUrl()}/api/v1/ChunkedUpload/GetSession?sessionId=${encodeURIComponent(sessionId)}`;

  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    throw new Error(`获取上传会话失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UploadSession>;
  const parsed = parseApiResponse<UploadSession>(json);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '获取上传会话失败');
  }

  return parsed.data;
}

/**
 * 合并分片
 * @param options 合并选项
 * @param t i18n 翻译函数
 * @returns 附件信息
 */
export async function mergeChunks(
  options: MergeChunksOptions
): Promise<AttachmentInfo> {
  const {
    sessionId,
    generateThumbnail = true,
    generateMultipleSizes = false,
    addWatermark = false,
    watermarkText = 'Radish',
    removeExif = true
  } = options;

  const url = `${getApiBaseUrl()}/api/v1/ChunkedUpload/MergeChunks`;

  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true,
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      sessionId,
      generateThumbnail,
      generateMultipleSizes,
      addWatermark,
      watermarkText,
      removeExif
    })
  });

  if (!response.ok) {
    throw new Error(`合并分片失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<AttachmentInfo>;
  const parsed = parseApiResponse<AttachmentInfo>(json);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '合并分片失败');
  }

  return parsed.data;
}

/**
 * 取消上传会话
 * @param sessionId 会话 ID
 * @param t i18n 翻译函数
 */
export async function cancelSession(
  sessionId: string
): Promise<void> {
  const url = `${getApiBaseUrl()}/api/v1/ChunkedUpload/CancelSession`;

  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true,
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(sessionId)
  });

  if (!response.ok) {
    throw new Error(`取消上传会话失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<void>;
  const parsed = parseApiResponse<void>(json);

  if (!parsed.ok) {
    throw new Error(parsed.message || '取消上传会话失败');
  }
}

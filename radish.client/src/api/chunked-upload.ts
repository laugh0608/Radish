/**
 * 分片上传相关的 API 调用
 */

import { apiGet, apiPost, configureApiClient, getApiClientConfig } from '@radish/ui';
import type { AttachmentInfo } from '@/api/attachment';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
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

  const response = await apiPost<UploadSession>(
    '/api/v1/ChunkedUpload/CreateSession',
    {
      fileName,
      totalSize,
      mimeType,
      chunkSize,
      businessType,
      businessId
    },
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '创建上传会话失败');
  }

  return response.data;
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
  onProgress?: (progress: number) => void
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
          const json = JSON.parse(xhr.responseText);
          // 假设后端返回的是标准 ApiResponse 格式
          if (json.isSuccess && json.responseData) {
            resolve(json.responseData);
          } else {
            reject(new Error(json.messageInfo || '上传分片失败'));
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
  const response = await apiGet<UploadSession>(
    `/api/v1/ChunkedUpload/GetSession?sessionId=${encodeURIComponent(sessionId)}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取上传会话失败');
  }

  return response.data;
}

/**
 * 合并分片
 * @param options 合并选项
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

  return response.data;
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

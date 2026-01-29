import { log } from '@/utils/logger';

/**
 * 附件上传相关的 API 调用
 */

import { configureApiClient, getApiClientConfig, apiFetch, parseApiResponseWithI18n, type ApiResponse } from '@radish/ui';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 获取API基础URL
 */
function getApiBaseUrl(): string {
  return getApiClientConfig().baseUrl;
}

/**
 * 延迟函数（用于重试）
 */
function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
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
  maxRetries: number = 3,
  baseDelay: number = 1000
): Promise<T> {
  let lastError: Error | null = null;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await uploadFn();
    } catch (error) {
      lastError = error instanceof Error ? error : new Error(String(error));

      // 如果是最后一次尝试，直接抛出错误
      if (attempt === maxRetries) {
        break;
      }

      // 指数退避：1s, 2s, 4s
      const delayTime = baseDelay * Math.pow(2, attempt);
      log.warn(`上传失败，${delayTime}ms 后重试（第 ${attempt + 1}/${maxRetries} 次）:`, lastError.message);
      await delay(delayTime);
    }
  }

  throw lastError || new Error('上传失败');
}

/**
 * 附件信息（匹配后端 AttachmentVo）
 */
export interface AttachmentInfo {
  /**
   * 附件 ID
   */
  id: number | string;

  /**
   * 原始文件名
   */
  originalName: string;

  /**
   * 文件扩展名
   */
  extension: string;

  /**
   * 文件大小（字节）
   */
  fileSize: number;

  /**
   * 文件大小（格式化后，如 1.5MB）
   */
  fileSizeFormatted?: string;

  /**
   * MIME 类型
   */
  mimeType: string;

  /**
   * 存储类型（Local/MinIO/OSS）
   */
  storageType?: string;

  /**
   * 访问 URL
   */
  url: string;

  /**
   * 缩略图 URL（仅图片）
   */
  thumbnailUrl?: string;

  /**
   * 上传者 ID
   */
  uploaderId?: number | string;

  /**
   * 上传者名称
   */
  uploaderName?: string;

  /**
   * 业务类型
   */
  businessType?: string;

  /**
   * 业务 ID
   */
  businessId?: number | string;

  /**
   * 是否公开
   */
  isPublic?: boolean;

  /**
   * 下载次数
   */
  downloadCount?: number;

  /**
   * 内容审核状态（Pending/Pass/Reject）
   */
  auditStatus?: string;

  /**
   * 备注
   */
  remark?: string;

  /**
   * 创建时间
   */
  createTime?: string;
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
  businessType?: 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document';

  /**
   * 业务 ID（可选，稍后可通过 UpdateBusinessAssociation 更新）
   */
  businessId?: number | string;

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
  businessType?: 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document';

  /**
   * 业务 ID（可选）
   */
  businessId?: number | string;

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
  // 使用重试机制包装上传逻辑
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

    const url = `${getApiBaseUrl()}/api/v1/Attachment/UploadImage`;

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

    // 使用 XMLHttpRequest 以支持上传进度
    return new Promise<AttachmentInfo>((resolve, reject) => {
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
            const json = JSON.parse(xhr.responseText) as ApiResponse<AttachmentInfo>;
            const parsed = parseApiResponseWithI18n<AttachmentInfo>(json, t);

            if (!parsed.ok || !parsed.data) {
              reject(new Error(parsed.message || '上传失败'));
            } else {
              resolve(parsed.data);
            }
          } catch (error) {
            reject(new Error('解析响应失败'));
          }
        } else {
          reject(new Error(`上传失败: HTTP ${xhr.status}`));
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
  // 使用重试机制包装上传逻辑
  return uploadWithRetry(async () => {
    const {
      file,
      businessType = 'Document',
      businessId,
      onProgress
    } = options;

    const url = `${getApiBaseUrl()}/api/v1/Attachment/UploadDocument`;

    const formData = new FormData();
    formData.append('file', file);
    formData.append('businessType', businessType);
    if (businessId) {
      formData.append('businessId', businessId.toString());
    }

    // 使用 XMLHttpRequest 以支持上传进度
    return new Promise<AttachmentInfo>((resolve, reject) => {
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
            const json = JSON.parse(xhr.responseText) as ApiResponse<AttachmentInfo>;
            const parsed = parseApiResponseWithI18n<AttachmentInfo>(json, t);

            if (!parsed.ok || !parsed.data) {
              reject(new Error(parsed.message || '上传失败'));
            } else {
              resolve(parsed.data);
            }
          } catch (error) {
            reject(new Error('解析响应失败'));
          }
        } else {
          reject(new Error(`上传失败: HTTP ${xhr.status}`));
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
  });
}

/**
 * 根据 ID 获取附件信息
 * @param id 附件 ID
 * @param t i18n 翻译函数
 * @returns 附件信息
 */
export async function getAttachmentById(
  id: number | string,
  t: TFunction
): Promise<AttachmentInfo> {
  const url = `${getApiBaseUrl()}/api/v1/Attachment/GetById/${id}`;
  const response = await apiFetch(url, { withAuth: false });

  if (!response.ok) {
    throw new Error(`获取附件失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<AttachmentInfo>;
  const parsed = parseApiResponseWithI18n<AttachmentInfo>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '获取附件失败');
  }

  return parsed.data;
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
  businessId: number | string,
  t: TFunction
): Promise<AttachmentInfo[]> {
  const url = `${getApiBaseUrl()}/api/v1/Attachment/GetByBusiness?businessType=${encodeURIComponent(businessType)}&businessId=${businessId}`;
  const response = await apiFetch(url, { withAuth: false });

  if (!response.ok) {
    throw new Error(`获取附件列表失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<AttachmentInfo[]>;
  const parsed = parseApiResponseWithI18n<AttachmentInfo[]>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '获取附件列表失败');
  }

  return parsed.data;
}

/**
 * 删除附件
 * @param id 附件 ID
 * @param t i18n 翻译函数
 */
export async function deleteAttachment(
  id: number | string,
  t: TFunction
): Promise<void> {
  const url = `${getApiBaseUrl()}/api/v1/Attachment/Delete/${id}`;
  const response = await apiFetch(url, {
    method: 'DELETE',
    withAuth: true
  });

  if (!response.ok) {
    throw new Error(`删除附件失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<void>;
  const parsed = parseApiResponseWithI18n<void>(json, t);

  if (!parsed.ok) {
    throw new Error(parsed.message || '删除附件失败');
  }
}

/**
 * 批量删除附件
 * @param ids 附件 ID 列表
 * @param t i18n 翻译函数
 */
export async function deleteAttachmentsBatch(
  ids: (number | string)[],
  t: TFunction
): Promise<void> {
  const url = `${getApiBaseUrl()}/api/v1/Attachment/DeleteBatch`;
  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true,
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(ids)
  });

  if (!response.ok) {
    throw new Error(`批量删除附件失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<void>;
  const parsed = parseApiResponseWithI18n<void>(json, t);

  if (!parsed.ok) {
    throw new Error(parsed.message || '批量删除附件失败');
  }
}

/**
 * 更新附件的业务关联
 * @param id 附件 ID
 * @param businessType 业务类型
 * @param businessId 业务 ID
 * @param t i18n 翻译函数
 */
export async function updateAttachmentBusinessAssociation(
  id: number | string,
  businessType: string,
  businessId: number | string,
  t: TFunction
): Promise<void> {
  const url = `${getApiBaseUrl()}/api/v1/Attachment/UpdateBusinessAssociation/${id}?businessType=${encodeURIComponent(businessType)}&businessId=${businessId}`;
  const response = await apiFetch(url, {
    method: 'PUT',
    withAuth: true
  });

  if (!response.ok) {
    throw new Error(`更新业务关联失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<void>;
  const parsed = parseApiResponseWithI18n<void>(json, t);

  if (!parsed.ok) {
    throw new Error(parsed.message || '更新业务关联失败');
  }
}

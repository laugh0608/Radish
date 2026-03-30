import { getApiClientConfig } from '@radish/http';
import { buildAttachmentAssetUrl } from '@radish/ui';

export interface UploadedAttachmentImage {
  attachmentId: string;
  url?: string;
  thumbnailUrl?: string;
}

interface UploadAttachmentImageOptions {
  businessType: string;
  generateThumbnail?: boolean;
  removeExif?: boolean;
}

function toStringOrUndefined(value: unknown): string | undefined {
  if (typeof value === 'string' && value.trim()) {
    return value.trim();
  }

  return undefined;
}

function toAttachmentId(value: unknown): string | undefined {
  if (typeof value === 'string' && /^[1-9]\d*$/.test(value.trim())) {
    return value.trim();
  }

  if (typeof value === 'number' && Number.isFinite(value) && value > 0) {
    return String(Math.trunc(value));
  }

  return undefined;
}

export function uploadAttachmentImage(
  file: File,
  options: UploadAttachmentImageOptions,
  onProgress?: (percent: number) => void
): Promise<UploadedAttachmentImage> {
  return new Promise((resolve, reject) => {
    const config = getApiClientConfig();
    const normalizedBaseUrl = (config.baseUrl || '').trim().replace(/\/$/, '');
    if (!normalizedBaseUrl) {
      reject(new Error('API baseUrl 未配置'));
      return;
    }

    const uploadUrl = `${normalizedBaseUrl}/api/v1/Attachment/UploadImage`;
    const xhr = new XMLHttpRequest();
    xhr.open('POST', uploadUrl, true);

    const token = config.getToken?.();
    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable || !onProgress) {
        return;
      }

      const percent = Math.round((event.loaded / event.total) * 100);
      onProgress(Math.min(100, Math.max(0, percent)));
    };

    xhr.onerror = () => {
      reject(new Error('图片上传失败，请检查网络连接'));
    };

    xhr.onload = () => {
      if (xhr.status < 200 || xhr.status >= 300) {
        reject(new Error(`图片上传失败（HTTP ${xhr.status}）`));
        return;
      }

      let payload: Record<string, unknown> | null = null;
      try {
        payload = JSON.parse(xhr.responseText) as Record<string, unknown>;
      } catch {
        reject(new Error('图片上传响应解析失败'));
        return;
      }

      const isSuccess = Boolean(payload.isSuccess ?? payload.IsSuccess);
      const messageText = toStringOrUndefined(payload.messageInfo ?? payload.MessageInfo) || '图片上传失败';
      if (!isSuccess) {
        reject(new Error(messageText));
        return;
      }

      const responseData = (payload.responseData ?? payload.ResponseData) as Record<string, unknown> | undefined;
      if (!responseData) {
        reject(new Error('图片上传成功但未返回附件信息'));
        return;
      }

      const attachmentId = toAttachmentId(
        responseData.voId
        ?? responseData.VoId
        ?? responseData.id
        ?? responseData.Id
      );
      if (!attachmentId) {
        reject(new Error('图片上传成功但未获取到附件 ID'));
        return;
      }

      resolve({
        attachmentId,
        url: toStringOrUndefined(
          responseData.voUrl
          ?? responseData.VoUrl
          ?? responseData.url
          ?? responseData.Url
        ) ?? buildAttachmentAssetUrl(attachmentId, 'original'),
        thumbnailUrl: toStringOrUndefined(
          responseData.voThumbnailUrl
          ?? responseData.VoThumbnailUrl
          ?? responseData.thumbnailUrl
          ?? responseData.ThumbnailUrl
        )
          ?? undefined,
      });
    };

    const formData = new FormData();
    formData.append('file', file);
    formData.append('businessType', options.businessType);
    formData.append('generateThumbnail', String(options.generateThumbnail ?? true));
    formData.append('removeExif', String(options.removeExif ?? true));
    xhr.send(formData);
  });
}

import { apiFetch } from './client';

export type AttachmentAssetVariant = 'original' | 'thumbnail';

function normalizeAttachmentAssetId(value: string): string {
  const normalized = value.trim();
  if (!/^[1-9]\d*$/.test(normalized)) {
    throw new Error('Attachment asset id is invalid');
  }

  return normalized;
}

/**
 * 使用统一 HTTP 客户端读取需要当前身份参与授权的附件二进制。
 *
 * 本函数不创建 object URL；请求取消、结果代次和 URL 生命周期由消费宿主持有。
 */
export async function loadAttachmentAssetBlob(
  attachmentId: string,
  variant: AttachmentAssetVariant = 'original',
  signal?: AbortSignal,
): Promise<Blob> {
  const normalizedId = normalizeAttachmentAssetId(attachmentId);
  const suffix = variant === 'thumbnail' ? '/thumbnail' : '';
  const response = await apiFetch(
    `/_assets/attachments/${encodeURIComponent(normalizedId)}${suffix}`,
    {
      method: 'GET',
      withAuth: true,
      headers: { Accept: '*/*' },
      signal,
    },
  );

  if (!response.ok) {
    throw new Error(`Attachment asset request failed with status ${response.status}`);
  }

  const blob = await response.blob();
  if (blob.size === 0) {
    throw new Error('Attachment asset response was empty');
  }

  return blob;
}

const supportedAttachmentImageMimeTypes = new Set([
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/bmp',
  'image/webp',
  'image/x-icon',
  'image/vnd.microsoft.icon',
]);

const supportedAttachmentImageExtensions = new Set([
  '.jpg',
  '.jpeg',
  '.png',
  '.gif',
  '.bmp',
  '.webp',
  '.ico',
]);

export const attachmentImageAccept = [
  ...supportedAttachmentImageMimeTypes,
  '.ico',
].join(',');

export function isSupportedAttachmentImageMimeType(value: string | null | undefined): boolean {
  return supportedAttachmentImageMimeTypes.has(value?.trim().toLowerCase() ?? '');
}

export function isSupportedAttachmentImageFile(
  file: Pick<File, 'name' | 'type'>,
): boolean {
  const mimeType = file.type.trim().toLowerCase();
  if (mimeType) {
    return supportedAttachmentImageMimeTypes.has(mimeType);
  }

  const normalizedName = file.name.trim().toLowerCase();
  const extensionIndex = normalizedName.lastIndexOf('.');
  return extensionIndex >= 0 && supportedAttachmentImageExtensions.has(
    normalizedName.slice(extensionIndex),
  );
}

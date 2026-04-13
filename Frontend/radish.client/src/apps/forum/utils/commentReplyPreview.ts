const MARKDOWN_IMAGE_PATTERN = /!\[[^\]]*]\(([^)]+)\)/g;
const MARKDOWN_LINK_PATTERN = /\[([^\]]+)]\(([^)]+)\)/g;
const ATTACHMENT_URI_PATTERN = /attachment:\/\/\S+/gi;
const STICKER_URI_PATTERN = /sticker:\/\/\S+/gi;
const MULTI_WHITESPACE_PATTERN = /\s+/g;
const DEFAULT_MAX_LENGTH = 30;

export function buildCommentReplyPreview(
  content: string | null | undefined,
  maxLength: number = DEFAULT_MAX_LENGTH
): string {
  if (!content) {
    return '';
  }

  const normalized = content
    .replace(MARKDOWN_IMAGE_PATTERN, (_, source: string) => (
      typeof source === 'string' && source.toLowerCase().startsWith('sticker://')
        ? '[表情]'
        : '[图片]'
    ))
    .replace(MARKDOWN_LINK_PATTERN, (_, label: string) => {
      const normalizedLabel = typeof label === 'string' ? label.trim() : '';
      return normalizedLabel || '[附件]';
    })
    .replace(ATTACHMENT_URI_PATTERN, '[附件]')
    .replace(STICKER_URI_PATTERN, '[表情]')
    .replace(MULTI_WHITESPACE_PATTERN, ' ')
    .trim();

  if (!normalized) {
    return '';
  }

  if (normalized.length <= maxLength) {
    return normalized;
  }

  return `${normalized.slice(0, Math.max(0, maxLength - 3))}...`;
}

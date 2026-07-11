const URI_SCHEME_PATTERN = /^[a-z][a-z\d+.-]*:/i;
const HTTP_PROTOCOL_PATTERN = /^https?:/i;
const ATTACHMENT_LINK_PATTERN = /^attachment:\/\/([1-9]\d*)$/i;

function containsAsciiControl(value: string): boolean {
  for (const character of value) {
    const codePoint = character.codePointAt(0) ?? 0;
    if (codePoint <= 31 || codePoint === 127) {
      return true;
    }
  }

  return false;
}

function isValidHttpUrl(value: string): boolean {
  try {
    const parsed = new URL(value);
    return (parsed.protocol === 'http:' || parsed.protocol === 'https:') && Boolean(parsed.hostname);
  } catch {
    return false;
  }
}

/**
 * 统一约束 Markdown 链接协议。
 *
 * 返回 null 表示链接应退化为不可点击文本。图片 URL 继续使用各自的资源解析规则。
 */
export function sanitizeMarkdownLinkHref(value: string | null | undefined): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim();
  if (!normalized || containsAsciiControl(normalized)) {
    return null;
  }

  const attachmentMatch = normalized.match(ATTACHMENT_LINK_PATTERN);
  if (attachmentMatch) {
    return `attachment://${attachmentMatch[1]}`;
  }

  if (/^attachment:/i.test(normalized)) {
    return null;
  }

  if (HTTP_PROTOCOL_PATTERN.test(normalized)) {
    return isValidHttpUrl(normalized) ? normalized : null;
  }

  if (URI_SCHEME_PATTERN.test(normalized)) {
    return null;
  }

  const slashNormalized = normalized.replace(/\\/g, '/');
  if (slashNormalized.startsWith('//')) {
    return null;
  }

  return normalized;
}

export function resolveSanitizedMarkdownLinkHref(
  value: string | null | undefined,
  resolver: (safeHref: string) => string | null | undefined
): string | null {
  const safeHref = sanitizeMarkdownLinkHref(value);
  if (!safeHref) {
    return null;
  }

  return sanitizeMarkdownLinkHref(resolver(safeHref));
}

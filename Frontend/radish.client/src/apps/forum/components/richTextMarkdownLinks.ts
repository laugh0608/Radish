import { sanitizeMarkdownLinkHref } from '@radish/ui/markdown-url';

const escapeHtml = (value: string) =>
  value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');

export function buildSanitizedRichLinkHtml(
  markdownHref: string,
  text: string,
  resolvedHref = markdownHref,
  attachmentId?: string
): string {
  const safeMarkdownHref = sanitizeMarkdownLinkHref(markdownHref);
  const escapedText = escapeHtml(text.trim() || safeMarkdownHref || markdownHref);
  if (!safeMarkdownHref) {
    return escapedText;
  }

  const safeResolvedHref = sanitizeMarkdownLinkHref(resolvedHref);
  if (!safeResolvedHref) {
    return escapedText;
  }

  const attachmentAttrs = attachmentId
    ? ` data-attachment-id="${escapeHtml(attachmentId)}"`
    : '';
  return `<a href="${escapeHtml(safeResolvedHref)}" data-markdown-href="${escapeHtml(safeMarkdownHref)}"${attachmentAttrs} target="_blank" rel="noreferrer">${escapedText}</a>`;
}

export function serializeRichTextLinkToMarkdown(text: string, href: string): string {
  const safeHref = sanitizeMarkdownLinkHref(href);
  const normalizedText = text.trim();
  if (!safeHref) {
    return normalizedText;
  }

  return `[${normalizedText || safeHref}](${safeHref})`;
}

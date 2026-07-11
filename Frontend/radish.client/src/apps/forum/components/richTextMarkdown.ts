import {
  buildAttachmentAssetUrl,
  buildAttachmentMarkdownUrl,
  parseAttachmentMarkdownUrl,
  resolveConfiguredMediaUrl,
} from '@radish/ui/attachment-markdown';
import { sanitizeMarkdownLinkHref } from '@radish/ui/markdown-url';
import {
  buildSanitizedRichLinkHtml,
  serializeRichTextLinkToMarkdown,
} from './richTextMarkdownLinks';

const escapeHtml = (value: string) =>
  value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');

const decodeHtmlAttribute = (value: string) =>
  value
    .replaceAll('&quot;', '"')
    .replaceAll('&#39;', "'")
    .replaceAll('&lt;', '<')
    .replaceAll('&gt;', '>')
    .replaceAll('&amp;', '&');

export const buildRichImageHtml = (markdownSrc: string, altText: string): string => {
  const attachmentMeta = parseAttachmentMarkdownUrl(markdownSrc);
  const escapedAltText = escapeHtml(altText);
  if (!attachmentMeta) {
    return `<img src="${escapeHtml(resolveConfiguredMediaUrl(markdownSrc))}" alt="${escapedAltText}" />`;
  }

  const previewSrc = buildAttachmentAssetUrl(attachmentMeta.attachmentId, attachmentMeta.displayVariant);
  const scaleStyle = attachmentMeta.scalePercent
    ? ` style="width:${attachmentMeta.scalePercent}%;max-width:100%;"`
    : '';
  const scaleAttr = attachmentMeta.scalePercent
    ? ` data-scale-percent="${attachmentMeta.scalePercent}"`
    : '';

  return `<img src="${escapeHtml(previewSrc)}" data-markdown-src="${escapeHtml(markdownSrc)}" data-attachment-id="${attachmentMeta.attachmentId}" data-display-variant="${attachmentMeta.displayVariant}"${scaleAttr} alt="${escapedAltText}"${scaleStyle} />`;
};

export const buildRichLinkHtml = (markdownHref: string, text: string): string => {
  const safeMarkdownHref = sanitizeMarkdownLinkHref(markdownHref);
  if (!safeMarkdownHref) {
    return buildSanitizedRichLinkHtml(markdownHref, text);
  }

  const attachmentMeta = parseAttachmentMarkdownUrl(safeMarkdownHref);
  const resolvedHref = attachmentMeta
    ? buildAttachmentAssetUrl(attachmentMeta.attachmentId, 'original')
    : resolveConfiguredMediaUrl(safeMarkdownHref);
  return buildSanitizedRichLinkHtml(
    safeMarkdownHref,
    text,
    resolvedHref,
    attachmentMeta?.attachmentId
  );
};

const inlineMarkdownToHtml = (value: string) => {
  let html = escapeHtml(value);
  html = html.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, (_, alt: string, src: string) =>
    buildRichImageHtml(decodeHtmlAttribute(src), decodeHtmlAttribute(alt))
  );
  html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, text: string, href: string) =>
    buildRichLinkHtml(decodeHtmlAttribute(href), decodeHtmlAttribute(text))
  );
  html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  html = html.replace(/~~([^~]+)~~/g, '<s>$1</s>');
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
  return html;
};

export const markdownToRichHtml = (markdown: string) => {
  const normalized = markdown.replace(/\r\n/g, '\n').trim();
  if (!normalized) {
    return '';
  }

  const lines = normalized.split('\n');
  const blocks: string[] = [];
  let index = 0;

  while (index < lines.length) {
    const line = lines[index];
    const trimmed = line.trim();

    if (!trimmed) {
      index += 1;
      continue;
    }

    if (trimmed.startsWith('```')) {
      const codeLines: string[] = [];
      index += 1;
      while (index < lines.length && !lines[index].trim().startsWith('```')) {
        codeLines.push(lines[index]);
        index += 1;
      }
      if (index < lines.length) {
        index += 1;
      }
      blocks.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
      continue;
    }

    const headingMatch = trimmed.match(/^(#{1,6})\s+(.+)$/);
    if (headingMatch) {
      const level = headingMatch[1].length;
      blocks.push(`<h${level}>${inlineMarkdownToHtml(headingMatch[2])}</h${level}>`);
      index += 1;
      continue;
    }

    if (/^---+$/.test(trimmed)) {
      blocks.push('<hr />');
      index += 1;
      continue;
    }

    if (trimmed.startsWith('>')) {
      const quoteLines: string[] = [];
      while (index < lines.length && lines[index].trim().startsWith('>')) {
        quoteLines.push(lines[index].trim().replace(/^>\s?/, ''));
        index += 1;
      }
      blocks.push(`<blockquote><p>${quoteLines.map(inlineMarkdownToHtml).join('<br />')}</p></blockquote>`);
      continue;
    }

    if (/^[-*+]\s+/.test(trimmed)) {
      const items: string[] = [];
      while (index < lines.length && /^[-*+]\s+/.test(lines[index].trim())) {
        items.push(`<li>${inlineMarkdownToHtml(lines[index].trim().replace(/^[-*+]\s+/, ''))}</li>`);
        index += 1;
      }
      blocks.push(`<ul>${items.join('')}</ul>`);
      continue;
    }

    if (/^\d+\.\s+/.test(trimmed)) {
      const items: string[] = [];
      while (index < lines.length && /^\d+\.\s+/.test(lines[index].trim())) {
        items.push(`<li>${inlineMarkdownToHtml(lines[index].trim().replace(/^\d+\.\s+/, ''))}</li>`);
        index += 1;
      }
      blocks.push(`<ol>${items.join('')}</ol>`);
      continue;
    }

    const paragraphLines: string[] = [];
    while (index < lines.length && lines[index].trim()) {
      const currentTrimmed = lines[index].trim();
      if (
        currentTrimmed.startsWith('```') ||
        /^#{1,6}\s+/.test(currentTrimmed) ||
        /^---+$/.test(currentTrimmed) ||
        currentTrimmed.startsWith('>') ||
        /^[-*+]\s+/.test(currentTrimmed) ||
        /^\d+\.\s+/.test(currentTrimmed)
      ) {
        break;
      }
      paragraphLines.push(lines[index]);
      index += 1;
    }
    blocks.push(`<p>${paragraphLines.map(inlineMarkdownToHtml).join('<br />')}</p>`);
  }

  return blocks.join('');
};

export const normalizeMarkdown = (value: string) =>
  value
    .replace(/\u00a0/g, ' ')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();

const rebuildAttachmentMarkdownUrlFromDataset = (element: HTMLElement): string | null => {
  const attachmentId = element.getAttribute('data-attachment-id');
  if (!attachmentId) {
    return null;
  }

  const displayVariant = element.getAttribute('data-display-variant');
  const scalePercentRaw = element.getAttribute('data-scale-percent');
  const scalePercent = scalePercentRaw ? Number(scalePercentRaw) : undefined;

  try {
    return buildAttachmentMarkdownUrl(attachmentId, {
      displayVariant: displayVariant === 'thumbnail' ? 'thumbnail' : 'original',
      scalePercent: Number.isFinite(scalePercent) ? scalePercent : undefined,
    });
  } catch {
    return null;
  }
};

const serializeNode = (node: Node): string => {
  if (node.nodeType === Node.TEXT_NODE) {
    return node.textContent ?? '';
  }

  if (!(node instanceof HTMLElement)) {
    return '';
  }

  const tagName = node.tagName.toLowerCase();

  switch (tagName) {
    case 'br':
      return '\n';
    case 'strong':
    case 'b':
      return `**${serializeChildren(node)}**`;
    case 'em':
    case 'i':
      return `*${serializeChildren(node)}*`;
    case 's':
    case 'strike':
    case 'del':
      return `~~${serializeChildren(node)}~~`;
    case 'code':
      if (node.parentElement?.tagName.toLowerCase() === 'pre') {
        return node.textContent ?? '';
      }
      return `\`${serializeChildren(node)}\``;
    case 'pre':
      return `\`\`\`\n${node.textContent?.replace(/\n+$/, '') ?? ''}\n\`\`\`\n\n`;
    case 'h1':
    case 'h2':
    case 'h3':
    case 'h4':
    case 'h5':
    case 'h6': {
      const level = Number(tagName.slice(1));
      return `${'#'.repeat(level)} ${serializeChildren(node).trim()}\n\n`;
    }
    case 'blockquote': {
      const content = normalizeMarkdown(serializeChildren(node));
      return content
        .split('\n')
        .map(line => `> ${line}`)
        .join('\n')
        .concat('\n\n');
    }
    case 'ul':
      return Array.from(node.children)
        .map(child => `- ${normalizeMarkdown(serializeChildren(child)).replace(/\n/g, ' ')}`)
        .join('\n')
        .concat('\n\n');
    case 'ol':
      return Array.from(node.children)
        .map((child, index) => `${index + 1}. ${normalizeMarkdown(serializeChildren(child)).replace(/\n/g, ' ')}`)
        .join('\n')
        .concat('\n\n');
    case 'li':
      return serializeChildren(node);
    case 'a': {
      const href = node.getAttribute('data-markdown-href')
        ?? rebuildAttachmentMarkdownUrlFromDataset(node)
        ?? node.getAttribute('href')
        ?? '';
      return serializeRichTextLinkToMarkdown(serializeChildren(node), href);
    }
    case 'img': {
      const src = node.getAttribute('data-markdown-src')
        ?? rebuildAttachmentMarkdownUrlFromDataset(node)
        ?? node.getAttribute('src')
        ?? '';
      const alt = node.getAttribute('alt') ?? '图片';
      return `![${alt}](${src})`;
    }
    case 'hr':
      return '---\n\n';
    case 'p':
      return `${serializeChildren(node).trim()}\n\n`;
    case 'div':
      return `${serializeChildren(node)}${node.childNodes.length > 0 ? '\n' : ''}`;
    default:
      return serializeChildren(node);
  }
};

const serializeChildren = (node: Node) =>
  Array.from(node.childNodes)
    .map(child => serializeNode(child))
    .join('');

export const htmlToMarkdown = (html: string) => {
  const parser = new DOMParser();
  const doc = parser.parseFromString(`<div>${html}</div>`, 'text/html');
  const root = doc.body.firstElementChild;
  if (!root) {
    return '';
  }

  return normalizeMarkdown(serializeChildren(root));
};

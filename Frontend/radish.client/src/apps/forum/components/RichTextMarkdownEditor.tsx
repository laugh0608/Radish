import { useEffect, useMemo, useRef, useState, type ReactNode, type ClipboardEvent } from 'react';
import { Icon } from '@radish/ui/icon';
import styles from './RichTextMarkdownEditor.module.css';

interface RichTextMarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  minHeight?: number;
  disabled?: boolean;
  toolbarExtras?: ReactNode;
  className?: string;
  onImageUpload?: (file: File) => Promise<{ url: string; thumbnailUrl?: string }>;
  onDocumentUpload?: (file: File) => Promise<{ url: string; fileName: string }>;
}

const escapeHtml = (value: string) =>
  value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');

const inlineMarkdownToHtml = (value: string) => {
  let html = escapeHtml(value);
  html = html.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '<img src="$2" alt="$1" />');
  html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" rel="noreferrer">$1</a>');
  html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  html = html.replace(/~~([^~]+)~~/g, '<s>$1</s>');
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
  return html;
};

const markdownToRichHtml = (markdown: string) => {
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

const normalizeMarkdown = (value: string) =>
  value
    .replace(/\u00a0/g, ' ')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();

const serializeNode = (node: Node, orderedIndex = 1): string => {
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
      const href = node.getAttribute('href') ?? '';
      return `[${serializeChildren(node).trim() || href}](${href})`;
    }
    case 'img': {
      const src = node.getAttribute('src') ?? '';
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
    .map((child, index) => serializeNode(child, index + 1))
    .join('');

const htmlToMarkdown = (html: string) => {
  const parser = new DOMParser();
  const doc = parser.parseFromString(`<div>${html}</div>`, 'text/html');
  const root = doc.body.firstElementChild;
  if (!root) {
    return '';
  }

  return normalizeMarkdown(serializeChildren(root));
};

export const RichTextMarkdownEditor = ({
  value,
  onChange,
  placeholder = '直接输入正文，底层会保存为 Markdown',
  minHeight = 320,
  disabled = false,
  toolbarExtras,
  className = '',
  onImageUpload,
  onDocumentUpload
}: RichTextMarkdownEditorProps) => {
  const editorRef = useRef<HTMLDivElement>(null);
  const imageInputRef = useRef<HTMLInputElement>(null);
  const documentInputRef = useRef<HTMLInputElement>(null);
  const lastEmittedMarkdownRef = useRef(value);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const editorHtml = useMemo(() => markdownToRichHtml(value), [value]);

  useEffect(() => {
    if (!editorRef.current || value === lastEmittedMarkdownRef.current) {
      return;
    }

    editorRef.current.innerHTML = editorHtml;
  }, [editorHtml, value]);

  const syncMarkdownFromDom = () => {
    const html = editorRef.current?.innerHTML ?? '';
    const markdown = htmlToMarkdown(html);
    lastEmittedMarkdownRef.current = markdown;
    onChange(markdown);
  };

  const focusEditor = () => {
    editorRef.current?.focus();
  };

  const runCommand = (command: string, commandValue?: string) => {
    if (disabled || uploading) {
      return;
    }

    focusEditor();
    document.execCommand(command, false, commandValue);
    syncMarkdownFromDom();
  };

  const insertHtml = (html: string) => {
    if (disabled || uploading) {
      return;
    }

    focusEditor();
    document.execCommand('insertHTML', false, html);
    syncMarkdownFromDom();
  };

  const handlePaste = (event: ClipboardEvent<HTMLDivElement>) => {
    event.preventDefault();
    const text = event.clipboardData.getData('text/plain');
    document.execCommand('insertText', false, text);
    syncMarkdownFromDom();
  };

  const handleImageSelection = async (file: File) => {
    if (!onImageUpload) {
      insertHtml('<p><em>请在 Markdown 模式中插入图片</em></p>');
      return;
    }

    setUploading(true);
    setUploadError(null);
    try {
      const result = await onImageUpload(file);
      insertHtml(`<p><img src="${result.url}" alt="${escapeHtml(file.name)}" /></p>`);
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : '图片上传失败');
    } finally {
      setUploading(false);
    }
  };

  const handleDocumentSelection = async (file: File) => {
    if (!onDocumentUpload) {
      return;
    }

    setUploading(true);
    setUploadError(null);
    try {
      const result = await onDocumentUpload(file);
      insertHtml(
        `<p><a href="${result.url}" target="_blank" rel="noreferrer">${escapeHtml(result.fileName || file.name)}</a></p>`
      );
    } catch (error) {
      setUploadError(error instanceof Error ? error.message : '文档上传失败');
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className={`${styles.container} ${className}`.trim()}>
      <div className={styles.toolbar}>
        <div className={styles.toolbarGroup}>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('formatBlock', 'h2')} title="标题" disabled={disabled}>
            <Icon icon="mdi:format-header-2" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('bold')} title="粗体" disabled={disabled}>
            <Icon icon="mdi:format-bold" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('italic')} title="斜体" disabled={disabled}>
            <Icon icon="mdi:format-italic" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('strikeThrough')} title="删除线" disabled={disabled}>
            <Icon icon="mdi:format-strikethrough-variant" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('formatBlock', 'blockquote')} title="引用" disabled={disabled}>
            <Icon icon="mdi:format-quote-close" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml('<code>行内代码</code>')} title="行内代码" disabled={disabled}>
            <Icon icon="mdi:code-tags" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml('<pre><code>代码块</code></pre>')} title="代码块" disabled={disabled}>
            <Icon icon="mdi:code-braces-box" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('insertUnorderedList')} title="无序列表" disabled={disabled}>
            <Icon icon="mdi:format-list-bulleted" size={18} />
          </button>
          <button type="button" className={styles.toolbarButton} onClick={() => runCommand('insertOrderedList')} title="有序列表" disabled={disabled}>
            <Icon icon="mdi:format-list-numbered" size={18} />
          </button>
          <button
            type="button"
            className={styles.toolbarButton}
            onClick={() => {
              const href = window.prompt('输入链接地址');
              if (href) {
                runCommand('createLink', href);
              }
            }}
            title="链接"
            disabled={disabled}
          >
            <Icon icon="mdi:link-variant" size={18} />
          </button>
          <button
            type="button"
            className={styles.toolbarButton}
            onClick={() => imageInputRef.current?.click()}
            title="图片"
            disabled={disabled || uploading}
          >
            <Icon icon="mdi:image-outline" size={18} />
          </button>
          {onDocumentUpload && (
            <button
              type="button"
              className={styles.toolbarButton}
              onClick={() => documentInputRef.current?.click()}
              title="附件"
              disabled={disabled || uploading}
            >
              <Icon icon="mdi:file-document-outline" size={18} />
            </button>
          )}
          <button type="button" className={styles.toolbarButton} onClick={() => insertHtml('<hr />')} title="分割线" disabled={disabled}>
            <Icon icon="mdi:minus" size={18} />
          </button>
        </div>

        <div className={styles.toolbarSpacer} />

        {toolbarExtras && <div className={styles.toolbarExtras}>{toolbarExtras}</div>}
      </div>

      <div className={styles.editorShell} style={{ minHeight }}>
        <div
          ref={editorRef}
          className={styles.editor}
          contentEditable={!disabled && !uploading}
          suppressContentEditableWarning={true}
          data-placeholder={placeholder}
          onInput={syncMarkdownFromDom}
          onPaste={handlePaste}
          dangerouslySetInnerHTML={{ __html: editorHtml }}
        />

        {uploading && (
          <div className={styles.overlay}>
            <Icon icon="mdi:loading" size={20} className={styles.spinIcon} />
            <span>上传中...</span>
          </div>
        )}

        {uploadError && (
          <div className={styles.error}>
            <span>{uploadError}</span>
            <button type="button" className={styles.errorDismiss} onClick={() => setUploadError(null)}>
              <Icon icon="mdi:close" size={14} />
            </button>
          </div>
        )}
      </div>

      <input
        ref={imageInputRef}
        type="file"
        accept="image/*"
        hidden
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) {
            void handleImageSelection(file);
          }
          event.currentTarget.value = '';
        }}
      />
      <input
        ref={documentInputRef}
        type="file"
        hidden
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) {
            void handleDocumentSelection(file);
          }
          event.currentTarget.value = '';
        }}
      />
    </div>
  );
};

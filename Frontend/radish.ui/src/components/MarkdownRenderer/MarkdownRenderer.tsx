/**
 * Markdown 渲染器组件
 *
 * 依赖包（需要在 Windows 中安装）：
 * npm install --workspace=@radish/ui react-markdown remark-gfm rehype-highlight highlight.js
 *
 * 注意：react-markdown v9 已内置 TypeScript 类型定义，无需安装 @types/react-markdown
 */

import { useMemo, useState } from 'react';
import ReactMarkdown, { defaultUrlTransform } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeHighlight from 'rehype-highlight';
import { ImageLightbox } from '../ImageLightbox/ImageLightbox';
import { buildAttachmentAssetUrl, parseAttachmentMarkdownUrl, resolveConfiguredMediaUrl } from '../../utils';
import styles from './MarkdownRenderer.module.css';

export interface MarkdownStickerItem {
  imageUrl: string;
  thumbnailUrl?: string | null;
  name?: string | null;
}

export type MarkdownStickerMap = Record<string, MarkdownStickerItem>;

interface MarkdownRendererProps {
  /** Markdown 内容 */
  content: string;
  /** 自定义类名 */
  className?: string;
  /** 是否启用图片灯箱 */
  enableImageLightbox?: boolean;
  /** sticker:// 资源映射 */
  stickerMap?: MarkdownStickerMap;
}

interface ParsedImageMeta {
  displaySrc: string;
  fullSrc: string;
  scalePercent?: number;
}

interface ParsedStickerUri {
  groupCode: string;
  stickerCode: string;
  fallbackImageUrl?: string;
  fallbackThumbnailUrl?: string;
}

const normalizeStickerKey = (groupCode: string, stickerCode: string): string =>
  `${groupCode.trim().toLowerCase()}/${stickerCode.trim().toLowerCase()}`;

const isSafeStickerUrl = (value?: string | null): value is string => {
  if (typeof value !== 'string') {
    return false;
  }

  const normalized = value.trim();
  if (!normalized) {
    return false;
  }

  // 允许站内相对路径（如 /uploads/...）和 http(s) 绝对路径
  return normalized.startsWith('/') || /^https?:\/\//i.test(normalized);
};

const parseStickerUri = (src: string): ParsedStickerUri | null => {
  const raw = src.trim();
  if (!raw.startsWith('sticker://')) {
    return null;
  }

  const withoutProtocol = raw.slice('sticker://'.length);
  const [pathPart, hashPart] = withoutProtocol.split('#');
  const [groupCodeRaw, stickerCodeRaw] = pathPart.split('/');
  const groupCode = decodeURIComponent(groupCodeRaw || '').trim();
  const stickerCode = decodeURIComponent(stickerCodeRaw || '').trim();

  if (!groupCode || !stickerCode) {
    return null;
  }

  const parsed: ParsedStickerUri = {
    groupCode,
    stickerCode,
  };

  if (hashPart && hashPart.startsWith('radish:')) {
    const params = new URLSearchParams(hashPart.slice('radish:'.length));
    const image = params.get('image');
    const thumbnail = params.get('thumbnail');

    if (isSafeStickerUrl(image)) {
      parsed.fallbackImageUrl = image;
    }
    if (isSafeStickerUrl(thumbnail)) {
      parsed.fallbackThumbnailUrl = thumbnail;
    }
  }

  return parsed;
};

const parseImageMeta = (src: string): ParsedImageMeta => {
  const attachmentMeta = parseAttachmentMarkdownUrl(src);
  if (attachmentMeta) {
    return {
      displaySrc: buildAttachmentAssetUrl(attachmentMeta.attachmentId, attachmentMeta.displayVariant),
      fullSrc: buildAttachmentAssetUrl(attachmentMeta.attachmentId, 'original'),
      scalePercent: attachmentMeta.scalePercent,
    };
  }

  const [baseSrc, hash] = src.split('#');
  const normalizedBaseSrc = resolveConfiguredMediaUrl(baseSrc || src);
  const fallback = {
    displaySrc: normalizedBaseSrc,
    fullSrc: normalizedBaseSrc,
  };

  if (!hash || !hash.startsWith('radish:')) {
    return fallback;
  }

  const metaPart = hash.slice('radish:'.length);
  const params = new URLSearchParams(metaPart);

  const fullRaw = params.get('full');
  const scaleRaw = params.get('scale');
  const scaleNum = scaleRaw ? Number(scaleRaw) : undefined;
  const safeScale = Number.isFinite(scaleNum) && scaleNum! > 0 ? Math.min(Math.max(scaleNum!, 10), 100) : undefined;

  return {
    displaySrc: normalizedBaseSrc,
    fullSrc: resolveConfiguredMediaUrl(fullRaw || baseSrc || src),
    scalePercent: safeScale,
  };
};

const resolveLinkHref = (href: string): string => {
  const attachmentMeta = parseAttachmentMarkdownUrl(href);
  if (attachmentMeta) {
    return buildAttachmentAssetUrl(attachmentMeta.attachmentId, 'original');
  }

  return resolveConfiguredMediaUrl(href);
};

const isRadishCustomProtocol = (value: string): boolean =>
  /^attachment:\/\//i.test(value) || /^sticker:\/\//i.test(value);

/**
 * Markdown 渲染器
 *
 * 支持的功能：
 * - 标准 Markdown 语法（标题、列表、链接、图片等）
 * - GitHub Flavored Markdown（表格、删除线、任务列表）
 * - 代码高亮（支持多种编程语言）
 * - 自动链接转换
 * - XSS 防护（默认开启）
 */
export const MarkdownRenderer: React.FC<MarkdownRendererProps> = ({
  content,
  className,
  enableImageLightbox = true,
  stickerMap,
}) => {
  const imageCollection = useMemo(() => {
    const regex = /!\[[^\]]*\]\(([^)]+)\)/g;
    const images: { src: string; alt?: string }[] = [];
    let match: RegExpExecArray | null;

    while ((match = regex.exec(content)) !== null) {
      const src = match[1]?.trim();
      if (!src) continue;
      if (parseStickerUri(src)) continue;
      const parsed = parseImageMeta(src);
      images.push({ src: parsed.fullSrc });
    }

    return images;
  }, [content]);

  const [lightboxOpen, setLightboxOpen] = useState(false);
  const [lightboxIndex, setLightboxIndex] = useState(0);

  const openLightbox = (targetSrc: string) => {
    const index = imageCollection.findIndex((item) => item.src === targetSrc);
    setLightboxIndex(index >= 0 ? index : 0);
    setLightboxOpen(true);
  };

  return (
    <>
      <div className={`${styles.markdownBody} ${className || ''}`}>
        <ReactMarkdown
          remarkPlugins={[remarkGfm]}
          rehypePlugins={[rehypeHighlight]}
          urlTransform={(url, key, node) => {
            if (isRadishCustomProtocol(url)) {
              return url;
            }

            return defaultUrlTransform(url);
          }}
          components={{
            // 自定义链接行为：在新标签页打开外部链接
            a: ({ node, ...props }) => {
              const href = props.href || '';
              const attachmentMeta = parseAttachmentMarkdownUrl(href);
              const resolvedHref = resolveLinkHref(href);
              const isExternal = href.startsWith('http://') || href.startsWith('https://');
              const openInNewTab = isExternal || Boolean(attachmentMeta);

              if (openInNewTab) {
                return (
                  <a
                    {...props}
                    href={resolvedHref}
                    target="_blank"
                    rel="noopener noreferrer"
                  />
                );
              }

              return <a {...props} href={resolvedHref} />;
            },
            img: ({ node, ...props }) => {
              const rawSrc = props.src || '';
              const stickerMeta = parseStickerUri(rawSrc);
              if (stickerMeta) {
                const stickerKey = normalizeStickerKey(stickerMeta.groupCode, stickerMeta.stickerCode);
                const stickerData = stickerMap?.[stickerKey];
                const stickerSrc = stickerData?.imageUrl
                  || stickerMeta.fallbackImageUrl
                  || stickerMeta.fallbackThumbnailUrl;
                const stickerAlt = props.alt || stickerData?.name || `${stickerMeta.groupCode}/${stickerMeta.stickerCode}`;

                if (!stickerSrc) {
                  return (
                    <span className={styles.stickerMissing}>
                      :{stickerMeta.groupCode}/{stickerMeta.stickerCode}:
                    </span>
                  );
                }

                return (
                    <img
                      alt={stickerAlt}
                      src={resolveConfiguredMediaUrl(stickerSrc)}
                      title={stickerAlt}
                    loading="lazy"
                    draggable={false}
                    className={styles.stickerInline}
                  />
                );
              }

              const parsed = parseImageMeta(rawSrc);

              return (
                <img
                  {...props}
                  src={parsed.displaySrc}
                  data-full-src={parsed.fullSrc}
                  style={
                    parsed.scalePercent
                      ? {
                          width: `${parsed.scalePercent}%`,
                          maxWidth: '100%',
                        }
                      : undefined
                  }
                  className={`${styles.markdownImage} ${enableImageLightbox ? styles.clickableImage : ''}`}
                  onClick={() => {
                    if (!enableImageLightbox) return;
                    openLightbox(parsed.fullSrc);
                  }}
                />
              );
            },
            // 为代码块添加复制按钮的容器
            pre: ({ node, ...props }) => {
              return <pre className={styles.codeBlock} {...props} />;
            },
            // 为表格添加包装器以支持横向滚动
            table: ({ node, ...props }) => {
              return (
                <div className={styles.tableWrapper}>
                  <table {...props} />
                </div>
              );
            },
          }}
        >
          {content}
        </ReactMarkdown>
      </div>

      {enableImageLightbox && (
        <ImageLightbox
          isOpen={lightboxOpen}
          images={imageCollection}
          initialIndex={lightboxIndex}
          onClose={() => setLightboxOpen(false)}
        />
      )}
    </>
  );
};

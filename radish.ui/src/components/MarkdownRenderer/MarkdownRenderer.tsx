/**
 * Markdown 渲染器组件
 *
 * 依赖包（需要在 Windows 中安装）：
 * npm install --workspace=@radish/ui react-markdown remark-gfm rehype-highlight highlight.js
 *
 * 注意：react-markdown v9 已内置 TypeScript 类型定义，无需安装 @types/react-markdown
 */

import { useMemo, useState } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeHighlight from 'rehype-highlight';
import { ImageLightbox } from '../ImageLightbox/ImageLightbox';
import styles from './MarkdownRenderer.module.css';

interface MarkdownRendererProps {
  /** Markdown 内容 */
  content: string;
  /** 自定义类名 */
  className?: string;
  /** 是否启用图片灯箱 */
  enableImageLightbox?: boolean;
}

interface ParsedImageMeta {
  displaySrc: string;
  fullSrc: string;
  scalePercent?: number;
}

const parseImageMeta = (src: string): ParsedImageMeta => {
  const [baseSrc, hash] = src.split('#');
  const fallback = {
    rawSrc: src,
    displaySrc: baseSrc || src,
    fullSrc: baseSrc || src,
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
    displaySrc: baseSrc || src,
    fullSrc: fullRaw || baseSrc || src,
    scalePercent: safeScale,
  };
};

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
}) => {
  const imageCollection = useMemo(() => {
    const regex = /!\[[^\]]*\]\(([^)]+)\)/g;
    const images: { src: string; alt?: string }[] = [];
    let match: RegExpExecArray | null;

    while ((match = regex.exec(content)) !== null) {
      const src = match[1]?.trim();
      if (!src) continue;
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
          components={{
            // 自定义链接行为：在新标签页打开外部链接
            a: ({ node, ...props }) => {
              const href = props.href || '';
              const isExternal = href.startsWith('http://') || href.startsWith('https://');

              if (isExternal) {
                return (
                  <a
                    {...props}
                    target="_blank"
                    rel="noopener noreferrer"
                  />
                );
              }

              return <a {...props} />;
            },
            img: ({ node, ...props }) => {
              const rawSrc = props.src || '';
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
            }
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

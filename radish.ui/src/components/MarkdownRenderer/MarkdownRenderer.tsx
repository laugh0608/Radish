/**
 * Markdown 渲染器组件
 *
 * 依赖包（需要在 Windows 中安装）：
 * npm install --workspace=@radish/ui react-markdown remark-gfm rehype-highlight highlight.js
 *
 * 注意：react-markdown v9 已内置 TypeScript 类型定义，无需安装 @types/react-markdown
 */

import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeHighlight from 'rehype-highlight';
import styles from './MarkdownRenderer.module.css';

interface MarkdownRendererProps {
  /** Markdown 内容 */
  content: string;
  /** 自定义类名 */
  className?: string;
}

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
  className
}) => {
  return (
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
  );
};

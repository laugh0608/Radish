import { useEffect, useMemo, useState } from 'react';
import { Icon } from '@radish/ui';
import styles from './UserAttachmentList.module.css';

interface Attachment {
  id: string | number;
  originalName: string;
  extension: string;
  fileSize: number;
  fileSizeFormatted?: string;
  mimeType: string;
  url: string;
  thumbnailUrl?: string | null;
  businessType?: string;
  createTime: string;
}

interface PageModel<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}

interface ApiResponse<T> {
  isSuccess: boolean;
  messageInfo?: string;
  responseData?: T;
}

interface UserAttachmentListProps {
  apiBaseUrl: string;
}

function isImageExtension(extension: string): boolean {
  const ext = extension.toLowerCase();
  return ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg'].includes(ext);
}

function resolveUrl(apiBaseUrl: string, url: string): string {
  if (!url) return '';
  if (/^https?:\/\//i.test(url)) return url;
  if (url.startsWith('/')) return `${apiBaseUrl}${url}`;
  return `${apiBaseUrl}/${url}`;
}

export const UserAttachmentList = ({ apiBaseUrl }: UserAttachmentListProps) => {
  const [attachments, setAttachments] = useState<Attachment[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const authHeader = useMemo(() => {
    if (typeof window === 'undefined') return undefined;
    const token = window.localStorage.getItem('access_token');
    return token ? `Bearer ${token}` : undefined;
  }, []);

  useEffect(() => {
    void loadAttachments();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  const loadAttachments = async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch(
        `${apiBaseUrl}/api/v1/Attachment/GetMyAttachments?pageIndex=${page}&pageSize=10`,
        {
          headers: {
            Accept: 'application/json',
            ...(authHeader ? { Authorization: authHeader } : {})
          }
        }
      );

      const json = (await response.json()) as ApiResponse<PageModel<Attachment>>;

      if (!response.ok) {
        setError(json.messageInfo || `请求失败: HTTP ${response.status}`);
        setAttachments([]);
        setTotalPages(1);
        return;
      }

      if (json.isSuccess && json.responseData) {
        setAttachments(json.responseData.data || []);
        setTotalPages(json.responseData.pageCount || 1);
      } else {
        setError(json.messageInfo || '加载附件失败');
        setAttachments([]);
        setTotalPages(1);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setAttachments([]);
      setTotalPages(1);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <div className={styles.loading}>加载中...</div>;
  }

  if (error) {
    return <div className={styles.error}>加载失败：{error}</div>;
  }

  if (attachments.length === 0) {
    return <div className={styles.empty}>还没有上传过附件</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.list}>
        {attachments.map(att => {
          const isImage = isImageExtension(att.extension);
          const href = resolveUrl(apiBaseUrl, att.url);

          return (
            <div key={String(att.id)} className={styles.item}>
              <div className={styles.fileRow}>
                <div className={styles.icon}>
                  <Icon icon={isImage ? 'mdi:image' : 'mdi:file-document-outline'} size={22} />
                </div>

                {att.thumbnailUrl && isImage && (
                  <img
                    className={styles.thumbnail}
                    src={resolveUrl(apiBaseUrl, att.thumbnailUrl)}
                    alt={att.originalName}
                    loading="lazy"
                  />
                )}

                <div className={styles.fileInfo}>
                  <div className={styles.fileName} title={att.originalName}>
                    {att.originalName}
                  </div>
                  <div className={styles.meta}>
                    <span className={styles.metaItem}>{att.fileSizeFormatted || `${att.fileSize} B`}</span>
                    <span className={styles.metaItem}>{att.businessType || 'General'}</span>
                    <span className={styles.metaItem}>
                      {att.createTime ? new Date(att.createTime).toLocaleDateString('zh-CN') : ''}
                    </span>
                  </div>
                </div>

                <div className={styles.actions}>
                  {href ? (
                    <a className={styles.link} href={href} target="_blank" rel="noreferrer">
                      下载
                    </a>
                  ) : (
                    <span className={styles.disabledLink}>无链接</span>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
            className={styles.pageButton}
          >
            上一页
          </button>
          <span className={styles.pageInfo}>
            {page} / {totalPages}
          </span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page === totalPages}
            className={styles.pageButton}
          >
            下一页
          </button>
        </div>
      )}
    </div>
  );
};

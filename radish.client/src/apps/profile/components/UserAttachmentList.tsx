import { useEffect, useMemo, useState } from 'react';
import { ConfirmDialog, Icon, Input, Select } from '@radish/ui';
import { copyToClipboard } from '@/utils/clipboard';
import { deleteAttachment } from '@/api/attachment';
import { useTranslation } from 'react-i18next';
import styles from './UserAttachmentList.module.css';

interface Attachment {
  voId: string | number;
  voOriginalName: string;
  voExtension?: string;
  voFileSize: number;
  voFileSizeFormatted?: string;
  voMimeType: string;
  voUrl: string;
  voThumbnailUrl?: string | null;
  voBusinessType?: string;
  voCreateTime: string;
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

type BusinessTypeFilter = 'All' | 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document';

function isImageExtension(extension: string | undefined | null): boolean {
  if (!extension) return false;
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
  const { t } = useTranslation();

  const [attachments, setAttachments] = useState<Attachment[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [error, setError] = useState<string | null>(null);

  const [businessType, setBusinessType] = useState<BusinessTypeFilter>('All');
  const [keyword, setKeyword] = useState('');

  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);
  const [deleteTargetId, setDeleteTargetId] = useState<string | number | null>(null);

  const authHeader = useMemo(() => {
    if (typeof window === 'undefined') return undefined;
    const token = window.localStorage.getItem('access_token');
    return token ? `Bearer ${token}` : undefined;
  }, []);

  useEffect(() => {
    void loadAttachments();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, businessType]);

  const loadAttachments = async () => {
    setLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams({
        pageIndex: String(page),
        pageSize: '10'
      });

      if (businessType !== 'All') {
        params.set('businessType', businessType);
      }

      if (keyword.trim()) {
        params.set('keyword', keyword.trim());
      }

      const response = await fetch(
        `${apiBaseUrl}/api/v1/Attachment/GetMyAttachments?${params.toString()}`,
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

  const businessOptions = useMemo(
    () => [
      { value: 'All', label: '全部' },
      { value: 'General', label: 'General' },
      { value: 'Post', label: 'Post' },
      { value: 'Comment', label: 'Comment' },
      { value: 'Avatar', label: 'Avatar' },
      { value: 'Document', label: 'Document' }
    ],
    []
  );

  const openDeleteConfirm = (id: string | number) => {
    setDeleteTargetId(id);
    setConfirmDeleteOpen(true);
  };

  const confirmDelete = async () => {
    if (!deleteTargetId) {
      setConfirmDeleteOpen(false);
      return;
    }

    setConfirmDeleteOpen(false);

    try {
      await deleteAttachment(deleteTargetId, t);
      await loadAttachments();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setDeleteTargetId(null);
    }
  };

  const handleCopyLink = async (url: string) => {
    try {
      await copyToClipboard(url);
    } catch {
      // ignore
    }
  };

  if (loading) {
    return <div className={styles.loading}>加载中...</div>;
  }

  if (error) {
    return <div className={styles.error}>加载失败：{error}</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.toolbar}>
        <Select
          options={businessOptions}
          value={businessType}
          onChange={(e) => {
            setPage(1);
            setBusinessType(e.target.value as BusinessTypeFilter);
          }}
        />
        <Input
          placeholder="按文件名搜索"
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
        />
        <button
          className={styles.actionButton}
          onClick={() => {
            setPage(1);
            void loadAttachments();
          }}
        >
          搜索
        </button>
        <div className={styles.toolbarRight}>{attachments.length} 条</div>
      </div>

      {attachments.length === 0 ? (
        <div className={styles.empty}>没有匹配的附件</div>
      ) : (
        <div className={styles.list}>
          {attachments.map(att => {
            const isImage = isImageExtension(att.voExtension);
            const href = resolveUrl(apiBaseUrl, att.voUrl);

            return (
              <div key={String(att.voId)} className={styles.item}>
                <div className={styles.fileRow}>
                  <div className={styles.icon}>
                    <Icon icon={isImage ? 'mdi:image' : 'mdi:file-document-outline'} size={22} />
                  </div>

                  {att.voThumbnailUrl && isImage && (
                    <img
                      className={styles.thumbnail}
                      src={resolveUrl(apiBaseUrl, att.voThumbnailUrl)}
                      alt={att.voOriginalName}
                      loading="lazy"
                    />
                  )}

                  <div className={styles.fileInfo}>
                    <div className={styles.fileName} title={att.voOriginalName}>
                      {att.voOriginalName}
                    </div>
                    <div className={styles.meta}>
                      <span className={styles.metaItem}>{att.voFileSizeFormatted || `${att.voFileSize} B`}</span>
                      <span className={styles.metaItem}>{att.voBusinessType || 'General'}</span>
                      <span className={styles.metaItem}>
                        {att.voCreateTime ? new Date(att.voCreateTime).toLocaleDateString('zh-CN') : ''}
                      </span>
                    </div>
                  </div>

                  <div className={styles.actions}>
                    {href ? (
                      <>
                        <a className={styles.link} href={href} target="_blank" rel="noreferrer">
                          下载
                        </a>
                        <button className={styles.actionButton} onClick={() => void handleCopyLink(href)}>
                          复制链接
                        </button>
                      </>
                    ) : (
                      <span className={styles.disabledLink}>无链接</span>
                    )}
                    <button
                      className={`${styles.actionButton} ${styles.dangerButton}`}
                      onClick={() => openDeleteConfirm(att.voId)}
                    >
                      删除
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

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

      <ConfirmDialog
        isOpen={confirmDeleteOpen}
        title="确认删除"
        message="确定要删除该附件吗？"
        danger
        onCancel={() => {
          setConfirmDeleteOpen(false);
          setDeleteTargetId(null);
        }}
        onConfirm={confirmDelete}
      />
    </div>
  );
};

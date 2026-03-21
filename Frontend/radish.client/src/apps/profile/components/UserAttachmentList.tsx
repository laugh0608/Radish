import { useEffect, useMemo, useState } from 'react';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { Icon } from '@radish/ui/icon';
import { Input } from '@radish/ui/input';
import { Select } from '@radish/ui/select';
import { copyToClipboard } from '@/utils/clipboard';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { deleteAttachment } from '@/api/attachment';
import { tokenService } from '@/services/tokenService';
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
  displayTimeZone: string;
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

export const UserAttachmentList = ({ apiBaseUrl, displayTimeZone }: UserAttachmentListProps) => {
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
    const token = tokenService.getAccessToken();
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
        setError(json.messageInfo || t('profile.attachments.requestFailed', { status: response.status }));
        setAttachments([]);
        setTotalPages(1);
        return;
      }

      if (json.isSuccess && json.responseData) {
        setAttachments(json.responseData.data || []);
        setTotalPages(json.responseData.pageCount || 1);
      } else {
        setError(json.messageInfo || t('profile.attachments.loadFailed'));
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
      { value: 'All', label: t('common.all') },
      { value: 'General', label: t('profile.attachments.business.general') },
      { value: 'Post', label: t('profile.attachments.business.post') },
      { value: 'Comment', label: t('profile.attachments.business.comment') },
      { value: 'Avatar', label: t('profile.attachments.business.avatar') },
      { value: 'Document', label: t('profile.attachments.business.document') }
    ],
    [t]
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
    return <div className={styles.loading}>{t('common.loading')}</div>;
  }

  if (error) {
    return <div className={styles.error}>{t('profile.attachments.loadFailedWithDetail', { error })}</div>;
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
          placeholder={t('profile.attachments.searchPlaceholder')}
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
          {t('profile.attachments.search')}
        </button>
        <div className={styles.toolbarRight}>{t('profile.attachments.count', { count: attachments.length })}</div>
      </div>

      {attachments.length === 0 ? (
        <div className={styles.empty}>{t('profile.attachments.empty')}</div>
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
                      <span className={styles.metaItem}>{att.voBusinessType || t('profile.attachments.business.general')}</span>
                      <span className={styles.metaItem}>
                        {att.voCreateTime ? formatDateTimeByTimeZone(att.voCreateTime, displayTimeZone) : ''}
                      </span>
                    </div>
                  </div>

                  <div className={styles.actions}>
                    {href ? (
                      <>
                        <a className={styles.link} href={href} target="_blank" rel="noreferrer">
                          {t('profile.attachments.download')}
                        </a>
                        <button className={styles.actionButton} onClick={() => void handleCopyLink(href)}>
                          {t('profile.attachments.copyLink')}
                        </button>
                      </>
                    ) : (
                      <span className={styles.disabledLink}>{t('profile.attachments.noLink')}</span>
                    )}
                    <button
                      className={`${styles.actionButton} ${styles.dangerButton}`}
                      onClick={() => openDeleteConfirm(att.voId)}
                    >
                      {t('common.delete')}
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
            {t('common.previousPage')}
          </button>
          <span className={styles.pageInfo}>
            {t('common.pageInfo', { current: page, total: totalPages })}
          </span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page === totalPages}
            className={styles.pageButton}
          >
            {t('common.nextPage')}
          </button>
        </div>
      )}

      <ConfirmDialog
        isOpen={confirmDeleteOpen}
        title={t('profile.attachments.confirmDeleteTitle')}
        message={t('profile.attachments.confirmDeleteMessage')}
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

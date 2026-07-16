import { useEffect, useMemo, useState } from 'react';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { Icon } from '@radish/ui/icon';
import { Input } from '@radish/ui/input';
import { Select } from '@radish/ui/select';
import { copyToClipboard } from '@/utils/clipboard';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { deleteAttachment, getMyAttachments, type MyAttachmentItem } from '@/api/attachment';
import { resolveMediaUrl } from '@/utils/media';
import { useTranslation } from 'react-i18next';
import { getIntlLocale } from '@/locales/language';
import {
  formatAttachmentFileSize,
  resolveAttachmentBusinessType,
} from '@/attachments/attachmentPresentation';
import { log } from '@/utils/logger';
import styles from './UserAttachmentList.module.css';

interface UserAttachmentListProps {
  apiBaseUrl: string;
  displayTimeZone: string;
  initialPage?: number;
  initialBusinessType?: BusinessTypeFilter;
  initialKeyword?: string;
  onItemsLoaded?: (items: MyAttachmentItem[]) => void;
  onStateChange?: (state: UserAttachmentListState) => void;
}

type BusinessTypeFilter = 'All' | 'General' | 'Post' | 'Comment' | 'Avatar' | 'Document';

interface UserAttachmentListState {
  page: number;
  businessType: BusinessTypeFilter;
  keyword: string;
}

function isImageExtension(extension: string | undefined | null): boolean {
  if (!extension) return false;
  const ext = extension.toLowerCase();
  return ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.webp', '.svg'].includes(ext);
}

function resolveUrl(apiBaseUrl: string, url: string): string {
  return resolveMediaUrl(url, apiBaseUrl) ?? '';
}

export const UserAttachmentList = ({
  apiBaseUrl,
  displayTimeZone,
  initialPage = 1,
  initialBusinessType = 'All',
  initialKeyword = '',
  onItemsLoaded,
  onStateChange
}: UserAttachmentListProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;

  const [attachments, setAttachments] = useState<MyAttachmentItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(initialPage);
  const [totalPages, setTotalPages] = useState(1);
  const [errorKey, setErrorKey] = useState<string | null>(null);

  const [businessType, setBusinessType] = useState<BusinessTypeFilter>(initialBusinessType);
  const [keyword, setKeyword] = useState(initialKeyword);
  const [keywordInput, setKeywordInput] = useState(initialKeyword);

  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null);

  useEffect(() => {
    setPage(initialPage);
  }, [initialPage]);

  useEffect(() => {
    setBusinessType(initialBusinessType);
  }, [initialBusinessType]);

  useEffect(() => {
    setKeyword(initialKeyword);
    setKeywordInput(initialKeyword);
  }, [initialKeyword]);

  useEffect(() => {
    void loadAttachments();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, businessType, keyword]);

  const loadAttachments = async () => {
    setLoading(true);
    setErrorKey(null);

    try {
      const result = await getMyAttachments({
        pageIndex: page,
        pageSize: 10,
        businessType,
        keyword
      });
      const loadedAttachments = result.data || [];
      setAttachments(loadedAttachments);
      onItemsLoaded?.(loadedAttachments);
      setTotalPages(result.pageCount || 1);
    } catch (error) {
      log.error('UserAttachmentList', '加载我的附件失败:', error);
      setErrorKey('profile.attachments.loadFailed');
      setAttachments([]);
      onItemsLoaded?.([]);
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

  const openDeleteConfirm = (id: string) => {
    setDeleteTargetId(id);
    setConfirmDeleteOpen(true);
  };

  const updateAppliedState = (nextState: Partial<UserAttachmentListState>) => {
    const normalizedState: UserAttachmentListState = {
      page: nextState.page ?? page,
      businessType: nextState.businessType ?? businessType,
      keyword: nextState.keyword ?? keyword
    };

    setPage(normalizedState.page);
    setBusinessType(normalizedState.businessType);
    setKeyword(normalizedState.keyword);
    setKeywordInput(normalizedState.keyword);
    onStateChange?.(normalizedState);
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
    } catch (error) {
      log.error('UserAttachmentList', '删除附件失败:', error);
      setErrorKey('attachment.api.deleteFailed');
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

  if (errorKey) {
    return <div className={styles.error}>{t(errorKey)}</div>;
  }

  return (
    <div className={styles.container}>
      <div className={styles.toolbar}>
        <Select
          options={businessOptions}
          value={businessType}
          onChange={(e) => {
            updateAppliedState({
              page: 1,
              businessType: e.target.value as BusinessTypeFilter,
              keyword: keywordInput.trim()
            });
          }}
        />
        <Input
          placeholder={t('profile.attachments.searchPlaceholder')}
          value={keywordInput}
          onChange={(e) => setKeywordInput(e.target.value)}
        />
        <button
          type="button"
          className={styles.actionButton}
          onClick={() => {
            updateAppliedState({
              page: 1,
              keyword: keywordInput.trim()
            });
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
                      <span className={styles.metaItem}>{formatAttachmentFileSize(att.voFileSize, language)}</span>
                      <span className={styles.metaItem}>{resolveAttachmentBusinessType(att.voBusinessType, t)}</span>
                      <span className={styles.metaItem}>
                        {att.voCreateTime
                          ? formatDateTimeByTimeZone(
                              att.voCreateTime,
                              displayTimeZone,
                              '-',
                              getIntlLocale(language),
                            )
                          : ''}
                      </span>
                    </div>
                  </div>

                  <div className={styles.actions}>
                    {href ? (
                      <>
                        <a className={styles.link} href={href} target="_blank" rel="noreferrer">
                          {t('profile.attachments.download')}
                        </a>
                        <button type="button" className={styles.actionButton} onClick={() => void handleCopyLink(href)}>
                          {t('profile.attachments.copyLink')}
                        </button>
                      </>
                    ) : (
                      <span className={styles.disabledLink}>{t('profile.attachments.noLink')}</span>
                    )}
                    <button
                      type="button"
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
            type="button"
            onClick={() => updateAppliedState({ page: Math.max(1, page - 1) })}
            disabled={page === 1}
            className={styles.pageButton}
          >
            {t('common.previousPage')}
          </button>
          <span className={styles.pageInfo}>
            {t('common.pageInfo', { current: page, total: totalPages })}
          </span>
          <button
            type="button"
            onClick={() => updateAppliedState({ page: Math.min(totalPages, page + 1) })}
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
        confirmText={t('common.delete')}
        cancelText={t('common.cancel')}
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

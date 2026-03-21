import { useTranslation } from 'react-i18next';
import { Modal } from '@radish/ui/modal';
import { Button } from '@radish/ui/button';
import type { PostEditHistory, CommentEditHistory } from '@/types/forum';
import styles from './EditHistoryModal.module.css';

type HistoryItem = PostEditHistory | CommentEditHistory;

interface EditHistoryModalProps {
  isOpen: boolean;
  title: string;
  loading?: boolean;
  error?: string | null;
  items: HistoryItem[];
  total: number;
  pageIndex: number;
  pageSize: number;
  onClose: () => void;
  onPageChange: (pageIndex: number) => void;
  renderContent: (item: HistoryItem) => { before: string; after: string; beforeTitle?: string; afterTitle?: string };
}

const formatDate = (value: string | undefined, locale: string) => {
  if (!value) return '-';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString(locale);
};

export const EditHistoryModal = ({
  isOpen,
  title,
  loading = false,
  error,
  items,
  total,
  pageIndex,
  pageSize,
  onClose,
  onPageChange,
  renderContent
}: EditHistoryModalProps) => {
  const { t, i18n } = useTranslation();
  const totalPages = total > 0 ? Math.ceil(total / pageSize) : 1;
  const locale = i18n.resolvedLanguage?.startsWith('zh') ? 'zh-CN' : 'en-US';

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      size="large"
      footer={
        <div className={styles.footer}>
          <span className={styles.summary}>{t('forum.history.summary', { count: total })}</span>
          <div className={styles.pager}>
            <Button
              variant="secondary"
              size="small"
              disabled={pageIndex <= 1 || loading}
              onClick={() => onPageChange(pageIndex - 1)}
            >
              {t('common.previousPage')}
            </Button>
            <span className={styles.pageInfo}>{t('common.pageInfo', { current: pageIndex, total: totalPages })}</span>
            <Button
              variant="secondary"
              size="small"
              disabled={pageIndex >= totalPages || loading}
              onClick={() => onPageChange(pageIndex + 1)}
            >
              {t('common.nextPage')}
            </Button>
          </div>
        </div>
      }
    >
      <div className={styles.container}>
        {loading && <div className={styles.loading}>{t('forum.history.loading')}</div>}
        {!loading && error && <div className={styles.error}>{error}</div>}
        {!loading && !error && items.length === 0 && <div className={styles.empty}>{t('forum.history.empty')}</div>}

        {!loading && !error && items.length > 0 && (
          <div className={styles.list}>
            {items.map(item => {
              const content = renderContent(item);
              const itemId = 'voId' in item ? item.voId : 0;
              const sequence = 'voEditSequence' in item ? item.voEditSequence : 0;
              const editorName = 'voEditorName' in item ? item.voEditorName : '-';
              const editedAt = 'voEditedAt' in item ? item.voEditedAt : '';

              return (
                <div key={itemId} className={styles.item}>
                  <div className={styles.itemHeader}>
                    <span className={styles.seq}>{t('forum.history.sequence', { count: sequence })}</span>
                    <span className={styles.meta}>
                      {t('forum.history.editorMeta', { name: editorName, time: formatDate(editedAt, locale) })}
                    </span>
                  </div>

                  {(content.beforeTitle !== undefined || content.afterTitle !== undefined) && (
                    <div className={styles.titleRow}>
                      <div className={styles.titleCol}>
                        <div className={styles.label}>{t('forum.history.beforeTitle')}</div>
                        <div className={styles.titleText}>{content.beforeTitle || '-'}</div>
                      </div>
                      <div className={styles.titleCol}>
                        <div className={styles.label}>{t('forum.history.afterTitle')}</div>
                        <div className={styles.titleText}>{content.afterTitle || '-'}</div>
                      </div>
                    </div>
                  )}

                  <div className={styles.contentRow}>
                    <div className={styles.contentCol}>
                      <div className={styles.label}>{t('forum.history.beforeContent')}</div>
                      <pre className={styles.content}>{content.before}</pre>
                    </div>
                    <div className={styles.contentCol}>
                      <div className={styles.label}>{t('forum.history.afterContent')}</div>
                      <pre className={styles.content}>{content.after}</pre>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </Modal>
  );
};

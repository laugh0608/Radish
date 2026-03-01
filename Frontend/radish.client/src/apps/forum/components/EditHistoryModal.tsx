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

const formatDate = (value?: string) => {
  if (!value) return '-';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString('zh-CN');
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
  const totalPages = total > 0 ? Math.ceil(total / pageSize) : 1;

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={title}
      size="large"
      footer={
        <div className={styles.footer}>
          <span className={styles.summary}>共 {total} 条</span>
          <div className={styles.pager}>
            <Button
              variant="secondary"
              size="small"
              disabled={pageIndex <= 1 || loading}
              onClick={() => onPageChange(pageIndex - 1)}
            >
              上一页
            </Button>
            <span className={styles.pageInfo}>{pageIndex} / {totalPages}</span>
            <Button
              variant="secondary"
              size="small"
              disabled={pageIndex >= totalPages || loading}
              onClick={() => onPageChange(pageIndex + 1)}
            >
              下一页
            </Button>
          </div>
        </div>
      }
    >
      <div className={styles.container}>
        {loading && <div className={styles.loading}>加载历史中...</div>}
        {!loading && error && <div className={styles.error}>{error}</div>}
        {!loading && !error && items.length === 0 && <div className={styles.empty}>暂无编辑历史</div>}

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
                    <span className={styles.seq}>第 {sequence} 次编辑</span>
                    <span className={styles.meta}>编辑者：{editorName} · {formatDate(editedAt)}</span>
                  </div>

                  {(content.beforeTitle !== undefined || content.afterTitle !== undefined) && (
                    <div className={styles.titleRow}>
                      <div className={styles.titleCol}>
                        <div className={styles.label}>编辑前标题</div>
                        <div className={styles.titleText}>{content.beforeTitle || '-'}</div>
                      </div>
                      <div className={styles.titleCol}>
                        <div className={styles.label}>编辑后标题</div>
                        <div className={styles.titleText}>{content.afterTitle || '-'}</div>
                      </div>
                    </div>
                  )}

                  <div className={styles.contentRow}>
                    <div className={styles.contentCol}>
                      <div className={styles.label}>编辑前内容</div>
                      <pre className={styles.content}>{content.before}</pre>
                    </div>
                    <div className={styles.contentCol}>
                      <div className={styles.label}>编辑后内容</div>
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


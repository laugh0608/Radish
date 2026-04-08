import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { MarkdownStickerMap } from '@radish/ui/markdown-renderer';
import type { CommentNode as CommentNodeType, CommentReplyTarget, ReactionSummaryVo } from '@/api/forum';
import type { ReactionTogglePayload } from '@radish/ui/reaction-bar';
import type { StickerPickerGroup } from '@radish/ui/sticker-picker';
import { CommentNode } from './CommentNode';
import styles from './CommentTree.module.css';

interface CommentTreeProps {
  comments: CommentNodeType[];
  loading?: boolean;
  loadingMoreRootComments?: boolean;
  hasPost?: boolean;
  displayTimeZone: string;
  currentUserId?: number;
  highlightedCommentId?: number | null;
  expandedRootCommentId?: number;
  pageSize?: number;
  rootCommentTotal?: number;
  loadedRootCommentCount?: number;
  rootCommentPageSize?: number;
  sortBy?: 'newest' | 'hottest' | null; // null表示默认排序
  onDeleteComment?: (commentId: number) => void;
  onEditComment?: (commentId: number, newContent: string) => Promise<void>;
  onViewCommentHistory?: (commentId: number) => void;
  onLikeComment?: (commentId: number) => Promise<{ isLiked: boolean; likeCount: number }>;
  onReplyComment?: (target: CommentReplyTarget) => void;
  onLoadMoreChildren?: (parentId: number, pageIndex: number, pageSize: number) => Promise<CommentNodeType[]>;
  onLoadMoreRootComments?: () => Promise<void>;
  onSortChange?: (sortBy: 'newest' | 'hottest') => void;
  stickerMap?: MarkdownStickerMap;
  reactionMap?: Record<number, ReactionSummaryVo[]>;
  isAuthenticated?: boolean;
  stickerGroups?: StickerPickerGroup[];
  onToggleReaction?: (commentId: number, payload: ReactionTogglePayload) => Promise<void>;
  isReactionPending?: (commentId: number) => boolean;
  onRequireReactionLogin?: () => void;
  onAuthorClick?: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
  onReportComment?: (commentId: number) => void;
  registerCommentAnchor?: (commentId: number, element: HTMLDivElement | null) => void;
}

export const CommentTree = ({
  comments,
  loading = false,
  loadingMoreRootComments = false,
  hasPost = false,
  displayTimeZone,
  currentUserId = 0,
  highlightedCommentId = null,
  expandedRootCommentId,
  pageSize = 10,
  rootCommentTotal = 0,
  loadedRootCommentCount = 0,
  rootCommentPageSize = 20,
  sortBy = null,
  onDeleteComment,
  onEditComment,
  onViewCommentHistory,
  onLikeComment,
  onReplyComment,
  onLoadMoreChildren,
  onLoadMoreRootComments,
  onSortChange,
  stickerMap,
  reactionMap = {},
  isAuthenticated = false,
  stickerGroups = [],
  onToggleReaction,
  isReactionPending,
  onRequireReactionLogin,
  onAuthorClick,
  onReportComment,
  registerCommentAnchor,
}: CommentTreeProps) => {
  const { t } = useTranslation();
  // 找出所有神评（后端标记的）
  const godComments = useMemo(() => {
    return comments.filter(c => c.voIsGodComment);
  }, [comments]);

  // 找出当前点赞数最高的神评（用于置顶显示）
  const topGodComment = useMemo(() => {
    if (godComments.length === 0) return null;
    return [...godComments].sort((a, b) => {
      // 先按点赞数降序
      if ((b.voLikeCount || 0) !== (a.voLikeCount || 0)) {
        return (b.voLikeCount || 0) - (a.voLikeCount || 0);
      }
      // 点赞数相同时按创建时间降序（最新的在前）
      return new Date(b.voCreateTime || 0).getTime() - new Date(a.voCreateTime || 0).getTime();
    })[0];
  }, [godComments]);

  // 根据排序状态决定显示顺序
  const displayComments = useMemo(() => {
    if (comments.length === 0) return [];

    if (sortBy === null && topGodComment) {
      // 默认排序：当前点赞数最高的神评置顶 + 其他按时间升序
      const others = comments.filter(c => c.voId !== topGodComment.voId);
      return [topGodComment, ...others];
    }

    // 手动排序时，直接使用后端返回的顺序（此时不再需要前端重新排序）
    return comments;
  }, [comments, sortBy, topGodComment]);
  const hasMoreRootComments = hasPost && loadedRootCommentCount < rootCommentTotal;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h4 className={styles.title}>{t('forum.commentTree.title')}</h4>
        {hasPost && comments.length > 0 && (
          <div className={styles.sortButtons}>
            <button
              type="button"
              className={`${styles.sortButton} ${sortBy === 'newest' ? styles.active : ''}`}
              onClick={() => onSortChange?.('newest')}
            >
              {t('forum.sort.newest')}
            </button>
            <button
              type="button"
              className={`${styles.sortButton} ${sortBy === 'hottest' ? styles.active : ''}`}
              onClick={() => onSortChange?.('hottest')}
            >
              {t('forum.sort.hottest')}
            </button>
          </div>
        )}
      </div>
      {loading && <p className={styles.loadingText}>{t('forum.loadingDiscussion')}</p>}
      {!loading && comments.length === 0 && hasPost && (
        <p className={styles.emptyText}>{t('forum.commentTree.empty')}</p>
      )}
      {!loading && comments.length === 0 && !hasPost && (
        <p className={styles.emptyText}>{t('forum.commentTree.selectPost')}</p>
      )}
      <div className={styles.list}>
        {displayComments.map(comment => (
          <CommentNode
            key={comment.voId}
            node={comment}
            level={0}
            displayTimeZone={displayTimeZone}
            currentUserId={currentUserId}
            highlightedCommentId={highlightedCommentId}
            expandedRootCommentId={expandedRootCommentId}
            pageSize={pageSize}
            isGodComment={comment.voIsGodComment || false}
            onDelete={onDeleteComment}
            onEdit={onEditComment}
            onViewHistory={onViewCommentHistory}
            onLike={onLikeComment}
            onReply={onReplyComment}
            onLoadMoreChildren={onLoadMoreChildren}
            stickerMap={stickerMap}
            reactionMap={reactionMap}
            isAuthenticated={isAuthenticated}
            stickerGroups={stickerGroups}
            onToggleReaction={onToggleReaction}
            isReactionPending={isReactionPending}
            onRequireReactionLogin={onRequireReactionLogin}
            onAuthorClick={onAuthorClick}
            onReport={onReportComment}
            registerCommentAnchor={registerCommentAnchor}
          />
        ))}
      </div>
      {!loading && hasMoreRootComments && (
        <div className={styles.loadMoreWrap}>
          <button
            type="button"
            className={styles.loadMoreButton}
            onClick={() => void onLoadMoreRootComments?.()}
            disabled={loadingMoreRootComments}
          >
            {loadingMoreRootComments
              ? t('forum.loadingDiscussion')
              : t('forum.comment.loadMoreReplies', {
                  loaded: loadedRootCommentCount,
                  total: rootCommentTotal
                })}
          </button>
          {rootCommentPageSize > 0 && (
            <p className={styles.loadMoreHint}>
              {loadedRootCommentCount}/{rootCommentTotal}
            </p>
          )}
        </div>
      )}
    </div>
  );
};

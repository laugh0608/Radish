import { useMemo } from 'react';
import type { MarkdownStickerMap } from '@radish/ui/markdown-renderer';
import type { CommentNode as CommentNodeType, ReactionSummaryVo } from '@/api/forum';
import type { ReactionTogglePayload } from '@radish/ui/reaction-bar';
import type { StickerPickerGroup } from '@radish/ui/sticker-picker';
import { CommentNode } from './CommentNode';
import styles from './CommentTree.module.css';

interface CommentTreeProps {
  comments: CommentNodeType[];
  loading?: boolean;
  hasPost?: boolean;
  displayTimeZone: string;
  currentUserId?: number;
  pageSize?: number;
  sortBy?: 'newest' | 'hottest' | null; // null表示默认排序
  onDeleteComment?: (commentId: number) => void;
  onEditComment?: (commentId: number, newContent: string) => Promise<void>;
  onViewCommentHistory?: (commentId: number) => void;
  onLikeComment?: (commentId: number) => Promise<{ isLiked: boolean; likeCount: number }>;
  onReplyComment?: (commentId: number, authorName: string) => void;
  onLoadMoreChildren?: (parentId: number, pageIndex: number, pageSize: number) => Promise<CommentNodeType[]>;
  onSortChange?: (sortBy: 'newest' | 'hottest') => void;
  stickerMap?: MarkdownStickerMap;
  reactionMap?: Record<number, ReactionSummaryVo[]>;
  isAuthenticated?: boolean;
  stickerGroups?: StickerPickerGroup[];
  onToggleReaction?: (commentId: number, payload: ReactionTogglePayload) => Promise<void>;
  isReactionPending?: (commentId: number) => boolean;
  onRequireReactionLogin?: () => void;
}

export const CommentTree = ({
  comments,
  loading = false,
  hasPost = false,
  displayTimeZone,
  currentUserId = 0,
  pageSize = 10,
  sortBy = null,
  onDeleteComment,
  onEditComment,
  onViewCommentHistory,
  onLikeComment,
  onReplyComment,
  onLoadMoreChildren,
  onSortChange,
  stickerMap,
  reactionMap = {},
  isAuthenticated = false,
  stickerGroups = [],
  onToggleReaction,
  isReactionPending,
  onRequireReactionLogin,
}: CommentTreeProps) => {
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

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h4 className={styles.title}>评论</h4>
        {hasPost && comments.length > 0 && (
          <div className={styles.sortButtons}>
            <button
              type="button"
              className={`${styles.sortButton} ${sortBy === 'newest' ? styles.active : ''}`}
              onClick={() => onSortChange?.('newest')}
            >
              最新
            </button>
            <button
              type="button"
              className={`${styles.sortButton} ${sortBy === 'hottest' ? styles.active : ''}`}
              onClick={() => onSortChange?.('hottest')}
            >
              最热
            </button>
          </div>
        )}
      </div>
      {loading && <p className={styles.loadingText}>加载评论中...</p>}
      {!loading && comments.length === 0 && hasPost && (
        <p className={styles.emptyText}>还没有评论，快来抢沙发吧！</p>
      )}
      {!loading && comments.length === 0 && !hasPost && (
        <p className={styles.emptyText}>请先选择一个帖子查看评论</p>
      )}
      <div className={styles.list}>
        {displayComments.map(comment => (
          <CommentNode
            key={comment.voId}
            node={comment}
            level={0}
            displayTimeZone={displayTimeZone}
            currentUserId={currentUserId}
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
          />
        ))}
      </div>
    </div>
  );
};

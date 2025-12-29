import { useMemo } from 'react';
import type { CommentNode as CommentNodeType } from '@/types/forum';
import { CommentNode } from './CommentNode';
import styles from './CommentTree.module.css';

interface CommentTreeProps {
  comments: CommentNodeType[];
  loading?: boolean;
  hasPost?: boolean;
  currentUserId?: number;
  pageSize?: number;
  sortBy?: 'newest' | 'hottest' | null; // null表示默认排序
  onDeleteComment?: (commentId: number) => void;
  onEditComment?: (commentId: number, newContent: string) => Promise<void>;
  onLikeComment?: (commentId: number) => Promise<{ isLiked: boolean; likeCount: number }>;
  onReplyComment?: (commentId: number, authorName: string) => void;
  onLoadMoreChildren?: (parentId: number, pageIndex: number, pageSize: number) => Promise<CommentNodeType[]>;
  onSortChange?: (sortBy: 'newest' | 'hottest') => void;
}

export const CommentTree = ({
  comments,
  loading = false,
  hasPost = false,
  currentUserId = 0,
  pageSize = 10,
  sortBy = null,
  onDeleteComment,
  onEditComment,
  onLikeComment,
  onReplyComment,
  onLoadMoreChildren,
  onSortChange
}: CommentTreeProps) => {
  // 找出所有神评（后端标记的）
  const godComments = useMemo(() => {
    return comments.filter(c => c.isGodComment);
  }, [comments]);

  // 找出当前点赞数最高的神评（用于置顶显示）
  const topGodComment = useMemo(() => {
    if (godComments.length === 0) return null;
    return [...godComments].sort((a, b) => {
      // 先按点赞数降序
      if ((b.likeCount || 0) !== (a.likeCount || 0)) {
        return (b.likeCount || 0) - (a.likeCount || 0);
      }
      // 点赞数相同时按创建时间降序（最新的在前）
      return new Date(b.createTime || 0).getTime() - new Date(a.createTime || 0).getTime();
    })[0];
  }, [godComments]);

  // 根据排序状态决定显示顺序
  const displayComments = useMemo(() => {
    if (comments.length === 0) return [];

    if (sortBy === null && topGodComment) {
      // 默认排序：当前点赞数最高的神评置顶 + 其他按时间升序
      const others = comments.filter(c => c.id !== topGodComment.id);
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
            key={comment.id}
            node={comment}
            level={0}
            currentUserId={currentUserId}
            pageSize={pageSize}
            isGodComment={comment.isGodComment || false}
            onDelete={onDeleteComment}
            onEdit={onEditComment}
            onLike={onLikeComment}
            onReply={onReplyComment}
            onLoadMoreChildren={onLoadMoreChildren}
          />
        ))}
      </div>
    </div>
  );
};

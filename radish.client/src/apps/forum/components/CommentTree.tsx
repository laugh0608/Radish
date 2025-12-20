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
  // 找出神评（点赞数最多的父评论，如果点赞数相同则按时间最新）
  const godComment = useMemo(() => {
    if (comments.length === 0) return null;
    return [...comments].sort((a, b) => {
      // 先按点赞数降序
      if ((b.likeCount || 0) !== (a.likeCount || 0)) {
        return (b.likeCount || 0) - (a.likeCount || 0);
      }
      // 点赞数相同时按时间降序（最新的在前）
      return new Date(b.createTime || 0).getTime() - new Date(a.createTime || 0).getTime();
    })[0];
  }, [comments]);

  // 根据排序状态决定显示顺序
  const displayComments = useMemo(() => {
    if (comments.length === 0) return [];

    if (sortBy === null && godComment) {
      // 默认排序：神评置顶 + 其他按时间升序
      const others = comments.filter(c => c.id !== godComment.id);
      return [godComment, ...others];
    }

    // 手动排序时，直接使用后端返回的顺序（此时不再需要前端重新排序）
    return comments;
  }, [comments, sortBy, godComment]);

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
            isGodComment={godComment !== null && comment.id === godComment.id}
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

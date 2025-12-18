import { useState } from 'react';
import type { CommentNode as CommentNodeType } from '@/types/forum';
import { CommentNode } from './CommentNode';
import styles from './CommentTree.module.css';

interface CommentTreeProps {
  comments: CommentNodeType[];
  loading?: boolean;
  hasPost?: boolean;
  currentUserId?: number;
  pageSize?: number;
  sortBy?: 'newest' | 'hottest';
  onDeleteComment?: (commentId: number) => void;
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
  sortBy = 'newest',
  onDeleteComment,
  onLikeComment,
  onReplyComment,
  onLoadMoreChildren,
  onSortChange
}: CommentTreeProps) => {
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
        {comments.map(comment => (
          <CommentNode
            key={comment.id}
            node={comment}
            level={0}
            currentUserId={currentUserId}
            pageSize={pageSize}
            onDelete={onDeleteComment}
            onLike={onLikeComment}
            onReply={onReplyComment}
            onLoadMoreChildren={onLoadMoreChildren}
          />
        ))}
      </div>
    </div>
  );
};

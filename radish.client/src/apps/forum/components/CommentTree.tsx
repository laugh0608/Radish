import type { CommentNode as CommentNodeType } from '@/types/forum';
import { CommentNode } from './CommentNode';
import styles from './CommentTree.module.css';

interface CommentTreeProps {
  comments: CommentNodeType[];
  loading?: boolean;
  hasPost?: boolean;
  currentUserId?: number;
  onDeleteComment?: (commentId: number) => void;
}

export const CommentTree = ({
  comments,
  loading = false,
  hasPost = false,
  currentUserId = 0,
  onDeleteComment
}: CommentTreeProps) => {
  return (
    <div className={styles.container}>
      <h4 className={styles.title}>评论</h4>
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
            onDelete={onDeleteComment}
          />
        ))}
      </div>
    </div>
  );
};

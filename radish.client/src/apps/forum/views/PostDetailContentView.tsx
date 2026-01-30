import { Icon } from '@radish/ui';
import type { PostDetail, CommentNode } from '@/api/forum';
import { PostDetail as PostDetailComponent } from '../components/PostDetail';
import { CommentTree } from '../components/CommentTree';
import { CreateCommentForm } from '../components/CreateCommentForm';
import styles from './PostDetailContentView.module.css';

interface PostDetailContentViewProps {
  // 数据
  post: PostDetail;
  comments: CommentNode[];
  loadingPostDetail: boolean;
  loadingComments: boolean;
  isLiked: boolean;
  isAuthenticated: boolean;
  currentUserId: number;
  commentSortBy: 'newest' | 'hottest' | null;
  replyTo: { commentId: number; authorName: string } | null;

  // 事件处理
  onBack: () => void;
  onLike: (postId: number) => void;
  onEdit: (postId: number) => void;
  onDelete: (postId: number) => void;
  onCommentSortChange: (sortBy: 'newest' | 'hottest') => void;
  onDeleteComment: (commentId: number) => void;
  onEditComment: (commentId: number, newContent: string) => Promise<void>;
  onLikeComment: (commentId: number) => Promise<{ isLiked: boolean; likeCount: number }>;
  onReplyComment: (commentId: number, authorName: string) => void;
  onLoadMoreChildren: (
    parentId: number,
    pageIndex: number,
    pageSize: number
  ) => Promise<CommentNode[]>;
  onCreateComment: (content: string) => Promise<void>;
  onCancelReply: () => void;
}

export const PostDetailContentView = ({
  post,
  comments,
  loadingPostDetail,
  loadingComments,
  isLiked,
  isAuthenticated,
  currentUserId,
  commentSortBy,
  replyTo,
  onBack,
  onLike,
  onEdit,
  onDelete,
  onCommentSortChange,
  onDeleteComment,
  onEditComment,
  onLikeComment,
  onReplyComment,
  onLoadMoreChildren,
  onCreateComment,
  onCancelReply
}: PostDetailContentViewProps) => {
  return (
    <>
      {/* 返回按钮 */}
      <div className={styles.detailToolbar}>
        <button className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={20} />
          返回列表
        </button>
      </div>

      {/* 帖子详情内容 */}
      <div className={styles.detailContent}>
        <PostDetailComponent
          post={post}
          loading={loadingPostDetail}
          isLiked={isLiked}
          onLike={onLike}
          isAuthenticated={isAuthenticated}
          currentUserId={currentUserId}
          onEdit={onEdit}
          onDelete={onDelete}
        />
        <CommentTree
          comments={comments}
          loading={loadingComments}
          hasPost={true}
          currentUserId={currentUserId}
          pageSize={5}
          sortBy={commentSortBy}
          onSortChange={onCommentSortChange}
          onDeleteComment={onDeleteComment}
          onEditComment={onEditComment}
          onLikeComment={onLikeComment}
          onReplyComment={onReplyComment}
          onLoadMoreChildren={onLoadMoreChildren}
        />
        <CreateCommentForm
          isAuthenticated={isAuthenticated}
          hasPost={true}
          onSubmit={onCreateComment}
          replyTo={replyTo}
          onCancelReply={onCancelReply}
        />
      </div>
    </>
  );
};

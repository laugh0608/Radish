import { useEffect, useRef, useState } from 'react';
import { BottomSheet, Icon } from '@radish/ui';
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
  const [isCommentSheetOpen, setIsCommentSheetOpen] = useState(false);
  const contentRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (replyTo) {
      setIsCommentSheetOpen(true);
    }
  }, [replyTo]);

  const handleScrollTop = () => {
    contentRef.current?.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const handleScrollBottom = () => {
    if (!contentRef.current) return;
    contentRef.current.scrollTo({
      top: contentRef.current.scrollHeight,
      behavior: 'smooth'
    });
  };

  const handleOpenCommentSheet = () => {
    setIsCommentSheetOpen(true);
  };

  const handleCloseCommentSheet = () => {
    setIsCommentSheetOpen(false);
    onCancelReply();
  };

  const handleCreateComment = async (content: string) => {
    await onCreateComment(content);
    setIsCommentSheetOpen(false);
  };

  return (
    <div className={styles.detailView}>
      {/* 返回按钮 */}
      <div className={styles.detailToolbar}>
        <button className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={20} />
          返回列表
        </button>
      </div>

      {/* 帖子详情内容 */}
      <div className={styles.detailBody}>
        <div className={styles.detailContent} ref={contentRef}>
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
            onReplyComment={(commentId, authorName) => {
              onReplyComment(commentId, authorName);
              setIsCommentSheetOpen(true);
            }}
            onLoadMoreChildren={onLoadMoreChildren}
          />
          <div className={styles.commentCta}>
            <button className={styles.commentButton} onClick={handleOpenCommentSheet}>
              发表评论
            </button>
          </div>
        </div>

        <div className={styles.floatingTools}>
          <button className={styles.toolButton} onClick={handleScrollTop} title="滚动到顶部">
            <Icon icon="mdi:chevron-up" size={20} className={styles.toolIcon} />
          </button>
          <button className={styles.toolButton} onClick={handleScrollBottom} title="滚动到底部">
            <Icon icon="mdi:chevron-down" size={20} className={styles.toolIcon} />
          </button>
          <button className={styles.toolButton} onClick={handleOpenCommentSheet} title="快速评论">
            <Icon icon="mdi:comment-outline" size={18} className={styles.toolIcon} />
          </button>
        </div>
      </div>

      <BottomSheet
        isOpen={isCommentSheetOpen}
        onClose={handleCloseCommentSheet}
        title="发表评论"
        height="70%"
      >
        <CreateCommentForm
          isAuthenticated={isAuthenticated}
          hasPost={true}
          onSubmit={handleCreateComment}
          replyTo={replyTo}
          onCancelReply={onCancelReply}
          variant="sheet"
        />
      </BottomSheet>
    </div>
  );
};

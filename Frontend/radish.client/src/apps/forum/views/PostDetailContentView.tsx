import { lazy, Suspense, useEffect, useRef, useState } from 'react';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Icon } from '@radish/ui/icon';
import type { PostDetail, CommentNode } from '@/api/forum';
import { FORUM_DETAIL_TOOL_EVENT, type ForumDetailToolAction } from '../constants/detailTools';
import styles from './PostDetailContentView.module.css';

const PostDetailComponent = lazy(() =>
  import('../components/PostDetail').then((module) => ({ default: module.PostDetail }))
);

const CommentTree = lazy(() =>
  import('../components/CommentTree').then((module) => ({ default: module.CommentTree }))
);

const CreateCommentForm = lazy(() =>
  import('../components/CreateCommentForm').then((module) => ({ default: module.CreateCommentForm }))
);

interface PostDetailContentViewProps {
  post: PostDetail;
  comments: CommentNode[];
  loadingPostDetail: boolean;
  loadingComments: boolean;
  isLiked: boolean;
  isAuthenticated: boolean;
  showFloatingTools?: boolean;
  currentUserId: number;
  commentSortBy: 'newest' | 'hottest' | null;
  replyTo: { commentId: number; authorName: string } | null;

  onBack: () => void;
  onLike: (postId: number) => void;
  onEdit: (postId: number) => void;
  onViewPostHistory: (postId: number) => void;
  onDelete: (postId: number) => void;
  onCommentSortChange: (sortBy: 'newest' | 'hottest') => void;
  onDeleteComment: (commentId: number) => void;
  onEditComment: (commentId: number, newContent: string) => Promise<void>;
  onViewCommentHistory: (commentId: number) => void;
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
  showFloatingTools = true,
  currentUserId,
  commentSortBy,
  replyTo,
  onBack,
  onLike,
  onEdit,
  onViewPostHistory,
  onDelete,
  onCommentSortChange,
  onDeleteComment,
  onEditComment,
  onViewCommentHistory,
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

  useEffect(() => {
    const handleToolAction = (event: Event) => {
      const action = (event as CustomEvent<ForumDetailToolAction>).detail;
      if (action === 'scrollTop') {
        handleScrollTop();
        return;
      }
      if (action === 'scrollBottom') {
        handleScrollBottom();
        return;
      }
      if (action === 'openComment') {
        handleOpenCommentSheet();
      }
    };

    window.addEventListener(FORUM_DETAIL_TOOL_EVENT, handleToolAction as EventListener);
    return () => {
      window.removeEventListener(FORUM_DETAIL_TOOL_EVENT, handleToolAction as EventListener);
    };
  }, []);

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
      <div className={styles.detailToolbar}>
        <button className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={20} />
          返回列表
        </button>
      </div>

      <div className={styles.detailBody}>
        <div
          className={`${styles.detailContent} ${showFloatingTools ? styles.withFloatingTools : ''}`}
          ref={contentRef}
        >
          <Suspense fallback={<div style={{ padding: '0.75rem 0' }}>帖子内容加载中...</div>}>
            <PostDetailComponent
              post={post}
              loading={loadingPostDetail}
              isLiked={isLiked}
              onLike={onLike}
              isAuthenticated={isAuthenticated}
              currentUserId={currentUserId}
              onEdit={onEdit}
              onViewHistory={onViewPostHistory}
              onDelete={onDelete}
            />
          </Suspense>

          <Suspense fallback={<p className={styles.loadingText}>加载评论中...</p>}>
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
              onViewCommentHistory={onViewCommentHistory}
              onLikeComment={onLikeComment}
              onReplyComment={(commentId, authorName) => {
                onReplyComment(commentId, authorName);
                setIsCommentSheetOpen(true);
              }}
              onLoadMoreChildren={onLoadMoreChildren}
            />
          </Suspense>

          <div className={styles.commentCta}>
            <button className={styles.commentButton} onClick={handleOpenCommentSheet}>
              发表评论
            </button>
          </div>
        </div>

        {showFloatingTools && (
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
        )}
      </div>

      <BottomSheet
        isOpen={isCommentSheetOpen}
        onClose={handleCloseCommentSheet}
        title="发表评论"
        height="70%"
      >
        {isCommentSheetOpen && (
          <Suspense fallback={<div style={{ padding: '0.75rem 0' }}>评论编辑器加载中...</div>}>
            <CreateCommentForm
              isAuthenticated={isAuthenticated}
              hasPost={true}
              onSubmit={handleCreateComment}
              replyTo={replyTo}
              onCancelReply={onCancelReply}
              variant="sheet"
            />
          </Suspense>
        )}
      </BottomSheet>
    </div>
  );
};

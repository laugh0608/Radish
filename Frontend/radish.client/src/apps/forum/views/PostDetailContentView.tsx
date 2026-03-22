import { lazy, Suspense, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Icon } from '@radish/ui/icon';
import type { PostDetail, CommentNode, QuestionAnswerSort, QuestionAnswerFilter } from '@/api/forum';
import type { UserFollowStatus } from '@/api/userFollow';
import { FORUM_DETAIL_TOOL_EVENT, type ForumDetailToolAction } from '../constants/detailTools';
import { useStickerCatalog } from '../hooks/useStickerCatalog';
import { useReactions } from '../hooks/useReactions';
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
  displayTimeZone: string;
  isLiked: boolean;
  isAuthenticated: boolean;
  showFloatingTools?: boolean;
  currentUserId: number;
  commentSortBy: 'newest' | 'hottest' | null;
  questionAnswerSort: QuestionAnswerSort;
  questionAnswerFilter: QuestionAnswerFilter;
  replyTo: { commentId: number; authorName: string } | null;
  followStatus: UserFollowStatus | null;
  followLoading: boolean;

  onBack: () => void;
  onLike: (postId: number) => void;
  onVotePoll: (optionId: number) => Promise<void>;
  onClosePoll: () => Promise<void>;
  onDrawLottery: () => Promise<void>;
  onAnswerQuestion: (content: string) => Promise<void>;
  onAcceptAnswer: (answerId: number) => Promise<void>;
  onQuestionAnswerSortChange: (sortBy: QuestionAnswerSort) => Promise<void>;
  onQuestionAnswerFilterChange: (filterBy: QuestionAnswerFilter) => void;
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
  onReactionError?: (message: string) => void;
  onToggleFollow: (targetUserId: number, isFollowing: boolean) => Promise<void>;
  onAuthorClick: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
}

const collectCommentIds = (nodes: CommentNode[]): number[] => {
  const ids: number[] = [];
  const stack = [...nodes];

  while (stack.length > 0) {
    const node = stack.pop();
    if (!node) {
      continue;
    }

    ids.push(node.voId);
    if (node.voChildren && node.voChildren.length > 0) {
      stack.push(...node.voChildren);
    }
  }

  return ids;
};

export const PostDetailContentView = ({
  post,
  comments,
  loadingPostDetail,
  loadingComments,
  displayTimeZone,
  isLiked,
  isAuthenticated,
  showFloatingTools = true,
  currentUserId,
  commentSortBy,
  questionAnswerSort,
  questionAnswerFilter,
  replyTo,
  followStatus,
  followLoading,
  onBack,
  onLike,
  onVotePoll,
  onClosePoll,
  onDrawLottery,
  onAnswerQuestion,
  onAcceptAnswer,
  onQuestionAnswerSortChange,
  onQuestionAnswerFilterChange,
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
  onCancelReply,
  onReactionError,
  onToggleFollow,
  onAuthorClick,
}: PostDetailContentViewProps) => {
  const { t } = useTranslation();
  const [isCommentSheetOpen, setIsCommentSheetOpen] = useState(false);
  const contentRef = useRef<HTMLDivElement>(null);
  const { stickerGroups, stickerMap, handleStickerSelect } = useStickerCatalog();
  const reactionsState = useReactions({ onError: onReactionError });

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

  useEffect(() => {
    if (!post?.voId) {
      return;
    }

    void reactionsState.loadPostReactions(post.voId);
  }, [post?.voId]);

  useEffect(() => {
    const commentIds = collectCommentIds(comments);
    void reactionsState.loadCommentReactions(commentIds, { replace: true });
  }, [comments]);

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

  const handleLoadMoreChildren = async (
    parentId: number,
    pageIndex: number,
    pageSize: number
  ): Promise<CommentNode[]> => {
    const children = await onLoadMoreChildren(parentId, pageIndex, pageSize);
    const childIds = collectCommentIds(children);
    if (childIds.length > 0) {
      await reactionsState.loadCommentReactions(childIds);
    }
    return children;
  };

  const handleRequireReactionLogin = () => {
    onReactionError?.(t('forum.loginRequiredToReact'));
  };

  return (
    <div className={styles.detailView}>
      <div className={styles.detailToolbar}>
        <button className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={20} />
          {t('forum.backToList')}
        </button>
      </div>

      <div className={styles.detailBody}>
        <div
          className={`${styles.detailContent} ${showFloatingTools ? styles.withFloatingTools : ''}`}
          ref={contentRef}
        >
          <Suspense fallback={<div style={{ padding: '0.75rem 0' }}>{t('forum.loadingPostContent')}</div>}>
            <PostDetailComponent
              post={post}
              loading={loadingPostDetail}
              displayTimeZone={displayTimeZone}
              isLiked={isLiked}
              onLike={onLike}
              onVotePoll={onVotePoll}
              onClosePoll={onClosePoll}
              onDrawLottery={onDrawLottery}
              onAnswerQuestion={onAnswerQuestion}
              onAcceptAnswer={onAcceptAnswer}
              answerSort={questionAnswerSort}
              answerFilter={questionAnswerFilter}
              onAnswerSortChange={onQuestionAnswerSortChange}
              onAnswerFilterChange={onQuestionAnswerFilterChange}
              isAuthenticated={isAuthenticated}
              currentUserId={currentUserId}
              onEdit={onEdit}
              onViewHistory={onViewPostHistory}
              onDelete={onDelete}
              postReactions={reactionsState.postItems}
              reactionLoading={reactionsState.loadingPost || reactionsState.isPending('Post', post.voId)}
              stickerGroups={stickerGroups}
              stickerMap={stickerMap}
              onToggleReaction={(payload) => reactionsState.togglePostReaction(post.voId, payload)}
              onRequireReactionLogin={handleRequireReactionLogin}
              followStatus={followStatus}
              followLoading={followLoading}
              onToggleFollow={onToggleFollow}
              onAuthorClick={onAuthorClick}
            />
          </Suspense>

          <Suspense fallback={<p className={styles.loadingText}>{t('forum.loadingDiscussion')}</p>}>
            <CommentTree
              comments={comments}
              loading={loadingComments}
              hasPost={true}
              displayTimeZone={displayTimeZone}
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
              onLoadMoreChildren={handleLoadMoreChildren}
              stickerMap={stickerMap}
              reactionMap={reactionsState.commentItemsMap}
              isAuthenticated={isAuthenticated}
              stickerGroups={stickerGroups}
              onToggleReaction={reactionsState.toggleCommentReaction}
              isReactionPending={(commentId) => reactionsState.isPending('Comment', commentId)}
              onRequireReactionLogin={handleRequireReactionLogin}
              onAuthorClick={onAuthorClick}
            />
          </Suspense>

          <div className={styles.commentCta}>
            <button className={styles.commentButton} onClick={handleOpenCommentSheet}>
              {t('forum.joinDiscussion')}
            </button>
          </div>
        </div>

        {showFloatingTools && (
          <div className={styles.floatingTools}>
            <button className={styles.toolButton} onClick={handleScrollTop} title={t('forum.scrollTop')}>
              <Icon icon="mdi:chevron-up" size={20} className={styles.toolIcon} />
            </button>
            <button className={styles.toolButton} onClick={handleScrollBottom} title={t('forum.scrollBottom')}>
              <Icon icon="mdi:chevron-down" size={20} className={styles.toolIcon} />
            </button>
            <button className={styles.toolButton} onClick={handleOpenCommentSheet} title={t('forum.quickComment')}>
              <Icon icon="mdi:comment-outline" size={18} className={styles.toolIcon} />
            </button>
          </div>
        )}
      </div>

      <BottomSheet
        isOpen={isCommentSheetOpen}
        onClose={handleCloseCommentSheet}
        title={t('forum.joinDiscussion')}
        height="60%"
        className={styles.commentSheet}
        bodyClassName={styles.commentSheetBody}
        overlayClassName={styles.commentSheetOverlay}
      >
        {isCommentSheetOpen && (
          <div className={styles.commentSheetContent}>
            <Suspense fallback={<div className={styles.commentSheetLoading}>{t('forum.loadingDiscussionEditor')}</div>}>
              <CreateCommentForm
                isAuthenticated={isAuthenticated}
                hasPost={true}
                onSubmit={handleCreateComment}
                replyTo={replyTo}
                onCancelReply={onCancelReply}
                variant="sheet"
                title={t('forum.joinDiscussion')}
                submitText={t('forum.submitDiscussion')}
                placeholder={t('forum.discussionPlaceholder')}
                stickerGroups={stickerGroups}
                onStickerSelect={(selection) => {
                  void handleStickerSelect(selection);
                }}
              />
            </Suspense>
          </div>
        )}
      </BottomSheet>
    </div>
  );
};

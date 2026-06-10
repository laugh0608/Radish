import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Icon } from '@radish/ui/icon';
import type { PostDetail, CommentNode, CommentReplyTarget, PostQuickReply, QuestionAnswerSort, QuestionAnswerFilter } from '@/api/forum';
import type { LongId } from '@/api/user';
import type { UserFollowStatus } from '@/api/userFollow';
import { FORUM_DETAIL_TOOL_EVENT, type ForumDetailToolAction } from '../constants/detailTools';
import { useStickerCatalog } from '../hooks/useStickerCatalog';
import { useReactions } from '../hooks/useReactions';
import { buildDesktopForumPostReturnPath } from '@/services/authReturnPath';
import type { ForumWorkspaceIntent } from '@/utils/forumNavigation';
import styles from './PostDetailContentView.module.css';

const PostDetailComponent = lazy(() =>
  import('../components/PostDetail').then((module) => ({ default: module.PostDetail }))
);

const CommentTree = lazy(() =>
  import('../components/CommentTree').then((module) => ({ default: module.CommentTree }))
);

const PostQuickReplyWall = lazy(() =>
  import('../components/PostQuickReplyWall').then((module) => ({ default: module.PostQuickReplyWall }))
);

const CreateCommentForm = lazy(() =>
  import('../components/CreateCommentForm').then((module) => ({ default: module.CreateCommentForm }))
);

interface PostDetailContentViewProps {
  post: PostDetail;
  comments: CommentNode[];
  quickReplies: PostQuickReply[];
  quickReplyTotal: number;
  commentTotal: number;
  commentPageSize: number;
  loadingPostDetail: boolean;
  loadingComments: boolean;
  loadingQuickReplies: boolean;
  loadingMoreComments: boolean;
  displayTimeZone: string;
  isLiked: boolean;
  isAuthenticated: boolean;
  showFloatingTools?: boolean;
  currentUserId: LongId;
  canToggleTop: boolean;
  commentSortBy: 'newest' | 'hottest' | null;
  questionAnswerSort: QuestionAnswerSort;
  questionAnswerFilter: QuestionAnswerFilter;
  replyTo: CommentReplyTarget | null;
  followStatus: UserFollowStatus | null;
  followLoading: boolean;
  commentNavigationTarget?: {
    commentId: LongId;
    expandedRootCommentId?: LongId;
    navigationKey: string;
  } | null;
  commentTypingUserNames?: string[];

  onBack: () => void;
  onLike: (postId: LongId) => void;
  onVotePoll: (optionId: number) => Promise<void>;
  onClosePoll: () => Promise<void>;
  onDrawLottery: () => Promise<void>;
  onAnswerQuestion: (content: string) => Promise<void>;
  onAcceptAnswer: (answerId: LongId) => Promise<void>;
  onQuestionAnswerSortChange: (sortBy: QuestionAnswerSort) => Promise<void>;
  onQuestionAnswerFilterChange: (filterBy: QuestionAnswerFilter) => void;
  onToggleTop: (isTop: boolean) => Promise<void>;
  onEdit: (postId: LongId) => void;
  onViewPostHistory: (postId: LongId) => void;
  onDelete: (postId: LongId) => void;
  onCreateQuickReply: (content: string) => Promise<void>;
  onDeleteQuickReply: (quickReplyId: LongId) => Promise<void>;
  onCommentSortChange: (sortBy: 'newest' | 'hottest') => void;
  onDeleteComment: (commentId: LongId) => void;
  onEditComment: (commentId: LongId, newContent: string) => Promise<void>;
  onViewCommentHistory: (commentId: LongId) => void;
  onLikeComment: (commentId: LongId) => Promise<{ isLiked: boolean; likeCount: number }>;
  onReplyComment: (target: CommentReplyTarget) => void;
  onLoadMoreChildren: (
    parentId: LongId,
    pageIndex: number,
    pageSize: number
  ) => Promise<CommentNode[]>;
  onLoadMoreComments: (postId: LongId) => Promise<void>;
  onCreateComment: (content: string) => Promise<void>;
  onCommentTyping?: (commentId?: LongId | null) => void;
  onCancelReply: () => void;
  onReactionError?: (message: string) => void;
  onToggleFollow: (targetUserId: LongId, isFollowing: boolean) => Promise<void>;
  onAuthorClick: (userId: LongId, userName?: string | null, avatarUrl?: string | null) => void;
  onReportPost: (postId: LongId) => void;
  onReportQuickReply: (quickReplyId: LongId) => void;
  onReportComment: (commentId: LongId) => void;
  onNavigateToComment: (commentId: LongId) => Promise<void> | void;
  workspaceIntent?: ForumWorkspaceIntent | null;
  workspaceIntentKey?: string | null;
}

const collectCommentIds = (nodes: CommentNode[]): LongId[] => {
  const ids: LongId[] = [];
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
  quickReplies,
  quickReplyTotal,
  commentTotal,
  commentPageSize,
  loadingPostDetail,
  loadingComments,
  loadingQuickReplies,
  loadingMoreComments,
  displayTimeZone,
  isLiked,
  isAuthenticated,
  showFloatingTools = true,
  currentUserId,
  canToggleTop,
  commentSortBy,
  questionAnswerSort,
  questionAnswerFilter,
  replyTo,
  followStatus,
  followLoading,
  commentNavigationTarget = null,
  commentTypingUserNames = [],
  onBack,
  onLike,
  onVotePoll,
  onClosePoll,
  onDrawLottery,
  onAnswerQuestion,
  onAcceptAnswer,
  onQuestionAnswerSortChange,
  onQuestionAnswerFilterChange,
  onToggleTop,
  onEdit,
  onViewPostHistory,
  onDelete,
  onCreateQuickReply,
  onDeleteQuickReply,
  onCommentSortChange,
  onDeleteComment,
  onEditComment,
  onViewCommentHistory,
  onLikeComment,
  onReplyComment,
  onLoadMoreChildren,
  onLoadMoreComments,
  onCreateComment,
  onCommentTyping,
  onCancelReply,
  onReactionError,
  onToggleFollow,
  onAuthorClick,
  onReportPost,
  onReportQuickReply,
  onReportComment,
  onNavigateToComment,
  workspaceIntent = null,
  workspaceIntentKey = null,
}: PostDetailContentViewProps) => {
  const { i18n, t } = useTranslation();
  const [isCommentSheetOpen, setIsCommentSheetOpen] = useState(false);
  const [highlightedCommentId, setHighlightedCommentId] = useState<LongId | null>(null);
  const [commentAnchorVersion, setCommentAnchorVersion] = useState(0);
  const contentRef = useRef<HTMLDivElement>(null);
  const handledCommentNavigationRef = useRef<string | null>(null);
  const highlightTimeoutRef = useRef<number | null>(null);
  const commentAnchorMapRef = useRef(new Map<string, HTMLDivElement>());
  const { stickerGroups, stickerMap, handleStickerSelect } = useStickerCatalog();
  const reactionsState = useReactions({ onError: onReactionError });
  const { loadPostReactions, loadCommentReactions } = reactionsState;
  const loginReturnPath = useMemo(
    () => buildDesktopForumPostReturnPath({
      postId: post.voId,
      postPublicId: post.voPublicId,
    }),
    [post.voId, post.voPublicId],
  );
  const commentLoginReturnPath = useMemo(
    () => buildDesktopForumPostReturnPath({
      postId: post.voId,
      postPublicId: post.voPublicId,
      commentId: replyTo?.targetCommentId,
    }),
    [post.voId, post.voPublicId, replyTo?.targetCommentId],
  );
  const commentTypingText = useMemo(() => {
    if (commentTypingUserNames.length === 0) {
      return '';
    }

    const separator = i18n.language.startsWith('zh') ? '、' : ', ';
    return `${commentTypingUserNames.join(separator)}${t('forum.comment.typingSuffix')}`;
  }, [commentTypingUserNames, i18n.language, t]);

  const registerCommentAnchor = useCallback((commentId: LongId, element: HTMLDivElement | null) => {
    const commentIdKey = String(commentId);
    if (element) {
      commentAnchorMapRef.current.set(commentIdKey, element);
    } else {
      commentAnchorMapRef.current.delete(commentIdKey);
    }

    if (String(commentId) === String(commentNavigationTarget?.commentId ?? '')) {
      setCommentAnchorVersion((prev) => prev + 1);
    }
  }, [commentNavigationTarget?.commentId]);

  useEffect(() => {
    if (replyTo) {
      setIsCommentSheetOpen(true);
    }
  }, [replyTo]);

  useEffect(() => {
    if (workspaceIntent !== 'comment' || !workspaceIntentKey || !isAuthenticated || !post?.voId) {
      return;
    }

    setIsCommentSheetOpen(true);
  }, [workspaceIntent, workspaceIntentKey, isAuthenticated, post?.voId]);

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

    void loadPostReactions(post.voId);
  }, [loadPostReactions, post?.voId]);

  useEffect(() => {
    const commentIds = collectCommentIds(comments);
    void loadCommentReactions(commentIds, { replace: true });
  }, [comments, loadCommentReactions]);

  useEffect(() => {
    return () => {
      if (highlightTimeoutRef.current !== null) {
        window.clearTimeout(highlightTimeoutRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (!commentNavigationTarget?.commentId || !contentRef.current) {
      return;
    }

    const navigationSignature = `${commentNavigationTarget.navigationKey}:${commentNavigationTarget.commentId}`;
    if (handledCommentNavigationRef.current === navigationSignature) {
      return;
    }

    const targetElement = commentAnchorMapRef.current.get(String(commentNavigationTarget.commentId));
    if (!targetElement) {
      return;
    }

    handledCommentNavigationRef.current = navigationSignature;

    const container = contentRef.current;
    const containerRect = container.getBoundingClientRect();
    const targetRect = targetElement.getBoundingClientRect();
    const offsetTop = Math.max(24, Math.round(container.clientHeight * 0.18));
    const nextTop = container.scrollTop + (targetRect.top - containerRect.top) - offsetTop;

    container.scrollTo({
      top: Math.max(0, nextTop),
      behavior: 'smooth'
    });

    setHighlightedCommentId(commentNavigationTarget.commentId);
    if (highlightTimeoutRef.current !== null) {
      window.clearTimeout(highlightTimeoutRef.current);
    }
    highlightTimeoutRef.current = window.setTimeout(() => {
      setHighlightedCommentId((current) => (
        current === commentNavigationTarget.commentId
          ? null
          : current
      ));
    }, 3200);
  }, [commentAnchorVersion, commentNavigationTarget, comments]);

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
    parentId: LongId,
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
              canToggleTop={canToggleTop}
              onToggleTop={onToggleTop}
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
              onReport={onReportPost}
            />
          </Suspense>

          <Suspense fallback={<p className={styles.loadingText}>{t('forum.quickReply.loading')}</p>}>
            <PostQuickReplyWall
              replies={quickReplies}
              total={quickReplyTotal}
              loading={loadingQuickReplies}
              isAuthenticated={isAuthenticated}
              currentUserId={currentUserId}
              onCreate={onCreateQuickReply}
              onDelete={onDeleteQuickReply}
              onReport={onReportQuickReply}
              loginReturnPath={loginReturnPath}
              autoFocusComposerKey={workspaceIntent === 'quickReply' ? workspaceIntentKey : null}
            />
          </Suspense>

          <div className={styles.discussionHeader}>
            <div>
              <h3 className={styles.discussionTitle}>{t('forum.quickReply.discussionTitle')}</h3>
              <p className={styles.discussionSubtitle}>{t('forum.quickReply.discussionSubtitle')}</p>
              {commentTypingText && (
                <p className={styles.commentTypingNotice}>
                  {commentTypingText}
                </p>
              )}
            </div>
            <button className={styles.commentButton} onClick={handleOpenCommentSheet}>
              {t('forum.quickReply.openDiscussion')}
            </button>
          </div>

          <Suspense fallback={<p className={styles.loadingText}>{t('forum.loadingDiscussion')}</p>}>
            <CommentTree
              comments={comments}
              loading={loadingComments}
              loadingMoreRootComments={loadingMoreComments}
              hasPost={true}
              displayTimeZone={displayTimeZone}
              currentUserId={currentUserId}
              highlightedCommentId={highlightedCommentId}
              expandedRootCommentId={commentNavigationTarget?.expandedRootCommentId}
              pageSize={5}
              rootCommentTotal={commentTotal}
              loadedRootCommentCount={comments.length}
              rootCommentPageSize={commentPageSize}
              registerCommentAnchor={registerCommentAnchor}
              sortBy={commentSortBy}
              onSortChange={onCommentSortChange}
              onDeleteComment={onDeleteComment}
              onEditComment={onEditComment}
              onViewCommentHistory={onViewCommentHistory}
              onLikeComment={onLikeComment}
              onReplyComment={(target) => {
                onReplyComment(target);
                setIsCommentSheetOpen(true);
              }}
              onCommentTyping={onCommentTyping}
              onLoadMoreChildren={handleLoadMoreChildren}
              onLoadMoreRootComments={() => onLoadMoreComments(post.voId)}
              stickerMap={stickerMap}
              reactionMap={reactionsState.commentItemsMap}
              isAuthenticated={isAuthenticated}
              stickerGroups={stickerGroups}
              onToggleReaction={reactionsState.toggleCommentReaction}
              isReactionPending={(commentId) => reactionsState.isPending('Comment', commentId)}
              onRequireReactionLogin={handleRequireReactionLogin}
              onAuthorClick={onAuthorClick}
              onReportComment={onReportComment}
              onNavigateToComment={onNavigateToComment}
            />
          </Suspense>
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
                loginReturnPath={commentLoginReturnPath}
                onTyping={onCommentTyping}
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

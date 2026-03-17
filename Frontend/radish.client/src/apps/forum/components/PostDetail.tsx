import { lazy, Suspense, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { PostDetail as PostDetailType, QuestionAnswerFilter, QuestionAnswerSort, ReactionSummaryVo } from '@/api/forum';
import { uploadDocument, uploadImage } from '@/api/attachment';
import type { UserFollowStatus } from '@/api/userFollow';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { Icon } from '@radish/ui/icon';
import { ReactionBar, type ReactionTogglePayload } from '@radish/ui/reaction-bar';
import type { StickerPickerGroup } from '@radish/ui/sticker-picker';
import type { MarkdownStickerMap } from '@radish/ui/markdown-renderer';
import styles from './PostDetail.module.css';

const MarkdownRenderer = lazy(() =>
  import('@radish/ui/markdown-renderer').then((module) => ({ default: module.MarkdownRenderer }))
);

const MarkdownEditor = lazy(() =>
  import('@radish/ui/markdown-editor').then((module) => ({ default: module.MarkdownEditor }))
);

const appendImageMeta = (displayUrl: string, fullUrl?: string): string => {
  const params = new URLSearchParams();
  if (fullUrl) {
    params.set('full', fullUrl);
  }

  const meta = params.toString();
  return meta ? `${displayUrl}#radish:${meta}` : displayUrl;
};

interface PostDetailProps {
  post: PostDetailType | null;
  loading?: boolean;
  displayTimeZone: string;
  isLiked?: boolean;
  onLike?: (postId: number) => void;
  onVotePoll?: (optionId: number) => Promise<void>;
  onDrawLottery?: () => Promise<void>;
  onAnswerQuestion?: (content: string) => Promise<void>;
  onAcceptAnswer?: (answerId: number) => Promise<void>;
  answerSort?: QuestionAnswerSort;
  answerFilter?: QuestionAnswerFilter;
  onAnswerSortChange?: (sortBy: QuestionAnswerSort) => Promise<void>;
  onAnswerFilterChange?: (filterBy: QuestionAnswerFilter) => void;
  isAuthenticated?: boolean;
  currentUserId?: number;
  onEdit?: (postId: number) => void;
  onDelete?: (postId: number) => void;
  onViewHistory?: (postId: number) => void;
  postReactions?: ReactionSummaryVo[];
  reactionLoading?: boolean;
  stickerGroups?: StickerPickerGroup[];
  stickerMap?: MarkdownStickerMap;
  onToggleReaction?: (payload: ReactionTogglePayload) => Promise<void>;
  onRequireReactionLogin?: () => void;
  followStatus?: UserFollowStatus | null;
  followLoading?: boolean;
  onToggleFollow?: (targetUserId: number, isFollowing: boolean) => Promise<void>;
  onAuthorClick?: (userId: number, userName?: string | null, avatarUrl?: string | null) => void;
}

export const PostDetail = ({
  post,
  loading = false,
  displayTimeZone,
  isLiked = false,
  onLike,
  onVotePoll,
  onDrawLottery,
  onAnswerQuestion,
  onAcceptAnswer,
  answerSort = 'default',
  answerFilter = 'all',
  onAnswerSortChange,
  onAnswerFilterChange,
  isAuthenticated = false,
  currentUserId = 0,
  onEdit,
  onDelete,
  onViewHistory,
  postReactions = [],
  reactionLoading = false,
  stickerGroups = [],
  stickerMap,
  onToggleReaction,
  onRequireReactionLogin,
  followStatus = null,
  followLoading = false,
  onToggleFollow,
  onAuthorClick,
}: PostDetailProps) => {
  const { t } = useTranslation();
  const parsedTags = post?.voTags
    ? post.voTags
        .split(',')
        .map(tag => tag.trim())
        .filter(Boolean)
    : [];
  const tagList = post?.voTagNames && post.voTagNames.length > 0 ? post.voTagNames : parsedTags;
  const [selectedOptionId, setSelectedOptionId] = useState<number | null>(post?.voPoll?.voSelectedOptionId ?? null);
  const [isVoting, setIsVoting] = useState(false);
  const [answerContent, setAnswerContent] = useState('');
  const [isSubmittingAnswer, setIsSubmittingAnswer] = useState(false);
  const [acceptingAnswerId, setAcceptingAnswerId] = useState<number | null>(null);
  const [isDrawingLottery, setIsDrawingLottery] = useState(false);

  const isAuthor = !!post && currentUserId > 0 && String(post.voAuthorId) === String(currentUserId);
  const showFollowAction = isAuthenticated && !!post && !isAuthor && post.voAuthorId > 0;
  const question = post?.voQuestion;
  const lottery = post?.voLottery;
  const isQuestionPost = !!post?.voIsQuestion;
  const isLotteryPost = !!post?.voHasLottery && !!lottery;
  const canAnswerQuestion = isAuthenticated && !isSubmittingAnswer;
  const canViewQuestionHistory = isQuestionPost && !!onViewHistory;
  const drawTimeValue = lottery?.voDrawTime ? new Date(lottery.voDrawTime) : null;
  const isDrawTimeReached = !drawTimeValue || Number.isNaN(drawTimeValue.getTime()) || drawTimeValue.getTime() <= Date.now();
  const canDrawLottery = Boolean(
    isLotteryPost &&
    isAuthenticated &&
    isAuthor &&
    !lottery?.voIsDrawn &&
    isDrawTimeReached &&
    onDrawLottery
  );
  const lotteryStatusText = !lottery
    ? ''
    : lottery.voIsDrawn
      ? '已开奖'
      : isAuthor
        ? isDrawTimeReached
          ? '可立即开奖'
          : '等待开奖时间'
        : '等待发帖者开奖';
  const sortedAnswers = !question?.voAnswers
    ? []
    : [...question.voAnswers].sort((left, right) => {
        if (answerSort === 'latest') {
          return new Date(right.voCreateTime).getTime() - new Date(left.voCreateTime).getTime();
        }

        if (left.voIsAccepted !== right.voIsAccepted) {
          return left.voIsAccepted ? -1 : 1;
        }

        const timeDiff = new Date(left.voCreateTime).getTime() - new Date(right.voCreateTime).getTime();
        if (timeDiff !== 0) {
          return timeDiff;
        }

        return left.voAnswerId - right.voAnswerId;
      });
  const displayedAnswers = answerFilter === 'accepted'
    ? sortedAnswers.filter(answer => answer.voIsAccepted)
    : sortedAnswers;
  const questionEmptyText = question?.voAnswerCount
    ? '当前筛选下还没有已采纳答案。'
    : '还没有回答，欢迎先来补充。';

  const buildAvatarText = (name: string) => {
    const source = name.trim();
    if (!source) return '?';
    return source.charAt(0).toUpperCase();
  };

  const buildAvatarStyle = (seed: string) => {
    let hash = 0;
    for (let i = 0; i < seed.length; i += 1) {
      hash = seed.charCodeAt(i) + ((hash << 5) - hash);
    }

    const hue = Math.abs(hash) % 360;
    return {
      backgroundColor: `hsl(${hue} 80% 92%)`,
      color: `hsl(${hue} 45% 30%)`
    };
  };

  useEffect(() => {
    setSelectedOptionId(post?.voPoll?.voSelectedOptionId ?? null);
  }, [post?.voId, post?.voPoll?.voSelectedOptionId]);

  useEffect(() => {
    setAnswerContent('');
    setAcceptingAnswerId(null);
  }, [post?.voId]);

  if (loading) {
    return (
      <div className={styles.container}>
        <h3 className={styles.title}>帖子详情</h3>
        <p className={styles.loadingText}>加载帖子详情中...</p>
      </div>
    );
  }

  if (!post) {
    return (
      <div className={styles.container}>
        <h3 className={styles.title}>帖子详情</h3>
        <p className={styles.emptyText}>请选择一个帖子查看详情</p>
      </div>
    );
  }

  const poll = post.voPoll;
  const canSubmitPoll = !!poll && !poll.voIsClosed && !poll.voHasVoted && isAuthenticated;
  const pollStatusText = !poll
    ? ''
    : poll.voIsClosed
      ? '投票已结束'
      : poll.voHasVoted
        ? '你已投票，可查看实时结果'
        : isAuthenticated
          ? '选择一个选项后即可提交'
          : '登录后可参与投票';

  const handleVoteSubmit = async () => {
    if (!canSubmitPoll || !selectedOptionId || !onVotePoll) {
      return;
    }

    setIsVoting(true);
    try {
      await onVotePoll(selectedOptionId);
    } finally {
      setIsVoting(false);
    }
  };

  const handleDrawLottery = async () => {
    if (!canDrawLottery || !onDrawLottery) {
      return;
    }

    setIsDrawingLottery(true);
    try {
      await onDrawLottery();
    } finally {
      setIsDrawingLottery(false);
    }
  };

  const handleAnswerSubmit = async () => {
    const trimmedContent = answerContent.trim();
    if (!trimmedContent || !onAnswerQuestion) {
      return;
    }

    setIsSubmittingAnswer(true);
    try {
      await onAnswerQuestion(trimmedContent);
      setAnswerContent('');
    } finally {
      setIsSubmittingAnswer(false);
    }
  };

  const handleAnswerImageUpload = async (file: File) => {
    const result = await uploadImage({
      file,
      businessType: 'Comment',
      generateThumbnail: true,
      generateMultipleSizes: false,
      removeExif: true
    }, t);

    const displayUrl = result.voThumbnailUrl || result.voUrl;
    return {
      url: appendImageMeta(displayUrl, result.voUrl),
      thumbnailUrl: result.voThumbnailUrl
    };
  };

  const handleAnswerDocumentUpload = async (file: File) => {
    const result = await uploadDocument({
      file,
      businessType: 'Comment'
    }, t);

    return {
      url: result.voUrl,
      fileName: result.voOriginalName || file.name
    };
  };

  const handleAcceptAnswer = async (answerId: number) => {
    if (!onAcceptAnswer) {
      return;
    }

    setAcceptingAnswerId(answerId);
    try {
      await onAcceptAnswer(answerId);
    } finally {
      setAcceptingAnswerId(null);
    }
  };

  return (
    <div className={styles.container}>
      <h3 className={styles.title}>帖子详情</h3>
      <div className={styles.postContent}>
        <h4 className={styles.postTitle}>{post.voTitle}</h4>
        {isQuestionPost && (
          <div className={styles.statusRow}>
            <span className={`${styles.statusBadge} ${styles.questionBadge}`}>问答</span>
            <span className={`${styles.statusBadge} ${post.voIsSolved ? styles.solvedBadge : styles.pendingBadge}`}>
              {post.voIsSolved ? '已解决' : '待解决'}
            </span>
            <span className={`${styles.statusBadge} ${styles.answerBadge}`}>
              回答 {post.voAnswerCount ?? question?.voAnswerCount ?? 0}
            </span>
          </div>
        )}
        <div className={styles.postMeta}>
          {post.voAuthorName && (
            <button
              type="button"
              className={styles.authorLink}
              onClick={() => onAuthorClick?.(post.voAuthorId, post.voAuthorName, post.voAuthorAvatarUrl)}
            >
              作者：{post.voAuthorName}
            </button>
          )}
          {post.voCreateTime && <span> · {formatDateTimeByTimeZone(post.voCreateTime, displayTimeZone)}</span>}
          {post.voViewCount !== undefined && <span> · 浏览 {post.voViewCount}</span>}
        </div>
        <Suspense fallback={<div className={styles.postBody}>正文渲染中...</div>}>
          <MarkdownRenderer content={post.voContent} className={styles.postBody} stickerMap={stickerMap} />
        </Suspense>
        {tagList.length > 0 && (
          <div className={styles.postTags}>
            {tagList.map((tag, index) => (
              <span key={index} className={styles.tag}>
                {tag}
              </span>
            ))}
          </div>
        )}

        {poll && (
          <section className={styles.pollCard}>
            <div className={styles.pollHeader}>
              <div>
                <div className={styles.pollTitleRow}>
                  <span className={styles.pollBadge}>投票</span>
                  <h5 className={styles.pollQuestion}>{poll.voQuestion}</h5>
                </div>
                <p className={styles.pollMeta}>
                  共 {poll.voTotalVoteCount} 票
                  {poll.voEndTime ? ` · 截止于 ${formatDateTimeByTimeZone(poll.voEndTime, displayTimeZone)}` : ' · 长期有效'}
                </p>
              </div>
              <span className={`${styles.pollState} ${poll.voIsClosed ? styles.pollStateClosed : ''}`}>
                {poll.voIsClosed ? '已截止' : poll.voHasVoted ? '已投票' : '进行中'}
              </span>
            </div>

            <div className={styles.pollOptions}>
              {poll.voOptions.map((option) => {
                const isSelected = selectedOptionId === option.voOptionId;
                const isVotedOption = poll.voSelectedOptionId === option.voOptionId;
                return (
                  <button
                    key={option.voOptionId}
                    type="button"
                    className={`${styles.pollOption} ${isSelected ? styles.pollOptionSelected : ''} ${isVotedOption ? styles.pollOptionVoted : ''}`}
                    onClick={() => {
                      if (canSubmitPoll) {
                        setSelectedOptionId(option.voOptionId);
                      }
                    }}
                    disabled={!canSubmitPoll}
                  >
                    <div className={styles.pollOptionTop}>
                      <span className={styles.pollOptionText}>{option.voOptionText}</span>
                      <span className={styles.pollOptionValue}>
                        {option.voVoteCount} 票 · {option.voVotePercent.toFixed(2)}%
                      </span>
                    </div>
                    <div className={styles.pollProgressTrack}>
                      <span
                        className={styles.pollProgressValue}
                        style={{ width: `${Math.max(0, Math.min(option.voVotePercent, 100))}%` }}
                      />
                    </div>
                  </button>
                );
              })}
            </div>

            <div className={styles.pollFooter}>
              <span className={styles.pollHint}>{pollStatusText}</span>
              <button
                type="button"
                className={styles.pollSubmitButton}
                onClick={handleVoteSubmit}
                disabled={!canSubmitPoll || !selectedOptionId || isVoting}
              >
                {isVoting ? '提交中...' : '提交投票'}
              </button>
            </div>
          </section>
        )}

        {lottery && (
          <section className={styles.lotteryCard}>
            <div className={styles.lotteryHeader}>
              <div>
                <div className={styles.lotteryTitleRow}>
                  <span className={styles.lotteryBadge}>抽奖</span>
                  <h5 className={styles.lotteryTitle}>{lottery.voPrizeName}</h5>
                </div>
                <p className={styles.lotteryMeta}>
                  参与 {lottery.voParticipantCount} 人
                  {' · '}
                  中奖 {lottery.voWinnerCount} 人
                  {lottery.voDrawTime
                    ? ` · 开奖时间 ${formatDateTimeByTimeZone(lottery.voDrawTime, displayTimeZone)}`
                    : ' · 未设置开奖时间'}
                </p>
              </div>
              <span className={`${styles.lotteryState} ${lottery.voIsDrawn ? styles.lotteryStateDrawn : ''}`}>
                {lotteryStatusText}
              </span>
            </div>

            {lottery.voPrizeDescription && (
              <p className={styles.lotteryDescription}>{lottery.voPrizeDescription}</p>
            )}

            <div className={styles.lotteryRules}>
              <span className={styles.lotteryRule}>发布一条顶级评论即可参与</span>
              <span className={styles.lotteryRule}>回复他人评论不计入资格</span>
              <span className={styles.lotteryRule}>同一用户按 1 个资格去重</span>
              <span className={styles.lotteryRule}>发帖者本人不进入中奖池</span>
            </div>

            <div className={styles.lotteryFooter}>
              <span className={styles.lotteryHint}>
                {lottery.voIsDrawn
                  ? lottery.voDrawnAt
                    ? `已于 ${formatDateTimeByTimeZone(lottery.voDrawnAt, displayTimeZone)} 开奖`
                    : '已完成开奖'
                  : isAuthor
                    ? isDrawTimeReached
                      ? '开奖后会固化中奖名单，不会重复随机。'
                      : '未到开奖时间前不可开奖。'
                    : '中奖名单会在发帖者开奖后展示。'}
              </span>
              {canDrawLottery && (
                <button
                  type="button"
                  className={styles.lotteryDrawButton}
                  onClick={() => {
                    void handleDrawLottery();
                  }}
                  disabled={isDrawingLottery}
                >
                  {isDrawingLottery ? '开奖中...' : '立即开奖'}
                </button>
              )}
            </div>

            {lottery.voWinners.length > 0 ? (
              <div className={styles.lotteryWinnerSection}>
                <div className={styles.lotteryWinnerHeader}>
                  <span className={styles.lotteryWinnerTitle}>中奖名单</span>
                  <span className={styles.lotteryWinnerCount}>共 {lottery.voWinners.length} 人</span>
                </div>
                <div className={styles.lotteryWinnerList}>
                  {lottery.voWinners.map((winner, index) => (
                    <article key={winner.voId} className={styles.lotteryWinnerItem}>
                      <div className={styles.lotteryWinnerMeta}>
                        <span className={styles.lotteryWinnerRank}>#{index + 1}</span>
                        <button
                          type="button"
                          className={styles.lotteryWinnerName}
                          onClick={() => onAuthorClick?.(winner.voUserId, winner.voUserName, null)}
                        >
                          {winner.voUserName}
                        </button>
                      </div>
                      {winner.voCommentContentSnapshot && (
                        <p className={styles.lotteryWinnerComment}>{winner.voCommentContentSnapshot}</p>
                      )}
                    </article>
                  ))}
                </div>
              </div>
            ) : (
              <p className={styles.lotteryEmpty}>
                {lottery.voIsDrawn ? '本次开奖暂无中奖名单。' : '开奖后将在这里展示中奖名单。'}
              </p>
            )}
          </section>
        )}

        {isQuestionPost && (
          <section className={styles.questionCard}>
            <div className={styles.questionHeader}>
              <div>
                <div className={styles.questionTitleRow}>
                  <span className={styles.questionSectionBadge}>回答</span>
                  <h5 className={styles.questionTitle}>解决方案</h5>
                </div>
                <p className={styles.questionMeta}>
                  {question?.voIsSolved ? '该问题已采纳答案' : '该问题尚未采纳答案'}
                  {' · '}
                  共 {question?.voAnswerCount ?? post.voAnswerCount ?? 0} 条回答
                </p>
                <div className={styles.answerToolbar}>
                  <div className={styles.answerToolbarGroup}>
                    <span className={styles.answerToolbarLabel}>排序</span>
                    <div className={styles.answerSortButtons}>
                      <button
                        type="button"
                        className={`${styles.answerSortButton} ${answerSort === 'default' ? styles.answerSortButtonActive : ''}`}
                        onClick={() => {
                          void onAnswerSortChange?.('default');
                        }}
                      >
                        默认排序
                      </button>
                      <button
                        type="button"
                        className={`${styles.answerSortButton} ${answerSort === 'latest' ? styles.answerSortButtonActive : ''}`}
                        onClick={() => {
                          void onAnswerSortChange?.('latest');
                        }}
                      >
                        最新回答
                      </button>
                    </div>
                  </div>
                  <div className={styles.answerToolbarGroup}>
                    <span className={styles.answerToolbarLabel}>筛选</span>
                    <div className={styles.answerSortButtons}>
                      <button
                        type="button"
                        className={`${styles.answerSortButton} ${answerFilter === 'all' ? styles.answerSortButtonActive : ''}`}
                        onClick={() => onAnswerFilterChange?.('all')}
                      >
                        全部回答
                      </button>
                      <button
                        type="button"
                        className={`${styles.answerSortButton} ${answerFilter === 'accepted' ? styles.answerSortButtonActive : ''}`}
                        onClick={() => onAnswerFilterChange?.('accepted')}
                      >
                        只看已采纳
                      </button>
                    </div>
                  </div>
                </div>
              </div>
              <div className={styles.questionHeaderActions}>
                <span className={`${styles.questionState} ${question?.voIsSolved ? styles.questionStateSolved : styles.questionStatePending}`}>
                  {question?.voIsSolved ? '已解决' : '待解决'}
                </span>
                {canViewQuestionHistory && (
                  <button
                    type="button"
                    onClick={() => onViewHistory?.(post.voId)}
                    className={styles.historyButton}
                    title="查看问题历史"
                  >
                    <Icon icon="mdi:history" size={18} />
                    问题历史
                  </button>
                )}
              </div>
            </div>

            {displayedAnswers.length > 0 ? (
              <div className={styles.answerList}>
                {displayedAnswers.map((answer) => {
                  const canAccept =
                    isAuthor &&
                    !question?.voIsSolved &&
                    !answer.voIsAccepted &&
                    answer.voAuthorId !== currentUserId;

                  return (
                    <article
                      key={answer.voAnswerId}
                      className={`${styles.answerItem} ${answer.voIsAccepted ? styles.answerItemAccepted : ''}`}
                    >
                      {answer.voIsAccepted && (
                        <div className={styles.answerAcceptedBanner}>最佳答案</div>
                      )}
                      <div className={styles.answerMeta}>
                        <div className={styles.answerAuthorBlock}>
                          <button
                            type="button"
                            className={styles.answerAuthorButton}
                            onClick={() => onAuthorClick?.(answer.voAuthorId, answer.voAuthorName, answer.voAuthorAvatarUrl)}
                            title={`查看 ${answer.voAuthorName || '匿名用户'} 的主页`}
                          >
                            <span
                              className={styles.answerAvatar}
                              style={answer.voAuthorAvatarUrl?.trim() ? undefined : buildAvatarStyle(answer.voAuthorName || '匿名用户')}
                            >
                              {answer.voAuthorAvatarUrl?.trim() ? (
                                <img
                                  src={answer.voAuthorAvatarUrl}
                                  alt={answer.voAuthorName || '匿名用户'}
                                  className={styles.answerAvatarImage}
                                  loading="lazy"
                                />
                              ) : (
                                buildAvatarText(answer.voAuthorName || '匿名用户')
                              )}
                            </span>
                            <span className={styles.answerAuthor}>{answer.voAuthorName || '匿名用户'}</span>
                          </button>
                          <span className={styles.answerTime}>
                            {formatDateTimeByTimeZone(answer.voCreateTime, displayTimeZone, '未知时间')}
                          </span>
                        </div>
                        <div className={styles.answerActions}>
                          {answer.voIsAccepted && (
                            <span className={styles.acceptedBadge}>已采纳</span>
                          )}
                          {canAccept && (
                            <button
                              type="button"
                              className={styles.acceptButton}
                              onClick={() => {
                                void handleAcceptAnswer(answer.voAnswerId);
                              }}
                              disabled={acceptingAnswerId === answer.voAnswerId}
                            >
                              {acceptingAnswerId === answer.voAnswerId ? '采纳中...' : '采纳答案'}
                            </button>
                          )}
                        </div>
                      </div>

                      <Suspense fallback={<div className={styles.answerBody}>回答渲染中...</div>}>
                        <MarkdownRenderer
                          content={answer.voContent}
                          className={styles.answerBody}
                          stickerMap={stickerMap}
                        />
                      </Suspense>
                    </article>
                  );
                })}
              </div>
            ) : (
              <p className={styles.questionEmpty}>{questionEmptyText}</p>
            )}

            <div className={styles.answerComposer}>
              <label className={styles.answerLabel}>
                发表回答
              </label>
              <Suspense fallback={<div className={styles.answerEditorLoading}>回答编辑器加载中...</div>}>
                <MarkdownEditor
                  value={answerContent}
                  onChange={setAnswerContent}
                  placeholder={isAuthenticated ? '写下你的回答，支持 Markdown' : '登录后可提交回答'}
                  minHeight={180}
                  disabled={!canAnswerQuestion}
                  showToolbar={true}
                  theme="light"
                  className={styles.answerEditor}
                  onImageUpload={handleAnswerImageUpload}
                  onDocumentUpload={handleAnswerDocumentUpload}
                  stickerGroups={stickerGroups}
                  stickerMap={stickerMap}
                />
              </Suspense>
              <div className={styles.answerComposerFooter}>
                <span className={styles.answerHint}>
                  {question?.voIsSolved
                    ? '问题已解决，仍可继续补充更多方案'
                    : isAuthenticated
                      ? '提交后会立即出现在回答列表中'
                      : '请先登录后再回答'}
                </span>
                <button
                  type="button"
                  className={styles.answerSubmitButton}
                  onClick={() => {
                    void handleAnswerSubmit();
                  }}
                  disabled={!isAuthenticated || !answerContent.trim() || isSubmittingAnswer}
                >
                  {isSubmittingAnswer ? '提交中...' : '提交回答'}
                </button>
              </div>
            </div>
          </section>
        )}

        <div className={styles.actions}>
          <button
            type="button"
            onClick={() => onLike?.(post.voId)}
            className={`${styles.likeButton} ${isLiked ? styles.liked : ''}`}
            disabled={!isAuthenticated}
            title={!isAuthenticated ? '请先登录' : isLiked ? '取消点赞' : '点赞'}
          >
            <span className={styles.likeIcon}>{isLiked ? '❤️' : '🤍'}</span>
            <span className={styles.likeCount}>{post.voLikeCount || 0}</span>
          </button>
          <span className={styles.commentCount}>
            💬 {post.voCommentCount || 0} 条讨论
          </span>

          {showFollowAction && (
            <div className={styles.relationActions}>
              <span className={styles.relationInfo}>
                粉丝 {followStatus?.voFollowerCount ?? 0} · 关注 {followStatus?.voFollowingCount ?? 0}
              </span>
              <button
                type="button"
                className={`${styles.followButton} ${followStatus?.voIsFollowing ? styles.following : ''}`}
                onClick={() => onToggleFollow?.(post.voAuthorId, !!followStatus?.voIsFollowing)}
                disabled={followLoading}
                title={followStatus?.voIsFollowing ? '取消关注' : '关注作者'}
              >
                {followLoading ? '处理中...' : followStatus?.voIsFollowing ? '已关注' : '关注'}
              </button>
            </div>
          )}

          {onToggleReaction && (
            <ReactionBar
              targetType="Post"
              targetId={post.voId}
              items={postReactions}
              isLoggedIn={isAuthenticated}
              loading={reactionLoading}
              stickerGroups={stickerGroups}
              onToggle={onToggleReaction}
              onRequireLogin={onRequireReactionLogin}
            />
          )}

          {isAuthor && (
            <div className={styles.authorActions}>
              <button
                type="button"
                onClick={() => onEdit?.(post.voId)}
                className={styles.editButton}
                title="编辑帖子"
              >
                <Icon icon="mdi:pencil" size={18} />
                编辑
              </button>
              <button
                type="button"
                onClick={() => onDelete?.(post.voId)}
                className={styles.deleteButton}
                title="删除帖子"
              >
                <Icon icon="mdi:delete" size={18} />
                删除
              </button>
              {!isQuestionPost && (
                <button
                  type="button"
                  onClick={() => onViewHistory?.(post.voId)}
                  className={styles.historyButton}
                  title="查看编辑历史"
                >
                  <Icon icon="mdi:history" size={18} />
                  历史
                </button>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

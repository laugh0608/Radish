import { lazy, Suspense, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { PostDetail as PostDetailType, QuestionAnswerFilter, QuestionAnswerSort, ReactionSummaryVo } from '@/api/forum';
import { uploadDocument, uploadImage } from '@/api/attachment';
import type { UserFollowStatus } from '@/api/userFollow';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
import { Icon } from '@radish/ui/icon';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import { ReactionBar, type ReactionTogglePayload } from '@radish/ui/reaction-bar';
import type { StickerPickerGroup } from '@radish/ui/sticker-picker';
import { MarkdownRenderer, type MarkdownStickerMap } from '@radish/ui/markdown-renderer';
import styles from './PostDetail.module.css';

const MarkdownEditor = lazy(() =>
  import('@radish/ui/markdown-editor').then((module) => ({ default: module.MarkdownEditor }))
);

interface PostDetailProps {
  post: PostDetailType | null;
  loading?: boolean;
  displayTimeZone: string;
  mode?: 'interactive' | 'readOnly';
  isLiked?: boolean;
  onLike?: (postId: number) => void;
  onVotePoll?: (optionId: number) => Promise<void>;
  onClosePoll?: () => Promise<void>;
  onDrawLottery?: () => Promise<void>;
  onAnswerQuestion?: (content: string) => Promise<void>;
  onAcceptAnswer?: (answerId: number) => Promise<void>;
  answerSort?: QuestionAnswerSort;
  answerFilter?: QuestionAnswerFilter;
  onAnswerSortChange?: (sortBy: QuestionAnswerSort) => Promise<void>;
  onAnswerFilterChange?: (filterBy: QuestionAnswerFilter) => void;
  isAuthenticated?: boolean;
  currentUserId?: number;
  canToggleTop?: boolean;
  onToggleTop?: (isTop: boolean) => Promise<void>;
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
  onReport?: (postId: number) => void;
}

export const PostDetail = ({
  post,
  loading = false,
  displayTimeZone,
  mode = 'interactive',
  isLiked = false,
  onLike,
  onVotePoll,
  onClosePoll,
  onDrawLottery,
  onAnswerQuestion,
  onAcceptAnswer,
  answerSort = 'default',
  answerFilter = 'all',
  onAnswerSortChange,
  onAnswerFilterChange,
  isAuthenticated = false,
  currentUserId = 0,
  canToggleTop = false,
  onToggleTop,
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
  onReport,
}: PostDetailProps) => {
  const { t } = useTranslation();
  const isReadOnly = mode === 'readOnly';
  const anonymousUserLabel = t('forum.postCard.anonymousUser');
  const unknownTimeLabel = t('forum.postCard.unknownTime');
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
  const [isClosingPoll, setIsClosingPoll] = useState(false);
  const [isTogglingTop, setIsTogglingTop] = useState(false);

  const isAuthor = !!post && currentUserId > 0 && String(post.voAuthorId) === String(currentUserId);
  const showFollowAction = isAuthenticated && !!post && !isAuthor && post.voAuthorId > 0;
  const question = post?.voQuestion;
  const lottery = post?.voLottery;
  const isQuestionPost = !!post?.voIsQuestion;
  const isLotteryPost = !!post?.voHasLottery && !!lottery;
  const canAnswerQuestion = isAuthenticated && !isSubmittingAnswer;
  const canViewQuestionHistory = isQuestionPost && !!onViewHistory;
  const totalAnswerCount = question?.voAnswerCount ?? post?.voAnswerCount ?? 0;
  const drawTimeValue = lottery?.voDrawTime ? new Date(lottery.voDrawTime) : null;
  const isDrawTimeReached = !drawTimeValue || Number.isNaN(drawTimeValue.getTime()) || drawTimeValue.getTime() <= Date.now();
  const authorAvatarUrl = resolveMediaUrl(post?.voAuthorAvatarUrl);
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
      ? t('forum.postDetail.lottery.status.drawn')
      : isAuthor
        ? isDrawTimeReached
          ? t('forum.postDetail.lottery.status.canDraw')
          : t('forum.postDetail.lottery.status.waitTime')
        : t('forum.postDetail.lottery.status.waitAuthor');
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
    ? t('forum.postDetail.question.emptyAccepted')
    : t('forum.postDetail.question.emptyAll');
  const lotteryRules = [
    t('forum.postDetail.lottery.rule.comment'),
    t('forum.postDetail.lottery.rule.reply'),
    t('forum.postDetail.lottery.rule.unique'),
    t('forum.postDetail.lottery.rule.author'),
  ];

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

  useEffect(() => {
    setIsTogglingTop(false);
  }, [post?.voId, post?.voIsTop]);

  if (loading) {
    return (
      <div className={styles.container}>
        <h3 className={styles.title}>{t('forum.postDetail.title')}</h3>
        <p className={styles.loadingText}>{t('forum.postDetail.loading')}</p>
      </div>
    );
  }

  if (!post) {
    return (
      <div className={styles.container}>
        <h3 className={styles.title}>{t('forum.postDetail.title')}</h3>
        <p className={styles.emptyText}>{t('forum.postDetail.empty')}</p>
      </div>
    );
  }

  const poll = post.voPoll;
  const canSubmitPoll = !!poll && !poll.voIsClosed && !poll.voHasVoted && isAuthenticated;
  const canClosePoll = !!poll && !poll.voIsClosed && isAuthor && !!onClosePoll;
  const pollStatusText = !poll
    ? ''
    : poll.voIsClosed
      ? t('forum.postDetail.poll.status.closed')
      : poll.voHasVoted
        ? t('forum.postDetail.poll.status.voted')
        : isAuthenticated
          ? t('forum.postDetail.poll.status.ready')
          : t('forum.postDetail.poll.status.loginRequired');

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

  const handleClosePoll = async () => {
    if (!canClosePoll || !onClosePoll) {
      return;
    }

    setIsClosingPoll(true);
    try {
      await onClosePoll();
    } finally {
      setIsClosingPoll(false);
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

  const handleAnswerImageUpload = async (file: File): Promise<MarkdownImageUploadResult> => {
    const result = await uploadImage({
      file,
      businessType: 'Comment',
      generateThumbnail: true,
      generateMultipleSizes: false,
      removeExif: true
    }, t);

    return {
      attachmentId: result.voId,
      displayVariant: result.voThumbnailUrl ? 'thumbnail' : 'original',
      previewUrl: buildAttachmentAssetUrl(result.voId, result.voThumbnailUrl ? 'thumbnail' : 'original'),
    };
  };

  const handleAnswerDocumentUpload = async (file: File): Promise<MarkdownDocumentUploadResult> => {
    const result = await uploadDocument({
      file,
      businessType: 'Comment'
    }, t);

    return {
      attachmentId: result.voId,
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

  const handleToggleTop = async () => {
    if (!onToggleTop || !post) {
      return;
    }

    setIsTogglingTop(true);
    try {
      await onToggleTop(!post.voIsTop);
    } finally {
      setIsTogglingTop(false);
    }
  };

  return (
    <div className={styles.container}>
      <h3 className={styles.title}>{t('forum.postDetail.title')}</h3>
      <div className={styles.postContent}>
        <h4 className={styles.postTitle}>{post.voTitle}</h4>
        {isQuestionPost && (
          <div className={styles.statusRow}>
            <span className={`${styles.statusBadge} ${styles.questionBadge}`}>{t('forum.postDetail.questionBadge')}</span>
            <span className={`${styles.statusBadge} ${post.voIsSolved ? styles.solvedBadge : styles.pendingBadge}`}>
              {post.voIsSolved ? t('forum.postDetail.statusSolved') : t('forum.postDetail.statusPending')}
            </span>
            <span className={`${styles.statusBadge} ${styles.answerBadge}`}>
              {t('forum.postDetail.answerCount', { count: totalAnswerCount })}
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
              <span
                className={styles.authorAvatar}
                style={authorAvatarUrl ? undefined : buildAvatarStyle(post.voAuthorName)}
                aria-hidden="true"
              >
                {authorAvatarUrl ? (
                  <img
                    className={styles.authorAvatarImage}
                    src={authorAvatarUrl}
                    alt={post.voAuthorName}
                    loading="lazy"
                  />
                ) : (
                  buildAvatarText(post.voAuthorName)
                )}
              </span>
              <span>{t('forum.postDetail.author', { name: post.voAuthorName })}</span>
            </button>
          )}
          {post.voCreateTime && <span> · {formatDateTimeByTimeZone(post.voCreateTime, displayTimeZone)}</span>}
          {post.voViewCount !== undefined && <span> · {t('forum.postDetail.views', { count: post.voViewCount })}</span>}
        </div>
        <MarkdownRenderer content={post.voContent} className={styles.postBody} stickerMap={stickerMap} />
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
                  <span className={styles.pollBadge}>{t('forum.postDetail.poll.badge')}</span>
                  <h5 className={styles.pollQuestion}>{poll.voQuestion}</h5>
                </div>
                <p className={styles.pollMeta}>
                  {t('forum.postDetail.poll.meta', { count: poll.voTotalVoteCount })}
                  {poll.voEndTime
                    ? ` · ${t('forum.postDetail.poll.endsAt', { time: formatDateTimeByTimeZone(poll.voEndTime, displayTimeZone) })}`
                    : ` · ${t('forum.postDetail.poll.noDeadline')}`}
                </p>
              </div>
              <span className={`${styles.pollState} ${poll.voIsClosed ? styles.pollStateClosed : ''}`}>
                {poll.voIsClosed
                  ? t('forum.postDetail.poll.state.closed')
                  : poll.voHasVoted
                    ? t('forum.postDetail.poll.state.voted')
                    : t('forum.postDetail.poll.state.active')}
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
                        {t('forum.postDetail.poll.optionValue', {
                          count: option.voVoteCount,
                          percent: option.voVotePercent.toFixed(2)
                        })}
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
              {!isReadOnly && (
                <div className={styles.pollActionButtons}>
                  {canClosePoll && (
                    <button
                      type="button"
                      className={styles.pollCloseButton}
                      onClick={() => {
                        void handleClosePoll();
                      }}
                      disabled={isClosingPoll}
                    >
                      {isClosingPoll ? t('forum.postDetail.poll.closeLoading') : t('forum.postDetail.poll.close')}
                    </button>
                  )}
                  <button
                    type="button"
                    className={styles.pollSubmitButton}
                    onClick={handleVoteSubmit}
                    disabled={!canSubmitPoll || !selectedOptionId || isVoting}
                  >
                    {isVoting ? t('forum.postDetail.poll.submitLoading') : t('forum.postDetail.poll.submit')}
                  </button>
                </div>
              )}
            </div>
          </section>
        )}

        {lottery && (
          <section className={styles.lotteryCard}>
            <div className={styles.lotteryHeader}>
              <div>
                <div className={styles.lotteryTitleRow}>
                  <span className={styles.lotteryBadge}>{t('forum.postDetail.lottery.badge')}</span>
                  <h5 className={styles.lotteryTitle}>{lottery.voPrizeName}</h5>
                </div>
                <p className={styles.lotteryMeta}>
                  {t('forum.postDetail.lottery.participants', { count: lottery.voParticipantCount })}
                  {' · '}
                  {t('forum.postDetail.lottery.winners', { count: lottery.voWinnerCount })}
                  {lottery.voDrawTime
                    ? ` · ${t('forum.postDetail.lottery.drawTime', { time: formatDateTimeByTimeZone(lottery.voDrawTime, displayTimeZone) })}`
                    : ` · ${t('forum.postDetail.lottery.drawTimeUnset')}`}
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
              {lotteryRules.map(rule => (
                <span key={rule} className={styles.lotteryRule}>{rule}</span>
              ))}
            </div>

            <div className={styles.lotteryFooter}>
              <span className={styles.lotteryHint}>
                {lottery.voIsDrawn
                  ? lottery.voDrawnAt
                    ? t('forum.postDetail.lottery.hint.drawnAt', { time: formatDateTimeByTimeZone(lottery.voDrawnAt, displayTimeZone) })
                    : t('forum.postDetail.lottery.hint.drawnDone')
                  : isAuthor
                    ? isDrawTimeReached
                      ? t('forum.postDetail.lottery.hint.authorReady')
                      : t('forum.postDetail.lottery.hint.authorWaiting')
                    : t('forum.postDetail.lottery.hint.visitor')}
              </span>
              {!isReadOnly && canDrawLottery && (
                <button
                  type="button"
                  className={styles.lotteryDrawButton}
                  onClick={() => {
                    void handleDrawLottery();
                  }}
                  disabled={isDrawingLottery}
                >
                  {isDrawingLottery ? t('forum.postDetail.lottery.drawLoading') : t('forum.postDetail.lottery.drawNow')}
                </button>
              )}
            </div>

            {lottery.voWinners.length > 0 ? (
              <div className={styles.lotteryWinnerSection}>
                <div className={styles.lotteryWinnerHeader}>
                  <span className={styles.lotteryWinnerTitle}>{t('forum.postDetail.lottery.winnerTitle')}</span>
                  <span className={styles.lotteryWinnerCount}>{t('forum.postDetail.lottery.winnerCount', { count: lottery.voWinners.length })}</span>
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
                {lottery.voIsDrawn ? t('forum.postDetail.lottery.emptyDrawn') : t('forum.postDetail.lottery.emptyPending')}
              </p>
            )}
          </section>
        )}

        {isQuestionPost && (
          <section className={styles.questionCard}>
            <div className={styles.questionHeader}>
              <div>
                <div className={styles.questionTitleRow}>
                  <span className={styles.questionSectionBadge}>{t('forum.postDetail.question.badge')}</span>
                  <h5 className={styles.questionTitle}>{t('forum.postDetail.question.title')}</h5>
                </div>
                <p className={styles.questionMeta}>
                  {question?.voIsSolved
                    ? t('forum.postDetail.question.meta.solved')
                    : t('forum.postDetail.question.meta.pending')}
                  {' · '}
                  {t('forum.postDetail.question.totalAnswers', { count: totalAnswerCount })}
                </p>
                <div className={styles.answerToolbar}>
                  <div className={styles.answerToolbarGroup}>
                    <span className={styles.answerToolbarLabel}>{t('forum.postDetail.question.sort')}</span>
                    <div className={styles.answerSortButtons}>
                      <button
                        type="button"
                        className={`${styles.answerSortButton} ${answerSort === 'default' ? styles.answerSortButtonActive : ''}`}
                        onClick={() => {
                          void onAnswerSortChange?.('default');
                        }}
                      >
                        {t('forum.postDetail.question.sortDefault')}
                      </button>
                      <button
                        type="button"
                        className={`${styles.answerSortButton} ${answerSort === 'latest' ? styles.answerSortButtonActive : ''}`}
                        onClick={() => {
                          void onAnswerSortChange?.('latest');
                        }}
                      >
                        {t('forum.postDetail.question.sortLatest')}
                      </button>
                    </div>
                  </div>
                  <div className={styles.answerToolbarGroup}>
                    <span className={styles.answerToolbarLabel}>{t('forum.postDetail.question.filter')}</span>
                    <div className={styles.answerSortButtons}>
                      <button
                        type="button"
                        className={`${styles.answerSortButton} ${answerFilter === 'all' ? styles.answerSortButtonActive : ''}`}
                        onClick={() => onAnswerFilterChange?.('all')}
                      >
                        {t('forum.postDetail.question.filterAll')}
                      </button>
                      <button
                        type="button"
                        className={`${styles.answerSortButton} ${answerFilter === 'accepted' ? styles.answerSortButtonActive : ''}`}
                        onClick={() => onAnswerFilterChange?.('accepted')}
                      >
                        {t('forum.postDetail.question.filterAccepted')}
                      </button>
                    </div>
                  </div>
                </div>
              </div>
              <div className={styles.questionHeaderActions}>
                <span className={`${styles.questionState} ${question?.voIsSolved ? styles.questionStateSolved : styles.questionStatePending}`}>
                  {question?.voIsSolved ? t('forum.postDetail.statusSolved') : t('forum.postDetail.statusPending')}
                </span>
                {canViewQuestionHistory && (
                  <button
                    type="button"
                    onClick={() => onViewHistory?.(post.voId)}
                    className={styles.historyButton}
                    title={t('forum.postDetail.question.history')}
                  >
                    <Icon icon="mdi:history" size={18} />
                    {t('forum.postDetail.question.history')}
                  </button>
                )}
              </div>
            </div>

            {displayedAnswers.length > 0 ? (
              <div className={styles.answerList}>
                {displayedAnswers.map((answer) => {
                  const answerAuthorName = answer.voAuthorName || anonymousUserLabel;
                  const answerAvatarUrl = resolveMediaUrl(answer.voAuthorAvatarUrl);
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
                        <div className={styles.answerAcceptedBanner}>{t('forum.postDetail.question.bestAnswer')}</div>
                      )}
                      <div className={styles.answerMeta}>
                        <div className={styles.answerAuthorBlock}>
                          <button
                            type="button"
                            className={styles.answerAuthorButton}
                            onClick={() => onAuthorClick?.(answer.voAuthorId, answer.voAuthorName, answer.voAuthorAvatarUrl)}
                            title={t('forum.postDetail.answerProfileTitle', { name: answerAuthorName })}
                          >
                            <span
                              className={styles.answerAvatar}
                              style={answer.voAuthorAvatarUrl?.trim() ? undefined : buildAvatarStyle(answerAuthorName)}
                            >
                              {answerAvatarUrl ? (
                                <img
                                  src={answerAvatarUrl}
                                  alt={answerAuthorName}
                                  className={styles.answerAvatarImage}
                                  loading="lazy"
                                />
                              ) : (
                                buildAvatarText(answerAuthorName)
                              )}
                            </span>
                            <span className={styles.answerAuthor}>{answerAuthorName}</span>
                          </button>
                          <span className={styles.answerTime}>
                            {formatDateTimeByTimeZone(answer.voCreateTime, displayTimeZone, unknownTimeLabel)}
                          </span>
                        </div>
                        <div className={styles.answerActions}>
                          {answer.voIsAccepted && (
                            <span className={styles.acceptedBadge}>{t('forum.postDetail.question.accepted')}</span>
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
                              {acceptingAnswerId === answer.voAnswerId
                                ? t('forum.postDetail.question.acceptLoading')
                                : t('forum.postDetail.question.accept')}
                            </button>
                          )}
                        </div>
                      </div>

                      <MarkdownRenderer
                        content={answer.voContent}
                        className={styles.answerBody}
                        stickerMap={stickerMap}
                      />
                    </article>
                  );
                })}
              </div>
            ) : (
              <p className={styles.questionEmpty}>{questionEmptyText}</p>
            )}

            {!isReadOnly && (
              <div className={styles.answerComposer}>
                <label className={styles.answerLabel}>
                  {t('forum.postDetail.question.composeLabel')}
                </label>
                <Suspense fallback={<div className={styles.answerEditorLoading}>{t('forum.postDetail.question.editorLoading')}</div>}>
                  <MarkdownEditor
                    value={answerContent}
                    onChange={setAnswerContent}
                    placeholder={isAuthenticated
                      ? t('forum.postDetail.question.placeholderLoggedIn')
                      : t('forum.postDetail.question.placeholderLoggedOut')}
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
                      ? t('forum.postDetail.question.hintSolved')
                      : isAuthenticated
                        ? t('forum.postDetail.question.hintLoggedIn')
                        : t('forum.postDetail.question.hintLoggedOut')}
                  </span>
                  <button
                    type="button"
                    className={styles.answerSubmitButton}
                    onClick={() => {
                      void handleAnswerSubmit();
                    }}
                    disabled={!isAuthenticated || !answerContent.trim() || isSubmittingAnswer}
                  >
                    {isSubmittingAnswer ? t('forum.postDetail.question.submitLoading') : t('forum.postDetail.question.submit')}
                  </button>
                </div>
              </div>
            )}
          </section>
        )}

        <div className={styles.actions}>
          {!isReadOnly ? (
            <button
              type="button"
              onClick={() => onLike?.(post.voId)}
              className={`${styles.likeButton} ${isLiked ? styles.liked : ''}`}
              disabled={!isAuthenticated}
              title={!isAuthenticated
                ? t('forum.postDetail.like.login')
                : isLiked
                  ? t('forum.postDetail.like.unlike')
                  : t('forum.postDetail.like.like')}
            >
              <span className={styles.likeIcon}>{isLiked ? '❤️' : '🤍'}</span>
              <span className={styles.likeCount}>{post.voLikeCount || 0}</span>
            </button>
          ) : (
            <span className={styles.commentCount}>
              ❤️ {post.voLikeCount || 0}
            </span>
          )}
          <span className={styles.commentCount}>
            💬 {t('forum.postDetail.commentCount', { count: post.voCommentCount || 0 })}
          </span>

          {!isReadOnly && !!onReport && !isAuthor && (
            <button
              type="button"
              onClick={() => onReport(post.voId)}
              className={styles.reportButton}
              title={t('report.action')}
            >
              <Icon icon="mdi:flag-outline" size={18} />
              {t('report.action')}
            </button>
          )}

          {!isReadOnly && showFollowAction && (
            <div className={styles.relationActions}>
              <span className={styles.relationInfo}>
                {t('forum.postDetail.followStats', {
                  followers: followStatus?.voFollowerCount ?? 0,
                  following: followStatus?.voFollowingCount ?? 0
                })}
              </span>
              <button
                type="button"
                className={`${styles.followButton} ${followStatus?.voIsFollowing ? styles.following : ''}`}
                onClick={() => onToggleFollow?.(post.voAuthorId, !!followStatus?.voIsFollowing)}
                disabled={followLoading}
                title={followStatus?.voIsFollowing
                  ? t('forum.postDetail.follow.unfollowTitle')
                  : t('forum.postDetail.follow.followTitle')}
              >
                {followLoading
                  ? t('forum.postDetail.follow.loading')
                  : followStatus?.voIsFollowing
                    ? t('forum.postDetail.follow.following')
                    : t('forum.postDetail.follow.follow')}
              </button>
            </div>
          )}

          {!isReadOnly && onToggleReaction && (
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

          {!isReadOnly && (canToggleTop || isAuthor) && (
            <div className={styles.authorActions}>
              {canToggleTop && (
                <button
                  type="button"
                  onClick={() => {
                    void handleToggleTop();
                  }}
                  className={styles.historyButton}
                  title={post.voIsTop ? t('forum.postDetail.action.untopTitle') : t('forum.postDetail.action.topTitle')}
                  disabled={isTogglingTop}
                >
                  <Icon icon={post.voIsTop ? 'mdi:pin-off-outline' : 'mdi:pin-outline'} size={18} />
                  {isTogglingTop
                    ? t('forum.postDetail.action.topping')
                    : post.voIsTop
                      ? t('forum.postDetail.action.untop')
                      : t('forum.postDetail.action.top')}
                </button>
              )}

              {isAuthor && (
                <>
                  <button
                    type="button"
                    onClick={() => onEdit?.(post.voId)}
                    className={styles.editButton}
                    title={t('forum.postDetail.action.editTitle')}
                  >
                    <Icon icon="mdi:pencil" size={18} />
                    {t('forum.postDetail.action.edit')}
                  </button>
                  <button
                    type="button"
                    onClick={() => onDelete?.(post.voId)}
                    className={styles.deleteButton}
                    title={t('forum.postDetail.action.deleteTitle')}
                  >
                    <Icon icon="mdi:delete" size={18} />
                    {t('forum.postDetail.action.delete')}
                  </button>
                  {!isQuestionPost && (
                    <button
                      type="button"
                      onClick={() => onViewHistory?.(post.voId)}
                      className={styles.historyButton}
                      title={t('forum.postDetail.action.historyTitle')}
                    >
                      <Icon icon="mdi:history" size={18} />
                      {t('forum.postDetail.action.history')}
                    </button>
                  )}
                </>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

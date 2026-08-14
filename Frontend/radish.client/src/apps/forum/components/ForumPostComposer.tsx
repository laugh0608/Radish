import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import type { UserMentionOption as UiUserMentionOption } from '@radish/ui';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import { log } from '@/utils/logger';
import {
  getAllTags,
  type Category,
  type CreateLotteryRequest,
  type CreatePollRequest
} from '@/api/forum';
import { searchUsersForMention } from '@/api/user';
import { uploadDocument, uploadImage } from '@/api/attachment';
import { createMarkdownEditorLabels } from '@/i18n/markdownEditorLabels';
import { redirectToLogin } from '@/services/auth';
import { buildDesktopForumReturnPath } from '@/services/authReturnPath';
import { useUserStore } from '@/stores/userStore';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import type { LongId } from '@/api/user';
import { useStickerCatalog } from '../hooks/useStickerCatalog';
import {
  hasMeaningfulForumPostDraft,
  loadForumPostDraft,
  removeForumPostDraft,
  saveForumPostDraft,
  type ForumPostDraft,
} from '../utils/forumPostDraftStorage';
import { resolveForumPublishErrorMessage } from '../utils/forumPublishPresentation';
import { RichTextMarkdownEditor } from './RichTextMarkdownEditor';
import styles from './PublishPostModal.module.css';

export interface ForumPostComposerProps {
  isOpen: boolean;
  surface?: 'sheet' | 'page';
  isAuthenticated: boolean;
  categories: Category[];
  selectedCategoryId: LongId | null;
  loginReturnPath?: string | null;
  onClose: () => void;
  onBusyChange?: (busy: boolean) => void;
  onPublish: (
    title: string,
    content: string,
    categoryId: LongId,
    tagNames: string[],
    isQuestion?: boolean,
    poll?: CreatePollRequest | null,
    lottery?: CreateLotteryRequest | null
  ) => Promise<void>;
}

interface CategorySelectionSnapshot {
  id: LongId;
  name: string;
}

type PublishBlockingIssueCode = 'title' | 'content' | 'category' | 'tag' | 'tag-input-pending';

interface PublishBlockingIssue {
  code: PublishBlockingIssueCode;
  label: string;
  message: string;
}

const MIN_TAG_COUNT = 1;
const MAX_TAG_COUNT = 5;
const MIN_POLL_OPTION_COUNT = 2;
const MAX_POLL_OPTION_COUNT = 6;
const MIN_LOTTERY_WINNER_COUNT = 1;
const MAX_LOTTERY_WINNER_COUNT = 20;
const MIN_LOTTERY_LEAD_TIME_MS = 60 * 60 * 1000;
const IMAGE_SCALE_OPTIONS = [30, 50, 70, 75, 100] as const;
const DEFAULT_POLL_OPTIONS = ['', ''];

const MarkdownEditor = lazy(() =>
  import('@radish/ui/markdown-editor').then((module) => ({ default: module.MarkdownEditor }))
);

function appendRecoveryHint(message: string, hint: string): string {
  const trimmedMessage = message.trim();
  const trimmedHint = hint.trim();

  if (/[。.!！?？]$/.test(trimmedMessage)) {
    return `${trimmedMessage} ${trimmedHint}`;
  }

  const separator = /^[\u4e00-\u9fff]/.test(trimmedHint) ? '。' : '. ';
  return `${trimmedMessage}${separator}${trimmedHint}`;
}

const findCategorySnapshot = (
  categories: Category[],
  targetCategoryId: LongId | null | undefined
): CategorySelectionSnapshot | null => {
  if (!targetCategoryId) {
    return null;
  }

  const category = categories.find((item) => item.voId === targetCategoryId);
  if (!category) {
    return null;
  }

  return {
    id: category.voId,
    name: category.voName
  };
};

export const ForumPostComposer = ({
  isOpen,
  surface = 'sheet',
  isAuthenticated,
  categories,
  selectedCategoryId,
  loginReturnPath,
  onClose,
  onBusyChange,
  onPublish
}: ForumPostComposerProps) => {
  const titleInputRef = useRef<HTMLInputElement>(null);
  const categorySelectRef = useRef<HTMLSelectElement>(null);
  const tagInputRef = useRef<HTMLInputElement>(null);
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [composerMode, setComposerMode] = useState<'markdown' | 'rich'>('markdown');
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isSettingsOpen, setIsSettingsOpen] = useState(
    () => (typeof window === 'undefined' ? true : window.innerWidth >= 1200)
  );
  const [addWatermark, setAddWatermark] = useState(false);
  const [watermarkText, setWatermarkText] = useState('Radish');
  const [generateMultipleSizes, setGenerateMultipleSizes] = useState(false);
  const [imageScalePercent, setImageScalePercent] = useState<number>(75);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isEditorUploading, setIsEditorUploading] = useState(false);
  const [categoryId, setCategoryId] = useState<LongId | null>(selectedCategoryId);
  const [selectedCategorySnapshot, setSelectedCategorySnapshot] = useState<CategorySelectionSnapshot | null>(
    () => findCategorySnapshot(categories, selectedCategoryId)
  );
  const [allTagNames, setAllTagNames] = useState<string[]>([]);
  const [tagInput, setTagInput] = useState('');
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [tagError, setTagError] = useState<string | null>(null);
  const [categoryError, setCategoryError] = useState<string | null>(null);
  const [showBlockingIssues, setShowBlockingIssues] = useState(false);
  const [enablePoll, setEnablePoll] = useState(false);
  const [isQuestionPost, setIsQuestionPost] = useState(false);
  const [pollQuestion, setPollQuestion] = useState('');
  const [pollEndTime, setPollEndTime] = useState('');
  const [pollOptions, setPollOptions] = useState<string[]>([...DEFAULT_POLL_OPTIONS]);
  const [pollError, setPollError] = useState<string | null>(null);
  const [enableLottery, setEnableLottery] = useState(false);
  const [lotteryPrizeName, setLotteryPrizeName] = useState('');
  const [lotteryPrizeDescription, setLotteryPrizeDescription] = useState('');
  const [lotteryDrawTime, setLotteryDrawTime] = useState('');
  const [lotteryWinnerCount, setLotteryWinnerCount] = useState('1');
  const [lotteryError, setLotteryError] = useState<string | null>(null);
  const [publishError, setPublishError] = useState<string | null>(null);
  const [draftOwnerUserId, setDraftOwnerUserId] = useState('');

  const roles = useUserStore((state) => state.roles || []);
  const userId = useUserStore((state) => state.userId);
  const isAdmin = roles.some((role) => {
    const normalized = role.trim().toLowerCase();
    return normalized === 'admin' || normalized === 'system';
  });
  const { t, i18n } = useTranslation();
  const { stickerGroups, stickerMap, handleStickerSelect } = useStickerCatalog();
  const markdownEditorLabels = useMemo(
    () => createMarkdownEditorLabels(t, i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage, t],
  );
  const richTextEditorLabels = useMemo(() => ({
    ...markdownEditorLabels,
    placeholder: t('markdownEditor.richText.placeholder'),
    linkPrompt: t('markdownEditor.richText.linkPrompt'),
    imageUnavailable: t('markdownEditor.richText.imageUnavailable'),
  }), [markdownEditorLabels, t]);

  const handleSearchUsers = useCallback(async (keyword: string): Promise<UiUserMentionOption[]> => {
    try {
      const users = await searchUsersForMention(keyword, t);
      return users.map((user) => ({
        id: user.voId,
        userName: resolveVisibleUserHandle(user, resolveVisibleUserDisplayName(user, t('common.unknownUser')))
          || resolveVisibleUserDisplayName(user, t('common.unknownUser')),
        displayName: resolveVisibleUserDisplayName(user, t('common.unknownUser')),
        avatar: user.voAvatar
      }));
    } catch (error) {
      log.error(t('forum.mention.searchFailed'), error);
      throw error;
    }
  }, [t]);

  const applyCategorySelection = useCallback((nextCategoryId: LongId | null, snapshot?: CategorySelectionSnapshot | null) => {
    setCategoryId(nextCategoryId);
    setSelectedCategorySnapshot(snapshot ?? findCategorySnapshot(categories, nextCategoryId));
  }, [categories]);

  useEffect(() => {
    if (!isOpen) {
      setShowBlockingIssues(false);
      setTagError(null);
      setCategoryError(null);
      setPublishError(null);
      return;
    }

    setDraftOwnerUserId('');
    setTitle('');
    setContent('');
    setSelectedTags([]);
    setTagInput('');
    setComposerMode('markdown');
    setIsQuestionPost(false);
    setEnablePoll(false);
    setPollQuestion('');
    setPollEndTime('');
    setPollOptions([...DEFAULT_POLL_OPTIONS]);
    setEnableLottery(false);
    setLotteryPrizeName('');
    setLotteryPrizeDescription('');
    setLotteryDrawTime('');
    setLotteryWinnerCount('1');
    setPollError(null);
    setLotteryError(null);
    applyCategorySelection(selectedCategoryId);

    const normalizedUserId = userId.trim();
    if (!isAuthenticated || !normalizedUserId) {
      return;
    }

    try {
      const draft = loadForumPostDraft(normalizedUserId);
      if (!draft) {
        setDraftOwnerUserId(normalizedUserId);
        return;
      }

      if (hasMeaningfulForumPostDraft(draft) || typeof draft.categoryId === 'string') {
        const draftIsQuestion = Boolean(draft.isQuestion);
        const draftCategoryId = typeof draft.categoryId === 'string' && draft.categoryId.trim()
          ? draft.categoryId
          : selectedCategoryId;
        const draftCategorySnapshot = findCategorySnapshot(categories, draftCategoryId) ?? (
          draftCategoryId && draft.categoryName
            ? {
                id: draftCategoryId,
                name: draft.categoryName
              }
            : null
        );
        setTitle(draft.title || '');
        setContent(draft.content || '');
        setSelectedTags(Array.isArray(draft.tags) ? draft.tags : []);
        applyCategorySelection(draftCategoryId, draftCategorySnapshot);
        setComposerMode(draft.composerMode === 'rich' ? 'rich' : 'markdown');
        setIsQuestionPost(draftIsQuestion);
        const draftLotteryEnabled = Boolean(draft.lottery?.enabled) && !draftIsQuestion;
        setEnableLottery(draftLotteryEnabled);
        setEnablePoll(Boolean(draft.poll?.enabled) && !draftIsQuestion && !draftLotteryEnabled);
        setPollQuestion(draft.poll?.question || '');
        setPollEndTime(draft.poll?.endTime || '');
        setPollOptions(
          Array.isArray(draft.poll?.options) && draft.poll.options.length >= MIN_POLL_OPTION_COUNT
            ? draft.poll.options
            : [...DEFAULT_POLL_OPTIONS]
        );
        setLotteryPrizeName(draft.lottery?.prizeName || '');
        setLotteryPrizeDescription(draft.lottery?.prizeDescription || '');
        setLotteryDrawTime(draft.lottery?.drawTime || '');
        setLotteryWinnerCount(draft.lottery?.winnerCount || '1');
      }
      setDraftOwnerUserId(normalizedUserId);
    } catch (error) {
      log.error('ForumPostComposer', 'Failed to load account-scoped draft:', error);
      setDraftOwnerUserId(normalizedUserId);
    }
  }, [applyCategorySelection, categories, isAuthenticated, isOpen, selectedCategoryId, userId]);

  useEffect(() => {
    if (!isOpen || !categoryId) {
      return;
    }

    const matchedSnapshot = findCategorySnapshot(categories, categoryId);
    if (matchedSnapshot && matchedSnapshot.name !== selectedCategorySnapshot?.name) {
      setSelectedCategorySnapshot(matchedSnapshot);
    }
  }, [categories, categoryId, isOpen, selectedCategorySnapshot]);

  useEffect(() => {
    const normalizedUserId = userId.trim();
    if (!isOpen || !isAuthenticated || !normalizedUserId || draftOwnerUserId !== normalizedUserId) {
      return;
    }

    try {
      const draft: ForumPostDraft = {
          title,
          content,
          tags: selectedTags,
          categoryId,
          categoryName: selectedCategorySnapshot?.name ?? null,
          composerMode,
          isQuestion: isQuestionPost,
          poll: {
            enabled: enablePoll,
            question: pollQuestion,
            endTime: pollEndTime,
            options: pollOptions
          },
          lottery: {
            enabled: enableLottery,
            prizeName: lotteryPrizeName,
            prizeDescription: lotteryPrizeDescription,
            drawTime: lotteryDrawTime,
            winnerCount: lotteryWinnerCount
          }
      };
      if (hasMeaningfulForumPostDraft(draft)) {
        saveForumPostDraft(normalizedUserId, draft);
      } else {
        removeForumPostDraft(normalizedUserId);
      }
    } catch (error) {
      log.error('ForumPostComposer', 'Failed to save account-scoped draft:', error);
    }
  }, [
    isOpen,
    isAuthenticated,
    userId,
    draftOwnerUserId,
    title,
    content,
    selectedTags,
    categoryId,
    selectedCategorySnapshot,
    composerMode,
    isQuestionPost,
    enablePoll,
    pollQuestion,
    pollEndTime,
    pollOptions,
    enableLottery,
    lotteryPrizeName,
    lotteryPrizeDescription,
    lotteryDrawTime,
    lotteryWinnerCount
  ]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const loadTags = async () => {
      try {
        const tags = await getAllTags(t);
        setAllTagNames(tags.map((tag) => tag.voName));
      } catch (error) {
        log.warn('ForumPostComposer', 'Failed to load tag options', error);
        setAllTagNames([]);
      }
    };

    void loadTags();
  }, [isOpen, t]);

  const normalizeTagName = useCallback((name: string) => name.trim(), []);

  const resolveExactMatchedTag = useCallback((rawTagName: string) => {
    const tagName = normalizeTagName(rawTagName);
    if (!tagName) {
      return null;
    }

    return allTagNames.find((name) => name.toLowerCase() === tagName.toLowerCase()) ?? null;
  }, [allTagNames, normalizeTagName]);

  const addTag = (rawTagName: string) => {
    const tagName = normalizeTagName(rawTagName);
    if (!tagName) {
      return;
    }

    if (selectedTags.some((tag) => tag.toLowerCase() === tagName.toLowerCase())) {
      setTagInput('');
      return;
    }

    if (selectedTags.length >= MAX_TAG_COUNT) {
      setTagError(t('forum.composer.validation.tagMaximum', { count: MAX_TAG_COUNT }));
      return;
    }

    const exists = allTagNames.some((name) => name.toLowerCase() === tagName.toLowerCase());
    if (!exists && !isAdmin) {
      setTagError(t('forum.composer.validation.tagCreateForbidden'));
      return;
    }

    setSelectedTags((prev) => [...prev, tagName]);
    setTagInput('');
    setTagError(null);
  };

  const tryAutoCommitExactMatchedTag = useCallback((rawTagName: string) => {
    const exactMatchedTag = resolveExactMatchedTag(rawTagName);
    if (!exactMatchedTag) {
      return null;
    }

    if (selectedTags.some((tag) => tag.toLowerCase() === exactMatchedTag.toLowerCase())) {
      setTagInput('');
      return null;
    }

    if (selectedTags.length >= MAX_TAG_COUNT) {
      return null;
    }

    const nextSelectedTags = [...selectedTags, exactMatchedTag];
    setSelectedTags(nextSelectedTags);
    setTagInput('');
    setTagError(null);
    return nextSelectedTags;
  }, [resolveExactMatchedTag, selectedTags]);

  const removeTag = (tagName: string) => {
    setSelectedTags((prev) => prev.filter((tag) => tag !== tagName));
    setTagError(null);
  };

  const matchedTags = tagInput.trim()
    ? allTagNames
        .filter(
          (name) =>
            name.toLowerCase().includes(tagInput.trim().toLowerCase()) &&
            !selectedTags.some((selected) => selected.toLowerCase() === name.toLowerCase())
        )
        .slice(0, 8)
    : [];

  const getPublishBlockingIssues = useCallback((
    nextSelectedTags: string[] = selectedTags,
    nextTagInput: string = tagInput
  ): PublishBlockingIssue[] => {
    const issues: PublishBlockingIssue[] = [];
    const normalizedPendingTagInput = normalizeTagName(nextTagInput);

    if (!title.trim()) {
      issues.push({
        code: 'title',
        label: t('forum.composer.field.title'),
        message: t('forum.composer.validation.titleRequired')
      });
    }

    if (!content.trim()) {
      issues.push({
        code: 'content',
        label: t('forum.composer.field.content'),
        message: t('forum.composer.validation.contentRequired')
      });
    }

    if (!categoryId) {
      issues.push({
        code: 'category',
        label: t('forum.composer.field.category'),
        message: t('forum.composer.validation.categoryRequired')
      });
    }

    if (normalizedPendingTagInput) {
      issues.push({
        code: 'tag-input-pending',
        label: t('forum.composer.validation.pendingTagLabel'),
        message: t('forum.composer.validation.pendingTag', { tag: normalizedPendingTagInput })
      });
    }

    if (nextSelectedTags.length < MIN_TAG_COUNT && !normalizedPendingTagInput) {
      issues.push({
        code: 'tag',
        label: t('forum.composer.field.tags'),
        message: t('forum.composer.validation.tagMinimum', { count: MIN_TAG_COUNT })
      });
    }

    if (nextSelectedTags.length > MAX_TAG_COUNT) {
      issues.push({
        code: 'tag',
        label: t('forum.composer.validation.tooManyTagsLabel'),
        message: t('forum.composer.validation.tagMaximum', { count: MAX_TAG_COUNT })
      });
    }

    return issues;
  }, [categoryId, content, normalizeTagName, selectedTags, tagInput, t, title]);

  const handleQuestionMode = (next: boolean) => {
    setIsQuestionPost(next);
    if (next) {
      setEnablePoll(false);
      setPollQuestion('');
      setPollEndTime('');
      setPollOptions([...DEFAULT_POLL_OPTIONS]);
      setPollError(null);
      setEnableLottery(false);
      setLotteryPrizeName('');
      setLotteryPrizeDescription('');
      setLotteryDrawTime('');
      setLotteryWinnerCount('1');
      setLotteryError(null);
    }
  };

  const handleTogglePoll = () => {
    setEnablePoll((current) => {
      const next = !current;
      if (next) {
        setIsQuestionPost(false);
        setEnableLottery(false);
        setLotteryPrizeName('');
        setLotteryPrizeDescription('');
        setLotteryDrawTime('');
        setLotteryWinnerCount('1');
        setLotteryError(null);
      } else {
        setPollQuestion('');
        setPollEndTime('');
        setPollOptions([...DEFAULT_POLL_OPTIONS]);
      }

      return next;
    });
    setPollError(null);
  };

  const handleToggleLottery = () => {
    setEnableLottery((current) => {
      const next = !current;
      if (next) {
        setIsQuestionPost(false);
        setEnablePoll(false);
        setPollQuestion('');
        setPollEndTime('');
        setPollOptions([...DEFAULT_POLL_OPTIONS]);
        setPollError(null);
      } else {
        setLotteryPrizeName('');
        setLotteryPrizeDescription('');
        setLotteryDrawTime('');
        setLotteryWinnerCount('1');
      }

      return next;
    });
    setLotteryError(null);
  };

  const focusFirstBlockingIssue = useCallback((issues: PublishBlockingIssue[]) => {
    const firstIssue = issues[0];
    if (!firstIssue) {
      return;
    }

    if (firstIssue.code === 'title') {
      titleInputRef.current?.focus();
      return;
    }

    if (firstIssue.code === 'category') {
      window.requestAnimationFrame(() => {
        categorySelectRef.current?.focus();
      });
      return;
    }

    if (firstIssue.code === 'tag' || firstIssue.code === 'tag-input-pending') {
      window.requestAnimationFrame(() => {
        tagInputRef.current?.focus();
      });
    }
  }, []);

  const showBlockedPublishFeedback = useCallback((issues: PublishBlockingIssue[]) => {
    if (issues.length === 0) {
      return;
    }

    setShowBlockingIssues(true);
    setIsSettingsOpen(true);

    const categoryIssue = issues.find((issue) => issue.code === 'category');
    const tagIssue = issues.find((issue) => issue.code === 'tag' || issue.code === 'tag-input-pending');

    setCategoryError(categoryIssue?.message ?? null);
    setTagError(tagIssue?.message ?? null);

    const pendingTagIssue = issues.find((issue) => issue.code === 'tag-input-pending');
    if (pendingTagIssue) {
      toast.info(pendingTagIssue.message);
    } else {
      toast.info(t('forum.composer.validation.missingSummary', {
        fields: issues.map((issue) => issue.label).join(t('forum.composer.validation.fieldSeparator')),
      }));
    }

    focusFirstBlockingIssue(issues);
  }, [focusFirstBlockingIssue, t]);

  const handleSubmit = async (resolvedSelectedTags: string[]) => {
    let pollRequest: CreatePollRequest | null = null;
    let lotteryRequest: CreateLotteryRequest | null = null;

    if (enablePoll) {
      const normalizedQuestion = pollQuestion.trim();
      if (!normalizedQuestion) {
        setPollError(t('forum.composer.validation.pollQuestionRequired'));
        return;
      }

      const normalizedOptions = pollOptions.map((option) => option.trim()).filter(Boolean);
      if (normalizedOptions.length < MIN_POLL_OPTION_COUNT || normalizedOptions.length > MAX_POLL_OPTION_COUNT) {
        setPollError(t('forum.composer.validation.pollOptionCount', {
          min: MIN_POLL_OPTION_COUNT,
          max: MAX_POLL_OPTION_COUNT,
        }));
        return;
      }

      const uniqueOptions = new Set(normalizedOptions.map((option) => option.toLowerCase()));
      if (uniqueOptions.size !== normalizedOptions.length) {
        setPollError(t('forum.composer.validation.pollOptionsDuplicate'));
        return;
      }

      if (pollEndTime) {
        const endTime = new Date(pollEndTime);
        if (Number.isNaN(endTime.getTime()) || endTime.getTime() <= Date.now()) {
          setPollError(t('forum.composer.validation.pollEndTime'));
          return;
        }
      }

      pollRequest = {
        question: normalizedQuestion,
        endTime: pollEndTime ? new Date(pollEndTime).toISOString() : null,
        options: normalizedOptions.map((optionText, index) => ({
          optionText,
          sortOrder: index + 1
        }))
      };
    }

    if (enableLottery) {
      const normalizedPrizeName = lotteryPrizeName.trim();
      const normalizedPrizeDescription = lotteryPrizeDescription.trim();
      const parsedWinnerCount = Number.parseInt(lotteryWinnerCount, 10);

      if (!normalizedPrizeName) {
        setLotteryError(t('forum.composer.validation.lotteryPrizeRequired'));
        return;
      }

      if (!normalizedPrizeDescription) {
        setLotteryError(t('forum.composer.validation.lotteryDescriptionRequired'));
        return;
      }

      if (!lotteryDrawTime) {
        setLotteryError(t('forum.composer.validation.lotteryDrawTimeRequired'));
        return;
      }

      const drawTime = new Date(lotteryDrawTime);
      if (Number.isNaN(drawTime.getTime()) || drawTime.getTime() < Date.now() + MIN_LOTTERY_LEAD_TIME_MS) {
        setLotteryError(t('forum.composer.validation.lotteryDrawTimeMinimum'));
        return;
      }

      if (
        !Number.isInteger(parsedWinnerCount) ||
        parsedWinnerCount < MIN_LOTTERY_WINNER_COUNT ||
        parsedWinnerCount > MAX_LOTTERY_WINNER_COUNT
      ) {
        setLotteryError(t('forum.composer.validation.lotteryWinnerCount', {
          min: MIN_LOTTERY_WINNER_COUNT,
          max: MAX_LOTTERY_WINNER_COUNT,
        }));
        return;
      }

      lotteryRequest = {
        prizeName: normalizedPrizeName,
        prizeDescription: normalizedPrizeDescription,
        drawTime: drawTime.toISOString(),
        winnerCount: parsedWinnerCount
      };
    }

    setIsSubmitting(true);
    setPublishError(null);
    try {
      await onPublish(title.trim(), content.trim(), categoryId!, resolvedSelectedTags, isQuestionPost, pollRequest, lotteryRequest);
      setTitle('');
      setContent('');
      setSelectedTags([]);
      setTagInput('');
      applyCategorySelection(selectedCategoryId);
      setTagError(null);
      setCategoryError(null);
      setShowBlockingIssues(false);
      setComposerMode('markdown');
      setIsQuestionPost(false);
      setEnablePoll(false);
      setPollQuestion('');
      setPollEndTime('');
      setPollOptions([...DEFAULT_POLL_OPTIONS]);
      setPollError(null);
      setEnableLottery(false);
      setLotteryPrizeName('');
      setLotteryPrizeDescription('');
      setLotteryDrawTime('');
      setLotteryWinnerCount('1');
      setLotteryError(null);
      removeForumPostDraft(userId);
      onClose();
    } catch (error) {
      const errorMessage = appendRecoveryHint(
        resolveForumPublishErrorMessage(error, t, t('forum.publishFailed')),
        t('forum.publishDraftRetainedHint')
      );
      setPublishError(errorMessage);
      toast.error(t('forum.publishDraftRetainedToast'));
      log.error('ForumPostComposer', 'Post publish failed', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handlePublishAttempt = async () => {
    if (isSubmitting || isEditorUploading) {
      return;
    }

    let resolvedSelectedTags = selectedTags;
    let pendingTagInput = tagInput;
    const exactMatchedPendingTag = resolveExactMatchedTag(tagInput);
    const autoCommittedTags = tryAutoCommitExactMatchedTag(tagInput);
    if (autoCommittedTags) {
      resolvedSelectedTags = autoCommittedTags;
      pendingTagInput = '';
    } else if (
      exactMatchedPendingTag &&
      selectedTags.some((tag) => tag.toLowerCase() === exactMatchedPendingTag.toLowerCase())
    ) {
      pendingTagInput = '';
    }

    const blockingIssues = getPublishBlockingIssues(resolvedSelectedTags, pendingTagInput);
    if (blockingIssues.length > 0) {
      showBlockedPublishFeedback(blockingIssues);
      return;
    }

    setShowBlockingIssues(false);
    setTagError(null);
    setCategoryError(null);
    await handleSubmit(resolvedSelectedTags);
  };

  const handleLoginClick = () => {
    redirectToLogin({ returnPath: loginReturnPath ?? buildDesktopForumReturnPath() });
  };

  const handleImageUpload = async (
    file: File,
    reportProgress: (progress: number) => void = () => undefined,
  ): Promise<MarkdownImageUploadResult> => {
    const result = await uploadImage(
      {
        file,
        businessType: 'Post',
        generateThumbnail: true,
        generateMultipleSizes,
        addWatermark,
        watermarkText,
        removeExif: true,
        onProgress: reportProgress,
      },
      t
    );

    return {
      attachmentId: result.voId,
      displayVariant: 'original',
      previewUrl: buildAttachmentAssetUrl(result.voId, 'original'),
      scalePercent: imageScalePercent,
    };
  };

  const handleDocumentUpload = async (
    file: File,
    reportProgress: (progress: number) => void = () => undefined,
  ): Promise<MarkdownDocumentUploadResult> => {
    const result = await uploadDocument(
      {
        file,
        businessType: 'Post',
        onProgress: reportProgress,
      },
      t
    );

    return {
      attachmentId: result.voId,
      fileName: result.voOriginalName || file.name
    };
  };

  const handleEditorUploadError = useCallback((kind: 'image' | 'document', error: unknown) => {
    log.error('ForumPostComposer', `Markdown ${kind} upload failed:`, error);
  }, []);

  const handleEditorUploadingChange = useCallback((uploading: boolean) => {
    setIsEditorUploading(uploading);
  }, []);

  const handleCloseAttempt = useCallback(() => {
    if (isSubmitting || isEditorUploading) {
      return;
    }

    onClose();
  }, [isEditorUploading, isSubmitting, onClose]);

  const updatePollOption = (index: number, value: string) => {
    setPollOptions((current) => current.map((item, itemIndex) => (itemIndex === index ? value : item)));
    if (pollError) {
      setPollError(null);
    }
  };

  const addPollOption = () => {
    if (pollOptions.length >= MAX_POLL_OPTION_COUNT) {
      return;
    }

    setPollOptions((current) => [...current, '']);
  };

  const removePollOption = (index: number) => {
    if (pollOptions.length <= MIN_POLL_OPTION_COUNT) {
      return;
    }

    setPollOptions((current) => current.filter((_, itemIndex) => itemIndex !== index));
    if (pollError) {
      setPollError(null);
    }
  };

  const selectedCategoryName = selectedCategorySnapshot?.name ?? t('forum.composer.categoryUnselected');
  const activeFeatureLabel = t(isQuestionPost
    ? 'forum.composer.type.question'
    : enablePoll
      ? 'forum.composer.type.poll'
      : enableLottery
        ? 'forum.composer.type.lottery'
        : 'forum.composer.type.standard');
  const blockingIssues = getPublishBlockingIssues();
  const completionCount = [
    Boolean(title.trim()),
    Boolean(content.trim()),
    Boolean(categoryId),
    selectedTags.length >= MIN_TAG_COUNT
  ].filter(Boolean).length;
  const isPublishBlocked = blockingIssues.length > 0;
  const isComposerBusy = isSubmitting || isEditorUploading;
  const canPublish = !isPublishBlocked && !isComposerBusy;

  useEffect(() => {
    onBusyChange?.(isComposerBusy);
  }, [isComposerBusy, onBusyChange]);

  useEffect(() => () => {
    onBusyChange?.(false);
  }, [onBusyChange]);

  const editorToolbarExtras = (
    <div className={styles.editorToggles}>
      <button
        type="button"
        className={`${styles.editorToggle} ${addWatermark ? styles.editorToggleActive : ''}`}
        onClick={() => setAddWatermark(!addWatermark)}
        aria-pressed={addWatermark}
        disabled={isComposerBusy}
      >
        <Icon icon="mdi:watermark" size={16} />
        <span>{t('forum.editor.watermark')}</span>
      </button>
      <button
        type="button"
        className={`${styles.editorToggle} ${generateMultipleSizes ? styles.editorToggleActive : ''}`}
        onClick={() => setGenerateMultipleSizes(!generateMultipleSizes)}
        aria-pressed={generateMultipleSizes}
        disabled={isComposerBusy}
      >
        <Icon icon="mdi:aspect-ratio" size={16} />
        <span>{t('forum.editor.multiSize')}</span>
      </button>
      <label className={styles.editorScaleLabel}>
        <span>{t('forum.editor.scale')}</span>
        <select
          value={imageScalePercent}
          onChange={(event) => setImageScalePercent(Number(event.target.value))}
          className={styles.editorScaleSelect}
          disabled={isComposerBusy}
        >
          {IMAGE_SCALE_OPTIONS.map((scale) => (
            <option key={scale} value={scale}>
              {scale}%
            </option>
          ))}
        </select>
      </label>
    </div>
  );

  const footer = (
    <div className={styles.footer}>
      <div className={styles.footerMeta}>
        <span className={styles.footerStatus}>
          {t(composerMode === 'rich' ? 'forum.composer.mode.richStatus' : 'forum.composer.mode.markdownStatus')}
          {' · '}
          {selectedCategoryName}
          {' · '}
          {t('forum.composer.tagCount', { count: selectedTags.length, max: MAX_TAG_COUNT })}
          {' · '}
          {activeFeatureLabel}
          {` · ${t('forum.composer.localDraftStatus')}`}
        </span>
      </div>
      <div className={styles.footerActions}>
        <button
          type="button"
          className={styles.cancelButton}
          onClick={handleCloseAttempt}
          disabled={isComposerBusy}
        >
          {t('common.cancel')}
        </button>
        <button
          type="button"
          className={`${styles.publishButton} ${!canPublish && !isSubmitting ? styles.publishButtonDisabled : ''}`}
          onClick={() => {
            void handlePublishAttempt();
          }}
          disabled={isComposerBusy}
          aria-disabled={!canPublish}
        >
          {t(isSubmitting ? 'forum.composer.publishing' : 'forum.composer.publish')}
        </button>
      </div>
    </div>
  );

  if (!isAuthenticated) {
    return (
      <div className={styles.loginPrompt}>
        <p>{t('forum.composer.loginRequired')}</p>
        <button type="button" onClick={handleLoginClick} className={styles.loginButton}>
          {t('forum.composer.goToLogin')}
        </button>
      </div>
    );
  }

  return (
      <div className={`${styles.composer} ${surface === 'page' ? styles.composerPage : ''} ${isFullscreen ? styles.composerFullscreen : ''}`.trim()}>
        <header className={styles.composerHeader}>
          <div className={styles.composerPrimary}>
            <div className={styles.modeSwitcher}>
              <button
                type="button"
                className={`${styles.modeButton} ${composerMode === 'markdown' ? styles.modeButtonActive : ''}`}
                onClick={() => setComposerMode('markdown')}
                disabled={isComposerBusy}
              >
                {t('forum.composer.mode.markdown')}
              </button>
              <button
                type="button"
                className={`${styles.modeButton} ${composerMode === 'rich' ? styles.modeButtonActive : ''}`}
                onClick={() => setComposerMode('rich')}
                disabled={isComposerBusy}
              >
                {t('forum.composer.mode.rich')}
              </button>
            </div>
            <div className={styles.composerMeta}>
              <span className={styles.summaryPill}>{selectedCategoryName}</span>
              <span className={styles.summaryPill}>{t('forum.composer.selectedTagCount', { count: selectedTags.length })}</span>
              <span className={styles.summaryPill}>{activeFeatureLabel}</span>
              <span className={styles.summaryPill}>{t('forum.composer.readyCount', { count: completionCount, total: 4 })}</span>
            </div>
          </div>

          <div className={styles.headerActions}>
            <button
              type="button"
              className={styles.headerActionButton}
              onClick={() => setIsSettingsOpen((current) => !current)}
              aria-label={t(isSettingsOpen ? 'forum.composer.settingsCollapse' : 'forum.composer.settingsOpen')}
            >
              <Icon icon="mdi:tune-vertical-variant" size={18} />
              <span>{t(isSettingsOpen ? 'forum.composer.settingsCollapse' : 'forum.composer.settingsOpen')}</span>
            </button>
            <button
              type="button"
              className={styles.headerActionButton}
              onClick={() => setIsFullscreen((current) => !current)}
              aria-label={t(isFullscreen ? 'forum.composer.fullscreenExit' : 'forum.composer.fullscreenEnter')}
            >
              <Icon icon={isFullscreen ? 'mdi:fullscreen-exit' : 'mdi:fullscreen'} size={18} />
              <span>{t(isFullscreen ? 'forum.composer.fullscreenExit' : 'forum.composer.fullscreenEnter')}</span>
            </button>
            <button
              type="button"
              className={styles.headerIconButton}
              onClick={handleCloseAttempt}
              aria-label={t('forum.composer.close')}
              disabled={isComposerBusy}
            >
              <Icon icon="mdi:close" size={20} />
            </button>
          </div>
        </header>

        <div className={styles.titleBar}>
          <input
            ref={titleInputRef}
            type="text"
            placeholder={t('forum.composer.titlePlaceholder')}
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            className={`${styles.titleInput} ${showBlockingIssues && blockingIssues.some((issue) => issue.code === 'title') ? styles.titleInputError : ''}`}
            aria-invalid={showBlockingIssues && blockingIssues.some((issue) => issue.code === 'title')}
            maxLength={100}
          />
          <div className={styles.titleMeta}>
            <span className={styles.titleHint}>
              {composerMode === 'rich'
                ? t('forum.composer.richModeHint')
                : t('forum.composer.markdownModeHint')}
            </span>
            <span className={styles.titleCount}>{title.length}/100</span>
          </div>
        </div>
        {showBlockingIssues && blockingIssues.length > 0 && (
          <div className={styles.validationBanner}>
            <strong className={styles.validationTitle}>{t('forum.composer.validation.title')}</strong>
            <ul className={styles.validationList}>
              {blockingIssues.map((issue) => (
                <li key={`${issue.code}-${issue.label}`}>{issue.message}</li>
              ))}
            </ul>
          </div>
        )}
        {publishError && (
          <div className={`${styles.validationBanner} ${styles.recoverableErrorBanner}`} role="alert">
            <strong className={styles.validationTitle}>{t('forum.publishDraftRetainedTitle')}</strong>
            <p className={styles.recoverableErrorText}>{publishError}</p>
          </div>
        )}

        <div className={`${styles.workspace} ${composerMode === 'rich' ? styles.workspaceRich : ''}`}>
          <div className={`${styles.editorFrame} ${composerMode === 'rich' ? styles.editorFrameRich : ''}`}>
            {composerMode === 'markdown' ? (
              <Suspense fallback={<div className={styles.editorLoading}>{t('markdownEditor.loading')}</div>}>
                <MarkdownEditor
                  value={content}
                  onChange={setContent}
                  labels={markdownEditorLabels}
                  onImageUpload={handleImageUpload}
                  onDocumentUpload={handleDocumentUpload}
                  onUploadError={handleEditorUploadError}
                  onUploadingChange={handleEditorUploadingChange}
                  stickerGroups={stickerGroups}
                  stickerMap={stickerMap}
                  onStickerSelect={(selection) => {
                    void handleStickerSelect(selection);
                  }}
                  minHeight={0}
                  defaultMode="edit"
                  className={styles.markdownEditor}
                  theme="light"
                  toolbarExtras={editorToolbarExtras}
                  onUserMentionSearch={handleSearchUsers}
                />
              </Suspense>
            ) : (
              <RichTextMarkdownEditor
                value={content}
                onChange={setContent}
                labels={richTextEditorLabels}
                minHeight={0}
                onImageUpload={handleImageUpload}
                onDocumentUpload={handleDocumentUpload}
                onUploadError={handleEditorUploadError}
                onUploadingChange={handleEditorUploadingChange}
                toolbarExtras={editorToolbarExtras}
                className={styles.richTextEditor}
              />
            )}

            {addWatermark && (
              <div className={styles.watermarkRow}>
                <span className={styles.watermarkLabel}>{t('forum.editor.watermarkLabel')}</span>
                <input
                  type="text"
                  placeholder={t('forum.editor.watermarkPlaceholder')}
                  value={watermarkText}
                  onChange={(event) => setWatermarkText(event.target.value)}
                  className={styles.watermarkInput}
                  disabled={isComposerBusy}
                />
              </div>
            )}
          </div>
        </div>

        <aside className={`${styles.settingsDrawer} ${isSettingsOpen ? styles.settingsDrawerOpen : ''}`}>
          <div className={styles.settingsPanelContent}>
            <div className={styles.settingsHeader}>
              <div>
                <p className={styles.settingsEyebrow}>{t('forum.composer.settingsEyebrow')}</p>
                <h3 className={styles.settingsTitle}>{t('forum.composer.settingsTitle')}</h3>
              </div>
              <button type="button" className={styles.settingsClose} onClick={() => setIsSettingsOpen(false)} aria-label={t('forum.composer.settingsClose')}>
                <Icon icon="mdi:close" size={18} />
              </button>
            </div>

            <div className={styles.settingsSummary}>
              <div className={styles.summaryCard}>
                <span className={styles.summaryCardLabel}>{t('forum.composer.field.category')}</span>
                <strong className={styles.summaryCardValue}>{selectedCategoryName}</strong>
              </div>
              <div className={styles.summaryCard}>
                <span className={styles.summaryCardLabel}>{t('forum.composer.field.tags')}</span>
                <strong className={styles.summaryCardValue}>{selectedTags.length}/{MAX_TAG_COUNT}</strong>
              </div>
              <div className={styles.summaryCard}>
                <span className={styles.summaryCardLabel}>{t('forum.composer.postType')}</span>
                <strong className={styles.summaryCardValue}>{activeFeatureLabel}</strong>
              </div>
              <div className={styles.summaryCard}>
                <span className={styles.summaryCardLabel}>{t('forum.composer.publishReadiness')}</span>
                <strong className={styles.summaryCardValue}>{completionCount}/4</strong>
              </div>
            </div>

            <section className={styles.settingsSection}>
              <div className={styles.sectionHeading}>
                <span className={styles.sectionTitle}>{t('forum.composer.categoryTitle')}</span>
                <span className={styles.sectionHint}>{t('forum.composer.categoryHint')}</span>
              </div>
              <select
                ref={categorySelectRef}
                value={categoryId ?? ''}
                onChange={(event) => {
                  const value = event.target.value;
                  applyCategorySelection(value || null);
                  setCategoryError(null);
                }}
                className={`${styles.control} ${showBlockingIssues && blockingIssues.some((issue) => issue.code === 'category') ? styles.controlAttention : ''}`}
                disabled={isSubmitting || categories.length === 0}
                aria-invalid={showBlockingIssues && blockingIssues.some((issue) => issue.code === 'category')}
              >
                <option value="">{t('forum.composer.categoryPlaceholder')}</option>
                {categories.map((category) => (
                  <option key={category.voId} value={category.voId}>
                    {category.voName}
                  </option>
                ))}
              </select>
              {categoryError && <p className={styles.errorText}>{categoryError}</p>}
              {!categoryError && categories.length === 0 && <p className={styles.errorText}>{t('forum.composer.categoryEmpty')}</p>}
            </section>

            <section className={styles.settingsSection}>
              <div className={styles.sectionHeading}>
                <span className={styles.sectionTitle}>{t('forum.composer.tagsTitle')}</span>
                <span className={styles.sectionHint}>{t('forum.composer.tagsHint', { min: MIN_TAG_COUNT, max: MAX_TAG_COUNT })}</span>
              </div>
              <div className={styles.inlineControlRow}>
                <input
                  ref={tagInputRef}
                  type="text"
                  placeholder={t('forum.composer.tagPlaceholder')}
                  value={tagInput}
                  onChange={(event) => {
                    setTagInput(event.target.value);
                    if (tagError) {
                      setTagError(null);
                    }
                  }}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter' || event.key === ',') {
                      event.preventDefault();
                      addTag(tagInput);
                    }
                  }}
                  onBlur={() => {
                    void tryAutoCommitExactMatchedTag(tagInput);
                  }}
                  className={`${styles.control} ${showBlockingIssues && blockingIssues.some((issue) => issue.code === 'tag' || issue.code === 'tag-input-pending') ? styles.controlAttention : ''}`}
                  maxLength={50}
                  aria-invalid={showBlockingIssues && blockingIssues.some((issue) => issue.code === 'tag' || issue.code === 'tag-input-pending')}
                />
                <button
                  type="button"
                  className={styles.inlineActionButton}
                  onClick={() => addTag(tagInput)}
                  disabled={!tagInput.trim()}
                >
                  {t('forum.composer.tagAdd')}
                </button>
              </div>
              <p className={`${styles.helperText} ${tagInput.trim() ? styles.helperTextStrong : ''}`}>
                {t('forum.composer.tagCommitHint')}
              </p>

              {selectedTags.length > 0 && (
                <div className={styles.tagList}>
                  {selectedTags.map((tag) => (
                    <button
                      key={tag}
                      type="button"
                      className={styles.selectedTag}
                      onClick={() => removeTag(tag)}
                      title={t('forum.composer.tagRemove')}
                    >
                      #{tag} ×
                    </button>
                  ))}
                </div>
              )}

              {matchedTags.length > 0 && (
                <div className={styles.tagList}>
                  {matchedTags.map((tag) => (
                    <button key={tag} type="button" className={styles.suggestedTag} onClick={() => addTag(tag)}>
                      {tag}
                    </button>
                  ))}
                </div>
              )}

              {tagInput.trim() && matchedTags.length === 0 && (
                <p className={styles.helperText}>
                  {t(isAdmin ? 'forum.composer.tagCreateAdminHint' : 'forum.composer.tagCreateForbiddenHint')}
                </p>
              )}
              {tagError && <p className={styles.errorText}>{tagError}</p>}
            </section>

            <section className={styles.settingsSection}>
              <div className={styles.sectionHeading}>
                <span className={styles.sectionTitle}>{t('forum.composer.postType')}</span>
                <span className={styles.sectionHint}>{t('forum.composer.postTypeHint')}</span>
              </div>
              <div className={styles.segmentedControl}>
                <button
                  type="button"
                  className={`${styles.segmentedButton} ${!isQuestionPost ? styles.segmentedButtonActive : ''}`}
                  onClick={() => handleQuestionMode(false)}
                >
                  {t('forum.composer.type.standard')}
                </button>
                <button
                  type="button"
                  className={`${styles.segmentedButton} ${isQuestionPost ? styles.segmentedButtonActive : ''}`}
                  onClick={() => handleQuestionMode(!isQuestionPost)}
                >
                  {t('forum.composer.type.questionPost')}
                </button>
              </div>
              {isQuestionPost && <p className={styles.helperText}>{t('forum.composer.questionExclusiveHint')}</p>}
            </section>

            <section className={styles.settingsSection}>
              <div className={styles.sectionHeading}>
                <span className={styles.sectionTitle}>{t('forum.composer.extensionsTitle')}</span>
                <span className={styles.sectionHint}>{t('forum.composer.extensionsHint')}</span>
              </div>

              <div className={`${styles.featureCard} ${enablePoll ? styles.featureCardActive : ''}`}>
                <div className={styles.featureCardHeader}>
                  <div>
                    <strong className={styles.featureTitle}>{t('forum.composer.pollTitle')}</strong>
                    <p className={styles.featureDescription}>{t('forum.composer.pollDescription')}</p>
                  </div>
                  <button type="button" className={styles.featureToggle} onClick={handleTogglePoll}>
                    {t(enablePoll ? 'forum.composer.disable' : 'forum.composer.enable')}
                  </button>
                </div>
                {enablePoll && (
                  <div className={styles.featureFields}>
                    <input
                      type="text"
                      placeholder={t('forum.composer.pollQuestionPlaceholder')}
                      value={pollQuestion}
                      onChange={(event) => {
                        setPollQuestion(event.target.value);
                        setPollError(null);
                      }}
                      className={styles.control}
                      maxLength={200}
                    />

                    <div className={styles.fieldStack}>
                      {pollOptions.map((option, index) => (
                        <div key={`poll-option-${index}`} className={styles.pollOptionRow}>
                          <span className={styles.pollOptionIndex}>{index + 1}</span>
                          <input
                            type="text"
                            placeholder={t('forum.composer.pollOptionPlaceholder', { index: index + 1 })}
                            value={option}
                            onChange={(event) => updatePollOption(index, event.target.value)}
                            className={styles.control}
                            maxLength={100}
                          />
                          <button
                            type="button"
                            className={styles.inlineGhostButton}
                            onClick={() => removePollOption(index)}
                            disabled={pollOptions.length <= MIN_POLL_OPTION_COUNT}
                          >
                            {t('common.delete')}
                          </button>
                        </div>
                      ))}
                    </div>

                    <div className={styles.inlineMetaRow}>
                      <button
                        type="button"
                        className={styles.inlineGhostButton}
                        onClick={addPollOption}
                        disabled={pollOptions.length >= MAX_POLL_OPTION_COUNT}
                      >
                        {t('forum.composer.pollAddOption')}
                      </button>
                      <label className={styles.fieldLabel}>
                        <span>{t('forum.composer.pollEndTime')}</span>
                        <input
                          type="datetime-local"
                          value={pollEndTime}
                          onChange={(event) => {
                            setPollEndTime(event.target.value);
                            setPollError(null);
                          }}
                          className={styles.control}
                        />
                      </label>
                    </div>

                    {pollError && <p className={styles.errorText}>{pollError}</p>}
                  </div>
                )}
              </div>

              <div className={`${styles.featureCard} ${enableLottery ? styles.featureCardWarm : ''}`}>
                <div className={styles.featureCardHeader}>
                  <div>
                    <strong className={styles.featureTitle}>{t('forum.composer.lotteryTitle')}</strong>
                    <p className={styles.featureDescription}>{t('forum.composer.lotteryDescription')}</p>
                  </div>
                  <button type="button" className={styles.featureToggle} onClick={handleToggleLottery}>
                    {t(enableLottery ? 'forum.composer.disable' : 'forum.composer.enable')}
                  </button>
                </div>
                {enableLottery && (
                  <div className={styles.featureFields}>
                    <input
                      type="text"
                      placeholder={t('forum.composer.lotteryPrizePlaceholder')}
                      value={lotteryPrizeName}
                      onChange={(event) => {
                        setLotteryPrizeName(event.target.value);
                        setLotteryError(null);
                      }}
                      className={styles.control}
                      maxLength={100}
                    />
                    <textarea
                      placeholder={t('forum.composer.lotteryDescriptionPlaceholder')}
                      value={lotteryPrizeDescription}
                      onChange={(event) => {
                        setLotteryPrizeDescription(event.target.value);
                        setLotteryError(null);
                      }}
                      className={`${styles.control} ${styles.multilineControl}`}
                      maxLength={500}
                      rows={3}
                    />
                    <div className={styles.inlineMetaRow}>
                      <label className={styles.fieldLabel}>
                        <span>{t('forum.composer.lotteryDrawTime')}</span>
                        <input
                          type="datetime-local"
                          value={lotteryDrawTime}
                          onChange={(event) => {
                            setLotteryDrawTime(event.target.value);
                            setLotteryError(null);
                          }}
                          className={styles.control}
                        />
                      </label>
                      <label className={styles.fieldLabel}>
                        <span>{t('forum.composer.lotteryWinnerCount')}</span>
                        <input
                          type="number"
                          min={MIN_LOTTERY_WINNER_COUNT}
                          max={MAX_LOTTERY_WINNER_COUNT}
                          value={lotteryWinnerCount}
                          onChange={(event) => {
                            setLotteryWinnerCount(event.target.value);
                            setLotteryError(null);
                          }}
                          className={styles.control}
                        />
                      </label>
                    </div>
                    <p className={styles.helperText}>{t('forum.composer.lotteryTimingHint')}</p>
                    {lotteryError && <p className={styles.errorText}>{lotteryError}</p>}
                  </div>
                )}
              </div>
            </section>
          </div>
        </aside>
        {isSettingsOpen && (
          <button
            type="button"
            className={styles.settingsBackdrop}
            onClick={() => setIsSettingsOpen(false)}
            aria-label={t('forum.composer.settingsBackdropClose')}
          />
        )}
        <div className={styles.composerFooter}>{footer}</div>
      </div>
  );
};

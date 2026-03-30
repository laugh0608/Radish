import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import { log } from '@/utils/logger';
import {
  getAllTags,
  getOidcLoginUrl,
  type Category,
  type CreateLotteryRequest,
  type CreatePollRequest
} from '@/api/forum';
import { uploadDocument, uploadImage } from '@/api/attachment';
import { useUserStore } from '@/stores/userStore';
import { useStickerCatalog } from '../hooks/useStickerCatalog';
import { RichTextMarkdownEditor } from './RichTextMarkdownEditor';
import styles from './PublishPostModal.module.css';

interface PublishPostModalProps {
  isOpen: boolean;
  isAuthenticated: boolean;
  categories: Category[];
  selectedCategoryId: number | null;
  onClose: () => void;
  onPublish: (
    title: string,
    content: string,
    categoryId: number,
    tagNames: string[],
    isQuestion?: boolean,
    poll?: CreatePollRequest | null,
    lottery?: CreateLotteryRequest | null
  ) => Promise<void>;
}

interface CategorySelectionSnapshot {
  id: number;
  name: string;
}

interface PublishPostDraft {
  title?: string;
  content?: string;
  tags?: string[];
  categoryId?: number | null;
  categoryName?: string | null;
  composerMode?: 'markdown' | 'rich';
  isQuestion?: boolean;
  poll?: {
    enabled?: boolean;
    question?: string;
    endTime?: string;
    options?: string[];
  };
  lottery?: {
    enabled?: boolean;
    prizeName?: string;
    prizeDescription?: string;
    drawTime?: string;
    winnerCount?: string;
  };
}

type PublishBlockingIssueCode = 'title' | 'content' | 'category' | 'tag' | 'tag-input-pending';

interface PublishBlockingIssue {
  code: PublishBlockingIssueCode;
  label: string;
  message: string;
}

const DRAFT_STORAGE_KEY = 'forum_post_draft';
const MIN_TAG_COUNT = 1;
const MAX_TAG_COUNT = 5;
const MIN_POLL_OPTION_COUNT = 2;
const MAX_POLL_OPTION_COUNT = 6;
const MIN_LOTTERY_WINNER_COUNT = 1;
const MAX_LOTTERY_WINNER_COUNT = 20;
const IMAGE_SCALE_OPTIONS = [30, 50, 70, 75, 100] as const;
const DEFAULT_POLL_OPTIONS = ['', ''];

const MarkdownEditor = lazy(() =>
  import('@radish/ui/markdown-editor').then((module) => ({ default: module.MarkdownEditor }))
);

const findCategorySnapshot = (
  categories: Category[],
  targetCategoryId: number | null | undefined
): CategorySelectionSnapshot | null => {
  if (!targetCategoryId || targetCategoryId <= 0) {
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

export const PublishPostModal = ({
  isOpen,
  isAuthenticated,
  categories,
  selectedCategoryId,
  onClose,
  onPublish
}: PublishPostModalProps) => {
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
  const [categoryId, setCategoryId] = useState<number | null>(selectedCategoryId);
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

  const roles = useUserStore((state) => state.roles || []);
  const isAdmin = roles.some((role) => {
    const normalized = role.trim().toLowerCase();
    return normalized === 'admin' || normalized === 'system';
  });
  const { t } = useTranslation();
  const { stickerGroups, stickerMap, handleStickerSelect } = useStickerCatalog();

  const applyCategorySelection = useCallback((nextCategoryId: number | null, snapshot?: CategorySelectionSnapshot | null) => {
    setCategoryId(nextCategoryId);
    setSelectedCategorySnapshot(snapshot ?? findCategorySnapshot(categories, nextCategoryId));
  }, [categories]);

  useEffect(() => {
    if (!isOpen) {
      setShowBlockingIssues(false);
      setTagError(null);
      setCategoryError(null);
      return;
    }

    try {
      const savedDraft = localStorage.getItem(DRAFT_STORAGE_KEY);
      if (!savedDraft) {
        applyCategorySelection(selectedCategoryId);
        return;
      }

      const draft = JSON.parse(savedDraft) as PublishPostDraft;
      if (draft.title || draft.content || draft.tags?.length || typeof draft.categoryId === 'number') {
        const draftIsQuestion = Boolean(draft.isQuestion);
        const draftCategoryId = typeof draft.categoryId === 'number' ? draft.categoryId : selectedCategoryId;
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
    } catch (error) {
      log.error('Failed to load draft:', error);
    }
  }, [applyCategorySelection, categories, isOpen, selectedCategoryId]);

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
    if (!isOpen || (!title && !content && selectedTags.length === 0 && categoryId == null)) {
      return;
    }

    try {
      localStorage.setItem(
        DRAFT_STORAGE_KEY,
        JSON.stringify({
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
          },
          savedAt: Date.now()
        })
      );
    } catch (error) {
      log.error('Failed to save draft:', error);
    }
  }, [
    isOpen,
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
        log.warn('加载标签列表失败:', error);
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
      setTagError(`最多可添加 ${MAX_TAG_COUNT} 个标签`);
      return;
    }

    const exists = allTagNames.some((name) => name.toLowerCase() === tagName.toLowerCase());
    if (!exists && !isAdmin) {
      setTagError('标签不存在，暂时仅管理员可创建新标签');
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
        label: '标题',
        message: '请先输入帖子标题'
      });
    }

    if (!content.trim()) {
      issues.push({
        code: 'content',
        label: '正文',
        message: '请先补充帖子正文'
      });
    }

    if (!categoryId || categoryId <= 0) {
      issues.push({
        code: 'category',
        label: '分类',
        message: '请先选择帖子分类'
      });
    }

    if (normalizedPendingTagInput) {
      issues.push({
        code: 'tag-input-pending',
        label: '标签未确认',
        message: `你已输入标签“${normalizedPendingTagInput}”，但还没有按回车或点击“添加”加入下方列表`
      });
    }

    if (nextSelectedTags.length < MIN_TAG_COUNT && !normalizedPendingTagInput) {
      issues.push({
        code: 'tag',
        label: '标签',
        message: `请至少添加 ${MIN_TAG_COUNT} 个标签`
      });
    }

    if (nextSelectedTags.length > MAX_TAG_COUNT) {
      issues.push({
        code: 'tag',
        label: '标签过多',
        message: `最多可添加 ${MAX_TAG_COUNT} 个标签`
      });
    }

    return issues;
  }, [categoryId, content, normalizeTagName, selectedTags, tagInput, title]);

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
      toast.info(`发布前还缺：${issues.map((issue) => issue.label).join('、')}`);
    }

    focusFirstBlockingIssue(issues);
  }, [focusFirstBlockingIssue]);

  const handleSubmit = async (resolvedSelectedTags: string[]) => {
    let pollRequest: CreatePollRequest | null = null;
    let lotteryRequest: CreateLotteryRequest | null = null;

    if (enablePoll) {
      const normalizedQuestion = pollQuestion.trim();
      if (!normalizedQuestion) {
        setPollError('投票问题不能为空');
        return;
      }

      const normalizedOptions = pollOptions.map((option) => option.trim()).filter(Boolean);
      if (normalizedOptions.length < MIN_POLL_OPTION_COUNT || normalizedOptions.length > MAX_POLL_OPTION_COUNT) {
        setPollError(`投票选项数量必须在 ${MIN_POLL_OPTION_COUNT} 到 ${MAX_POLL_OPTION_COUNT} 个之间`);
        return;
      }

      const uniqueOptions = new Set(normalizedOptions.map((option) => option.toLowerCase()));
      if (uniqueOptions.size !== normalizedOptions.length) {
        setPollError('投票选项不能重复');
        return;
      }

      if (pollEndTime) {
        const endTime = new Date(pollEndTime);
        if (Number.isNaN(endTime.getTime()) || endTime.getTime() <= Date.now()) {
          setPollError('投票截止时间必须晚于当前时间');
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
        setLotteryError('奖品名称不能为空');
        return;
      }

      if (!normalizedPrizeDescription) {
        setLotteryError('奖品说明不能为空');
        return;
      }

      if (!lotteryDrawTime) {
        setLotteryError('开奖时间不能为空');
        return;
      }

      const drawTime = new Date(lotteryDrawTime);
      if (Number.isNaN(drawTime.getTime()) || drawTime.getTime() <= Date.now()) {
        setLotteryError('开奖时间必须晚于当前时间');
        return;
      }

      if (
        !Number.isInteger(parsedWinnerCount) ||
        parsedWinnerCount < MIN_LOTTERY_WINNER_COUNT ||
        parsedWinnerCount > MAX_LOTTERY_WINNER_COUNT
      ) {
        setLotteryError(`中奖人数必须在 ${MIN_LOTTERY_WINNER_COUNT} 到 ${MAX_LOTTERY_WINNER_COUNT} 之间`);
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
      localStorage.removeItem(DRAFT_STORAGE_KEY);
      onClose();
    } catch (error) {
      log.error('发布失败:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handlePublishAttempt = async () => {
    if (isSubmitting) {
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
    const loginUrl = getOidcLoginUrl();
    if (loginUrl) {
      window.location.href = loginUrl;
    }
  };

  const handleImageUpload = async (file: File): Promise<MarkdownImageUploadResult> => {
    const result = await uploadImage(
      {
        file,
        businessType: 'Post',
        generateThumbnail: true,
        generateMultipleSizes,
        addWatermark,
        watermarkText,
        removeExif: true
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

  const handleDocumentUpload = async (file: File): Promise<MarkdownDocumentUploadResult> => {
    const result = await uploadDocument(
      {
        file,
        businessType: 'Post'
      },
      t
    );

    return {
      attachmentId: result.voId,
      fileName: result.voOriginalName || file.name
    };
  };

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

  const selectedCategoryName = selectedCategorySnapshot?.name ?? '未选分类';
  const activeFeatureLabel = isQuestionPost ? '问答' : enablePoll ? '投票' : enableLottery ? '抽奖' : '普通帖';
  const blockingIssues = getPublishBlockingIssues();
  const completionCount = [
    Boolean(title.trim()),
    Boolean(content.trim()),
    Boolean(categoryId),
    selectedTags.length >= MIN_TAG_COUNT
  ].filter(Boolean).length;
  const isPublishBlocked = blockingIssues.length > 0;
  const canPublish = !isPublishBlocked && !isSubmitting;

  const editorToolbarExtras = (
    <div className={styles.editorToggles}>
      <button
        type="button"
        className={`${styles.editorToggle} ${addWatermark ? styles.editorToggleActive : ''}`}
        onClick={() => setAddWatermark(!addWatermark)}
        aria-pressed={addWatermark}
      >
        <Icon icon="mdi:watermark" size={16} />
        <span>水印</span>
      </button>
      <button
        type="button"
        className={`${styles.editorToggle} ${generateMultipleSizes ? styles.editorToggleActive : ''}`}
        onClick={() => setGenerateMultipleSizes(!generateMultipleSizes)}
        aria-pressed={generateMultipleSizes}
      >
        <Icon icon="mdi:aspect-ratio" size={16} />
        <span>多尺寸</span>
      </button>
      <label className={styles.editorScaleLabel}>
        <span>缩放</span>
        <select
          value={imageScalePercent}
          onChange={(event) => setImageScalePercent(Number(event.target.value))}
          className={styles.editorScaleSelect}
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
          {composerMode === 'rich' ? '富文本写作' : 'Markdown 写作'}
          {' · '}
          {selectedCategoryName}
          {' · '}
          {selectedTags.length}/{MAX_TAG_COUNT} 标签
          {' · '}
          {activeFeatureLabel}
          {' · 草稿自动保存在本地'}
        </span>
      </div>
      <div className={styles.footerActions}>
        <button type="button" className={styles.cancelButton} onClick={onClose}>
          取消
        </button>
        <button
          type="button"
          className={`${styles.publishButton} ${!canPublish && !isSubmitting ? styles.publishButtonDisabled : ''}`}
          onClick={() => {
            void handlePublishAttempt();
          }}
          disabled={isSubmitting}
          aria-disabled={!canPublish}
        >
          {isSubmitting ? '发布中...' : '发布帖子'}
        </button>
      </div>
    </div>
  );

  if (!isAuthenticated) {
    return (
      <BottomSheet
        isOpen={isOpen}
        onClose={onClose}
        height="62%"
        className={styles.sheet}
        bodyClassName={styles.sheetBody}
        footerClassName={styles.sheetFooter}
      >
        <div className={styles.loginPrompt}>
          <p>请先登录后再发帖</p>
          <button type="button" onClick={handleLoginClick} className={styles.loginButton}>
            前往登录
          </button>
        </div>
      </BottomSheet>
    );
  }

  return (
    <BottomSheet
      isOpen={isOpen}
      onClose={onClose}
      height={isFullscreen ? '96%' : '76%'}
      className={`${styles.sheet} ${isFullscreen ? styles.sheetFullscreen : ''}`.trim()}
      bodyClassName={styles.sheetBody}
      footer={footer}
      footerClassName={styles.sheetFooter}
    >
      <div className={styles.composer}>
        <header className={styles.composerHeader}>
          <div className={styles.composerPrimary}>
            <div className={styles.modeSwitcher}>
              <button
                type="button"
                className={`${styles.modeButton} ${composerMode === 'markdown' ? styles.modeButtonActive : ''}`}
                onClick={() => setComposerMode('markdown')}
              >
                Markdown
              </button>
              <button
                type="button"
                className={`${styles.modeButton} ${composerMode === 'rich' ? styles.modeButtonActive : ''}`}
                onClick={() => setComposerMode('rich')}
              >
                富文本
              </button>
            </div>
            <div className={styles.composerMeta}>
              <span className={styles.summaryPill}>{selectedCategoryName}</span>
              <span className={styles.summaryPill}>{selectedTags.length} 个标签</span>
              <span className={styles.summaryPill}>{activeFeatureLabel}</span>
              <span className={styles.summaryPill}>{completionCount}/4 就绪</span>
            </div>
          </div>

          <div className={styles.headerActions}>
            <button
              type="button"
              className={styles.headerActionButton}
              onClick={() => setIsSettingsOpen((current) => !current)}
            >
              <Icon icon="mdi:tune-vertical-variant" size={18} />
              <span>{isSettingsOpen ? '收起设置' : '帖子设置'}</span>
            </button>
            <button type="button" className={styles.headerActionButton} onClick={() => setIsFullscreen((current) => !current)}>
              <Icon icon={isFullscreen ? 'mdi:fullscreen-exit' : 'mdi:fullscreen'} size={18} />
              <span>{isFullscreen ? '退出全屏' : '全屏创作'}</span>
            </button>
            <button type="button" className={styles.headerIconButton} onClick={onClose} aria-label="关闭创作器">
              <Icon icon="mdi:close" size={20} />
            </button>
          </div>
        </header>

        <div className={styles.titleBar}>
          <input
            ref={titleInputRef}
            type="text"
            placeholder="输入标题"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            className={`${styles.titleInput} ${showBlockingIssues && blockingIssues.some((issue) => issue.code === 'title') ? styles.titleInputError : ''}`}
            aria-invalid={showBlockingIssues && blockingIssues.some((issue) => issue.code === 'title')}
            maxLength={100}
          />
          <div className={styles.titleMeta}>
            <span className={styles.titleHint}>
              {composerMode === 'rich'
                ? '富文本输入，保存为 Markdown'
                : '默认编辑模式，可在工具栏切换预览或分栏'}
            </span>
            <span className={styles.titleCount}>{title.length}/100</span>
          </div>
        </div>
        {showBlockingIssues && blockingIssues.length > 0 && (
          <div className={styles.validationBanner}>
            <strong className={styles.validationTitle}>发布前还需要补全这些内容</strong>
            <ul className={styles.validationList}>
              {blockingIssues.map((issue) => (
                <li key={`${issue.code}-${issue.label}`}>{issue.message}</li>
              ))}
            </ul>
          </div>
        )}

        <div className={`${styles.workspace} ${composerMode === 'rich' ? styles.workspaceRich : ''}`}>
          <div className={`${styles.editorFrame} ${composerMode === 'rich' ? styles.editorFrameRich : ''}`}>
            {composerMode === 'markdown' ? (
              <Suspense fallback={<div className={styles.editorLoading}>编辑器加载中...</div>}>
                <MarkdownEditor
                  value={content}
                  onChange={setContent}
                  placeholder="开始写正文，支持 Markdown。右侧预览、左侧编辑、分屏切换都在工具栏末尾。"
                  onImageUpload={handleImageUpload}
                  onDocumentUpload={handleDocumentUpload}
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
                />
              </Suspense>
            ) : (
              <RichTextMarkdownEditor
                value={content}
                onChange={setContent}
                placeholder="直接输入正文，使用上方工具栏完成标题、强调、引用、列表、链接和图片等排版。"
                minHeight={0}
                onImageUpload={handleImageUpload}
                onDocumentUpload={handleDocumentUpload}
                toolbarExtras={editorToolbarExtras}
                className={styles.richTextEditor}
              />
            )}

            {addWatermark && (
              <div className={styles.watermarkRow}>
                <span className={styles.watermarkLabel}>水印文字</span>
                <input
                  type="text"
                  placeholder="输入水印文字"
                  value={watermarkText}
                  onChange={(event) => setWatermarkText(event.target.value)}
                  className={styles.watermarkInput}
                />
              </div>
            )}
          </div>
        </div>

        <aside className={`${styles.settingsDrawer} ${isSettingsOpen ? styles.settingsDrawerOpen : ''}`}>
          <div className={styles.settingsPanelContent}>
            <div className={styles.settingsHeader}>
              <div>
                <p className={styles.settingsEyebrow}>帖子设置</p>
                <h3 className={styles.settingsTitle}>分类、标签与附加功能</h3>
              </div>
              <button type="button" className={styles.settingsClose} onClick={() => setIsSettingsOpen(false)} aria-label="关闭设置">
                <Icon icon="mdi:close" size={18} />
              </button>
            </div>

            <div className={styles.settingsSummary}>
              <div className={styles.summaryCard}>
                <span className={styles.summaryCardLabel}>分类</span>
                <strong className={styles.summaryCardValue}>{selectedCategoryName}</strong>
              </div>
              <div className={styles.summaryCard}>
                <span className={styles.summaryCardLabel}>标签</span>
                <strong className={styles.summaryCardValue}>{selectedTags.length}/{MAX_TAG_COUNT}</strong>
              </div>
              <div className={styles.summaryCard}>
                <span className={styles.summaryCardLabel}>帖子类型</span>
                <strong className={styles.summaryCardValue}>{activeFeatureLabel}</strong>
              </div>
              <div className={styles.summaryCard}>
                <span className={styles.summaryCardLabel}>发布准备</span>
                <strong className={styles.summaryCardValue}>{completionCount}/4</strong>
              </div>
            </div>

            <section className={styles.settingsSection}>
              <div className={styles.sectionHeading}>
                <span className={styles.sectionTitle}>帖子分类</span>
                <span className={styles.sectionHint}>发帖前必须选择</span>
              </div>
              <select
                ref={categorySelectRef}
                value={categoryId ?? ''}
                onChange={(event) => {
                  const value = event.target.value;
                  applyCategorySelection(value ? Number(value) : null);
                  setCategoryError(null);
                }}
                className={`${styles.control} ${showBlockingIssues && blockingIssues.some((issue) => issue.code === 'category') ? styles.controlAttention : ''}`}
                disabled={isSubmitting || categories.length === 0}
                aria-invalid={showBlockingIssues && blockingIssues.some((issue) => issue.code === 'category')}
              >
                <option value="">请选择分类</option>
                {categories.map((category) => (
                  <option key={category.voId} value={category.voId}>
                    {category.voName}
                  </option>
                ))}
              </select>
              {categoryError && <p className={styles.errorText}>{categoryError}</p>}
              {!categoryError && categories.length === 0 && <p className={styles.errorText}>暂无可用分类</p>}
            </section>

            <section className={styles.settingsSection}>
              <div className={styles.sectionHeading}>
                <span className={styles.sectionTitle}>帖子标签</span>
                <span className={styles.sectionHint}>至少 {MIN_TAG_COUNT} 个，最多 {MAX_TAG_COUNT} 个</span>
              </div>
              <div className={styles.inlineControlRow}>
                <input
                  ref={tagInputRef}
                  type="text"
                  placeholder="输入标签名后按回车或点击添加"
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
                  添加
                </button>
              </div>
              <p className={`${styles.helperText} ${tagInput.trim() ? styles.helperTextStrong : ''}`}>
                输入后需要按回车、逗号或点击“添加”，进入下方标签列表后才算已选择。
              </p>

              {selectedTags.length > 0 && (
                <div className={styles.tagList}>
                  {selectedTags.map((tag) => (
                    <button
                      key={tag}
                      type="button"
                      className={styles.selectedTag}
                      onClick={() => removeTag(tag)}
                      title="点击移除"
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
                  {isAdmin ? '未匹配现有标签，发布时将创建新标签。' : '未匹配现有标签，暂时仅管理员可创建。'}
                </p>
              )}
              {tagError && <p className={styles.errorText}>{tagError}</p>}
            </section>

            <section className={styles.settingsSection}>
              <div className={styles.sectionHeading}>
                <span className={styles.sectionTitle}>帖子类型</span>
                <span className={styles.sectionHint}>问答与其他扩展互斥</span>
              </div>
              <div className={styles.segmentedControl}>
                <button
                  type="button"
                  className={`${styles.segmentedButton} ${!isQuestionPost ? styles.segmentedButtonActive : ''}`}
                  onClick={() => handleQuestionMode(false)}
                >
                  普通帖子
                </button>
                <button
                  type="button"
                  className={`${styles.segmentedButton} ${isQuestionPost ? styles.segmentedButtonActive : ''}`}
                  onClick={() => handleQuestionMode(!isQuestionPost)}
                >
                  问答帖
                </button>
              </div>
              {isQuestionPost && <p className={styles.helperText}>问答帖开启后，投票和抽奖会自动关闭。</p>}
            </section>

            <section className={styles.settingsSection}>
              <div className={styles.sectionHeading}>
                <span className={styles.sectionTitle}>扩展功能</span>
                <span className={styles.sectionHint}>按需启用即可</span>
              </div>

              <div className={`${styles.featureCard} ${enablePoll ? styles.featureCardActive : ''}`}>
                <div className={styles.featureCardHeader}>
                  <div>
                    <strong className={styles.featureTitle}>附带投票</strong>
                    <p className={styles.featureDescription}>适合收集反馈，保存时仍会作为帖子的一部分提交。</p>
                  </div>
                  <button type="button" className={styles.featureToggle} onClick={handleTogglePoll}>
                    {enablePoll ? '关闭' : '开启'}
                  </button>
                </div>
                {enablePoll && (
                  <div className={styles.featureFields}>
                    <input
                      type="text"
                      placeholder="投票问题，例如：本周论坛最想先补什么？"
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
                            placeholder={`选项 ${index + 1}`}
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
                            删除
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
                        添加选项
                      </button>
                      <label className={styles.fieldLabel}>
                        <span>截止时间</span>
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
                    <strong className={styles.featureTitle}>附带抽奖</strong>
                    <p className={styles.featureDescription}>适合活动帖，评论参与，开奖信息会跟随帖子展示。</p>
                  </div>
                  <button type="button" className={styles.featureToggle} onClick={handleToggleLottery}>
                    {enableLottery ? '关闭' : '开启'}
                  </button>
                </div>
                {enableLottery && (
                  <div className={styles.featureFields}>
                    <input
                      type="text"
                      placeholder="奖品名称，例如：论坛头像挂件一份"
                      value={lotteryPrizeName}
                      onChange={(event) => {
                        setLotteryPrizeName(event.target.value);
                        setLotteryError(null);
                      }}
                      className={styles.control}
                      maxLength={100}
                    />
                    <textarea
                      placeholder="奖品说明，例如：抽中后私信联系领取方式"
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
                        <span>开奖时间</span>
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
                        <span>中奖人数</span>
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
                    <p className={styles.helperText}>发布一条顶级评论即可参与抽奖，发帖者本人不进入中奖池。</p>
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
            aria-label="关闭设置面板"
          />
        )}
      </div>
    </BottomSheet>
  );
};

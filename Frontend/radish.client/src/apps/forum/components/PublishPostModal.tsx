import { lazy, Suspense, useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Icon } from '@radish/ui/icon';
import {
  getAllTags,
  getOidcLoginUrl,
  type Category,
  type CreateLotteryRequest,
  type CreatePollRequest
} from '@/api/forum';
import { useUserStore } from '@/stores/userStore';
import { uploadImage, uploadDocument } from '@/api/attachment';
import { useStickerCatalog } from '../hooks/useStickerCatalog';
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

const appendImageMeta = (displayUrl: string, fullUrl?: string, scalePercent?: number): string => {
  const params = new URLSearchParams();
  if (fullUrl) {
    params.set('full', fullUrl);
  }
  if (scalePercent && Number.isFinite(scalePercent)) {
    params.set('scale', String(Math.min(Math.max(scalePercent, 10), 100)));
  }
  const meta = params.toString();
  return meta ? `${displayUrl}#radish:${meta}` : displayUrl;
};

export const PublishPostModal = ({
  isOpen,
  isAuthenticated,
  categories,
  selectedCategoryId,
  onClose,
  onPublish
}: PublishPostModalProps) => {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [addWatermark, setAddWatermark] = useState(false);
  const [watermarkText, setWatermarkText] = useState('Radish');
  const [generateMultipleSizes, setGenerateMultipleSizes] = useState(false);
  const [imageScalePercent, setImageScalePercent] = useState<number>(75);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [categoryId, setCategoryId] = useState<number | null>(selectedCategoryId);
  const [allTagNames, setAllTagNames] = useState<string[]>([]);
  const [tagInput, setTagInput] = useState('');
  const [selectedTags, setSelectedTags] = useState<string[]>([]);
  const [tagError, setTagError] = useState<string | null>(null);
  const [categoryError, setCategoryError] = useState<string | null>(null);
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
  const roles = useUserStore(state => state.roles || []);
  const isAdmin = roles.some(role => {
    const normalized = role.trim().toLowerCase();
    return normalized === 'admin' || normalized === 'system';
  });
  const { t } = useTranslation();
  const { stickerGroups, stickerMap, handleStickerSelect } = useStickerCatalog();

  // 组件打开时恢复草稿
  useEffect(() => {
    if (isOpen) {
      try {
        const savedDraft = localStorage.getItem(DRAFT_STORAGE_KEY);
        if (savedDraft) {
          const draft = JSON.parse(savedDraft);
          if (draft.title || draft.content || draft.tags?.length || typeof draft.categoryId === 'number') {
            const draftIsQuestion = Boolean(draft.isQuestion);
            setTitle(draft.title || '');
            setContent(draft.content || '');
            setSelectedTags(Array.isArray(draft.tags) ? draft.tags : []);
            setCategoryId(typeof draft.categoryId === 'number' ? draft.categoryId : selectedCategoryId);
            setIsQuestionPost(draftIsQuestion);
            const draftLotteryEnabled = Boolean(draft.lottery?.enabled) && !draftIsQuestion;
            setEnableLottery(draftLotteryEnabled);
            setEnablePoll(Boolean(draft.poll?.enabled) && !draftIsQuestion && !draftLotteryEnabled);
            setPollQuestion(draft.poll?.question || '');
            setPollEndTime(draft.poll?.endTime || '');
            setPollOptions(Array.isArray(draft.poll?.options) && draft.poll.options.length >= MIN_POLL_OPTION_COUNT
              ? draft.poll.options
              : [...DEFAULT_POLL_OPTIONS]);
            setLotteryPrizeName(draft.lottery?.prizeName || '');
            setLotteryPrizeDescription(draft.lottery?.prizeDescription || '');
            setLotteryDrawTime(draft.lottery?.drawTime || '');
            setLotteryWinnerCount(draft.lottery?.winnerCount || '1');
          } else {
            setCategoryId(selectedCategoryId);
          }
        } else {
          setCategoryId(selectedCategoryId);
        }
      } catch (err) {
        log.error('Failed to load draft:', err);
      }
    }
  }, [isOpen, selectedCategoryId]);

  // 自动保存草稿
  useEffect(() => {
    if (isOpen && (title || content || selectedTags.length > 0 || categoryId != null)) {
      try {
        localStorage.setItem(
          DRAFT_STORAGE_KEY,
          JSON.stringify({
            title,
            content,
            tags: selectedTags,
            categoryId,
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
      } catch (err) {
        log.error('Failed to save draft:', err);
      }
    }
  }, [
    title,
    content,
    selectedTags,
    categoryId,
    isQuestionPost,
    enablePoll,
    pollQuestion,
    pollEndTime,
    pollOptions,
    enableLottery,
    lotteryPrizeName,
    lotteryPrizeDescription,
    lotteryDrawTime,
    lotteryWinnerCount,
    isOpen
  ]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    const loadTags = async () => {
      try {
        const tags = await getAllTags(t);
        setAllTagNames(tags.map(tag => tag.voName));
      } catch (err) {
        log.warn('加载标签列表失败:', err);
        setAllTagNames([]);
      }
    };

    void loadTags();
  }, [isOpen, t]);

  const normalizeTagName = (name: string) => name.trim();

  const addTag = (rawTagName: string) => {
    const tagName = normalizeTagName(rawTagName);
    if (!tagName) {
      return;
    }

    if (selectedTags.some(tag => tag.toLowerCase() === tagName.toLowerCase())) {
      setTagInput('');
      return;
    }

    if (selectedTags.length >= MAX_TAG_COUNT) {
      setTagError(`最多可添加 ${MAX_TAG_COUNT} 个标签`);
      return;
    }

    const exists = allTagNames.some(name => name.toLowerCase() === tagName.toLowerCase());
    if (!exists && !isAdmin) {
      setTagError('标签不存在，暂时仅管理员可创建新标签');
      return;
    }

    setSelectedTags(prev => [...prev, tagName]);
    setTagInput('');
    setTagError(null);
    setCategoryError(null);
  };

  const removeTag = (tagName: string) => {
    setSelectedTags(prev => prev.filter(tag => tag !== tagName));
    setTagError(null);
    setCategoryError(null);
  };

  const matchedTags = tagInput.trim()
    ? allTagNames
        .filter(name =>
          name.toLowerCase().includes(tagInput.trim().toLowerCase()) &&
          !selectedTags.some(selected => selected.toLowerCase() === name.toLowerCase())
        )
        .slice(0, 8)
    : [];

  const handleSubmit = async () => {
    if (!title.trim() || !content.trim()) {
      return;
    }

    if (selectedTags.length < MIN_TAG_COUNT) {
      setTagError(`请至少添加 ${MIN_TAG_COUNT} 个标签`);
      return;
    }

    if (selectedTags.length > MAX_TAG_COUNT) {
      setTagError(`最多可添加 ${MAX_TAG_COUNT} 个标签`);
      return;
    }

    if (!categoryId || categoryId <= 0) {
      setCategoryError('请先选择分类');
      return;
    }

    let pollRequest: CreatePollRequest | null = null;
    let lotteryRequest: CreateLotteryRequest | null = null;
    if (enablePoll) {
      const normalizedQuestion = pollQuestion.trim();
      if (!normalizedQuestion) {
        setPollError('投票问题不能为空');
        return;
      }

      const normalizedOptions = pollOptions
        .map(option => option.trim())
        .filter(Boolean);

      if (normalizedOptions.length < MIN_POLL_OPTION_COUNT || normalizedOptions.length > MAX_POLL_OPTION_COUNT) {
        setPollError(`投票选项数量必须在 ${MIN_POLL_OPTION_COUNT} 到 ${MAX_POLL_OPTION_COUNT} 个之间`);
        return;
      }

      const uniqueOptions = new Set(normalizedOptions.map(option => option.toLowerCase()));
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
      await onPublish(title, content, categoryId, selectedTags, isQuestionPost, pollRequest, lotteryRequest);
      // 发布成功后清空表单和草稿
      setTitle('');
      setContent('');
      setSelectedTags([]);
      setTagInput('');
      setCategoryId(selectedCategoryId);
      setTagError(null);
      setCategoryError(null);
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
    } catch (err) {
      log.error('发布失败:', err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLoginClick = () => {
    const loginUrl = getOidcLoginUrl();
    if (loginUrl) {
      window.location.href = loginUrl;
    }
  };

  // 处理图片上传
  const handleImageUpload = async (file: File) => {
    try {
      const result = await uploadImage({
        file,
        businessType: 'Post',
        generateThumbnail: true,
        generateMultipleSizes,
        addWatermark,
        watermarkText,
        removeExif: true
      }, t);

      return {
        url: appendImageMeta(result.voUrl, result.voUrl, imageScalePercent),
        thumbnailUrl: result.voThumbnailUrl
      };
    } catch (error) {
      log.error('图片上传失败:', error);
      throw error;
    }
  };

  // 处理文档上传
  const handleDocumentUpload = async (file: File) => {
    try {
      const result = await uploadDocument({
        file,
        businessType: 'Post'
      }, t);

      return {
        url: result.voUrl,
        fileName: result.voOriginalName || file.name
      };
    } catch (error) {
      log.error('文档上传失败:', error);
      throw error;
    }
  };

  const footer = (
    <div className={styles.footer}>
      <button
        onClick={handleSubmit}
      disabled={!title.trim() || !content.trim() || !categoryId || selectedTags.length < MIN_TAG_COUNT || isSubmitting}
      className={styles.publishButton}
      >
        {isSubmitting ? '发布中...' : '发布帖子'}
      </button>
      <button onClick={onClose} className={styles.cancelButton}>
        取消
      </button>
    </div>
  );

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
          onChange={(e) => setImageScalePercent(Number(e.target.value))}
          className={styles.editorScaleSelect}
        >
          {IMAGE_SCALE_OPTIONS.map(scale => (
            <option key={scale} value={scale}>{scale}%</option>
          ))}
        </select>
      </label>
    </div>
  );

  if (!isAuthenticated) {
    return (
      <BottomSheet isOpen={isOpen} onClose={onClose} title="发布新帖" height="70%">
        <div className={styles.loginPrompt}>
          <p>请先登录后再发帖</p>
          <button onClick={handleLoginClick} className={styles.loginButton}>
            前往登录
          </button>
        </div>
      </BottomSheet>
    );
  }

  return (
    <BottomSheet isOpen={isOpen} onClose={onClose} title="发布新帖" height="70%" footer={footer}>
      <div className={styles.container}>
        

        <div className={styles.titleRow}>
          <input
            type="text"
            placeholder="帖子标题"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className={styles.titleInput}
            maxLength={100}
          />
          <span className={styles.titleCount}>{title.length}/100</span>
        </div>

<div className={styles.editorWrapper}>
          <Suspense fallback={<div className={styles.editorLoading}>编辑器加载中...</div>}>
            <MarkdownEditor
              value={content}
              onChange={setContent}
              placeholder="帖子内容（支持 Markdown）"
              onImageUpload={handleImageUpload}
              onDocumentUpload={handleDocumentUpload}
              stickerGroups={stickerGroups}
              stickerMap={stickerMap}
              onStickerSelect={(selection) => {
                void handleStickerSelect(selection);
              }}
              minHeight={320}
              className={styles.editor}
              theme="light"
              toolbarExtras={editorToolbarExtras}
            />
          </Suspense>
        </div>

        {addWatermark && (
          <div className={styles.watermarkRow}>
            <span className={styles.watermarkLabel}>水印文字</span>
            <input
              type="text"
              placeholder="输入水印文字"
              value={watermarkText}
              onChange={(e) => setWatermarkText(e.target.value)}
              className={styles.watermarkInput}
            />
          </div>
        )}

<div className={styles.categorySection}>
          <div className={styles.categoryHeader}>
            <span className={styles.categoryLabel}>帖子分类</span>
            <span className={styles.categoryHint}>发布前请选择一个分类</span>
          </div>
          <select
            value={categoryId ?? ''}
            onChange={(e) => {
              const value = e.target.value;
              setCategoryId(value ? Number(value) : null);
              setCategoryError(null);
            }}
            className={styles.categorySelect}
            disabled={isSubmitting || categories.length === 0}
          >
            <option value="">请选择分类</option>
            {categories.map(category => (
              <option key={category.voId} value={category.voId}>
                {category.voName}
              </option>
            ))}
          </select>
          {categoryError && <p className={styles.categoryError}>{categoryError}</p>}
          {!categoryError && categories.length === 0 && <p className={styles.categoryError}>暂无可用分类</p>}
        </div>

        <div className={styles.tagSection}>
          <div className={styles.tagHeader}>
            <span className={styles.tagLabel}>帖子标签</span>
            <span className={styles.tagHint}>至少 {MIN_TAG_COUNT} 个，最多 {MAX_TAG_COUNT} 个</span>
          </div>
          <div className={styles.tagInputRow}>
            <input
              type="text"
              placeholder="输入标签名后按回车"
              value={tagInput}
              onChange={e => {
                setTagInput(e.target.value);
                if (tagError) {
                  setTagError(null);
                }
              }}
              onKeyDown={e => {
                if (e.key === 'Enter' || e.key === ',') {
                  e.preventDefault();
                  addTag(tagInput);
                }
              }}
              className={styles.tagInput}
              maxLength={50}
            />
            <button
              type="button"
              className={styles.addTagButton}
              onClick={() => addTag(tagInput)}
              disabled={!tagInput.trim()}
            >
              添加
            </button>
          </div>

          {matchedTags.length > 0 && (
            <div className={styles.suggestionWrap}>
              {matchedTags.map(tag => (
                <button
                  key={tag}
                  type="button"
                  className={styles.suggestionTag}
                  onClick={() => addTag(tag)}
                >
                  {tag}
                </button>
              ))}
            </div>
          )}

          {tagInput.trim() && matchedTags.length === 0 && (
            <p className={styles.tagTip}>
              {isAdmin ? '未匹配现有标签，发布时将创建新标签。' : '未匹配现有标签，暂时仅管理员可创建。'}
            </p>
          )}

          {selectedTags.length > 0 && (
            <div className={styles.selectedTagWrap}>
              {selectedTags.map(tag => (
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

          {tagError && <p className={styles.tagError}>{tagError}</p>}
        </div>

        <div className={`${styles.optionSection} ${styles.questionSection}`}>
          <div className={styles.optionSectionHeader}>
            <div>
              <span className={styles.optionSectionLabel}>问答帖</span>
              <p className={styles.optionSectionHint}>适合提问求助，发布后可继续提交回答并由提问者采纳</p>
            </div>
            <button
              type="button"
              className={`${styles.optionToggle} ${isQuestionPost ? styles.questionToggleActive : ''}`}
              onClick={() => {
                setIsQuestionPost((current) => {
                  const next = !current;
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
                  return next;
                });
              }}
            >
              {isQuestionPost ? '已开启' : '开启'}
            </button>
          </div>
          {isQuestionPost && (
            <p className={styles.optionNotice}>问答帖与附带投票、附带抽奖暂时互斥，当前将以问答模式发布。</p>
          )}
        </div>

        <div className={`${styles.optionSection} ${styles.pollSection}`}>
          <div className={styles.optionSectionHeader}>
            <div>
              <span className={styles.optionSectionLabel}>附带投票</span>
              <p className={styles.optionSectionHint}>支持 2 到 6 个单选项，适合快速收集社区反馈</p>
            </div>
            <button
              type="button"
              className={`${styles.optionToggle} ${enablePoll ? styles.pollToggleActive : ''}`}
              onClick={() => {
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
                  }
                  return next;
                });
                setPollError(null);
              }}
            >
              {enablePoll ? '已开启' : '开启'}
            </button>
          </div>

          {!enablePoll && isQuestionPost && (
            <p className={styles.optionNotice}>已开启问答帖时，投票会保持关闭。</p>
          )}

          {enablePoll && (
            <div className={styles.pollFields}>
              <input
                type="text"
                placeholder="投票问题，例如：本周论坛最想先补什么？"
                value={pollQuestion}
                onChange={(event) => {
                  setPollQuestion(event.target.value);
                  if (pollError) {
                    setPollError(null);
                  }
                }}
                className={styles.pollQuestionInput}
                maxLength={200}
              />

              <div className={styles.pollOptionsList}>
                {pollOptions.map((option, index) => (
                  <div key={`poll-option-${index}`} className={styles.pollOptionRow}>
                    <span className={styles.pollOptionIndex}>{index + 1}</span>
                    <input
                      type="text"
                      placeholder={`选项 ${index + 1}`}
                      value={option}
                      onChange={(event) => updatePollOption(index, event.target.value)}
                      className={styles.pollOptionInput}
                      maxLength={100}
                    />
                    <button
                      type="button"
                      className={styles.pollOptionRemove}
                      onClick={() => removePollOption(index)}
                      disabled={pollOptions.length <= MIN_POLL_OPTION_COUNT}
                    >
                      删除
                    </button>
                  </div>
                ))}
              </div>

              <div className={styles.pollControls}>
                <button
                  type="button"
                  className={styles.pollAddButton}
                  onClick={addPollOption}
                  disabled={pollOptions.length >= MAX_POLL_OPTION_COUNT}
                >
                  添加选项
                </button>
                <label className={styles.pollEndTimeLabel}>
                  <span>截止时间</span>
                  <input
                    type="datetime-local"
                    value={pollEndTime}
                    onChange={(event) => {
                      setPollEndTime(event.target.value);
                      if (pollError) {
                        setPollError(null);
                      }
                    }}
                    className={styles.pollEndTimeInput}
                  />
                </label>
              </div>

              {pollError && <p className={styles.pollError}>{pollError}</p>}
            </div>
          )}
        </div>

        <div className={`${styles.optionSection} ${styles.lotterySection}`}>
          <div className={styles.optionSectionHeader}>
            <div>
              <span className={styles.optionSectionLabel}>附带抽奖</span>
              <p className={styles.optionSectionHint}>父评论参与，按用户去重，发帖者在开奖时间后手动开奖</p>
            </div>
            <button
              type="button"
              className={`${styles.optionToggle} ${enableLottery ? styles.lotteryToggleActive : ''}`}
              onClick={() => {
                setEnableLottery((current) => {
                  const next = !current;
                  if (next) {
                    setIsQuestionPost(false);
                    setEnablePoll(false);
                    setPollQuestion('');
                    setPollEndTime('');
                    setPollOptions([...DEFAULT_POLL_OPTIONS]);
                    setPollError(null);
                  }
                  return next;
                });
                setLotteryError(null);
              }}
            >
              {enableLottery ? '已开启' : '开启'}
            </button>
          </div>

          {!enableLottery && isQuestionPost && (
            <p className={styles.optionNotice}>已开启问答帖时，抽奖会保持关闭。</p>
          )}

          {!enableLottery && enablePoll && (
            <p className={styles.optionNotice}>已开启附带投票时，抽奖会保持关闭。</p>
          )}

          {enableLottery && (
            <div className={styles.lotteryFields}>
              <input
                type="text"
                placeholder="奖品名称，例如：论坛头像挂件一份"
                value={lotteryPrizeName}
                onChange={(event) => {
                  setLotteryPrizeName(event.target.value);
                  if (lotteryError) {
                    setLotteryError(null);
                  }
                }}
                className={styles.lotteryInput}
                maxLength={100}
              />

              <textarea
                placeholder="奖品说明，例如：抽中后私信联系领取方式"
                value={lotteryPrizeDescription}
                onChange={(event) => {
                  setLotteryPrizeDescription(event.target.value);
                  if (lotteryError) {
                    setLotteryError(null);
                  }
                }}
                className={styles.lotteryTextarea}
                maxLength={500}
                rows={3}
              />

              <div className={styles.lotteryControls}>
                <label className={styles.lotteryFieldLabel}>
                  <span>开奖时间</span>
                  <input
                    type="datetime-local"
                    value={lotteryDrawTime}
                    onChange={(event) => {
                      setLotteryDrawTime(event.target.value);
                      if (lotteryError) {
                        setLotteryError(null);
                      }
                    }}
                    className={styles.lotteryInput}
                  />
                </label>
                <label className={styles.lotteryFieldLabel}>
                  <span>中奖人数</span>
                  <input
                    type="number"
                    min={MIN_LOTTERY_WINNER_COUNT}
                    max={MAX_LOTTERY_WINNER_COUNT}
                    value={lotteryWinnerCount}
                    onChange={(event) => {
                      setLotteryWinnerCount(event.target.value);
                      if (lotteryError) {
                        setLotteryError(null);
                      }
                    }}
                    className={styles.lotteryInput}
                  />
                </label>
              </div>

              <p className={styles.lotteryHint}>
                发布一条顶级评论即可参与抽奖，回复他人评论不计入参与资格，发帖者本人不进入中奖池。
              </p>

              {lotteryError && <p className={styles.lotteryError}>{lotteryError}</p>}
            </div>
          )}
        </div>

        

        
      </div>
    </BottomSheet>
  );
};

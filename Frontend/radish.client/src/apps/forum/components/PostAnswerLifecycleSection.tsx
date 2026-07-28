import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ApiResponseError,
  ForumQuestionErrorCode,
  acceptPostAnswer,
  createApiResponseError,
  createPostAnswer,
  deletePostAnswer,
  getPostAnswerRevision,
  getPostAnswerRevisions,
  restorePostAnswerRevision,
  revokePostAnswerAcceptance,
  updatePostAnswer,
  type ParsedApiResponse,
  type PostAnswerPageVo,
  type PostAnswerRevisionDetailVo,
  type PostAnswerRevisionListVo,
  type PostAnswerSort,
  type PostAnswerVo,
} from '@radish/http';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Modal } from '@radish/ui/modal';
import { Icon } from '@radish/ui/icon';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import { MarkdownRenderer, type MarkdownStickerMap } from '@radish/ui/markdown-renderer';
import type { StickerPickerGroup } from '@radish/ui/sticker-picker';
import { uploadDocument, uploadImage } from '@/api/attachment';
import type { LongId } from '@/api/user';
import { createMarkdownEditorLabels } from '@/i18n/markdownEditorLabels';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import {
  createClientSubmissionState,
  type ClientSubmissionState,
} from '@/utils/clientSubmission';
import { log } from '@/utils/logger';
import { resolveMediaUrl } from '@/utils/media';
import {
  buildAnswerAcceptanceFingerprint,
  buildAnswerCreateFingerprint,
  buildAnswerDeleteFingerprint,
  buildAnswerEditFingerprint,
  buildAnswerRestoreFingerprint,
} from '../utils/forumSubmissionFingerprint';
import styles from './PostAnswerLifecycleSection.module.css';

const MarkdownEditor = lazy(() =>
  import('@radish/ui/markdown-editor').then((module) => ({ default: module.MarkdownEditor }))
);

const MOBILE_QUERY = '(max-width: 768px)';

type DialogState =
  | { kind: 'edit'; answer: PostAnswerVo }
  | { kind: 'history'; answer: PostAnswerVo }
  | { kind: 'delete'; answer: PostAnswerVo }
  | { kind: 'accept'; answer: PostAnswerVo; replacing: boolean }
  | { kind: 'revoke'; answer: PostAnswerVo };

interface PostAnswerLifecycleSectionProps {
  postIdentifier: string;
  answerPage: PostAnswerPageVo | null;
  loading: boolean;
  error: string | null;
  sort: PostAnswerSort;
  pageIndex: number;
  targetAnswerPublicId?: string;
  targetUnavailable?: boolean;
  isAuthenticated: boolean;
  isQuestionAuthor: boolean;
  currentUserId?: LongId;
  displayTimeZone: string;
  stickerGroups?: StickerPickerGroup[];
  stickerMap?: MarkdownStickerMap;
  autoFocusKey?: string | null;
  onRequireLogin: () => void;
  onPageChange: (pageIndex: number) => void;
  onSortChange: (sort: PostAnswerSort) => void;
  onReload: () => Promise<void> | void;
  onAuthorClick?: (userPublicId: string) => void;
  onReport?: (answerId: LongId) => void;
  onUploadingChange?: (uploading: boolean) => void;
}

function useMobileDialog(): boolean {
  const [isMobile, setIsMobile] = useState(() => (
    typeof window !== 'undefined' && window.matchMedia(MOBILE_QUERY).matches
  ));

  useEffect(() => {
    const media = window.matchMedia(MOBILE_QUERY);
    const handleChange = () => setIsMobile(media.matches);
    handleChange();
    media.addEventListener('change', handleChange);
    return () => media.removeEventListener('change', handleChange);
  }, []);

  return isMobile;
}

function requireData<T>(response: ParsedApiResponse<T>, fallbackMessage: string): T {
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }
  return response.data;
}

const isSameId = (
  left: string | number | null | undefined,
  right: string | number | null | undefined,
): boolean => (
  left != null && right != null && String(left) === String(right)
);

const avatarText = (name: string): string => name.trim().slice(0, 1).toUpperCase() || '?';

const avatarStyle = (seed: string) => {
  let hash = 0;
  for (let index = 0; index < seed.length; index += 1) {
    hash = seed.charCodeAt(index) + ((hash << 5) - hash);
  }
  const hue = Math.abs(hash) % 360;
  return {
    backgroundColor: `hsl(${hue} 42% 90%)`,
    color: `hsl(${hue} 42% 28%)`,
  };
};

export const PostAnswerLifecycleSection = ({
  postIdentifier,
  answerPage,
  loading,
  error,
  sort,
  pageIndex,
  targetAnswerPublicId,
  targetUnavailable = false,
  isAuthenticated,
  isQuestionAuthor,
  currentUserId,
  displayTimeZone,
  stickerGroups = [],
  stickerMap,
  autoFocusKey,
  onRequireLogin,
  onPageChange,
  onSortChange,
  onReload,
  onAuthorClick,
  onReport,
  onUploadingChange,
}: PostAnswerLifecycleSectionProps) => {
  const { t, i18n } = useTranslation();
  const isMobile = useMobileDialog();
  const sectionRef = useRef<HTMLElement | null>(null);
  const composerRef = useRef<HTMLDivElement | null>(null);
  const createSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const actionSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const [composerContent, setComposerContent] = useState('');
  const [composerUploading, setComposerUploading] = useState(false);
  const [editUploading, setEditUploading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [dialogState, setDialogState] = useState<DialogState | null>(null);
  const [editContent, setEditContent] = useState('');
  const [actionBusy, setActionBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionMessage, setActionMessage] = useState<string | null>(null);
  const [revisionList, setRevisionList] = useState<PostAnswerRevisionListVo | null>(null);
  const [revisionDetail, setRevisionDetail] = useState<PostAnswerRevisionDetailVo | null>(null);
  const [revisionLoading, setRevisionLoading] = useState(false);
  const [highlightedPublicId, setHighlightedPublicId] = useState<string | null>(null);
  const editorLabels = useMemo(
    () => createMarkdownEditorLabels(t, i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage, t],
  );
  const unknownTimeLabel = t('forum.postCard.unknownTime');
  const acceptedAnswer = answerPage?.voAcceptedAnswer ?? null;
  const otherAnswers = answerPage?.voItems ?? [];
  const totalOtherAnswers = answerPage?.voOtherTotal ?? 0;
  const pageSize = answerPage?.voPageSize ?? 20;
  const totalPages = Math.max(1, Math.ceil(totalOtherAnswers / pageSize));

  useEffect(() => {
    if (!autoFocusKey || !isAuthenticated) {
      return;
    }
    const frameId = window.requestAnimationFrame(() => {
      composerRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      composerRef.current?.querySelector<HTMLElement>('textarea, [contenteditable="true"]')?.focus();
    });
    return () => window.cancelAnimationFrame(frameId);
  }, [autoFocusKey, isAuthenticated]);

  useEffect(() => {
    if (!targetAnswerPublicId || loading || targetUnavailable) {
      return;
    }
    const selector = `[data-answer-public-id="${targetAnswerPublicId}"]`;
    const target = sectionRef.current?.querySelector<HTMLElement>(selector);
    if (!target) {
      return;
    }
    const frameId = window.requestAnimationFrame(() => {
      target.scrollIntoView({ behavior: 'smooth', block: 'center' });
      target.focus({ preventScroll: true });
      setHighlightedPublicId(targetAnswerPublicId);
    });
    const timerId = window.setTimeout(() => setHighlightedPublicId(null), 3200);
    return () => {
      window.cancelAnimationFrame(frameId);
      window.clearTimeout(timerId);
    };
  }, [loading, pageIndex, targetAnswerPublicId, targetUnavailable]);

  const handleUploadingChange = useCallback((uploading: boolean) => {
    setComposerUploading(uploading);
  }, []);

  useEffect(() => {
    onUploadingChange?.(composerUploading || editUploading);
  }, [composerUploading, editUploading, onUploadingChange]);

  const uploadAnswerImage = useCallback(async (
    file: File,
    reportProgress: (progress: number) => void,
  ): Promise<MarkdownImageUploadResult> => {
    const result = await uploadImage({
      file,
      businessType: 'PostAnswer',
      generateThumbnail: true,
      generateMultipleSizes: false,
      removeExif: true,
      onProgress: reportProgress,
    }, t);
    return {
      attachmentId: result.voId,
      displayVariant: result.voThumbnailUrl ? 'thumbnail' : 'original',
      previewUrl: buildAttachmentAssetUrl(
        result.voId,
        result.voThumbnailUrl ? 'thumbnail' : 'original',
      ),
    };
  }, [t]);

  const uploadAnswerDocument = useCallback(async (
    file: File,
    reportProgress: (progress: number) => void,
  ): Promise<MarkdownDocumentUploadResult> => {
    const result = await uploadDocument({
      file,
      businessType: 'PostAnswer',
      onProgress: reportProgress,
    }, t);
    return {
      attachmentId: result.voId,
      fileName: result.voOriginalName || file.name,
    };
  }, [t]);

  const handleUploadError = useCallback((kind: 'image' | 'document', uploadError: unknown) => {
    log.error('PostAnswerLifecycleSection', `Answer ${kind} upload failed:`, uploadError);
  }, []);

  const reloadAfterMutation = useCallback(async (message: string) => {
    setActionMessage(message);
    await onReload();
  }, [onReload]);

  const resolveMutationError = useCallback((mutationError: unknown): string => {
    if (mutationError instanceof ApiResponseError) {
      if (
        mutationError.code === ForumQuestionErrorCode.Conflict
        || mutationError.code === ForumQuestionErrorCode.AcceptanceConflict
      ) {
        return t('forum.answerLifecycle.conflict');
      }
      if (mutationError.code === ForumQuestionErrorCode.AcceptedAnswerLocked) {
        return t('forum.answerLifecycle.acceptedLocked');
      }
      if (
        mutationError.code === ForumQuestionErrorCode.NotFound
        || mutationError.code === ForumQuestionErrorCode.AnswerNotFound
      ) {
        return t('forum.answerLifecycle.unavailable');
      }
    }
    return mutationError instanceof Error
      ? mutationError.message
      : t('forum.answerLifecycle.actionFailed');
  }, [t]);

  const handleCreate = async () => {
    const content = composerContent.trim();
    if (!content || composerUploading) {
      return;
    }
    if (!isAuthenticated) {
      onRequireLogin();
      return;
    }

    const submission = createClientSubmissionState(
      createSubmissionRef.current,
      'forum-answer',
      buildAnswerCreateFingerprint(postIdentifier, content),
    );
    createSubmissionRef.current = submission;
    setSubmitting(true);
    setActionMessage(null);
    setActionError(null);
    try {
      requireData(await createPostAnswer({
        postIdentifier,
        content,
        clientSubmissionId: submission.clientSubmissionId,
      }), t('forum.public.answerFailed'));
      createSubmissionRef.current = null;
      setComposerContent('');
      await reloadAfterMutation(t('forum.public.answerPublished'));
    } catch (mutationError) {
      setActionError(resolveMutationError(mutationError));
    } finally {
      setSubmitting(false);
    }
  };

  const openDialog = useCallback((state: DialogState) => {
    setDialogState(state);
    setEditContent(state.answer.voContent);
    setEditUploading(false);
    setActionError(null);
    setActionMessage(null);
    setRevisionList(null);
    setRevisionDetail(null);
  }, []);

  const closeDialog = useCallback(() => {
    if (!actionBusy && !revisionLoading && !editUploading) {
        setDialogState(null);
        setActionError(null);
    }
  }, [actionBusy, editUploading, revisionLoading]);

  const loadRevisionDetail = useCallback(async (
    answerPublicId: string,
    revisionNumber: number,
  ) => {
    setRevisionLoading(true);
    setActionError(null);
    try {
      setRevisionDetail(requireData(
        await getPostAnswerRevision(answerPublicId, revisionNumber),
        t('forum.answerLifecycle.historyLoadFailed'),
      ));
    } catch (revisionError) {
      setActionError(resolveMutationError(revisionError));
    } finally {
      setRevisionLoading(false);
    }
  }, [resolveMutationError, t]);

  useEffect(() => {
    if (dialogState?.kind !== 'history') {
      return;
    }
    let cancelled = false;
    const load = async () => {
      setRevisionLoading(true);
      setActionError(null);
      try {
        const list = requireData(
          await getPostAnswerRevisions(dialogState.answer.voPublicId),
          t('forum.answerLifecycle.historyLoadFailed'),
        );
        if (cancelled) {
          return;
        }
        setRevisionList(list);
        const initial = list.voItems.find((item) => !item.voIsCurrent) ?? list.voItems[0];
        if (initial) {
          await loadRevisionDetail(dialogState.answer.voPublicId, initial.voRevisionNumber);
        }
      } catch (revisionError) {
        if (!cancelled) {
          setActionError(resolveMutationError(revisionError));
        }
      } finally {
        if (!cancelled) {
          setRevisionLoading(false);
        }
      }
    };
    void load();
    return () => {
      cancelled = true;
    };
  }, [dialogState, loadRevisionDetail, resolveMutationError, t]);

  const runDialogMutation = async () => {
    if (!dialogState) {
      return;
    }
    if (dialogState.kind === 'edit' && editUploading) {
      return;
    }
    const answer = dialogState.answer;
    setActionBusy(true);
    setActionError(null);
    try {
      if (dialogState.kind === 'edit') {
        const normalized = editContent.trim();
        const submission = createClientSubmissionState(
          actionSubmissionRef.current,
          'forum-answer-edit',
          buildAnswerEditFingerprint(answer.voPublicId, normalized, answer.voContentRevision),
        );
        actionSubmissionRef.current = submission;
        requireData(await updatePostAnswer({
          answerPublicId: answer.voPublicId,
          content: normalized,
          expectedContentRevision: answer.voContentRevision,
          clientSubmissionId: submission.clientSubmissionId,
        }), t('forum.answerLifecycle.editFailed'));
        await reloadAfterMutation(t('forum.answerLifecycle.editSuccess'));
      } else if (dialogState.kind === 'delete') {
        const submission = createClientSubmissionState(
          actionSubmissionRef.current,
          'forum-answer-delete',
          buildAnswerDeleteFingerprint(answer.voPublicId, answer.voContentRevision),
        );
        actionSubmissionRef.current = submission;
        requireData(await deletePostAnswer({
          answerPublicId: answer.voPublicId,
          expectedContentRevision: answer.voContentRevision,
          clientSubmissionId: submission.clientSubmissionId,
        }), t('forum.answerLifecycle.deleteFailed'));
        await reloadAfterMutation(t('forum.answerLifecycle.deleteSuccess'));
      } else if (dialogState.kind === 'accept') {
        const acceptanceRevision = answerPage?.voAcceptanceRevision ?? 0;
        const submission = createClientSubmissionState(
          actionSubmissionRef.current,
          'forum-answer-accept',
          buildAnswerAcceptanceFingerprint(
            postIdentifier,
            answer.voPublicId,
            acceptanceRevision,
            'accept',
          ),
        );
        actionSubmissionRef.current = submission;
        requireData(await acceptPostAnswer({
          postIdentifier,
          answerPublicId: answer.voPublicId,
          expectedAcceptanceRevision: acceptanceRevision,
          clientSubmissionId: submission.clientSubmissionId,
        }), t('forum.public.answerAcceptFailed'));
        await reloadAfterMutation(
          dialogState.replacing
            ? t('forum.answerLifecycle.replaceSuccess')
            : t('forum.public.answerAccepted'),
        );
      } else if (dialogState.kind === 'revoke') {
        const acceptanceRevision = answerPage?.voAcceptanceRevision ?? 0;
        const submission = createClientSubmissionState(
          actionSubmissionRef.current,
          'forum-answer-revoke',
          buildAnswerAcceptanceFingerprint(
            postIdentifier,
            answer.voPublicId,
            acceptanceRevision,
            'revoke',
          ),
        );
        actionSubmissionRef.current = submission;
        requireData(await revokePostAnswerAcceptance({
          postIdentifier,
          expectedAcceptanceRevision: acceptanceRevision,
          clientSubmissionId: submission.clientSubmissionId,
        }), t('forum.answerLifecycle.revokeFailed'));
        await reloadAfterMutation(t('forum.answerLifecycle.revokeSuccess'));
      }
      actionSubmissionRef.current = null;
      setDialogState(null);
    } catch (mutationError) {
      setActionError(resolveMutationError(mutationError));
      if (
        mutationError instanceof ApiResponseError
        && (
          mutationError.code === ForumQuestionErrorCode.Conflict
          || mutationError.code === ForumQuestionErrorCode.AcceptanceConflict
        )
      ) {
        await onReload();
      }
    } finally {
      setActionBusy(false);
    }
  };

  const restoreRevision = async () => {
    if (dialogState?.kind !== 'history' || !revisionDetail) {
      return;
    }
    const answer = dialogState.answer;
    const submission = createClientSubmissionState(
      actionSubmissionRef.current,
      'forum-answer-restore',
      buildAnswerRestoreFingerprint(
        answer.voPublicId,
        revisionDetail.voRevisionNumber,
        answer.voContentRevision,
      ),
    );
    actionSubmissionRef.current = submission;
    setActionBusy(true);
    setActionError(null);
    try {
      requireData(await restorePostAnswerRevision({
        answerPublicId: answer.voPublicId,
        revisionNumber: revisionDetail.voRevisionNumber,
        expectedContentRevision: answer.voContentRevision,
        clientSubmissionId: submission.clientSubmissionId,
      }), t('forum.answerLifecycle.restoreFailed'));
      actionSubmissionRef.current = null;
      await reloadAfterMutation(t('forum.answerLifecycle.restoreSuccess'));
      setDialogState(null);
    } catch (mutationError) {
      setActionError(resolveMutationError(mutationError));
      if (mutationError instanceof ApiResponseError && mutationError.code === ForumQuestionErrorCode.Conflict) {
        await onReload();
      }
    } finally {
      setActionBusy(false);
    }
  };

  const renderAnswer = (answer: PostAnswerVo, accepted: boolean): ReactNode => {
    const authorName = answer.voAuthorName || t('forum.postCard.anonymousUser');
    const avatarUrl = resolveMediaUrl(answer.voAuthorAvatarUrl);
    const canAccept = (
      isQuestionAuthor
      && !isSameId(answer.voAuthorId, currentUserId)
      && !accepted
    );
    const replacing = Boolean(answerPage?.voAcceptedAnswerPublicId);
    const highlighted = highlightedPublicId === answer.voPublicId;
    const isAnswerAuthor = isSameId(answer.voAuthorId, currentUserId);
    const canOpenAuthor = Boolean(answer.voAuthorPublicId && onAuthorClick);

    return (
      <article
        key={answer.voPublicId}
        className={`${styles.answerCard} ${accepted ? styles.answerCardAccepted : ''} ${
          highlighted ? styles.answerCardHighlighted : ''
        }`}
        data-answer-public-id={answer.voPublicId}
        tabIndex={-1}
      >
        <header className={styles.answerHeader}>
          <button
            type="button"
            className={styles.authorButton}
            onClick={() => {
              if (answer.voAuthorPublicId) {
                onAuthorClick?.(answer.voAuthorPublicId);
              }
            }}
            disabled={!canOpenAuthor}
          >
            <span
              className={styles.avatar}
              style={avatarUrl ? undefined : avatarStyle(authorName)}
            >
              {avatarUrl
                ? <img src={avatarUrl} alt="" loading="lazy" />
                : avatarText(authorName)}
            </span>
            <span>
              <strong>{authorName}</strong>
              <small>
                {formatDateTimeByTimeZone(answer.voCreateTime, displayTimeZone, unknownTimeLabel)}
                {answer.voEditCount > 0
                  ? ` · ${t('forum.answerLifecycle.version', { version: answer.voContentRevision })}`
                  : ''}
              </small>
            </span>
          </button>
          <div className={styles.answerActions}>
            {accepted && <span className={styles.acceptedBadge}>{t('forum.postDetail.question.bestAnswer')}</span>}
            {answer.voCanEdit && (
              <button type="button" onClick={() => openDialog({ kind: 'edit', answer })}>
                {t('common.edit')}
              </button>
            )}
            {isAnswerAuthor && (
              <button type="button" onClick={() => openDialog({ kind: 'history', answer })}>
                {t('forum.answerLifecycle.history')}
              </button>
            )}
            {answer.voCanDelete && (
              <button type="button" onClick={() => openDialog({ kind: 'delete', answer })}>
                {t('common.delete')}
              </button>
            )}
            {answer.voCanReport && onReport && (
              <button type="button" onClick={() => onReport(String(answer.voAnswerId))}>
                {t('report.action')}
              </button>
            )}
            {canAccept && (
              <button
                type="button"
                className={styles.primaryAction}
                onClick={() => openDialog({ kind: 'accept', answer, replacing })}
              >
                {replacing
                  ? t('forum.answerLifecycle.replace')
                  : t('forum.postDetail.question.accept')}
              </button>
            )}
            {accepted && isQuestionAuthor && (
              <button
                type="button"
                className={styles.dangerAction}
                onClick={() => openDialog({ kind: 'revoke', answer })}
              >
                {t('forum.answerLifecycle.revoke')}
              </button>
            )}
          </div>
        </header>
        <MarkdownRenderer
          content={answer.voContent}
          className={styles.answerContent}
          stickerMap={stickerMap}
        />
        {accepted && isAnswerAuthor && (
          <p className={styles.acceptedLock}>{t('forum.answerLifecycle.acceptedLocked')}</p>
        )}
      </article>
    );
  };

  const renderDialogBody = (): ReactNode => {
    if (!dialogState) {
      return null;
    }
    if (dialogState.kind === 'edit') {
      return (
        <div className={styles.dialogBody}>
          <p className={styles.dialogHint}>
            {t('forum.answerLifecycle.editRevision', {
              version: dialogState.answer.voContentRevision,
            })}
          </p>
          <Suspense fallback={<div>{t('forum.postDetail.question.editorLoading')}</div>}>
            <MarkdownEditor
              value={editContent}
              onChange={setEditContent}
              labels={editorLabels}
              defaultMode="edit"
              minHeight={240}
              disabled={actionBusy}
              showToolbar={true}
              theme="light"
              onImageUpload={uploadAnswerImage}
              onDocumentUpload={uploadAnswerDocument}
              onUploadError={handleUploadError}
              onUploadingChange={(uploading) => {
                setEditUploading(uploading);
              }}
              stickerGroups={stickerGroups}
              stickerMap={stickerMap}
            />
          </Suspense>
        </div>
      );
    }
    if (dialogState.kind === 'history') {
      return (
        <div className={styles.historyWorkspace}>
          <div className={styles.historyList}>
            {revisionList?.voItems.map((revision) => (
              <button
                type="button"
                key={revision.voRevisionNumber}
                className={
                  revisionDetail?.voRevisionNumber === revision.voRevisionNumber
                    ? styles.historyItemSelected
                    : undefined
                }
                onClick={() => void loadRevisionDetail(
                  dialogState.answer.voPublicId,
                  revision.voRevisionNumber,
                )}
                disabled={revisionLoading}
              >
                <strong>v{revision.voRevisionNumber}</strong>
                <span>
                  {revision.voIsCurrent
                    ? t('forum.answerLifecycle.current')
                    : revision.voSourceType === 'Restore'
                      ? t('forum.answerLifecycle.restored')
                      : t('forum.answerLifecycle.complete')}
                </span>
              </button>
            ))}
          </div>
          <div className={styles.historyDetail}>
            {revisionLoading && <p>{t('forum.answerLifecycle.historyLoading')}</p>}
            {!revisionLoading && revisionDetail && (
              <>
                <div className={styles.historyDetailHeader}>
                  <strong>v{revisionDetail.voRevisionNumber}</strong>
                  <span>{revisionDetail.voEditorName}</span>
                </div>
                <pre>{revisionDetail.voContent}</pre>
                {revisionList?.voItems.find(
                  (item) => item.voRevisionNumber === revisionDetail.voRevisionNumber,
                )?.voCanRestore && (
                  <button
                    type="button"
                    className={styles.dialogPrimaryButton}
                    onClick={() => void restoreRevision()}
                    disabled={actionBusy}
                  >
                    {actionBusy
                      ? t('forum.answerLifecycle.restoring')
                      : t('forum.answerLifecycle.restore')}
                  </button>
                )}
              </>
            )}
          </div>
        </div>
      );
    }

    const description = dialogState.kind === 'delete'
      ? t('forum.answerLifecycle.deleteConfirm')
      : dialogState.kind === 'accept'
        ? dialogState.replacing
          ? t('forum.answerLifecycle.replaceConfirm')
          : t('forum.answerLifecycle.acceptConfirm')
        : t('forum.answerLifecycle.revokeConfirm');
    return <p className={styles.confirmCopy}>{description}</p>;
  };

  const dialogTitle = dialogState?.kind === 'edit'
    ? t('forum.answerLifecycle.edit')
    : dialogState?.kind === 'history'
      ? t('forum.answerLifecycle.history')
      : dialogState?.kind === 'delete'
        ? t('forum.answerLifecycle.delete')
        : dialogState?.kind === 'accept'
          ? dialogState.replacing
            ? t('forum.answerLifecycle.replace')
            : t('forum.postDetail.question.accept')
          : t('forum.answerLifecycle.revoke');

  const dialogFooter = dialogState && dialogState.kind !== 'history' ? (
    <div className={styles.dialogFooter}>
      <button type="button" onClick={closeDialog} disabled={actionBusy}>
        {t('common.cancel')}
      </button>
      <button
        type="button"
        className={styles.dialogPrimaryButton}
        onClick={() => void runDialogMutation()}
        disabled={
          actionBusy
          || editUploading
          || (dialogState.kind === 'edit' && !editContent.trim())
        }
      >
        {actionBusy ? t('common.loading') : t('common.confirm')}
      </button>
    </div>
  ) : undefined;

  const dialogContent = dialogState ? (
    <>
      {renderDialogBody()}
      {actionError && <p className={styles.actionError} role="alert">{actionError}</p>}
    </>
  ) : null;

  return (
    <section className={styles.section} ref={sectionRef}>
      <header className={styles.sectionHeader}>
        <div>
          <div className={styles.titleRow}>
            <span>{t('forum.postDetail.question.badge')}</span>
            <h5>{t('forum.postDetail.question.title')}</h5>
          </div>
          <p>
            {answerPage?.voIsSolved
              ? t('forum.postDetail.question.meta.solved')
              : t('forum.postDetail.question.meta.pending')}
            {' · '}
            {t('forum.postDetail.question.totalAnswers', {
              count: answerPage?.voTotal ?? 0,
            })}
          </p>
        </div>
        <div className={styles.sortActions} aria-label={t('forum.postDetail.question.sort')}>
          {(['default', 'latest'] as const).map((value) => (
            <button
              type="button"
              key={value}
              className={sort === value ? styles.sortActive : undefined}
              onClick={() => onSortChange(value)}
              aria-pressed={sort === value}
            >
              {value === 'default'
                ? t('forum.postDetail.question.sortDefault')
                : t('forum.postDetail.question.sortLatest')}
            </button>
          ))}
        </div>
      </header>

      {actionMessage && <p className={styles.actionMessage} role="status">{actionMessage}</p>}
      {actionError && !dialogState && <p className={styles.actionError} role="alert">{actionError}</p>}
      {targetUnavailable && (
        <div className={styles.unavailable} role="status">
          <Icon icon="mdi:alert-circle-outline" size={20} />
          <span>{t('forum.answerLifecycle.targetUnavailable')}</span>
        </div>
      )}
      {loading && <p className={styles.stateText}>{t('forum.answerLifecycle.loading')}</p>}
      {!loading && error && (
        <div className={styles.unavailable} role="alert">
          <span>{error}</span>
          <button type="button" onClick={() => void onReload()}>{t('common.retry')}</button>
        </div>
      )}

      {!loading && !error && (
        <>
          {acceptedAnswer && (
            <div className={styles.acceptedRegion}>
              <h6>{t('forum.postDetail.question.bestAnswer')}</h6>
              {renderAnswer(acceptedAnswer, true)}
            </div>
          )}

          <div className={styles.otherRegion}>
            <div className={styles.otherHeader}>
              <h6>{t('forum.answerLifecycle.otherAnswers')}</h6>
              <span>{t('common.pageInfo', { current: pageIndex, total: totalPages })}</span>
            </div>
            {otherAnswers.length > 0
              ? <div className={styles.answerList}>{otherAnswers.map((answer) => renderAnswer(answer, false))}</div>
              : <p className={styles.stateText}>{t('forum.postDetail.question.emptyAll')}</p>}
            {totalPages > 1 && (
              <nav className={styles.pagination} aria-label={t('forum.answerLifecycle.pagination')}>
                <button
                  type="button"
                  disabled={pageIndex <= 1 || loading}
                  onClick={() => onPageChange(pageIndex - 1)}
                >
                  {t('common.previousPage')}
                </button>
                <span>{pageIndex} / {totalPages}</span>
                <button
                  type="button"
                  disabled={pageIndex >= totalPages || loading}
                  onClick={() => onPageChange(pageIndex + 1)}
                >
                  {t('common.nextPage')}
                </button>
              </nav>
            )}
          </div>

          <div className={styles.composer} ref={composerRef}>
            <label>{t('forum.postDetail.question.composeLabel')}</label>
            <Suspense fallback={<div>{t('forum.postDetail.question.editorLoading')}</div>}>
              <MarkdownEditor
                value={composerContent}
                onChange={setComposerContent}
                labels={editorLabels}
                placeholder={isAuthenticated
                  ? t('forum.postDetail.question.placeholderLoggedIn')
                  : t('forum.postDetail.question.placeholderLoggedOut')}
                defaultMode="edit"
                minHeight={180}
                disabled={!isAuthenticated || submitting}
                showToolbar={true}
                theme="light"
                onImageUpload={uploadAnswerImage}
                onDocumentUpload={uploadAnswerDocument}
                onUploadError={handleUploadError}
                onUploadingChange={handleUploadingChange}
                stickerGroups={stickerGroups}
                stickerMap={stickerMap}
              />
            </Suspense>
            <div className={styles.composerFooter}>
              <span>
                {isAuthenticated
                  ? t('forum.postDetail.question.hintLoggedIn')
                  : t('forum.postDetail.question.hintLoggedOut')}
              </span>
              <button
                type="button"
                className={styles.submitButton}
                onClick={() => void handleCreate()}
                disabled={!composerContent.trim() || submitting || composerUploading}
              >
                {submitting
                  ? t('forum.postDetail.question.submitLoading')
                  : t('forum.postDetail.question.submit')}
              </button>
            </div>
          </div>
        </>
      )}

      {dialogState && (isMobile ? (
        <BottomSheet
          isOpen={true}
          onClose={closeDialog}
          closeLabel={t('common.close')}
          closeOnEscape={!actionBusy && !revisionLoading && !editUploading}
          closeOnOverlayClick={!actionBusy && !revisionLoading && !editUploading}
          title={dialogTitle}
          height={dialogState.kind === 'history' ? '88%' : '72%'}
          footer={dialogFooter}
        >
          {dialogContent}
        </BottomSheet>
      ) : (
        <Modal
          isOpen={true}
          onClose={closeDialog}
          closeLabel={t('common.close')}
          closeOnEscape={!actionBusy && !revisionLoading && !editUploading}
          closeOnOverlayClick={!actionBusy && !revisionLoading && !editUploading}
          title={dialogTitle}
          size={dialogState.kind === 'history' ? 'large' : 'medium'}
          footer={dialogFooter}
        >
          {dialogContent}
        </Modal>
      ))}
    </section>
  );
};

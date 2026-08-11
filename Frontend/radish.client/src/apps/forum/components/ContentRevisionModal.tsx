import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import {
  ApiResponseError,
  ForumContentRevisionErrorCode,
  type CommentContentRevisionDetailVo,
  type ForumContentRevisionSummaryVo,
  type ForumContentRevisionWriteResult,
  type PostContentRevisionDetailVo,
} from '@radish/http';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Button } from '@radish/ui/button';
import { Icon } from '@radish/ui/icon';
import { Modal } from '@radish/ui/modal';
import {
  ContentSnapshotDiff,
  type ContentSnapshot,
} from '@/components/content-diff/ContentSnapshotDiff';
import {
  getCommentRevisionDetail,
  getCommentEditHistory,
  getCommentRevisionList,
  getPostEditHistory,
  getPostRevisionDetail,
  getPostRevisionList,
  restoreCommentRevision,
  restorePostRevision,
  type CommentEditHistory,
  type PostEditHistory,
} from '@/api/forum';
import type { LongId } from '@/api/user';
import {
  createClientSubmissionState,
  type ClientSubmissionState,
} from '@/utils/clientSubmission';
import { log } from '@/utils/logger';
import { buildContentRevisionRestoreFingerprint } from '../utils/forumSubmissionFingerprint';
import styles from './ContentRevisionModal.module.css';

export type ContentRevisionTarget =
  | {
      kind: 'post';
      targetId: LongId;
      currentContentRevision: number;
    }
  | {
      kind: 'comment';
      targetId: LongId;
      currentContentRevision: number;
    };

export type ContentRevisionDetail = PostContentRevisionDetailVo | CommentContentRevisionDetailVo;

interface ContentRevisionModalProps {
  isOpen: boolean;
  target: ContentRevisionTarget | null;
  sessionKey: string;
  onClose: () => void;
  onRestored: (result: ForumContentRevisionWriteResult) => void | Promise<void>;
  onUseInEditor: (detail: ContentRevisionDetail) => void;
}

interface RevisionListState {
  isEdited: boolean;
  editCount: number;
  currentContentRevision: number;
  lastEditedAt?: string | null;
  canViewDetails: boolean;
  items: ForumContentRevisionSummaryVo[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

const PAGE_SIZE = 20;
const LEGACY_PAGE_SIZE = 10;
const MOBILE_QUERY = '(max-width: 768px)';

const isPostRevisionDetail = (
  detail: ContentRevisionDetail
): detail is PostContentRevisionDetailVo => 'voTitle' in detail;

const isSensitiveRevisionError = (error: unknown): boolean => (
  error instanceof ApiResponseError
  && (
    error.code === ForumContentRevisionErrorCode.AccessDenied
    || error.code === ForumContentRevisionErrorCode.NotFound
  )
);

const useMobileDialog = (): boolean => {
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
};

const formatDate = (value: string | null | undefined, locale: string): string => {
  if (!value) {
    return '-';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString(locale);
};

export const ContentRevisionModal = ({
  isOpen,
  target,
  sessionKey,
  onClose,
  onRestored,
  onUseInEditor,
}: ContentRevisionModalProps) => {
  const { t, i18n } = useTranslation();
  const isMobile = useMobileDialog();
  const locale = i18n.resolvedLanguage?.startsWith('zh') ? 'zh-CN' : 'en-US';
  const targetKind = target?.kind;
  const targetId = target?.targetId;
  const [list, setList] = useState<RevisionListState | null>(null);
  const [selectedRevisionId, setSelectedRevisionId] = useState<LongId | null>(null);
  const [selectedDetail, setSelectedDetail] = useState<ContentRevisionDetail | null>(null);
  const [comparisonMode, setComparisonMode] = useState<'previous' | 'current'>('previous');
  const [comparisonDetail, setComparisonDetail] = useState<ContentRevisionDetail | null>(null);
  const [comparisonRevisionNumber, setComparisonRevisionNumber] = useState<number | null>(null);
  const [loadingList, setLoadingList] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [loadingComparison, setLoadingComparison] = useState(false);
  const [restoring, setRestoring] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [comparisonError, setComparisonError] = useState<string | null>(null);
  const [comparisonStale, setComparisonStale] = useState(false);
  const [comparisonMissing, setComparisonMissing] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [accessRevoked, setAccessRevoked] = useState(false);
  const [confirmingRestore, setConfirmingRestore] = useState(false);
  const [hasRevisionConflict, setHasRevisionConflict] = useState(false);
  const [legacyExpanded, setLegacyExpanded] = useState(false);
  const [legacyItems, setLegacyItems] = useState<Array<PostEditHistory | CommentEditHistory>>([]);
  const [legacyTotal, setLegacyTotal] = useState(0);
  const [legacyPageIndex, setLegacyPageIndex] = useState(1);
  const [legacyLoading, setLegacyLoading] = useState(false);
  const [legacyError, setLegacyError] = useState<string | null>(null);
  const restoreSubmissionRef = useRef<ClientSubmissionState | null>(null);
  const requestEpochRef = useRef(0);
  const comparisonEpochRef = useRef(0);
  const comparisonDetailRef = useRef<ContentRevisionDetail | null>(null);
  const comparisonRevisionIdRef = useRef<LongId | null>(null);

  const clearSensitiveState = useCallback(() => {
    setSelectedDetail(null);
    setSelectedRevisionId(null);
    setComparisonMode('previous');
    setComparisonDetail(null);
    setComparisonRevisionNumber(null);
    setLoadingComparison(false);
    setComparisonError(null);
    setComparisonStale(false);
    setComparisonMissing(false);
    comparisonDetailRef.current = null;
    comparisonRevisionIdRef.current = null;
    comparisonEpochRef.current += 1;
    setLegacyExpanded(false);
    setLegacyItems([]);
    setLegacyTotal(0);
    setLegacyPageIndex(1);
    setLegacyError(null);
    setConfirmingRestore(false);
    restoreSubmissionRef.current = null;
  }, []);

  const loadDetail = useCallback(async (
    revisionId: LongId,
    options: { updateSelection?: boolean } = {}
  ): Promise<ContentRevisionDetail | null> => {
    if (!targetKind) {
      return null;
    }

    const requestEpoch = requestEpochRef.current;
    if (options.updateSelection !== false) {
      setLoadingDetail(true);
      setDetailError(null);
      setSelectedRevisionId(revisionId);
      setSelectedDetail(null);
      setComparisonDetail(null);
      setComparisonRevisionNumber(null);
      setLoadingComparison(false);
      setComparisonError(null);
      setComparisonStale(false);
      setComparisonMissing(false);
      comparisonDetailRef.current = null;
      comparisonRevisionIdRef.current = null;
      comparisonEpochRef.current += 1;
    }

    try {
      const detail = targetKind === 'post'
        ? await getPostRevisionDetail(String(revisionId))
        : await getCommentRevisionDetail(String(revisionId));
      if (requestEpoch !== requestEpochRef.current) {
        return null;
      }
      if (options.updateSelection !== false) {
        setSelectedDetail(detail);
      }
      return detail;
    } catch (error) {
      if (requestEpoch !== requestEpochRef.current) {
        return null;
      }
      if (isSensitiveRevisionError(error)) {
        clearSensitiveState();
        setAccessRevoked(true);
      } else if (options.updateSelection !== false) {
        setSelectedDetail(null);
        setDetailError(error instanceof Error ? error.message : String(error));
      }
      return null;
    } finally {
      if (
        requestEpoch === requestEpochRef.current
        && options.updateSelection !== false
      ) {
        setLoadingDetail(false);
      }
    }
  }, [clearSensitiveState, targetKind]);

  const loadList = useCallback(async (
    pageIndex = 1,
    options: { preserveSelection?: boolean } = {}
  ) => {
    if (!targetKind || targetId == null) {
      return;
    }

    const requestEpoch = requestEpochRef.current;
    setLoadingList(true);
    setListError(null);
    try {
      const response = targetKind === 'post'
        ? await getPostRevisionList(targetId, pageIndex, PAGE_SIZE)
        : await getCommentRevisionList(targetId, pageIndex, PAGE_SIZE);
      if (requestEpoch !== requestEpochRef.current) {
        return;
      }
      const nextList: RevisionListState = {
        isEdited: response.voIsEdited,
        editCount: response.voEditCount,
        currentContentRevision: response.voCurrentContentRevision,
        lastEditedAt: response.voLastEditedAt,
        canViewDetails: response.voCanViewDetails,
        items: response.voItems ?? [],
        total: response.voTotal,
        pageIndex: response.voPageIndex,
        pageSize: response.voPageSize,
      };
      setList(nextList);
      setAccessRevoked(false);

      if (!nextList.canViewDetails) {
        clearSensitiveState();
        return;
      }

      if (options.preserveSelection) {
        return;
      }

      const initialSummary = nextList.items.find(item => item.voIsCurrent && item.voCanViewSnapshot)
        ?? nextList.items.find(item => item.voCanViewSnapshot);
      if (initialSummary) {
        await loadDetail(String(initialSummary.voRevisionId));
      } else {
        clearSensitiveState();
      }
    } catch (error) {
      if (requestEpoch !== requestEpochRef.current) {
        return;
      }
      setList(null);
      clearSensitiveState();
      setListError(error instanceof Error ? error.message : String(error));
    } finally {
      if (requestEpoch === requestEpochRef.current) {
        setLoadingList(false);
      }
    }
  }, [clearSensitiveState, loadDetail, targetId, targetKind]);

  const loadComparison = useCallback(async (
    currentList: RevisionListState,
    selectedSummary: ForumContentRevisionSummaryVo,
    mode: 'previous' | 'current',
  ) => {
    if (!targetKind || targetId == null) {
      return;
    }

    const requestEpoch = requestEpochRef.current;
    const comparisonEpoch = ++comparisonEpochRef.current;
    setLoadingComparison(true);
    setComparisonError(null);
    setComparisonStale(false);
    setComparisonMissing(false);
    const fetchPage = async (pageIndex: number) => targetKind === 'post'
      ? await getPostRevisionList(targetId, pageIndex, PAGE_SIZE)
      : await getCommentRevisionList(targetId, pageIndex, PAGE_SIZE);
    let comparisonSummary = mode === 'current'
      ? currentList.items.find((item) => item.voIsCurrent && item.voCanViewSnapshot) ?? null
      : currentList.items
          .filter((item) => item.voCanViewSnapshot && item.voRevisionNumber < selectedSummary.voRevisionNumber)
          .sort((left, right) => right.voRevisionNumber - left.voRevisionNumber)[0] ?? null;

    try {
      if (mode === 'current' && !comparisonSummary && currentList.pageIndex > 1) {
        const firstPage = await fetchPage(1);
        comparisonSummary = (firstPage.voItems ?? []).find(
          (item) => item.voIsCurrent && item.voCanViewSnapshot,
        ) ?? null;
      } else if (
        mode === 'previous'
        && !comparisonSummary
        && selectedSummary.voRevisionNumber > 1
        && currentList.pageIndex * currentList.pageSize < currentList.total
      ) {
        const nextPage = await fetchPage(currentList.pageIndex + 1);
        comparisonSummary = (nextPage.voItems ?? [])
          .filter((item) => item.voCanViewSnapshot && item.voRevisionNumber < selectedSummary.voRevisionNumber)
          .sort((left, right) => right.voRevisionNumber - left.voRevisionNumber)[0] ?? null;
      }

      if (requestEpoch !== requestEpochRef.current || comparisonEpoch !== comparisonEpochRef.current) {
        return;
      }

      if (!comparisonSummary || String(comparisonSummary.voRevisionId) === String(selectedSummary.voRevisionId)) {
        setComparisonDetail(null);
        setComparisonRevisionNumber(null);
        setLoadingComparison(false);
        setComparisonError(null);
        setComparisonStale(false);
        setComparisonMissing(mode === 'previous');
        comparisonDetailRef.current = null;
        comparisonRevisionIdRef.current = null;
        return;
      }

      const nextRevisionId = comparisonSummary.voRevisionId;
      const preserveExisting = String(comparisonRevisionIdRef.current) === String(nextRevisionId);
      setComparisonDetail((current) => preserveExisting ? current : null);
      comparisonDetailRef.current = preserveExisting ? comparisonDetailRef.current : null;
      comparisonRevisionIdRef.current = String(nextRevisionId);
      setComparisonRevisionNumber(comparisonSummary.voRevisionNumber);
      setLoadingComparison(true);
      setComparisonError(null);
      setComparisonStale(false);
      setComparisonMissing(false);

      const detail = targetKind === 'post'
        ? await getPostRevisionDetail(String(nextRevisionId))
        : await getCommentRevisionDetail(String(nextRevisionId));
      if (requestEpoch !== requestEpochRef.current || comparisonEpoch !== comparisonEpochRef.current) {
        return;
      }
      setComparisonDetail(detail);
      comparisonDetailRef.current = detail;
      setLoadingComparison(false);
    } catch (error) {
      if (requestEpoch !== requestEpochRef.current || comparisonEpoch !== comparisonEpochRef.current) {
        return;
      }
      log.warn('ContentRevisionModal', 'Failed to load revision comparison baseline', error);
      setLoadingComparison(false);
      setComparisonError(t('forum.revision.comparisonUnavailable'));
      setComparisonStale(Boolean(comparisonDetailRef.current));
      setComparisonMissing(false);
    }
  }, [t, targetId, targetKind]);

  const loadLegacyHistory = useCallback(async (pageIndex = 1) => {
    if (!targetKind || targetId == null) {
      return;
    }

    const requestEpoch = requestEpochRef.current;
    setLegacyLoading(true);
    setLegacyError(null);
    try {
      const response = targetKind === 'post'
        ? await getPostEditHistory(targetId, pageIndex, LEGACY_PAGE_SIZE, t)
        : await getCommentEditHistory(targetId, pageIndex, LEGACY_PAGE_SIZE, t);
      if (requestEpoch !== requestEpochRef.current) {
        return;
      }
      setLegacyItems(response.voItems ?? []);
      setLegacyTotal(response.voTotal);
      setLegacyPageIndex(response.voPageIndex || pageIndex);
    } catch (error) {
      if (requestEpoch !== requestEpochRef.current) {
        return;
      }
      setLegacyItems([]);
      setLegacyTotal(0);
      setLegacyError(error instanceof Error ? error.message : String(error));
    } finally {
      if (requestEpoch === requestEpochRef.current) {
        setLegacyLoading(false);
      }
    }
  }, [t, targetId, targetKind]);

  useEffect(() => {
    const requestEpoch = requestEpochRef.current + 1;
    requestEpochRef.current = requestEpoch;
    setList(null);
    clearSensitiveState();
    setLoadingList(false);
    setLoadingDetail(false);
    setRestoring(false);
    setListError(null);
    setDetailError(null);
    setActionError(null);
    setAccessRevoked(false);
    setHasRevisionConflict(false);
    setLegacyExpanded(false);
    setLegacyItems([]);
    setLegacyTotal(0);
    setLegacyPageIndex(1);
    setLegacyError(null);

    if (isOpen && targetKind && targetId != null) {
      void loadList(1);
    }

    return () => {
      if (requestEpochRef.current === requestEpoch) {
        requestEpochRef.current += 1;
      }
    };
  }, [
    clearSensitiveState,
    isOpen,
    loadList,
    sessionKey,
    target?.currentContentRevision,
    targetId,
    targetKind,
  ]);

  useEffect(() => {
    if (!confirmingRestore) {
      return;
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !restoring) {
        event.preventDefault();
        setConfirmingRestore(false);
      }
    };
    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [confirmingRestore, restoring]);

  const selectedSummary = useMemo(
    () => list?.items.find(item => String(item.voRevisionId) === String(selectedRevisionId)) ?? null,
    [list?.items, selectedRevisionId]
  );
  useEffect(() => {
    if (!list?.canViewDetails || !selectedSummary || !selectedDetail) {
      return;
    }

    void loadComparison(list, selectedSummary, comparisonMode);
  }, [comparisonMode, list, loadComparison, selectedDetail, selectedSummary]);
  const handleComparisonModeChange = (nextMode: 'previous' | 'current') => {
    if (nextMode === comparisonMode) {
      return;
    }

    comparisonEpochRef.current += 1;
    comparisonDetailRef.current = null;
    comparisonRevisionIdRef.current = null;
    setComparisonMode(nextMode);
    setComparisonDetail(null);
    setComparisonRevisionNumber(null);
    setLoadingComparison(false);
    setComparisonError(null);
    setComparisonStale(false);
    setComparisonMissing(false);
  };
  const totalPages = list && list.total > 0
    ? Math.max(1, Math.ceil(list.total / list.pageSize))
    : 1;
  const selectedIsCurrent = Boolean(selectedSummary?.voIsCurrent);
  const legacyTotalPages = legacyTotal > 0
    ? Math.max(1, Math.ceil(legacyTotal / LEGACY_PAGE_SIZE))
    : 1;
  const canRestoreSelected = Boolean(
    selectedDetail
    && selectedSummary?.voCanRestore
    && !selectedIsCurrent
    && !restoring
  );

  const handleRestore = async () => {
    if (!target || !selectedDetail || !selectedSummary || !canRestoreSelected) {
      return;
    }

    setRestoring(true);
    const requestEpoch = requestEpochRef.current;
    setActionError(null);
    setHasRevisionConflict(false);
    const expectedContentRevision = list?.currentContentRevision ?? target.currentContentRevision;
    const submissionState = createClientSubmissionState(
      restoreSubmissionRef.current,
      `forum-${target.kind}-revision-restore`,
      buildContentRevisionRestoreFingerprint(
        target.kind,
        target.targetId,
        String(selectedSummary.voRevisionId),
        expectedContentRevision
      )
    );
    restoreSubmissionRef.current = submissionState;

    try {
      const request = {
        targetId: target.targetId,
        revisionId: selectedSummary.voRevisionId,
        expectedContentRevision,
        clientSubmissionId: submissionState.clientSubmissionId,
      };
      const result = target.kind === 'post'
        ? await restorePostRevision(request)
        : await restoreCommentRevision(request);
      if (requestEpoch !== requestEpochRef.current) {
        return;
      }
      restoreSubmissionRef.current = null;
      setConfirmingRestore(false);
      await onRestored(result);
      onClose();
    } catch (error) {
      if (requestEpoch !== requestEpochRef.current) {
        return;
      }
      const message = error instanceof Error ? error.message : String(error);
      setActionError(message);
      setConfirmingRestore(false);
      if (
        error instanceof ApiResponseError
        && error.code === ForumContentRevisionErrorCode.Conflict
      ) {
        setHasRevisionConflict(true);
        await loadList(1, { preserveSelection: true });
      } else if (isSensitiveRevisionError(error)) {
        clearSensitiveState();
        setAccessRevoked(true);
      }
    } finally {
      if (requestEpoch === requestEpochRef.current) {
        setRestoring(false);
      }
    }
  };

  const handleUseInEditor = () => {
    if (!selectedDetail) {
      return;
    }

    onUseInEditor(selectedDetail);
    onClose();
  };

  const handleDialogClose = () => {
    if (restoring) {
      return;
    }
    if (confirmingRestore) {
      setConfirmingRestore(false);
      return;
    }
    onClose();
  };

  const renderSummary = (): ReactNode => {
    if (!list) {
      return null;
    }

    return (
      <section className={styles.summaryCard} aria-label={t('forum.revision.summaryLabel')}>
        <div>
          <strong>{t('forum.revision.summary', { count: list.editCount })}</strong>
          <span>
            {t('forum.revision.currentVersion', { version: list.currentContentRevision })}
          </span>
        </div>
        <span>
          {list.lastEditedAt
            ? t('forum.revision.lastEditedAt', { time: formatDate(list.lastEditedAt, locale) })
            : t('forum.revision.neverEdited')}
        </span>
      </section>
    );
  };

  const renderRevisionList = (): ReactNode => {
    if (!list?.canViewDetails) {
      return (
        <div className={styles.publicBoundary}>
          <Icon icon="mdi:shield-lock-outline" size={22} />
          <div>
            <strong>{t('forum.revision.publicBoundaryTitle')}</strong>
            <p>{t('forum.revision.publicBoundaryDescription')}</p>
          </div>
        </div>
      );
    }

    return (
      <aside className={styles.timeline} aria-label={t('forum.revision.timelineLabel')}>
        {list.items.map(item => {
          const selected = String(item.voRevisionId) === String(selectedRevisionId);
          return (
            <button
              key={String(item.voRevisionId)}
              type="button"
              className={`${styles.timelineItem} ${selected ? styles.timelineItemSelected : ''}`}
              onClick={() => {
                if (item.voIsCurrent) {
                  setComparisonMode('previous');
                }
                void loadDetail(String(item.voRevisionId));
              }}
              disabled={!item.voCanViewSnapshot || loadingDetail}
              aria-pressed={selected}
            >
              <span className={styles.timelineItemTop}>
                <strong>v{item.voRevisionNumber}</strong>
                <span>{formatDate(item.voCreateTime, locale)}</span>
              </span>
              <span className={styles.timelineItemState}>
                {item.voIsCurrent
                  ? t('forum.revision.state.current')
                  : item.voIntegrityStatus === 'LegacyIncomplete'
                    ? t('forum.revision.state.legacyIncomplete')
                    : item.voSourceType === 'Restore'
                      ? t('forum.revision.state.restored', {
                          version: item.voRestoredFromRevisionNumber ?? '-'
                        })
                      : t('forum.revision.state.complete')}
              </span>
            </button>
          );
        })}
      </aside>
    );
  };

  const buildSnapshot = (detail: ContentRevisionDetail | null): ContentSnapshot | null => {
    if (!detail) {
      return null;
    }

    const fields = isPostRevisionDetail(detail) ? [
      { key: 'title', label: t('forum.revision.field.title'), value: detail.voTitle },
      { key: 'category', label: t('forum.revision.field.category'), value: detail.voCategoryName },
      { key: 'type', label: t('forum.revision.field.type'), value: detail.voContentType },
      {
        key: 'tags',
        label: t('forum.revision.field.tags'),
        value: detail.voTags.map((tag) => tag.voTagName).join(' / ') || '—',
      },
      {
        key: 'cover',
        label: t('forum.revision.field.cover'),
        value: detail.voCoverAttachmentId ? String(detail.voCoverAttachmentId) : '—',
      },
      {
        key: 'attachments',
        label: t('forum.revision.field.attachments'),
        value: detail.voAttachmentIds.map(String).join(' / ') || '—',
      },
    ] : [];

    return { content: detail.voContent, fields };
  };

  const renderDetail = (): ReactNode => {
    if (!list?.canViewDetails) {
      return null;
    }

    if (accessRevoked) {
      return (
        <div className={styles.safeState} role="alert">
          <Icon icon="mdi:shield-alert-outline" size={24} />
          <div>
            <strong>{t('forum.revision.accessRevokedTitle')}</strong>
            <p>{t('forum.revision.accessRevokedDescription')}</p>
          </div>
        </div>
      );
    }

    if (loadingDetail) {
      return <div className={styles.state}>{t('forum.revision.detailLoading')}</div>;
    }

    if (detailError) {
      return <div className={styles.error} role="alert">{detailError}</div>;
    }

    if (!selectedDetail || !selectedSummary) {
      return <div className={styles.state}>{t('forum.revision.selectVersion')}</div>;
    }

    const selectedIsCurrentVersion = Boolean(selectedSummary.voIsCurrent);
    const beforeDetail = comparisonMode === 'previous' ? comparisonDetail : selectedDetail;
    const afterDetail = comparisonMode === 'previous' ? selectedDetail : comparisonDetail;
    const beforeRevisionNumber = comparisonMode === 'previous'
      ? comparisonRevisionNumber
      : selectedSummary.voRevisionNumber;
    const afterRevisionNumber = comparisonMode === 'previous'
      ? selectedSummary.voRevisionNumber
      : comparisonRevisionNumber;
    const comparisonUnavailableText = comparisonMissing
      ? t('forum.revision.noEarlierVersion')
      : comparisonError || t('forum.revision.comparisonUnavailable');

    return (
      <div className={styles.detail}>
        <div className={styles.detailHeader}>
          <div>
            <h3>{t('forum.revision.targetVersion', { version: selectedSummary.voRevisionNumber })}</h3>
            <p>
              {t('forum.revision.editorMeta', {
                name: selectedSummary.voEditorName,
                time: formatDate(selectedSummary.voCreateTime, locale)
              })}
            </p>
          </div>
          <span className={`${styles.integrityBadge} ${
            selectedSummary.voCanRestore ? styles.integrityBadgeAvailable : ''
          }`}>
            {selectedSummary.voCanRestore
              ? t('forum.revision.restoreAvailable')
              : t('forum.revision.restoreUnavailable')}
          </span>
        </div>

        <div className={styles.comparisonToolbar}>
          <div className={styles.comparisonModes} aria-label={t('forum.revision.comparisonModeLabel')}>
            <button
              type="button"
              className={comparisonMode === 'previous' ? styles.comparisonModeActive : styles.comparisonMode}
              aria-pressed={comparisonMode === 'previous'}
              onClick={() => handleComparisonModeChange('previous')}
            >
              {t('forum.revision.comparePrevious')}
            </button>
            <button
              type="button"
              className={comparisonMode === 'current' ? styles.comparisonModeActive : styles.comparisonMode}
              aria-pressed={comparisonMode === 'current'}
              disabled={selectedIsCurrentVersion}
              onClick={() => handleComparisonModeChange('current')}
            >
              {t('forum.revision.compareCurrent')}
            </button>
          </div>
          <span>{t('forum.revision.comparisonSummary', {
            before: beforeRevisionNumber ?? '—',
            after: afterRevisionNumber ?? '—',
          })}</span>
        </div>

        {hasRevisionConflict && (
          <div className={styles.conflict} role="alert">
            <Icon icon="mdi:alert-outline" size={20} />
            <div>
              <strong>{t('forum.revision.conflictTitle')}</strong>
              <p>{t('forum.revision.conflictDescription')}</p>
            </div>
          </div>
        )}

        {comparisonStale && comparisonError ? (
          <div className={styles.comparisonNotice} role="status">
            <strong>{t('forum.revision.comparisonStaleTitle')}</strong>
            <span>{comparisonError}</span>
            <button type="button" onClick={() => void loadComparison(list, selectedSummary, comparisonMode)}>
              {t('forum.revision.retryComparison')}
            </button>
          </div>
        ) : null}

        <ContentSnapshotDiff
          before={buildSnapshot(beforeDetail)}
          after={buildSnapshot(afterDetail)}
          beforeLabel={t('forum.revision.versionLabel', { version: beforeRevisionNumber ?? '—' })}
          afterLabel={t('forum.revision.versionLabel', { version: afterRevisionNumber ?? '—' })}
          ariaLabel={t('forum.revision.diffAriaLabel')}
          emptyText={t('forum.revision.selectVersion')}
          beforeUnavailableText={comparisonMode === 'previous'
            ? comparisonUnavailableText
            : detailError || t('forum.revision.snapshotUnavailable')}
          afterUnavailableText={comparisonMode === 'current'
            ? comparisonUnavailableText
            : detailError || t('forum.revision.snapshotUnavailable')}
          loadingBefore={comparisonMode === 'previous' ? loadingComparison : loadingDetail}
          loadingAfter={comparisonMode === 'current' ? loadingComparison : loadingDetail}
          loadingText={t('forum.revision.comparisonLoading')}
          onRetryBefore={comparisonMode === 'previous' && !comparisonMissing
            ? () => void loadComparison(list, selectedSummary, comparisonMode)
            : undefined}
          onRetryAfter={comparisonMode === 'current' && !comparisonMissing
            ? () => void loadComparison(list, selectedSummary, comparisonMode)
            : undefined}
          retryLabel={t('forum.revision.retryComparison')}
        />

        {!selectedSummary.voCanRestore && (
          <p className={styles.unavailableReason}>
            {t('forum.revision.unavailableReason', {
              reason: selectedSummary.voUnavailableReasonCode === ForumContentRevisionErrorCode.Incomplete
                ? t('forum.revision.reason.incomplete')
                : t('forum.revision.unknownReason')
            })}
          </p>
        )}
        {actionError && <div className={styles.error} role="alert">{actionError}</div>}

        {confirmingRestore && (
          <div
            className={styles.restoreConfirmation}
            role="alertdialog"
            aria-label={t('forum.revision.confirmTitle')}
          >
            <div>
              <strong>{t('forum.revision.confirmTitle')}</strong>
              <p>
                {t('forum.revision.confirmMessage', {
                  target: selectedSummary.voRevisionNumber,
                  next: list.currentContentRevision + 1
                })}
              </p>
            </div>
            <div>
              <Button
                variant="secondary"
                size="small"
                disabled={restoring}
                onClick={() => setConfirmingRestore(false)}
              >
                {t('common.cancel')}
              </Button>
              <Button
                variant="primary"
                size="small"
                disabled={restoring}
                onClick={() => void handleRestore()}
              >
                {restoring ? t('forum.revision.restoring') : t('forum.revision.confirmAction')}
              </Button>
            </div>
          </div>
        )}

        <div className={styles.detailActions}>
          <p>
            {selectedIsCurrent
              ? t('forum.revision.currentVersionHint')
              : t('forum.revision.restoreCreatesNewVersion')}
          </p>
          <div>
            <Button
              variant="secondary"
              size="small"
              onClick={handleUseInEditor}
              disabled={restoring}
            >
              {t('forum.revision.useInEditor')}
            </Button>
            <Button
              variant="primary"
              size="small"
              onClick={() => setConfirmingRestore(true)}
              disabled={!canRestoreSelected}
            >
              {restoring ? t('forum.revision.restoring') : t('forum.revision.restore')}
            </Button>
          </div>
        </div>
      </div>
    );
  };

  const renderLegacyHistory = (): ReactNode => {
    if (!list?.canViewDetails) {
      return null;
    }

    return (
      <section className={styles.legacySection}>
        <button
          type="button"
          className={styles.legacyToggle}
          aria-expanded={legacyExpanded}
          onClick={() => {
            const nextExpanded = !legacyExpanded;
            setLegacyExpanded(nextExpanded);
            if (nextExpanded && legacyItems.length === 0 && !legacyLoading) {
              void loadLegacyHistory(1);
            }
          }}
        >
          <span>
            <strong>{t('forum.revision.legacyTitle')}</strong>
            <small>{t('forum.revision.legacyDescription')}</small>
          </span>
          <Icon icon={legacyExpanded ? 'mdi:chevron-up' : 'mdi:chevron-down'} size={18} />
        </button>

        {legacyExpanded && (
          <div className={styles.legacyContent}>
            {legacyLoading && <div className={styles.state}>{t('forum.revision.legacyLoading')}</div>}
            {!legacyLoading && legacyError && <div className={styles.error}>{legacyError}</div>}
            {!legacyLoading && !legacyError && legacyItems.length === 0 && (
              <div className={styles.state}>{t('forum.revision.legacyEmpty')}</div>
            )}
            {!legacyLoading && !legacyError && legacyItems.map(item => (
              <article key={String(item.voId)} className={styles.legacyItem}>
                <header>
                  <strong>
                    {t('forum.history.sequence', { count: item.voEditSequence })}
                  </strong>
                  <span>
                    {t('forum.history.editorMeta', {
                      name: item.voEditorName,
                      time: formatDate(item.voEditedAt, locale)
                    })}
                  </span>
                </header>
                {'voOldTitle' in item && (
                  <div className={styles.legacyTitleCompare}>
                    <span>{item.voOldTitle || '-'}</span>
                    <span>{item.voNewTitle || '-'}</span>
                  </div>
                )}
                <div className={styles.legacyCompare}>
                  <pre>{item.voOldContent}</pre>
                  <pre>{item.voNewContent}</pre>
                </div>
              </article>
            ))}
            {legacyTotal > LEGACY_PAGE_SIZE && (
              <div className={styles.footer}>
                <span>
                  {t('common.pageInfo', { current: legacyPageIndex, total: legacyTotalPages })}
                </span>
                <div>
                  <Button
                    variant="secondary"
                    size="small"
                    disabled={legacyPageIndex <= 1 || legacyLoading}
                    onClick={() => void loadLegacyHistory(legacyPageIndex - 1)}
                  >
                    {t('common.previousPage')}
                  </Button>
                  <Button
                    variant="secondary"
                    size="small"
                    disabled={legacyPageIndex >= legacyTotalPages || legacyLoading}
                    onClick={() => void loadLegacyHistory(legacyPageIndex + 1)}
                  >
                    {t('common.nextPage')}
                  </Button>
                </div>
              </div>
            )}
          </div>
        )}
      </section>
    );
  };

  const body = (
    <div className={styles.container}>
      {renderSummary()}
      {loadingList && <div className={styles.state}>{t('forum.revision.loading')}</div>}
      {!loadingList && listError && <div className={styles.error} role="alert">{listError}</div>}
      {!loadingList && !listError && list && (
        <>
          <div className={styles.workspace}>
            {renderRevisionList()}
            {renderDetail()}
          </div>
          {renderLegacyHistory()}
        </>
      )}
    </div>
  );

  const footer = list?.canViewDetails && list.total > list.pageSize ? (
    <div className={styles.footer}>
      <span>
        {t('common.pageInfo', { current: list.pageIndex, total: totalPages })}
      </span>
      <div>
        <Button
          variant="secondary"
          size="small"
          disabled={list.pageIndex <= 1 || loadingList}
          onClick={() => void loadList(list.pageIndex - 1)}
        >
          {t('common.previousPage')}
        </Button>
        <Button
          variant="secondary"
          size="small"
          disabled={list.pageIndex >= totalPages || loadingList}
          onClick={() => void loadList(list.pageIndex + 1)}
        >
          {t('common.nextPage')}
        </Button>
      </div>
    </div>
  ) : undefined;

  const title = target?.kind === 'comment'
    ? t('forum.revision.commentTitle')
    : t('forum.revision.postTitle');

  return (
    <>
      {isMobile ? (
        <BottomSheet
          isOpen={isOpen}
          onClose={handleDialogClose}
          closeLabel={t('common.close')}
          closeOnEscape={!restoring && !confirmingRestore}
          closeOnOverlayClick={!restoring && !confirmingRestore}
          title={title}
          height="84%"
          footer={footer}
          bodyClassName={styles.mobileBody}
        >
          {body}
        </BottomSheet>
      ) : (
        <Modal
          isOpen={isOpen}
          onClose={handleDialogClose}
          closeLabel={t('common.close')}
          closeOnEscape={!restoring && !confirmingRestore}
          closeOnOverlayClick={!restoring && !confirmingRestore}
          title={title}
          size="large"
          footer={footer}
        >
          {body}
        </Modal>
      )}

    </>
  );
};

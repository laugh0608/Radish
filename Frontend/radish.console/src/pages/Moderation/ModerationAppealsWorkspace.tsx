import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import {
  ApiResponseError,
  isApiResponseNotFoundError,
  type ContentModerationAppealOutcome,
  type ContentModerationAppealVo,
  type ContentModerationCaseDetailVo,
  type ContentModerationReliefScope,
} from '@radish/http';
import {
  AntInput as Input,
  AntSelect as Select,
  Button,
  Checkbox,
  Space,
  message,
} from '@radish/ui';
import { LeftOutlined, ReloadOutlined, SafetyOutlined } from '@radish/ui';
import {
  captureModerationAppealEvidence,
  executeModerationAppealRelief,
  getAppealQueue,
  getModerationAppeal,
  getModerationCase,
  reviewModerationAppeal,
  startModerationAppealReview,
} from '@/api/moderationApi';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
} from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import { normalizeConsoleReturnTo } from '@/utils/returnTo';
import {
  buildModerationPath,
  parseModerationAppealPublicId,
} from './moderationPageUrlState';
import './ModerationAppealsWorkspace.css';

type ReviewOutcome = Exclude<ContentModerationAppealOutcome, 'None'>;
type QueueReadState = 'loading' | 'ready' | 'stale';
type DetailReadState = 'idle' | 'loading' | 'ready' | 'stale' | 'unavailable';

interface AppealDraft {
  outcome: ReviewOutcome;
  grantedScope: ContentModerationReliefScope;
  publicResultSummary: string;
  internalRemark: string;
  reviewOperationKey: string;
  evidenceTitle: string;
  evidenceSummary: string;
  evidenceOperationKey: string;
}

const createOperationKey = (scope: string) => `${scope}:${crypto.randomUUID()}`;

const createAppealDraft = (): AppealDraft => ({
  outcome: 'Upheld',
  grantedScope: 0,
  publicResultSummary: '',
  internalRemark: '',
  reviewOperationKey: createOperationKey('moderation-appeal-review'),
  evidenceTitle: '',
  evidenceSummary: '',
  evidenceOperationKey: createOperationKey('moderation-appeal-evidence'),
});

const outcomeValue: Record<ReviewOutcome, 1 | 2 | 3> = {
  Upheld: 1,
  PartiallyGranted: 2,
  Granted: 3,
};

const scopeOptions: Array<{ value: 1 | 2 | 4; key: string }> = [
  { value: 1, key: 'targetContent' },
  { value: 2, key: 'mute' },
  { value: 4, key: 'ban' },
];

const isConflictError = (error: unknown) => (
  error instanceof ApiResponseError
  && (error.httpStatus ?? error.statusCode ?? 0) === 409
);

const isUnavailableError = (error: unknown) => (
  isApiResponseNotFoundError(error)
  || (
    error instanceof ApiResponseError
    && (error.httpStatus ?? error.statusCode ?? 0) === 403
  )
);

const statusTone = (status: ContentModerationAppealVo['voStatus']) => {
  if (status === 'Resolved') {
    return 'success' as const;
  }
  if (status === 'ReliefFailed') {
    return 'danger' as const;
  }
  if (status === 'Reviewing' || status === 'ReliefPending') {
    return 'warning' as const;
  }
  return 'info' as const;
};

const hasScope = (scope: ContentModerationReliefScope, value: 1 | 2 | 4) => (
  (scope & value) === value
);

export interface ModerationAppealsWorkspaceProps {
  onOpenCases?: () => void;
}

export const ModerationAppealsWorkspace = ({
  onOpenCases,
}: ModerationAppealsWorkspaceProps) => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const canAppeal = usePermission(CONSOLE_PERMISSIONS.moderationAppeal);
  const canAction = usePermission(CONSOLE_PERMISSIONS.moderationAction);
  const returnTo = normalizeConsoleReturnTo(searchParams.get('returnTo'));
  const selectedAppealId = parseModerationAppealPublicId(searchParams.get('appeal')) ?? null;
  const language = i18n.resolvedLanguage ?? i18n.language;
  const [queueItems, setQueueItems] = useState<ContentModerationAppealVo[]>([]);
  const [queueTotal, setQueueTotal] = useState(0);
  const [queuePageIndex, setQueuePageIndex] = useState(1);
  const [queuePageSize, setQueuePageSize] = useState(20);
  const [statusFilter, setStatusFilter] = useState<number>(-1);
  const [keywordInput, setKeywordInput] = useState('');
  const [keyword, setKeyword] = useState('');
  const [detail, setDetail] = useState<ContentModerationAppealVo | null>(null);
  const [caseDetail, setCaseDetail] = useState<ContentModerationCaseDetailVo | null>(null);
  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [queueReadState, setQueueReadState] = useState<QueueReadState>('loading');
  const [detailReadState, setDetailReadState] = useState<DetailReadState>('idle');
  const [submitting, setSubmitting] = useState(false);
  const [conflictNotice, setConflictNotice] = useState(false);
  const [draft, setDraft] = useState<AppealDraft>(createAppealDraft);
  const draftAppealIdRef = useRef<string | null>(null);
  const appealLoadSequenceRef = useRef(0);
  const loadedAppealIdRef = useRef<string | null>(null);

  const formatDateTime = useCallback((value?: string | null) => {
    if (!value) {
      return '-';
    }
    return new Intl.DateTimeFormat(language.startsWith('zh') ? 'zh-CN' : 'en-US', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }, [language]);

  const loadAppeal = useCallback(async (appealPublicId: string, initializeDraft = true) => {
    if (!canAppeal) {
      setDetail(null);
      setCaseDetail(null);
      setDetailReadState('idle');
      return;
    }

    const loadSequence = appealLoadSequenceRef.current + 1;
    appealLoadSequenceRef.current = loadSequence;
    setLoadingDetail(true);
    setDetailReadState('loading');
    if (loadedAppealIdRef.current !== appealPublicId) {
      setDetail(null);
      setCaseDetail(null);
    }
    try {
      const nextDetail = await getModerationAppeal(appealPublicId);
      if (appealLoadSequenceRef.current !== loadSequence) {
        return;
      }
      const nextCase = await getModerationCase(nextDetail.voCasePublicId);
      if (appealLoadSequenceRef.current !== loadSequence) {
        return;
      }
      setDetail(nextDetail);
      setCaseDetail(nextCase);
      loadedAppealIdRef.current = appealPublicId;
      setDetailReadState('ready');
      if (initializeDraft && draftAppealIdRef.current !== appealPublicId) {
        draftAppealIdRef.current = appealPublicId;
        setDraft({
          ...createAppealDraft(),
          internalRemark: nextDetail.voInternalRemark ?? '',
        });
        setConflictNotice(false);
      }
    } catch (error) {
      if (appealLoadSequenceRef.current !== loadSequence) {
        return;
      }
      log.error('ModerationAppealsWorkspace', 'Failed to load appeal detail:', error);
      if (isUnavailableError(error)) {
        setDetail(null);
        setCaseDetail(null);
        loadedAppealIdRef.current = null;
        setDetailReadState('unavailable');
        message.warning(t('moderation.appeal.detailUnavailableTitle'));
      } else {
        setDetailReadState('stale');
        message.error(t('moderation.appeal.loadDetailFailed'));
      }
    } finally {
      if (appealLoadSequenceRef.current === loadSequence) {
        setLoadingDetail(false);
      }
    }
  }, [canAppeal, t]);

  const loadQueue = useCallback(async (requestedPage = queuePageIndex) => {
    setLoadingQueue(true);
    setQueueReadState((current) => current === 'ready' ? 'ready' : 'loading');
    try {
      const page = await getAppealQueue({
        status: statusFilter >= 0 ? statusFilter : undefined,
        keyword,
        pageIndex: requestedPage,
        pageSize: queuePageSize,
      });
      setQueueItems(page.voItems);
      setQueueTotal(page.voTotal);
      setQueuePageIndex(page.voPageIndex);
      setQueuePageSize(page.voPageSize);
      setQueueReadState('ready');
    } catch (error) {
      log.error('ModerationAppealsWorkspace', 'Failed to load appeal queue:', error);
      setQueueReadState('stale');
      message.error(t('moderation.appeal.loadQueueFailed'));
    } finally {
      setLoadingQueue(false);
    }
  }, [
    keyword,
    queuePageIndex,
    queuePageSize,
    statusFilter,
    t,
  ]);

  useEffect(() => {
    void loadQueue(1);
    // The callback owns the current filter snapshot and intentionally resets pagination.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter, keyword, queuePageSize]);

  useEffect(() => {
    if (!selectedAppealId) {
      appealLoadSequenceRef.current += 1;
      loadedAppealIdRef.current = null;
      draftAppealIdRef.current = null;
      setDetail(null);
      setCaseDetail(null);
      setDetailReadState('idle');
      setConflictNotice(false);
      return;
    }

    void loadAppeal(selectedAppealId);
  }, [loadAppeal, selectedAppealId]);

  const selectedQueueItem = useMemo(
    () => queueItems.find((item) => item.voAppealPublicId === selectedAppealId) ?? null,
    [queueItems, selectedAppealId],
  );
  const selectedSummary = selectedQueueItem
    ?? (detail?.voAppealPublicId === selectedAppealId ? detail : null);
  const reviewingCount = queueItems.filter((item) => item.voStatus === 'Reviewing').length;
  const reliefPendingCount = queueItems.filter(
    (item) => item.voStatus === 'ReliefPending' || item.voStatus === 'ReliefFailed',
  ).length;
  const isResolved = detail?.voStatus === 'Resolved' || detail?.voStatus === 'Withdrawn';
  const actionsAreAuthoritative = queueReadState === 'ready'
    && (!canAppeal || detailReadState === 'ready');

  const handleConflict = async () => {
    if (!selectedAppealId) {
      return;
    }
    setConflictNotice(true);
    await loadAppeal(selectedAppealId, false);
    setDraft((current) => ({
      ...current,
      reviewOperationKey: createOperationKey('moderation-appeal-review'),
      evidenceOperationKey: createOperationKey('moderation-appeal-evidence'),
    }));
  };

  const runVersionedWrite = async (
    action: (appeal: ContentModerationAppealVo) => Promise<ContentModerationAppealVo>,
    successKey: string,
    failureKey: string,
  ): Promise<boolean> => {
    if (!detail || !canAppeal || !actionsAreAuthoritative) {
      return false;
    }
    setSubmitting(true);
    try {
      const nextDetail = await action(detail);
      setDetail(nextDetail);
      setConflictNotice(false);
      message.success(t(successKey));
      await loadQueue(queuePageIndex);
      return true;
    } catch (error) {
      if (isConflictError(error)) {
        await handleConflict();
        message.warning(t('moderation.appeal.conflictDraftPreserved'));
      } else {
        log.error('ModerationAppealsWorkspace', failureKey, error);
        message.error(t(failureKey));
      }
      return false;
    } finally {
      setSubmitting(false);
    }
  };

  const startReview = async () => {
    await runVersionedWrite(
      (appeal) => startModerationAppealReview({
        appealPublicId: appeal.voAppealPublicId,
        expectedVersion: appeal.voVersion,
        operationKey: createOperationKey('moderation-appeal-start'),
      }),
      'moderation.appeal.startSuccess',
      'moderation.appeal.startFailed',
    );
  };

  const captureEvidence = async () => {
    if (!draft.evidenceTitle.trim() && !draft.evidenceSummary.trim()) {
      message.error(t('moderation.appeal.evidenceRequired'));
      return;
    }
    const succeeded = await runVersionedWrite(
      (appeal) => captureModerationAppealEvidence({
        appealPublicId: appeal.voAppealPublicId,
        expectedVersion: appeal.voVersion,
        snapshotTitle: draft.evidenceTitle.trim() || null,
        snapshotSummary: draft.evidenceSummary.trim() || null,
        operationKey: draft.evidenceOperationKey,
      }),
      'moderation.appeal.evidenceSuccess',
      'moderation.appeal.evidenceFailed',
    );
    if (succeeded) {
      setDraft((current) => ({
        ...current,
        evidenceTitle: '',
        evidenceSummary: '',
        evidenceOperationKey: createOperationKey('moderation-appeal-evidence'),
      }));
    }
  };

  const submitReview = async () => {
    if (!detail || !draft.publicResultSummary.trim()) {
      message.error(t('moderation.appeal.publicSummaryRequired'));
      return;
    }
    const eligibleScope = detail.voEligibleScope;
    if (
      (draft.outcome === 'Upheld' && draft.grantedScope !== 0)
      || (draft.outcome === 'Granted' && draft.grantedScope !== eligibleScope)
      || (
        draft.outcome === 'PartiallyGranted'
        && (
          draft.grantedScope === 0
          || draft.grantedScope === eligibleScope
          || (draft.grantedScope & ~eligibleScope) !== 0
        )
      )
    ) {
      message.error(t('moderation.appeal.invalidScope'));
      return;
    }
    await runVersionedWrite(
      (appeal) => reviewModerationAppeal({
        appealPublicId: appeal.voAppealPublicId,
        expectedVersion: appeal.voVersion,
        outcome: outcomeValue[draft.outcome],
        grantedScope: draft.grantedScope,
        publicResultSummary: draft.publicResultSummary.trim(),
        internalRemark: draft.internalRemark.trim() || null,
        operationKey: draft.reviewOperationKey,
      }),
      'moderation.appeal.reviewSuccess',
      'moderation.appeal.reviewFailed',
    );
  };

  const executeRelief = async () => {
    if (
      !selectedSummary
      || !canAction
      || !actionsAreAuthoritative
      || (selectedSummary.voStatus !== 'ReliefPending' && selectedSummary.voStatus !== 'ReliefFailed')
    ) {
      return;
    }

    setSubmitting(true);
    try {
      await executeModerationAppealRelief({
        appealPublicId: selectedSummary.voAppealPublicId,
        expectedVersion: selectedSummary.voVersion,
        operationKey: createOperationKey('moderation-appeal-relief'),
      });
      message.success(t('moderation.appeal.reliefSuccess'));
      if (canAppeal) {
        await loadAppeal(selectedSummary.voAppealPublicId, false);
      }
      await loadQueue(queuePageIndex);
    } catch (error) {
      if (isConflictError(error)) {
        if (canAppeal) {
          await handleConflict();
        } else {
          await loadQueue(queuePageIndex);
        }
        message.warning(t('moderation.appeal.conflictReloaded'));
      } else {
        log.error('ModerationAppealsWorkspace', 'Failed to execute appeal relief:', error);
        message.error(t('moderation.appeal.reliefFailed'));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const toggleScope = (scope: 1 | 2 | 4, checked: boolean) => {
    setDraft((current) => ({
      ...current,
      grantedScope: (checked
        ? current.grantedScope | scope
        : current.grantedScope & ~scope) as ContentModerationReliefScope,
    }));
  };

  const selectAppeal = (appealPublicId: string) => {
    navigate(buildModerationPath({
      view: 'appeals',
      appealPublicId,
      returnTo,
    }));
  };

  const closeAppeal = (replace = true) => {
    navigate(buildModerationPath({ view: 'appeals', returnTo }), { replace });
  };

  const refreshWorkspace = async () => {
    const reads: Promise<void>[] = [loadQueue(queuePageIndex)];
    if (selectedAppealId && canAppeal) {
      reads.push(loadAppeal(selectedAppealId, false));
    }
    await Promise.all(reads);
  };

  return (
    <div
      className="admin-feature-page moderation-appeal-page"
      data-task-active={selectedAppealId ? 'true' : 'false'}
    >
      <ConsolePageHeader
        eyebrow={t('moderation.title')}
        title={t('moderation.appeal.title')}
        description={t('moderation.appeal.description')}
        icon={<SafetyOutlined />}
        status={(
          <Space wrap>
            <ConsoleStatusChip tone={canAppeal ? 'success' : 'neutral'}>
              {t(canAppeal ? 'moderation.appeal.canReview' : 'moderation.appeal.queueOnly')}
            </ConsoleStatusChip>
            <ConsoleStatusChip tone={canAction ? 'warning' : 'neutral'}>
              {t(canAction ? 'moderation.appeal.canRelief' : 'moderation.appeal.noReliefPermission')}
            </ConsoleStatusChip>
          </Space>
        )}
        actions={(
          <Space wrap>
            {returnTo ? <Button onClick={() => navigate(returnTo)}>{t('moderation.backToSource')}</Button> : null}
            <Button onClick={onOpenCases ?? (() => navigate(buildModerationPath({ returnTo })))}>
              {t('moderation.appeal.openCases')}
            </Button>
            <Button
              icon={<ReloadOutlined />}
              disabled={loadingQueue || loadingDetail}
              onClick={() => void refreshWorkspace()}
            >
              {loadingQueue || loadingDetail ? t('moderation.loading') : t('moderation.refresh')}
            </Button>
          </Space>
        )}
      />

      <ConsoleMetricGrid label={t('moderation.appeal.metricsLabel')}>
        <ConsoleMetricCard label={t('moderation.appeal.metric.total')} value={queueTotal} tone="info" />
        <ConsoleMetricCard label={t('moderation.appeal.metric.reviewing')} value={reviewingCount} tone="warning" />
        <ConsoleMetricCard label={t('moderation.appeal.metric.relief')} value={reliefPendingCount} tone={reliefPendingCount ? 'danger' : 'neutral'} />
        <ConsoleMetricCard label={t('moderation.appeal.metric.visible')} value={queueItems.length} />
      </ConsoleMetricGrid>

      <section className="admin-feature-card moderation-appeal-filters">
        <Select
          value={statusFilter}
          aria-label={t('moderation.appeal.filter.status')}
          className="moderation-filter-control moderation-filter-control--md"
          options={[
            { value: -1, label: t('moderation.appeal.filter.allStatuses') },
            ...['Submitted', 'Reviewing', 'ReliefPending', 'ReliefFailed', 'Resolved', 'Withdrawn']
              .map((status, value) => ({
                value,
                label: t(`moderation.appeal.status.${status}`),
              })),
          ]}
          onChange={(status) => {
            setStatusFilter(status);
            if (selectedAppealId) {
              closeAppeal(true);
            }
          }}
        />
        <Input
          value={keywordInput}
          aria-label={t('moderation.appeal.filter.keyword')}
          className="moderation-filter-control moderation-filter-control--xl"
          placeholder={t('moderation.appeal.filter.keyword')}
          onChange={(event) => setKeywordInput(event.target.value)}
          onPressEnter={() => {
            setKeyword(keywordInput.trim());
            if (selectedAppealId) {
              closeAppeal(true);
            }
          }}
        />
        <Button variant="primary" onClick={() => {
          setKeyword(keywordInput.trim());
          if (selectedAppealId) {
            closeAppeal(true);
          }
        }}>
          {t('moderation.query')}
        </Button>
        <Button onClick={() => {
          setStatusFilter(-1);
          setKeywordInput('');
          setKeyword('');
          if (selectedAppealId) {
            closeAppeal(true);
          }
        }}>
          {t('moderation.reset')}
        </Button>
      </section>

      <div className="moderation-appeal-mobile-boundary" role="note">
        <strong>{t('moderation.appeal.mobileTitle')}</strong>
        <span>{t('moderation.appeal.mobileDescription')}</span>
      </div>

      <section className="moderation-case-workbench">
        <aside className="moderation-case-queue" aria-label={t('moderation.appeal.queueTitle')}>
          <div className="moderation-case-section-heading">
            <div>
              <h2>{t('moderation.appeal.queueTitle')}</h2>
              <p>{t('moderation.appeal.queueDescription')}</p>
            </div>
            <ConsoleStatusChip tone="info">{queueTotal}</ConsoleStatusChip>
          </div>
          {queueReadState === 'stale' ? (
            <div className="moderation-case-read-state moderation-case-read-state--stale" role="alert">
              <strong>{t('moderation.appeal.queueStaleTitle')}</strong>
              <span>{t('moderation.appeal.queueStaleDescription')}</span>
            </div>
          ) : null}
          <div className="moderation-case-queue-list">
            {queueItems.map((item) => (
              <button
                key={item.voAppealPublicId}
                type="button"
                className="moderation-case-queue-item"
                data-active={item.voAppealPublicId === selectedAppealId}
                onClick={() => selectAppeal(item.voAppealPublicId)}
              >
                <span className="moderation-case-queue-item__topline">
                  <strong>{item.voAppealPublicId}</strong>
                  <ConsoleStatusChip tone={statusTone(item.voStatus)}>
                    {t(`moderation.appeal.status.${item.voStatus}`)}
                  </ConsoleStatusChip>
                </span>
                <span>{item.voCasePublicId}</span>
                <span className="moderation-case-queue-item__bottomline">
                  <small>{formatDateTime(item.voSubmittedAt)}</small>
                  <small>v{item.voVersion}</small>
                </span>
              </button>
            ))}
            {!loadingQueue && queueItems.length === 0
              ? <p className="admin-feature-rail__empty">{t('moderation.appeal.queueEmpty')}</p>
              : null}
          </div>
          <div className="moderation-case-pagination">
            <Button disabled={queuePageIndex <= 1} onClick={() => void loadQueue(queuePageIndex - 1)}>
              {t('moderation.case.previous')}
            </Button>
            <span>{queuePageIndex}</span>
            <Button
              disabled={queuePageIndex * queuePageSize >= queueTotal}
              onClick={() => void loadQueue(queuePageIndex + 1)}
            >
              {t('moderation.case.next')}
            </Button>
          </div>
        </aside>

        <main
          className="moderation-case-detail"
          data-console-fullscreen-task={selectedAppealId ? 'moderation-appeal' : undefined}
        >
          {selectedAppealId ? (
            <header className="moderation-case-task-header">
              <Button variant="ghost" size="small" icon={<LeftOutlined />} onClick={() => closeAppeal()}>
                {t('moderation.appeal.backToQueue')}
              </Button>
              <strong>{selectedAppealId}</strong>
              <Button variant="ghost" size="small" onClick={() => closeAppeal()}>
                {t('moderation.appeal.closeDetail')}
              </Button>
            </header>
          ) : null}

          {canAppeal && detailReadState === 'unavailable' ? (
            <section className="admin-feature-card moderation-case-empty-state" role="alert">
              <strong>{t('moderation.appeal.detailUnavailableTitle')}</strong>
              <span>{t('moderation.appeal.detailUnavailableDescription')}</span>
              <Space wrap>
                <Button onClick={() => closeAppeal()}>{t('moderation.appeal.backToQueue')}</Button>
                {selectedAppealId ? (
                  <Button icon={<ReloadOutlined />} onClick={() => void loadAppeal(selectedAppealId, false)}>
                    {t('moderation.appeal.retryDetail')}
                  </Button>
                ) : null}
              </Space>
            </section>
          ) : canAppeal && detailReadState === 'stale' && !detail ? (
            <section className="admin-feature-card moderation-case-empty-state" role="alert">
              <strong>{t('moderation.appeal.detailReadFailedTitle')}</strong>
              <span>{t('moderation.appeal.detailReadFailedDescription')}</span>
              <Space wrap>
                <Button onClick={() => closeAppeal()}>{t('moderation.appeal.backToQueue')}</Button>
                {selectedAppealId ? (
                  <Button icon={<ReloadOutlined />} onClick={() => void loadAppeal(selectedAppealId, false)}>
                    {t('moderation.appeal.retryDetail')}
                  </Button>
                ) : null}
              </Space>
            </section>
          ) : !canAppeal
            && selectedAppealId
            && queueReadState !== 'loading'
            && !selectedSummary ? (
              <section className="admin-feature-card moderation-case-empty-state" role="alert">
                <strong>{t('moderation.appeal.detailUnavailableTitle')}</strong>
                <span>{t('moderation.appeal.detailUnavailableDescription')}</span>
                <Button onClick={() => closeAppeal()}>{t('moderation.appeal.backToQueue')}</Button>
              </section>
          ) : !selectedSummary ? (
            <section className="admin-feature-card moderation-case-empty-state">
              <span>{t(selectedAppealId
                ? 'moderation.appeal.loadingDetail'
                : 'moderation.appeal.select')}</span>
            </section>
          ) : !canAppeal ? (
            <section className="admin-feature-card moderation-appeal-redacted">
              {queueReadState === 'stale' ? (
                <div className="moderation-case-read-state moderation-case-read-state--stale" role="alert">
                  <strong>{t('moderation.appeal.queueStaleTitle')}</strong>
                  <span>{t('moderation.appeal.queueStaleDescription')}</span>
                </div>
              ) : null}
              <span className="admin-feature-rail__eyebrow">{selectedSummary.voAppealPublicId}</span>
              <h2>{selectedSummary.voCasePublicId}</h2>
              <ConsoleStatusChip tone={statusTone(selectedSummary.voStatus)}>
                {t(`moderation.appeal.status.${selectedSummary.voStatus}`)}
              </ConsoleStatusChip>
              <p>
                {t(canAction
                  ? 'moderation.appeal.actionOnlyDescription'
                  : 'moderation.appeal.redactedDescription')}
              </p>
              <div className="moderation-case-summary-grid">
                <span>
                  <strong>{t('moderation.appeal.grantedScope')}</strong>
                  {selectedSummary.voGrantedScope}
                </span>
                <span>
                  <strong>{t('moderation.appeal.publicSummary')}</strong>
                  {selectedSummary.voPublicResultSummary || '-'}
                </span>
              </div>
              {canAction
                && actionsAreAuthoritative
                && (selectedSummary.voStatus === 'ReliefPending'
                  || selectedSummary.voStatus === 'ReliefFailed') ? (
                    <Button
                      variant="danger"
                      className="moderation-appeal-desktop-action"
                      disabled={submitting}
                      onClick={() => void executeRelief()}
                    >
                      {t('moderation.appeal.executeRelief')}
                    </Button>
                  ) : null}
            </section>
          ) : !detail ? (
            <section className="admin-feature-card moderation-case-empty-state">
              <span>{t('moderation.appeal.loadingDetail')}</span>
            </section>
          ) : (
            <>
              {detailReadState === 'loading' || detailReadState === 'stale' ? (
                <div
                  className={`moderation-case-read-state moderation-case-read-state--${detailReadState}`}
                  role={detailReadState === 'stale' ? 'alert' : 'status'}
                >
                  <strong>{t(detailReadState === 'stale'
                    ? 'moderation.appeal.detailStaleTitle'
                    : 'moderation.appeal.detailVerifyingTitle')}</strong>
                  <span>{t(detailReadState === 'stale'
                    ? 'moderation.appeal.detailStaleDescription'
                    : 'moderation.appeal.detailVerifyingDescription')}</span>
                </div>
              ) : null}
              <section className="admin-feature-card moderation-case-summary">
                <div className="moderation-case-section-heading">
                  <div>
                    <span className="admin-feature-rail__eyebrow">{detail.voAppealPublicId}</span>
                    <h2>{caseDetail?.voEvidence.at(-1)?.voSnapshotTitle || detail.voCasePublicId}</h2>
                    <p>{caseDetail?.voEvidence.at(-1)?.voSnapshotSummary || t('moderation.case.snapshotUnavailable')}</p>
                  </div>
                  <Space wrap>
                    <ConsoleStatusChip tone={statusTone(detail.voStatus)}>
                      {t(`moderation.appeal.status.${detail.voStatus}`)} · v{detail.voVersion}
                    </ConsoleStatusChip>
                    <ConsoleStatusChip tone="neutral">
                      {t(`moderation.appeal.outcome.${detail.voOutcome}`)}
                    </ConsoleStatusChip>
                  </Space>
                </div>
                <div className="moderation-case-summary-grid">
                  <span><strong>{t('moderation.appeal.case')}</strong>{detail.voCasePublicId}</span>
                  <span><strong>{t('moderation.appeal.submittedAt')}</strong>{formatDateTime(detail.voSubmittedAt)}</span>
                  <span><strong>{t('moderation.appeal.eligibleUntil')}</strong>{formatDateTime(detail.voEligibleUntilUtc)}</span>
                  <span><strong>{t('moderation.appeal.eligibleScope')}</strong>{detail.voEligibleScope}</span>
                  <span>
                    <strong>{t('moderation.appeal.originalDecision')}</strong>
                    {caseDetail
                      ? t(`moderation.case.decision.${caseDetail.voCase.voDecision}`)
                      : '-'}
                  </span>
                  <span>
                    <strong>{t('moderation.appeal.originalDisposition')}</strong>
                    {caseDetail
                      ? t(`moderation.case.disposition.${caseDetail.voCase.voTargetDisposition}`)
                      : '-'}
                  </span>
                  <span>
                    <strong>{t('moderation.appeal.originalPublicResult')}</strong>
                    {caseDetail?.voPublicResultCode || '-'}
                  </span>
                </div>
              </section>

              {conflictNotice ? (
                <div className="moderation-case-conflict" role="alert">
                  <strong>{t('moderation.appeal.conflictTitle')}</strong>
                  <span>{t('moderation.appeal.conflictDraftPreserved')}</span>
                </div>
              ) : null}

              <section className="admin-feature-card moderation-appeal-statement">
                <div className="moderation-case-section-heading">
                  <div>
                    <h3>{t('moderation.appeal.statementTitle')}</h3>
                    <p>{t('moderation.appeal.statementDescription')}</p>
                  </div>
                </div>
                <p>{detail.voStatement}</p>
              </section>

              <section className="admin-feature-card">
                <div className="moderation-case-section-heading">
                  <div>
                    <h3>{t('moderation.appeal.eventsTitle')}</h3>
                    <p>{t('moderation.appeal.eventsDescription')}</p>
                  </div>
                </div>
                <div className="moderation-case-timeline moderation-case-timeline--events">
                  {detail.voEvents.map((event) => (
                    <article key={event.voSequence}>
                      <div>
                        <strong>#{event.voSequence} · {event.voEventType}</strong>
                        <small>{formatDateTime(event.voCreateTime)}</small>
                      </div>
                      <p>{event.voRemark || event.voResultCode || t('moderation.appeal.noEventRemark')}</p>
                      <small>{event.voActorName}</small>
                    </article>
                  ))}
                </div>
              </section>

              {!isResolved && actionsAreAuthoritative ? (
                <section className="admin-feature-card moderation-appeal-write-panel">
                  <div className="moderation-case-section-heading">
                    <div>
                      <h3>{t('moderation.appeal.reviewTitle')}</h3>
                      <p>{t('moderation.appeal.reviewDescription')}</p>
                    </div>
                    {detail.voStatus === 'Submitted'
                      ? <Button disabled={submitting} onClick={() => void startReview()}>{t('moderation.appeal.startReview')}</Button>
                      : null}
                  </div>

                  {detail.voStatus === 'Reviewing' ? (
                    <>
                      <div className="moderation-appeal-evidence-form">
                        <label className="moderation-case-field">
                          <span>{t('moderation.appeal.evidenceTitle')}</span>
                          <Input
                            value={draft.evidenceTitle}
                            maxLength={200}
                            onChange={(event) => setDraft((current) => ({
                              ...current,
                              evidenceTitle: event.target.value,
                            }))}
                          />
                        </label>
                        <label className="moderation-case-field">
                          <span>{t('moderation.appeal.evidenceSummary')}</span>
                          <Input.TextArea
                            rows={3}
                            maxLength={1000}
                            value={draft.evidenceSummary}
                            onChange={(event) => setDraft((current) => ({
                              ...current,
                              evidenceSummary: event.target.value,
                            }))}
                          />
                        </label>
                        <Button disabled={submitting} onClick={() => void captureEvidence()}>
                          {t('moderation.appeal.captureEvidence')}
                        </Button>
                      </div>

                      <div className="moderation-case-form-grid">
                        <label>
                          <span>{t('moderation.appeal.outcome')}</span>
                          <Select
                            value={draft.outcome}
                            options={(['Upheld', 'PartiallyGranted', 'Granted'] as ReviewOutcome[]).map((outcome) => ({
                              value: outcome,
                              label: t(`moderation.appeal.outcome.${outcome}`),
                            }))}
                            onChange={(outcome: ReviewOutcome) => {
                              setDraft((current) => ({
                                ...current,
                                outcome,
                                grantedScope: outcome === 'Granted' ? detail.voEligibleScope : 0,
                              }));
                            }}
                          />
                        </label>
                        <fieldset className="moderation-appeal-scopes">
                          <legend>{t('moderation.appeal.grantedScope')}</legend>
                          {scopeOptions.map((option) => (
                            <Checkbox
                              key={option.value}
                              checked={hasScope(draft.grantedScope, option.value)}
                              disabled={
                                !hasScope(detail.voEligibleScope, option.value)
                                || draft.outcome !== 'PartiallyGranted'
                              }
                              onChange={(event) => toggleScope(option.value, event.target.checked)}
                            >
                              {t(`moderation.appeal.scope.${option.key}`)}
                            </Checkbox>
                          ))}
                        </fieldset>
                      </div>
                      <label className="moderation-case-field">
                        <span>{t('moderation.appeal.publicSummary')}</span>
                        <Input.TextArea
                          rows={3}
                          maxLength={500}
                          value={draft.publicResultSummary}
                          onChange={(event) => setDraft((current) => ({
                            ...current,
                            publicResultSummary: event.target.value,
                          }))}
                        />
                      </label>
                      <label className="moderation-case-field">
                        <span>{t('moderation.appeal.internalRemark')}</span>
                        <Input.TextArea
                          rows={3}
                          maxLength={1000}
                          value={draft.internalRemark}
                          onChange={(event) => setDraft((current) => ({
                            ...current,
                            internalRemark: event.target.value,
                          }))}
                        />
                      </label>
                    </>
                  ) : null}
                  <Space wrap>
                    {detail.voStatus === 'Reviewing' ? (
                      <Button variant="primary" disabled={submitting} onClick={() => void submitReview()}>
                        {t('moderation.appeal.submitReview')}
                      </Button>
                    ) : null}
                    {canAction && (detail.voStatus === 'ReliefPending' || detail.voStatus === 'ReliefFailed') ? (
                      <Button variant="danger" disabled={submitting} onClick={() => void executeRelief()}>
                        {t('moderation.appeal.executeRelief')}
                      </Button>
                    ) : null}
                  </Space>
                </section>
              ) : null}
            </>
          )}
        </main>
      </section>
    </div>
  );
};

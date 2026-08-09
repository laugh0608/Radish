import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import {
  ApiResponseError,
  isApiResponseNotFoundError,
  type ContentModerationCaseDetailVo,
  type ContentModerationCaseQueueItemVo,
  type ContentModerationCaseUserActionRequest,
} from '@radish/http';
import {
  AntInput as Input,
  AntSelect as Select,
  Button,
  InputNumber,
  Space,
  message,
} from '@radish/ui';
import { LeftOutlined, ReloadOutlined, SafetyOutlined } from '@radish/ui';
import {
  applyModerationCorrectiveAction,
  captureModerationEvidence,
  getCaseQueue,
  getModerationCase,
  reviewModerationCase,
} from '@/api/moderationApi';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
} from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import { normalizeConsoleReturnTo } from '@/utils/returnTo';
import { ModerationAppealsWorkspace } from './ModerationAppealsWorkspace';
import {
  buildModerationPath,
  buildModerationSearchParams,
  DEFAULT_MODERATION_PAGE_INDEX,
  DEFAULT_MODERATION_PAGE_SIZE,
  MODERATION_TARGET_TYPES,
  parseModerationCasePublicId,
  parseModerationCaseStatusQuery,
  parseModerationPageIndexQuery,
  parseModerationPageSizeQuery,
  parseModerationTargetTypeQuery,
} from './moderationPageUrlState';
import './index.css';
import '../adminFeature.css';

type DecisionValue = 1 | 2 | 3;
type TargetDispositionValue = 1 | 2 | 3;
type UserActionValue = 1 | 2 | 3 | 4;
type QueueReadState = 'loading' | 'ready' | 'stale';
type DetailReadState = 'idle' | 'loading' | 'ready' | 'stale' | 'unavailable';

interface DecisionDraft {
  decision: DecisionValue;
  targetDisposition: TargetDispositionValue;
  internalRemark: string;
  includeUserAction: boolean;
  userActionType: UserActionValue;
  durationHours: number | null;
  userActionReason: string;
  operationKey: string;
}

interface CorrectiveDraft {
  actionType: UserActionValue;
  durationHours: number | null;
  reason: string;
  operationKey: string;
}

const createOperationKey = (scope: string) => `${scope}:${crypto.randomUUID()}`;

const createDecisionDraft = (): DecisionDraft => ({
  decision: 1,
  targetDisposition: 1,
  internalRemark: '',
  includeUserAction: false,
  userActionType: 1,
  durationHours: 24,
  userActionReason: '',
  operationKey: createOperationKey('moderation-case-review'),
});

const createCorrectiveDraft = (): CorrectiveDraft => ({
  actionType: 3,
  durationHours: null,
  reason: '',
  operationKey: createOperationKey('moderation-case-corrective'),
});

const publicResultCodeByDecision: Record<DecisionValue, string> = {
  1: 'NoViolation',
  2: 'MeasuresTaken',
  3: 'InsufficientEvidence',
};

const statusTone = (status: string): 'neutral' | 'info' | 'success' | 'warning' | 'danger' => {
  if (status === 'Resolved') {
    return 'success';
  }
  if (status === 'Reviewing') {
    return 'warning';
  }
  return 'info';
};

const dispositionTone = (disposition: string): 'neutral' | 'info' | 'success' | 'warning' | 'danger' => {
  if (disposition === 'ActionFailed') {
    return 'danger';
  }
  if (disposition === 'ActionPending') {
    return 'warning';
  }
  if (disposition === 'Restricted') {
    return 'success';
  }
  return 'neutral';
};

const isConflictError = (error: unknown) => (
  error instanceof ApiResponseError
  && [409].includes(error.httpStatus ?? error.statusCode ?? 0)
);

const isUnavailableError = (error: unknown) => (
  isApiResponseNotFoundError(error)
  || (
    error instanceof ApiResponseError
    && (error.httpStatus ?? error.statusCode ?? 0) === 403
  )
);

const resolveLatestTargetRevision = (detail: ContentModerationCaseDetailVo | null): number | null => {
  if (!detail) {
    return null;
  }

  const evidence = [...detail.voEvidence]
    .reverse()
    .find((item) => item.voContentRevision != null);
  return evidence?.voContentRevision ?? null;
};

const resolveUserStateVersion = (
  detail: ContentModerationCaseDetailVo,
  actionType: UserActionValue,
): number => {
  const policyType = actionType === 1 || actionType === 3 ? 'Mute' : 'Ban';
  return detail.voUserStates.find((item) => item.voPolicyType === policyType)?.voVersion ?? 0;
};

const ModerationCasesWorkspace = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const selectedCaseId = parseModerationCasePublicId(searchParams.get('case')) ?? null;
  const statusFilter = parseModerationCaseStatusQuery(searchParams.get('status')) ?? -1;
  const targetTypeFilter = parseModerationTargetTypeQuery(searchParams.get('targetType'));
  const keyword = searchParams.get('keyword')?.trim() ?? '';
  const queuePageIndex = parseModerationPageIndexQuery(searchParams.get('pageIndex'))
    ?? DEFAULT_MODERATION_PAGE_INDEX;
  const queuePageSize = parseModerationPageSizeQuery(searchParams.get('pageSize'))
    ?? DEFAULT_MODERATION_PAGE_SIZE;
  const returnTo = normalizeConsoleReturnTo(searchParams.get('returnTo'));
  useDocumentTitle(t('moderation.case.title'));
  const canReview = usePermission(CONSOLE_PERMISSIONS.moderationReview);
  const canAction = usePermission(CONSOLE_PERMISSIONS.moderationAction);
  const [queueItems, setQueueItems] = useState<ContentModerationCaseQueueItemVo[]>([]);
  const [queueTotal, setQueueTotal] = useState(0);
  const [keywordInput, setKeywordInput] = useState(keyword);
  const [detail, setDetail] = useState<ContentModerationCaseDetailVo | null>(null);
  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [queueReadState, setQueueReadState] = useState<QueueReadState>('loading');
  const [detailReadState, setDetailReadState] = useState<DetailReadState>('idle');
  const [submitting, setSubmitting] = useState(false);
  const [capturing, setCapturing] = useState(false);
  const [conflictNotice, setConflictNotice] = useState(false);
  const [decisionDraft, setDecisionDraft] = useState<DecisionDraft>(createDecisionDraft);
  const [correctiveDraft, setCorrectiveDraft] = useState<CorrectiveDraft>(createCorrectiveDraft);
  const draftCaseIdRef = useRef<string | null>(null);
  const caseLoadSequenceRef = useRef(0);

  const formatDateTime = useCallback((value?: string | null) => {
    if (!value) {
      return '-';
    }
    return new Intl.DateTimeFormat(language.startsWith('zh') ? 'zh-CN' : 'en-US', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }, [language]);

  const updateCaseSearch = useCallback((
    changes: Partial<Parameters<typeof buildModerationSearchParams>[0]>,
    replace = false,
  ) => {
    setSearchParams(buildModerationSearchParams({
      casePublicId: selectedCaseId,
      status: statusFilter,
      targetType: targetTypeFilter,
      keyword,
      pageIndex: queuePageIndex,
      pageSize: queuePageSize,
      returnTo,
      ...changes,
    }), { replace });
  }, [
    keyword,
    queuePageIndex,
    queuePageSize,
    returnTo,
    selectedCaseId,
    setSearchParams,
    statusFilter,
    targetTypeFilter,
  ]);

  useEffect(() => {
    setKeywordInput(keyword);
  }, [keyword]);

  const loadCase = useCallback(async (casePublicId: string, initializeDraft = true) => {
    const loadSequence = caseLoadSequenceRef.current + 1;
    caseLoadSequenceRef.current = loadSequence;
    setLoadingDetail(true);
    setDetailReadState('loading');
    setDetail((current) => (
      current?.voCase.voCasePublicId === casePublicId ? current : null
    ));
    try {
      const nextDetail = await getModerationCase(casePublicId);
      if (caseLoadSequenceRef.current !== loadSequence) {
        return;
      }
      setDetail(nextDetail);
      setDetailReadState('ready');
      if (initializeDraft && draftCaseIdRef.current !== casePublicId) {
        draftCaseIdRef.current = casePublicId;
        setDecisionDraft({
          ...createDecisionDraft(),
          internalRemark: nextDetail.voInternalRemark ?? '',
        });
        setCorrectiveDraft(createCorrectiveDraft());
        setConflictNotice(false);
      }
    } catch (error) {
      if (caseLoadSequenceRef.current !== loadSequence) {
        return;
      }
      log.error('ModerationPage', 'Failed to load moderation case detail:', error);
      if (isUnavailableError(error)) {
        setDetail(null);
        setDetailReadState('unavailable');
        message.warning(t('moderation.case.detailUnavailableTitle'));
      } else {
        setDetailReadState('stale');
        message.error(t('moderation.case.loadDetailFailed'));
      }
    } finally {
      if (caseLoadSequenceRef.current === loadSequence) {
        setLoadingDetail(false);
      }
    }
  }, [t]);

  const loadQueue = useCallback(async (requestedPage = queuePageIndex) => {
    setLoadingQueue(true);
    setQueueReadState((current) => current === 'ready' ? 'ready' : 'loading');
    try {
      const page = await getCaseQueue({
        status: statusFilter >= 0 ? statusFilter : undefined,
        targetType: targetTypeFilter,
        keyword,
        pageIndex: requestedPage,
        pageSize: queuePageSize,
      });
      setQueueItems(page.voItems);
      setQueueTotal(page.voTotal);
      setQueueReadState('ready');
    } catch (error) {
      log.error('ModerationPage', 'Failed to load moderation case queue:', error);
      setQueueReadState('stale');
      message.error(t('moderation.case.loadQueueFailed'));
    } finally {
      setLoadingQueue(false);
    }
  }, [
    keyword,
    queuePageIndex,
    queuePageSize,
    statusFilter,
    t,
    targetTypeFilter,
  ]);

  useEffect(() => {
    void loadQueue(queuePageIndex);
  }, [loadQueue, queuePageIndex]);

  useEffect(() => {
    if (!selectedCaseId) {
      caseLoadSequenceRef.current += 1;
      draftCaseIdRef.current = null;
      setDetail(null);
      setDetailReadState('idle');
      setConflictNotice(false);
      return;
    }

    void loadCase(selectedCaseId);
  }, [loadCase, selectedCaseId]);

  const latestTargetRevision = useMemo(() => resolveLatestTargetRevision(detail), [detail]);
  const latestEvidence = detail?.voEvidence.at(-1) ?? null;
  const actionFailedCount = queueItems.filter((item) => item.voTargetDisposition === 'ActionFailed').length;
  const reviewingCount = queueItems.filter((item) => item.voStatus === 'Reviewing').length;
  const actionsAreAuthoritative = queueReadState === 'ready' && detailReadState === 'ready';

  const updateDecision = (decision: DecisionValue) => {
    setDecisionDraft((current) => ({
      ...current,
      decision,
      targetDisposition: decision === 2 ? 2 : 1,
      includeUserAction: decision === 2 ? current.includeUserAction : false,
    }));
  };

  const buildUserAction = (
    actionType: UserActionValue,
    durationHours: number | null,
    reason: string,
  ): ContentModerationCaseUserActionRequest | null => {
    if (!detail || !reason.trim()) {
      return null;
    }

    return {
      actionType,
      expectedStateVersion: resolveUserStateVersion(detail, actionType),
      durationHours: actionType === 1 || actionType === 2 ? durationHours : null,
      reason: reason.trim(),
    };
  };

  const handleConflict = async (casePublicId: string) => {
    setConflictNotice(true);
    await loadCase(casePublicId, false);
    setDecisionDraft((current) => ({
      ...current,
      operationKey: createOperationKey('moderation-case-review'),
    }));
    setCorrectiveDraft((current) => ({
      ...current,
      operationKey: createOperationKey('moderation-case-corrective'),
    }));
  };

  const submitDecision = async () => {
    if (!detail || !canReview || !actionsAreAuthoritative) {
      return;
    }
    if (!decisionDraft.internalRemark.trim()) {
      message.error(t('moderation.case.internalRemarkRequired'));
      return;
    }
    if (decisionDraft.includeUserAction && !canAction) {
      message.error(t('moderation.case.actionPermissionRequired'));
      return;
    }

    const userAction = decisionDraft.includeUserAction
      ? buildUserAction(
          decisionDraft.userActionType,
          decisionDraft.durationHours,
          decisionDraft.userActionReason,
        )
      : null;
    if (decisionDraft.includeUserAction && !userAction) {
      message.error(t('moderation.case.userActionReasonRequired'));
      return;
    }

    const requiresTargetRevision = ['Post', 'Comment', 'PostAnswer', 'Product']
      .includes(detail.voCase.voTargetType);
    if (
      decisionDraft.targetDisposition === 2
      && requiresTargetRevision
      && latestTargetRevision == null
    ) {
      message.error(t('moderation.case.captureBeforeRestrict'));
      return;
    }

    setSubmitting(true);
    try {
      await reviewModerationCase({
        casePublicId: detail.voCase.voCasePublicId,
        expectedVersion: detail.voCase.voVersion,
        decision: decisionDraft.decision,
        targetDisposition: decisionDraft.targetDisposition,
        expectedTargetVersion: latestTargetRevision,
        publicResultCode: publicResultCodeByDecision[decisionDraft.decision],
        internalRemark: decisionDraft.internalRemark.trim(),
        userAction,
        operationKey: decisionDraft.operationKey,
      });
      message.success(t('moderation.case.reviewSuccess'));
      draftCaseIdRef.current = null;
      setConflictNotice(false);
      await loadQueue(queuePageIndex);
      await loadCase(detail.voCase.voCasePublicId);
    } catch (error) {
      if (isConflictError(error)) {
        await handleConflict(detail.voCase.voCasePublicId);
        message.warning(t('moderation.case.conflictDraftPreserved'));
      } else {
        log.error('ModerationPage', 'Failed to review moderation case:', error);
        message.error(t('moderation.case.reviewFailed'));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const captureCurrentEvidence = async () => {
    if (!detail || !canReview || !actionsAreAuthoritative) {
      return;
    }

    setCapturing(true);
    try {
      const nextDetail = await captureModerationEvidence({
        casePublicId: detail.voCase.voCasePublicId,
        expectedVersion: detail.voCase.voVersion,
        evidenceType: 2,
      });
      setDetail(nextDetail);
      setConflictNotice(false);
      message.success(t('moderation.case.captureSuccess'));
      await loadQueue(queuePageIndex);
    } catch (error) {
      if (isConflictError(error)) {
        await handleConflict(detail.voCase.voCasePublicId);
        message.warning(t('moderation.case.conflictDraftPreserved'));
      } else {
        log.error('ModerationPage', 'Failed to capture moderation evidence:', error);
        message.error(t('moderation.case.captureFailed'));
      }
    } finally {
      setCapturing(false);
    }
  };

  const submitCorrectiveAction = async () => {
    if (!detail || !canAction || !actionsAreAuthoritative || !correctiveDraft.reason.trim()) {
      message.error(t('moderation.case.userActionReasonRequired'));
      return;
    }

    const userAction = buildUserAction(
      correctiveDraft.actionType,
      correctiveDraft.durationHours,
      correctiveDraft.reason,
    );
    if (!userAction) {
      return;
    }

    setSubmitting(true);
    try {
      await applyModerationCorrectiveAction({
        casePublicId: detail.voCase.voCasePublicId,
        expectedVersion: detail.voCase.voVersion,
        userAction,
        operationKey: correctiveDraft.operationKey,
        remark: correctiveDraft.reason.trim(),
      });
      message.success(t('moderation.case.correctiveSuccess'));
      setCorrectiveDraft(createCorrectiveDraft());
      await loadCase(detail.voCase.voCasePublicId, false);
      await loadQueue(queuePageIndex);
    } catch (error) {
      if (isConflictError(error)) {
        await handleConflict(detail.voCase.voCasePublicId);
        message.warning(t('moderation.case.conflictDraftPreserved'));
      } else {
        log.error('ModerationPage', 'Failed to apply corrective moderation action:', error);
        message.error(t('moderation.case.correctiveFailed'));
      }
    } finally {
      setSubmitting(false);
    }
  };

  const applyKeyword = () => {
    updateCaseSearch({
      casePublicId: null,
      keyword: keywordInput.trim(),
      pageIndex: DEFAULT_MODERATION_PAGE_INDEX,
    });
  };

  const selectCase = (casePublicId: string) => {
    updateCaseSearch({ casePublicId });
  };

  const closeCase = () => {
    updateCaseSearch({ casePublicId: null }, true);
  };

  const refreshWorkspace = async () => {
    const reads: Promise<void>[] = [loadQueue(queuePageIndex)];
    if (selectedCaseId) {
      reads.push(loadCase(selectedCaseId, false));
    }
    await Promise.all(reads);
  };

  return (
    <div
      className="admin-feature-page moderation-case-page"
      data-task-active={selectedCaseId ? 'true' : 'false'}
    >
      <ConsolePageHeader
        eyebrow={t('moderation.title')}
        title={t('moderation.case.title')}
        description={t('moderation.case.description')}
        icon={<SafetyOutlined />}
        status={(
          <Space wrap>
            <ConsoleStatusChip tone={canReview ? 'success' : 'neutral'}>
              {t(canReview
                ? 'moderation.case.canReview'
                : canAction
                  ? 'moderation.case.noReviewPermission'
                  : 'moderation.readOnly')}
            </ConsoleStatusChip>
            <ConsoleStatusChip tone={canAction ? 'warning' : 'neutral'}>
              {t(canAction ? 'moderation.case.canAction' : 'moderation.case.noActionPermission')}
            </ConsoleStatusChip>
          </Space>
        )}
        actions={(
          <Space wrap>
            {returnTo ? (
              <Button onClick={() => navigate(returnTo)}>
                {t('moderation.backToSource')}
              </Button>
            ) : null}
            <Button onClick={() => navigate(buildModerationPath({ view: 'appeals', returnTo }))}>
              {t('moderation.case.openAppeals')}
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

      <ConsoleMetricGrid label={t('moderation.case.metricsLabel')}>
        <ConsoleMetricCard label={t('moderation.case.metric.total')} value={queueTotal} tone="info" />
        <ConsoleMetricCard label={t('moderation.case.metric.reviewing')} value={reviewingCount} tone="warning" />
        <ConsoleMetricCard label={t('moderation.case.metric.actionFailed')} value={actionFailedCount} tone={actionFailedCount ? 'danger' : 'neutral'} />
        <ConsoleMetricCard label={t('moderation.case.metric.visible')} value={queueItems.length} />
      </ConsoleMetricGrid>

      <section className="admin-feature-card moderation-case-filters">
        <Select
          value={statusFilter}
          aria-label={t('moderation.case.filter.allStatuses')}
          className="moderation-filter-control moderation-filter-control--md"
          options={[
            { value: -1, label: t('moderation.case.filter.allStatuses') },
            { value: 0, label: t('moderation.case.status.Open') },
            { value: 1, label: t('moderation.case.status.Reviewing') },
            { value: 2, label: t('moderation.case.status.Resolved') },
          ]}
          onChange={(status) => updateCaseSearch({
            casePublicId: null,
            status,
            pageIndex: DEFAULT_MODERATION_PAGE_INDEX,
          })}
        />
        <Select
          allowClear
          value={targetTypeFilter}
          aria-label={t('moderation.case.filter.targetType')}
          placeholder={t('moderation.case.filter.targetType')}
          className="moderation-filter-control moderation-filter-control--md"
          options={MODERATION_TARGET_TYPES.map((value) => ({
            value,
            label: t(`moderation.targetType.${value}`),
          }))}
          onChange={(targetType) => updateCaseSearch({
            casePublicId: null,
            targetType,
            pageIndex: DEFAULT_MODERATION_PAGE_INDEX,
          })}
        />
        <Input
          value={keywordInput}
          aria-label={t('moderation.case.filter.keyword')}
          className="moderation-filter-control moderation-filter-control--xl"
          placeholder={t('moderation.case.filter.keyword')}
          onChange={(event) => setKeywordInput(event.target.value)}
          onPressEnter={applyKeyword}
        />
        <Button variant="primary" onClick={applyKeyword}>
          {t('moderation.query')}
        </Button>
        <Button onClick={() => {
          setKeywordInput('');
          updateCaseSearch({
            casePublicId: null,
            status: undefined,
            targetType: undefined,
            keyword: undefined,
            pageIndex: DEFAULT_MODERATION_PAGE_INDEX,
            pageSize: DEFAULT_MODERATION_PAGE_SIZE,
          });
        }}>
          {t('moderation.reset')}
        </Button>
      </section>

      <section className="moderation-case-workbench">
        <aside className="moderation-case-queue" aria-label={t('moderation.case.queueTitle')}>
          <div className="moderation-case-section-heading">
            <div>
              <h2>{t('moderation.case.queueTitle')}</h2>
              <p>{t('moderation.case.queueDescription')}</p>
            </div>
            <ConsoleStatusChip tone="info">{queueTotal}</ConsoleStatusChip>
          </div>
          {queueReadState === 'stale' ? (
            <div className="moderation-case-read-state moderation-case-read-state--stale" role="alert">
              <strong>{t('moderation.case.queueStaleTitle')}</strong>
              <span>{t('moderation.case.queueStaleDescription')}</span>
            </div>
          ) : null}
          <div className="moderation-case-queue-list">
            {queueItems.map((item) => (
              <button
                key={item.voCasePublicId}
                type="button"
                className="moderation-case-queue-item"
                data-active={item.voCasePublicId === selectedCaseId}
                onClick={() => selectCase(item.voCasePublicId)}
              >
                <span className="moderation-case-queue-item__topline">
                  <strong>{item.voCasePublicId}</strong>
                  <ConsoleStatusChip tone={statusTone(item.voStatus)}>
                    {t(`moderation.case.status.${item.voStatus}`)}
                  </ConsoleStatusChip>
                </span>
                <span>{t(`moderation.targetType.${item.voTargetType}`)} · #{item.voTargetContentId}</span>
                <span>{t('moderation.case.reportCount', { count: item.voReportCount })}</span>
                <span className="moderation-case-queue-item__bottomline">
                  <small>{formatDateTime(item.voModifiedAt ?? item.voOpenedAt)}</small>
                  <ConsoleStatusChip tone={dispositionTone(item.voTargetDisposition)}>
                    {t(`moderation.case.disposition.${item.voTargetDisposition}`)}
                  </ConsoleStatusChip>
                </span>
              </button>
            ))}
            {!loadingQueue && queueItems.length === 0 ? (
              <p className="admin-feature-rail__empty">{t('moderation.case.queueEmpty')}</p>
            ) : null}
          </div>
          <div className="moderation-case-pagination">
            <Button
              disabled={queuePageIndex <= 1}
              onClick={() => updateCaseSearch({
                casePublicId: null,
                pageIndex: queuePageIndex - 1,
              })}
            >
              {t('moderation.case.previous')}
            </Button>
            <span>{queuePageIndex}</span>
            <Button
              disabled={queuePageIndex * queuePageSize >= queueTotal}
              onClick={() => updateCaseSearch({
                casePublicId: null,
                pageIndex: queuePageIndex + 1,
              })}
            >
              {t('moderation.case.next')}
            </Button>
          </div>
        </aside>

        <main
          className="moderation-case-detail"
          data-console-fullscreen-task={selectedCaseId ? 'moderation-case' : undefined}
        >
          {selectedCaseId ? (
            <header className="moderation-case-task-header">
              <Button variant="ghost" size="small" icon={<LeftOutlined />} onClick={closeCase}>
                {t('moderation.case.backToQueue')}
              </Button>
              <strong>{selectedCaseId}</strong>
              <Button variant="ghost" size="small" onClick={closeCase}>
                {t('moderation.case.closeDetail')}
              </Button>
            </header>
          ) : null}

          {detailReadState === 'unavailable' ? (
            <section className="admin-feature-card moderation-case-empty-state" role="alert">
              <strong>{t('moderation.case.detailUnavailableTitle')}</strong>
              <span>{t('moderation.case.detailUnavailableDescription')}</span>
              <Space wrap>
                <Button onClick={closeCase}>{t('moderation.case.backToQueue')}</Button>
                {selectedCaseId ? (
                  <Button icon={<ReloadOutlined />} onClick={() => void loadCase(selectedCaseId, false)}>
                    {t('moderation.case.retryDetail')}
                  </Button>
                ) : null}
              </Space>
            </section>
          ) : detailReadState === 'stale' && !detail ? (
            <section className="admin-feature-card moderation-case-empty-state" role="alert">
              <strong>{t('moderation.case.detailReadFailedTitle')}</strong>
              <span>{t('moderation.case.detailReadFailedDescription')}</span>
              <Space wrap>
                <Button onClick={closeCase}>{t('moderation.case.backToQueue')}</Button>
                {selectedCaseId ? (
                  <Button icon={<ReloadOutlined />} onClick={() => void loadCase(selectedCaseId, false)}>
                    {t('moderation.case.retryDetail')}
                  </Button>
                ) : null}
              </Space>
            </section>
          ) : !detail ? (
            <section className="admin-feature-card moderation-case-empty-state">
              <span>{t(selectedCaseId
                ? 'moderation.case.loadingDetail'
                : 'moderation.case.selectCase')}</span>
            </section>
          ) : (
            <>
              {detailReadState === 'loading' || detailReadState === 'stale' ? (
                <div
                  className={`moderation-case-read-state moderation-case-read-state--${detailReadState}`}
                  role={detailReadState === 'stale' ? 'alert' : 'status'}
                >
                  <strong>{t(detailReadState === 'stale'
                    ? 'moderation.case.detailStaleTitle'
                    : 'moderation.case.detailVerifyingTitle')}</strong>
                  <span>{t(detailReadState === 'stale'
                    ? 'moderation.case.detailStaleDescription'
                    : 'moderation.case.detailVerifyingDescription')}</span>
                </div>
              ) : null}
              <section className="admin-feature-card moderation-case-summary">
                <div className="moderation-case-section-heading">
                  <div>
                    <span className="admin-feature-rail__eyebrow">{detail.voCase.voCasePublicId}</span>
                    <h2>{latestEvidence?.voSnapshotTitle || `${detail.voCase.voTargetType} #${detail.voCase.voTargetContentId}`}</h2>
                    <p>{latestEvidence?.voSnapshotSummary || t('moderation.case.snapshotUnavailable')}</p>
                  </div>
                  <Space wrap>
                    <ConsoleStatusChip tone={statusTone(detail.voCase.voStatus)}>
                      {t(`moderation.case.status.${detail.voCase.voStatus}`)} · v{detail.voCase.voVersion}
                    </ConsoleStatusChip>
                    <ConsoleStatusChip tone={dispositionTone(detail.voCase.voTargetDisposition)}>
                      {t(`moderation.case.disposition.${detail.voCase.voTargetDisposition}`)}
                    </ConsoleStatusChip>
                  </Space>
                </div>
                <div className="moderation-case-summary-grid">
                  <span><strong>{t('moderation.case.targetType')}</strong>{t(`moderation.targetType.${detail.voCase.voTargetType}`)}</span>
                  <span><strong>{t('moderation.case.targetUser')}</strong>#{detail.voCase.voTargetUserId}</span>
                  <span><strong>{t('moderation.case.reportTotal')}</strong>{detail.voReports.length}</span>
                  <span><strong>{t('moderation.case.targetRevision')}</strong>{latestTargetRevision ?? '-'}</span>
                </div>
              </section>

              {conflictNotice ? (
                <div className="moderation-case-conflict" role="alert">
                  <strong>{t('moderation.case.conflictTitle')}</strong>
                  <span>{t('moderation.case.conflictDraftPreserved')}</span>
                </div>
              ) : null}

              {!canReview && !canAction ? (
                <div className="moderation-case-read-state moderation-case-read-state--readonly" role="note">
                  <strong>{t('moderation.case.readOnlyTitle')}</strong>
                  <span>{t('moderation.case.readOnlyDescription')}</span>
                </div>
              ) : null}

              <section className="admin-feature-card">
                <div className="moderation-case-section-heading">
                  <div>
                    <h3>{t('moderation.case.evidenceTitle')}</h3>
                    <p>{t('moderation.case.evidenceDescription')}</p>
                  </div>
                  {canReview && actionsAreAuthoritative && detail.voCase.voStatus !== 'Resolved' ? (
                    <Button disabled={capturing} onClick={() => void captureCurrentEvidence()}>
                      {capturing ? t('moderation.loading') : t('moderation.case.captureCurrent')}
                    </Button>
                  ) : null}
                </div>
                <div className="moderation-case-timeline">
                  {detail.voEvidence.map((item) => (
                    <article key={item.voSequence}>
                      <div>
                        <strong>#{item.voSequence} · {t(`moderation.case.evidenceType.${item.voEvidenceType}`)}</strong>
                        <ConsoleStatusChip tone={item.voTargetState === 'Available' ? 'success' : 'warning'}>
                          {t(`moderation.case.targetState.${item.voTargetState}`)}
                        </ConsoleStatusChip>
                      </div>
                      <h4>{item.voSnapshotTitle || t('moderation.case.untitledEvidence')}</h4>
                      <p>{item.voSnapshotSummary || t('moderation.case.noEvidenceSummary')}</p>
                      <small>
                        {formatDateTime(item.voCapturedAt)}
                        {item.voContentRevision != null ? ` · revision ${item.voContentRevision}` : ''}
                      </small>
                    </article>
                  ))}
                </div>
              </section>

              <section className="admin-feature-card">
                <div className="moderation-case-section-heading">
                  <div>
                    <h3>{t('moderation.case.reportsTitle')}</h3>
                    <p>{t('moderation.case.reportsDescription')}</p>
                  </div>
                </div>
                <div className="moderation-case-report-list">
                  {detail.voReports.map((report) => (
                    <article key={report.voReportPublicId}>
                      <strong>{report.voReporterUserName}</strong>
                      <span>{report.voReasonType}</span>
                      <p>{report.voReasonDetail || t('moderation.case.noReasonDetail')}</p>
                      <small>{formatDateTime(report.voCreateTime)}</small>
                    </article>
                  ))}
                </div>
              </section>

              {canReview && actionsAreAuthoritative && detail.voCase.voStatus !== 'Resolved' ? (
                <section className="admin-feature-card moderation-case-decision">
                  <div className="moderation-case-section-heading">
                    <div>
                      <h3>{t('moderation.case.decisionTitle')}</h3>
                      <p>{t('moderation.case.decisionDescription')}</p>
                    </div>
                    <ConsoleStatusChip tone={canAction ? 'warning' : 'neutral'}>
                      {t(canAction ? 'moderation.case.actionPayloadAllowed' : 'moderation.case.decisionOnly')}
                    </ConsoleStatusChip>
                  </div>
                  <div className="moderation-case-form-grid">
                    <label>
                      <span>{t('moderation.case.decision')}</span>
                      <Select
                        value={decisionDraft.decision}
                        options={[
                          { value: 1, label: t('moderation.case.decision.NoViolation') },
                          { value: 2, label: t('moderation.case.decision.Violation') },
                          { value: 3, label: t('moderation.case.decision.InsufficientEvidence') },
                        ]}
                        onChange={updateDecision}
                      />
                    </label>
                    <label>
                      <span>{t('moderation.case.targetDisposition')}</span>
                      <Select
                        value={decisionDraft.targetDisposition}
                        options={
                          decisionDraft.decision === 2
                            ? [
                                { value: 2, label: t('moderation.case.disposition.Restricted') },
                                { value: 1, label: t('moderation.case.disposition.Keep') },
                              ]
                            : decisionDraft.decision === 3
                              ? [
                                  { value: 1, label: t('moderation.case.disposition.Keep') },
                                  { value: 3, label: t('moderation.case.disposition.Unavailable') },
                                ]
                              : [{ value: 1, label: t('moderation.case.disposition.Keep') }]
                        }
                        onChange={(value: TargetDispositionValue) => {
                          setDecisionDraft((current) => ({ ...current, targetDisposition: value }));
                        }}
                      />
                    </label>
                  </div>
                  <label className="moderation-case-field">
                    <span>{t('moderation.case.internalRemark')}</span>
                    <Input.TextArea
                      rows={4}
                      maxLength={1000}
                      value={decisionDraft.internalRemark}
                      placeholder={t('moderation.case.internalRemarkPlaceholder')}
                      onChange={(event) => {
                        setDecisionDraft((current) => ({ ...current, internalRemark: event.target.value }));
                      }}
                    />
                  </label>

                  {canAction && decisionDraft.decision === 2 ? (
                    <div className="moderation-case-user-action">
                      <label className="moderation-case-action-toggle">
                        <input
                          type="checkbox"
                          checked={decisionDraft.includeUserAction}
                          onChange={(event) => {
                            setDecisionDraft((current) => ({
                              ...current,
                              includeUserAction: event.target.checked,
                            }));
                          }}
                        />
                        <span>{t('moderation.case.includeUserAction')}</span>
                      </label>
                      {decisionDraft.includeUserAction ? (
                        <>
                          <div className="moderation-case-form-grid">
                            <label>
                              <span>{t('moderation.case.userAction')}</span>
                              <Select
                                value={decisionDraft.userActionType}
                                options={[1, 2, 3, 4].map((value) => ({
                                  value,
                                  label: t(`moderation.case.userAction.${value}`),
                                }))}
                                onChange={(value: UserActionValue) => {
                                  setDecisionDraft((current) => ({ ...current, userActionType: value }));
                                }}
                              />
                            </label>
                            <label>
                              <span>{t('moderation.case.durationHours')}</span>
                              <InputNumber
                                min={1}
                                max={720}
                                disabled={decisionDraft.userActionType === 3 || decisionDraft.userActionType === 4}
                                value={decisionDraft.durationHours}
                                onChange={(value) => {
                                  setDecisionDraft((current) => ({
                                    ...current,
                                    durationHours: typeof value === 'number' ? value : null,
                                  }));
                                }}
                              />
                            </label>
                          </div>
                          <label className="moderation-case-field">
                            <span>{t('moderation.case.userActionReason')}</span>
                            <Input.TextArea
                              rows={3}
                              maxLength={500}
                              value={decisionDraft.userActionReason}
                              onChange={(event) => {
                                setDecisionDraft((current) => ({
                                  ...current,
                                  userActionReason: event.target.value,
                                }));
                              }}
                            />
                          </label>
                        </>
                      ) : null}
                    </div>
                  ) : null}
                  <Button
                    variant="primary"
                    disabled={submitting}
                    onClick={() => void submitDecision()}
                  >
                    {submitting ? t('moderation.loading') : t('moderation.case.submitDecision')}
                  </Button>
                </section>
              ) : null}

              {detail.voCase.voStatus === 'Resolved' && canAction && actionsAreAuthoritative ? (
                <section className="admin-feature-card moderation-case-decision">
                  <div className="moderation-case-section-heading">
                    <div>
                      <h3>{t('moderation.case.correctiveTitle')}</h3>
                      <p>{t('moderation.case.correctiveDescription')}</p>
                    </div>
                  </div>
                  <div className="moderation-case-form-grid">
                    <label>
                      <span>{t('moderation.case.userAction')}</span>
                      <Select
                        value={correctiveDraft.actionType}
                        options={[1, 2, 3, 4].map((value) => ({
                          value,
                          label: t(`moderation.case.userAction.${value}`),
                        }))}
                        onChange={(value: UserActionValue) => {
                          setCorrectiveDraft((current) => ({ ...current, actionType: value }));
                        }}
                      />
                    </label>
                    <label>
                      <span>{t('moderation.case.durationHours')}</span>
                      <InputNumber
                        min={1}
                        max={720}
                        disabled={correctiveDraft.actionType === 3 || correctiveDraft.actionType === 4}
                        value={correctiveDraft.durationHours}
                        onChange={(value) => {
                          setCorrectiveDraft((current) => ({
                            ...current,
                            durationHours: typeof value === 'number' ? value : null,
                          }));
                        }}
                      />
                    </label>
                  </div>
                  <label className="moderation-case-field">
                    <span>{t('moderation.case.correctiveRemark')}</span>
                    <Input.TextArea
                      rows={3}
                      maxLength={500}
                      value={correctiveDraft.reason}
                      onChange={(event) => {
                        setCorrectiveDraft((current) => ({ ...current, reason: event.target.value }));
                      }}
                    />
                  </label>
                  <Button
                    variant="danger"
                    disabled={submitting}
                    onClick={() => void submitCorrectiveAction()}
                  >
                    {submitting ? t('moderation.loading') : t('moderation.case.submitCorrective')}
                  </Button>
                </section>
              ) : null}

              <section className="admin-feature-card">
                <div className="moderation-case-section-heading">
                  <div>
                    <h3>{t('moderation.case.eventsTitle')}</h3>
                    <p>{t('moderation.case.eventsDescription')}</p>
                  </div>
                </div>
                <div className="moderation-case-timeline moderation-case-timeline--events">
                  {detail.voEvents.map((event) => (
                    <article key={event.voSequence}>
                      <div>
                        <strong>#{event.voSequence} · {event.voEventType}</strong>
                        <small>v{event.voExpectedCaseVersion} → v{event.voResultCaseVersion}</small>
                      </div>
                      <p>{event.voRemark || event.voResultCode || t('moderation.case.noEventRemark')}</p>
                      <small>{event.voActorName} · {formatDateTime(event.voCreateTime)}</small>
                    </article>
                  ))}
                </div>
              </section>
            </>
          )}
        </main>
      </section>
    </div>
  );
};

export const ModerationPage = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const returnTo = normalizeConsoleReturnTo(searchParams.get('returnTo'));

  if (searchParams.get('view') === 'appeals') {
    return (
      <ModerationAppealsWorkspace
        onOpenCases={() => navigate(buildModerationPath({ returnTo }))}
      />
    );
  }

  return <ModerationCasesWorkspace />;
};

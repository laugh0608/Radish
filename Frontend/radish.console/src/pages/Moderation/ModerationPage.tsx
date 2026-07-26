import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import {
  ApiResponseError,
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
import { ReloadOutlined, SafetyOutlined } from '@radish/ui';
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
import { buildModerationPath } from './moderationPageUrlState';
import './index.css';
import '../adminFeature.css';

type DecisionValue = 1 | 2 | 3;
type TargetDispositionValue = 1 | 2 | 3;
type UserActionValue = 1 | 2 | 3 | 4;

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
  const [searchParams] = useSearchParams();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const linkedKeyword = searchParams.get('keyword')?.trim() ?? '';
  const returnTo = normalizeConsoleReturnTo(searchParams.get('returnTo'));
  useDocumentTitle(t('moderation.case.title'));
  const canReview = usePermission(CONSOLE_PERMISSIONS.moderationReview);
  const canAction = usePermission(CONSOLE_PERMISSIONS.moderationAction);
  const [queueItems, setQueueItems] = useState<ContentModerationCaseQueueItemVo[]>([]);
  const [queueTotal, setQueueTotal] = useState(0);
  const [queuePageIndex, setQueuePageIndex] = useState(1);
  const [queuePageSize, setQueuePageSize] = useState(20);
  const [statusFilter, setStatusFilter] = useState<number>(-1);
  const [targetTypeFilter, setTargetTypeFilter] = useState<string>();
  const [keywordInput, setKeywordInput] = useState(linkedKeyword);
  const [keyword, setKeyword] = useState(linkedKeyword);
  const [selectedCaseId, setSelectedCaseId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ContentModerationCaseDetailVo | null>(null);
  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [capturing, setCapturing] = useState(false);
  const [conflictNotice, setConflictNotice] = useState(false);
  const [decisionDraft, setDecisionDraft] = useState<DecisionDraft>(createDecisionDraft);
  const [correctiveDraft, setCorrectiveDraft] = useState<CorrectiveDraft>(createCorrectiveDraft);
  const draftCaseIdRef = useRef<string | null>(null);

  const formatDateTime = useCallback((value?: string | null) => {
    if (!value) {
      return '-';
    }
    return new Intl.DateTimeFormat(language.startsWith('zh') ? 'zh-CN' : 'en-US', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }, [language]);

  const loadCase = useCallback(async (casePublicId: string, initializeDraft = true) => {
    setLoadingDetail(true);
    try {
      const nextDetail = await getModerationCase(casePublicId);
      setDetail(nextDetail);
      setSelectedCaseId(casePublicId);
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
      log.error('ModerationPage', 'Failed to load moderation case detail:', error);
      message.error(t('moderation.case.loadDetailFailed'));
    } finally {
      setLoadingDetail(false);
    }
  }, [t]);

  const loadQueue = useCallback(async (requestedPage = queuePageIndex) => {
    setLoadingQueue(true);
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
      setQueuePageIndex(page.voPageIndex);
      setQueuePageSize(page.voPageSize);

      const selectedStillVisible = selectedCaseId
        && page.voItems.some((item) => item.voCasePublicId === selectedCaseId);
      const nextCaseId = selectedStillVisible ? selectedCaseId : page.voItems[0]?.voCasePublicId;
      if (nextCaseId && nextCaseId !== selectedCaseId) {
        await loadCase(nextCaseId);
      }
      if (!nextCaseId) {
        setSelectedCaseId(null);
        setDetail(null);
      }
    } catch (error) {
      log.error('ModerationPage', 'Failed to load moderation case queue:', error);
      message.error(t('moderation.case.loadQueueFailed'));
    } finally {
      setLoadingQueue(false);
    }
  }, [
    keyword,
    loadCase,
    queuePageIndex,
    queuePageSize,
    selectedCaseId,
    statusFilter,
    t,
    targetTypeFilter,
  ]);

  useEffect(() => {
    void loadQueue(1);
    // The callback owns the current filter snapshot and intentionally resets pagination.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter, targetTypeFilter, keyword, queuePageSize]);

  const latestTargetRevision = useMemo(() => resolveLatestTargetRevision(detail), [detail]);
  const latestEvidence = detail?.voEvidence.at(-1) ?? null;
  const actionFailedCount = queueItems.filter((item) => item.voTargetDisposition === 'ActionFailed').length;
  const reviewingCount = queueItems.filter((item) => item.voStatus === 'Reviewing').length;

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
    if (!detail || !canReview) {
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

    const requiresTargetRevision = ['Post', 'Comment', 'Product'].includes(detail.voCase.voTargetType);
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
    if (!detail || !canReview) {
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
    if (!detail || !canAction || !correctiveDraft.reason.trim()) {
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

  return (
    <div className="admin-feature-page moderation-case-page">
      <ConsolePageHeader
        eyebrow={t('moderation.title')}
        title={t('moderation.case.title')}
        description={t('moderation.case.description')}
        icon={<SafetyOutlined />}
        status={(
          <Space wrap>
            <ConsoleStatusChip tone={canReview ? 'success' : 'neutral'}>
              {t(canReview ? 'moderation.case.canReview' : 'moderation.readOnly')}
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
              onClick={() => void loadQueue(queuePageIndex)}
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
          onChange={setStatusFilter}
        />
        <Select
          allowClear
          value={targetTypeFilter}
          aria-label={t('moderation.case.filter.targetType')}
          placeholder={t('moderation.case.filter.targetType')}
          className="moderation-filter-control moderation-filter-control--md"
          options={['Post', 'Comment', 'PostQuickReply', 'ChatMessage', 'Product'].map((value) => ({
            value,
            label: t(`moderation.targetType.${value}`),
          }))}
          onChange={setTargetTypeFilter}
        />
        <Input
          value={keywordInput}
          aria-label={t('moderation.case.filter.keyword')}
          className="moderation-filter-control moderation-filter-control--xl"
          placeholder={t('moderation.case.filter.keyword')}
          onChange={(event) => setKeywordInput(event.target.value)}
          onPressEnter={() => setKeyword(keywordInput.trim())}
        />
        <Button variant="primary" onClick={() => setKeyword(keywordInput.trim())}>
          {t('moderation.query')}
        </Button>
        <Button onClick={() => {
          setStatusFilter(-1);
          setTargetTypeFilter(undefined);
          setKeywordInput('');
          setKeyword('');
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
          <div className="moderation-case-queue-list">
            {queueItems.map((item) => (
              <button
                key={item.voCasePublicId}
                type="button"
                className="moderation-case-queue-item"
                data-active={item.voCasePublicId === selectedCaseId}
                onClick={() => void loadCase(item.voCasePublicId)}
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
              onClick={() => void loadQueue(queuePageIndex - 1)}
            >
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

        <main className="moderation-case-detail">
          {!detail ? (
            <div className="admin-feature-card">
              <p className="admin-feature-rail__empty">{t('moderation.case.selectCase')}</p>
            </div>
          ) : (
            <>
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

              <section className="admin-feature-card">
                <div className="moderation-case-section-heading">
                  <div>
                    <h3>{t('moderation.case.evidenceTitle')}</h3>
                    <p>{t('moderation.case.evidenceDescription')}</p>
                  </div>
                  {canReview && detail.voCase.voStatus !== 'Resolved' ? (
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

              {detail.voCase.voStatus !== 'Resolved' ? (
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
                    disabled={!canReview || submitting}
                    onClick={() => void submitDecision()}
                  >
                    {submitting ? t('moderation.loading') : t('moderation.case.submitDecision')}
                  </Button>
                </section>
              ) : null}

              {detail.voCase.voStatus === 'Resolved' && canAction ? (
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

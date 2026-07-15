import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  Button,
  Form,
  InputNumber,
  Space,
  Table,
  message,
} from '@radish/ui';
import { ReloadOutlined, SafetyOutlined } from '@radish/ui';
import {
  applyUserModerationAction,
  getActionLogs,
  getReviewQueue,
  reviewReport,
  type ContentReportQueueItemVo,
  type UserModerationActionVo,
} from '@/api/moderationApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
} from '@/components/ConsolePage';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import {
  MANUAL_ACTION_TYPE,
  buildManualModerationStatusSnapshot,
  buildQueueTargetDisplayInput,
  getActionLogActionTypeOptions,
  getActionLogStatusOptions,
  getActionOptions,
  getActionTypeText,
  getReasonTypeLabel,
  getReasonTypeOptions,
  getReviewStatusOptions,
  getTargetNavigationStatusOptions,
  getTargetTypeLabel,
  getTargetTypeOptions,
  hasPositiveLongId,
  resolveNavigationStatusLabel,
  resolveMissingTargetMessage,
  resolveOpenTarget,
  toOptionalString,
  toPositiveLongString,
  type ActionLogActiveFilter,
  type ActionLogPreset,
  type ManualActionPreset,
  type ManualModerationStatusSnapshot,
  type ModerationTargetNavigationStateInput,
  type QueuePreset,
} from './moderationPageHelpers';
import {
  parseModerationLongIdQuery,
  parseModerationSectionQuery,
} from './moderationPageUrlState';
import { normalizeConsoleReturnTo } from '@/utils/returnTo';
import { ModerationTargetDisplay } from './moderationPageRenderers';
import {
  createModerationLogColumns,
  createModerationQueueColumns,
} from './moderationPageColumns';
import { ManualModerationActionSection } from './ManualModerationActionSection';
import './index.css';
import '../adminFeature.css';

export const ModerationPage = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('moderation.title'));
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const querySection = parseModerationSectionQuery(searchParams.get('section'));
  const queryTargetUserId = parseModerationLongIdQuery(searchParams.get('targetUserId'));
  const querySourceReportId = parseModerationLongIdQuery(searchParams.get('sourceReportId'));
  const returnTo = normalizeConsoleReturnTo(searchParams.get('returnTo'));

  const [form] = Form.useForm();
  const [manualActionForm] = Form.useForm();
  const queueSectionRef = useRef<HTMLElement | null>(null);
  const manualActionSectionRef = useRef<HTMLElement | null>(null);
  const logSectionRef = useRef<HTMLElement | null>(null);
  const manualStatusRequestIdRef = useRef(0);
  const appliedUrlPresetRef = useRef<string | null>(null);
  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingLogs, setLoadingLogs] = useState(false);
  const [queueItems, setQueueItems] = useState<ContentReportQueueItemVo[]>([]);
  const [queueTotal, setQueueTotal] = useState(0);
  const [queuePageIndex, setQueuePageIndex] = useState(1);
  const [queuePageSize, setQueuePageSize] = useState(20);
  const [statusFilter, setStatusFilter] = useState(-1);
  const [queueTargetTypeFilter, setQueueTargetTypeFilter] = useState<string | undefined>();
  const [queueReasonTypeFilter, setQueueReasonTypeFilter] = useState<string | undefined>();
  const [queueNavigationStatusFilter, setQueueNavigationStatusFilter] = useState<string | undefined>();
  const [queueKeywordInput, setQueueKeywordInput] = useState('');
  const [queueKeyword, setQueueKeyword] = useState('');
  const [queueContextHint, setQueueContextHint] = useState<string | null>(null);
  const [logItems, setLogItems] = useState<UserModerationActionVo[]>([]);
  const [logTotal, setLogTotal] = useState(0);
  const [logPageIndex, setLogPageIndex] = useState(1);
  const [logPageSize, setLogPageSize] = useState(10);
  const [logTargetUserIdInput, setLogTargetUserIdInput] = useState('');
  const [logTargetUserId, setLogTargetUserId] = useState('');
  const [logSourceReportIdInput, setLogSourceReportIdInput] = useState('');
  const [logSourceReportId, setLogSourceReportId] = useState('');
  const [logActionTypeFilter, setLogActionTypeFilter] = useState<string | undefined>();
  const [logIsActiveFilter, setLogIsActiveFilter] = useState<ActionLogActiveFilter | undefined>();
  const [logKeywordInput, setLogKeywordInput] = useState('');
  const [logKeyword, setLogKeyword] = useState('');
  const [logContextHint, setLogContextHint] = useState<string | null>(null);
  const [manualActionContextHint, setManualActionContextHint] = useState<string | null>(null);
  const [manualStatusLoading, setManualStatusLoading] = useState(false);
  const [manualStatusError, setManualStatusError] = useState<string | null>(null);
  const [manualStatusSnapshot, setManualStatusSnapshot] = useState<ManualModerationStatusSnapshot | null>(null);
  const [reviewingItem, setReviewingItem] = useState<ContentReportQueueItemVo | null>(null);
  const [submittingReview, setSubmittingReview] = useState(false);
  const [submittingManualAction, setSubmittingManualAction] = useState(false);

  const canReview = usePermission(CONSOLE_PERMISSIONS.moderationReview);

  const focusQueueSection = () => {
    queueSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const focusManualActionSection = () => {
    manualActionSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const focusLogSection = () => {
    logSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const handleOpenTarget = (input: ModerationTargetNavigationStateInput) => {
    const target = resolveOpenTarget(input, t);
    if (!target) {
      message.error(resolveMissingTargetMessage(input.targetType, input.navigationMessage, t));
      return;
    }

    window.open(target.url, '_blank', 'noopener');
  };

  const loadQueue = async (
    targetPageIndex = queuePageIndex,
    targetPageSize = queuePageSize,
    overrides?: {
      status?: number;
      targetType?: string;
      reasonType?: string;
      navigationStatus?: string;
      keyword?: string;
    }
  ) => {
    try {
      setLoadingQueue(true);
      const nextStatusFilter = overrides?.status ?? statusFilter;
      const nextTargetTypeFilter = overrides?.targetType ?? queueTargetTypeFilter;
      const nextReasonTypeFilter = overrides?.reasonType ?? queueReasonTypeFilter;
      const nextNavigationStatusFilter = overrides?.navigationStatus ?? queueNavigationStatusFilter;
      const nextKeyword = overrides?.keyword ?? queueKeyword;
      let page = await getReviewQueue({
        status: nextStatusFilter,
        targetType: nextTargetTypeFilter,
        reasonType: nextReasonTypeFilter,
        navigationStatus: nextNavigationStatusFilter,
        keyword: nextKeyword,
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
      });

      if (page.voItems.length === 0 && page.voTotal > 0 && targetPageIndex > 1) {
        page = await getReviewQueue({
          status: nextStatusFilter,
          targetType: nextTargetTypeFilter,
          reasonType: nextReasonTypeFilter,
          navigationStatus: nextNavigationStatusFilter,
          keyword: nextKeyword,
          pageIndex: targetPageIndex - 1,
          pageSize: targetPageSize,
        });
      }

      setQueueItems(page.voItems);
      setQueueTotal(page.voTotal);
      setQueuePageIndex(page.voPageIndex);
      setQueuePageSize(page.voPageSize);
    } catch (error) {
      log.error('ModerationPage', 'Failed to load moderation review queue:', error);
      message.error(t('moderation.loadQueueFailed'));
    } finally {
      setLoadingQueue(false);
    }
  };

  const loadLogs = async (
    targetPageIndex = logPageIndex,
    targetPageSize = logPageSize,
    overrides?: Partial<{
      targetUserId: string;
      sourceReportId: string;
      actionType: string;
      isActive: ActionLogActiveFilter;
      keyword: string;
    }>
  ) => {
    try {
      setLoadingLogs(true);
      const targetUserIdText = overrides?.targetUserId ?? logTargetUserId;
      const sourceReportIdText = overrides?.sourceReportId ?? logSourceReportId;
      const targetUserId = toPositiveLongString(targetUserIdText ?? '');
      const sourceReportId = toPositiveLongString(sourceReportIdText ?? '');
      const actionType = overrides?.actionType ?? logActionTypeFilter;
      const isActiveFilter = overrides?.isActive ?? logIsActiveFilter;
      const keyword = overrides?.keyword ?? logKeyword;
      const page = await getActionLogs({
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        targetUserId,
        sourceReportId,
        actionType: actionType || undefined,
        isActive: isActiveFilter === 'active' ? true : isActiveFilter === 'inactive' ? false : undefined,
        keyword: keyword || undefined,
      });

      setLogItems(page.voItems);
      setLogTotal(page.voTotal);
      setLogPageIndex(page.voPageIndex);
      setLogPageSize(page.voPageSize);
    } catch (error) {
      log.error('ModerationPage', 'Failed to load moderation action logs:', error);
      message.error(t('moderation.loadLogsFailed'));
    } finally {
      setLoadingLogs(false);
    }
  };

  useEffect(() => {
    void loadQueue(1, queuePageSize);
    // Queue loader reads current filters and pagination defaults; keeping this effect state-scoped avoids pagination changes replaying a filter reset.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter, queueTargetTypeFilter, queueReasonTypeFilter, queueNavigationStatusFilter, queueKeyword, queuePageSize]);

  useEffect(() => {
    void loadLogs(1, logPageSize);
    // Initial log load should not replay whenever the non-memoized loader captures table state.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const applyQueueKeywordSearch = () => {
    const nextKeyword = queueKeywordInput.trim();
    setQueueContextHint(null);
    if (nextKeyword === queueKeyword) {
      void loadQueue(1, queuePageSize);
      return;
    }

    setQueueKeyword(nextKeyword);
  };

  const resetQueueFilters = () => {
    setQueueContextHint(null);
    const shouldReloadImmediately =
      statusFilter === -1 &&
      !queueTargetTypeFilter &&
      !queueReasonTypeFilter &&
      !queueNavigationStatusFilter &&
      queueKeyword.length === 0 &&
      queueKeywordInput.length === 0;

    setStatusFilter(-1);
    setQueueTargetTypeFilter(undefined);
    setQueueReasonTypeFilter(undefined);
    setQueueNavigationStatusFilter(undefined);
    setQueueKeywordInput('');
    setQueueKeyword('');

    if (shouldReloadImmediately) {
      void loadQueue(1, queuePageSize);
    }
  };

  const applyQueuePreset = (preset: QueuePreset) => {
    setStatusFilter(preset.status ?? -1);
    setQueueTargetTypeFilter(preset.targetType);
    setQueueReasonTypeFilter(preset.reasonType);
    setQueueNavigationStatusFilter(preset.navigationStatus);
    setQueueKeywordInput(preset.keyword ?? '');
    setQueueKeyword(preset.keyword ?? '');
    setQueueContextHint(preset.hint);
    focusQueueSection();
  };

  const applyLogFilters = () => {
    const nextTargetUserId = logTargetUserIdInput.trim();
    const nextSourceReportId = logSourceReportIdInput.trim();
    const normalizedTargetUserId = nextTargetUserId.length > 0 ? toPositiveLongString(nextTargetUserId) : '';
    const normalizedSourceReportId = nextSourceReportId.length > 0 ? toPositiveLongString(nextSourceReportId) : '';
    if (nextTargetUserId.length > 0 && !normalizedTargetUserId) {
      message.error(t('moderation.invalidTargetUserId'));
      return;
    }

    if (nextSourceReportId.length > 0 && !normalizedSourceReportId) {
      message.error(t('moderation.invalidSourceReportId'));
      return;
    }

    const nextKeyword = logKeywordInput.trim();
    setLogTargetUserId(normalizedTargetUserId || '');
    setLogSourceReportId(normalizedSourceReportId || '');
    setLogKeyword(nextKeyword);
    setLogContextHint(null);
    void loadLogs(1, logPageSize, {
      targetUserId: normalizedTargetUserId || '',
      sourceReportId: normalizedSourceReportId || '',
      actionType: logActionTypeFilter,
      isActive: logIsActiveFilter,
      keyword: nextKeyword,
    });
  };

  const resetLogFilters = () => {
    setLogTargetUserIdInput('');
    setLogTargetUserId('');
    setLogSourceReportIdInput('');
    setLogSourceReportId('');
    setLogActionTypeFilter(undefined);
    setLogIsActiveFilter(undefined);
    setLogKeywordInput('');
    setLogKeyword('');
    setLogContextHint(null);
    void loadLogs(1, logPageSize, {
      targetUserId: '',
      sourceReportId: '',
      actionType: undefined,
      isActive: undefined,
      keyword: '',
    });
  };

  const applyActionLogPreset = (preset: ActionLogPreset) => {
    const normalizedTargetUserId = preset.targetUserId ? (toPositiveLongString(preset.targetUserId) ?? '') : '';
    const normalizedSourceReportId = preset.sourceReportId ? (toPositiveLongString(preset.sourceReportId) ?? '') : '';
    const nextKeyword = preset.keyword?.trim() ?? '';
    setLogTargetUserIdInput(normalizedTargetUserId);
    setLogTargetUserId(normalizedTargetUserId);
    setLogSourceReportIdInput(normalizedSourceReportId);
    setLogSourceReportId(normalizedSourceReportId);
    setLogActionTypeFilter(preset.actionType);
    setLogIsActiveFilter(preset.isActive);
    setLogKeywordInput(nextKeyword);
    setLogKeyword(nextKeyword);
    setLogContextHint(preset.hint);
    focusLogSection();
    void loadLogs(1, logPageSize, {
      targetUserId: normalizedTargetUserId,
      sourceReportId: normalizedSourceReportId,
      actionType: preset.actionType,
      isActive: preset.isActive,
      keyword: nextKeyword,
    });
  };

  const loadManualActionStatus = async (targetUserIdText?: string) => {
    const normalizedTargetUserId = toPositiveLongString(targetUserIdText?.trim() ?? '');
    if (!normalizedTargetUserId) {
      manualStatusRequestIdRef.current += 1;
      setManualStatusLoading(false);
      setManualStatusSnapshot(null);
      setManualStatusError(null);
      return;
    }

    const requestId = ++manualStatusRequestIdRef.current;
    try {
      setManualStatusLoading(true);
      setManualStatusError(null);
      const page = await getActionLogs({
        pageIndex: 1,
        pageSize: 20,
        targetUserId: normalizedTargetUserId,
        isActive: true,
      });
      if (requestId !== manualStatusRequestIdRef.current) {
        return;
      }

      setManualStatusSnapshot(buildManualModerationStatusSnapshot(normalizedTargetUserId, page.voItems));
    } catch (error) {
      log.error('ModerationPage', 'Failed to load current manual moderation status:', error);
      if (requestId !== manualStatusRequestIdRef.current) {
        return;
      }

      setManualStatusSnapshot(null);
      setManualStatusError(t('moderation.loadStatusFailed'));
    } finally {
      if (requestId === manualStatusRequestIdRef.current) {
        setManualStatusLoading(false);
      }
    }
  };

  const refreshManualActionStatus = () => {
    const targetUserIdValue = manualActionForm.getFieldValue('targetUserId');
    void loadManualActionStatus(typeof targetUserIdValue === 'string' ? targetUserIdValue : String(targetUserIdValue ?? ''));
  };

  const applyManualActionPreset = (preset: ManualActionPreset) => {
    const normalizedTargetUserId = preset.targetUserId ? (toPositiveLongString(preset.targetUserId) ?? '') : '';
    const normalizedSourceReportId = preset.sourceReportId ? (toPositiveLongString(preset.sourceReportId) ?? '') : '';
    manualActionForm.setFieldsValue({
      targetUserId: normalizedTargetUserId,
      sourceReportId: normalizedSourceReportId,
      actionType: preset.actionType,
      durationHours: preset.durationHours ?? undefined,
      reason: preset.reason ?? '',
    });
    setManualActionContextHint(preset.hint);
    focusManualActionSection();
    void loadManualActionStatus(normalizedTargetUserId);
  };

  useEffect(() => {
    if (!querySection && !queryTargetUserId && !querySourceReportId) {
      return;
    }

    const presetKey = [
      querySection ?? 'logs',
      queryTargetUserId ?? '',
      querySourceReportId ?? '',
      returnTo ?? '',
    ].join(':');
    if (appliedUrlPresetRef.current === presetKey) {
      return;
    }

    appliedUrlPresetRef.current = presetKey;

    if (querySection === 'queue') {
      if (!querySourceReportId) {
        focusQueueSection();
        return;
      }

      applyQueuePreset({
        keyword: querySourceReportId,
        hint: t('moderation.hint.queueFromRoute', { reportId: querySourceReportId }),
      });
      return;
    }

    if (querySection === 'manual') {
      applyManualActionPreset({
        targetUserId: queryTargetUserId,
        sourceReportId: querySourceReportId,
        hint: queryTargetUserId
          ? t('moderation.hint.manualFromRoute', { userId: queryTargetUserId })
          : t('moderation.hint.manualRouteEmpty'),
      });
      return;
    }

    applyActionLogPreset({
      targetUserId: queryTargetUserId,
      sourceReportId: querySourceReportId,
      hint: queryTargetUserId
        ? t('moderation.hint.logsFromRoute', { userId: queryTargetUserId })
        : t('moderation.hint.logsRouteEmpty'),
    });
    // URL presets are applied once per query key; depending on non-memoized page commands would replay them on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [querySection, querySourceReportId, queryTargetUserId, returnTo, t]);

  const resetManualActionForm = () => {
    manualActionForm.resetFields();
    setManualActionContextHint(null);
    manualStatusRequestIdRef.current += 1;
    setManualStatusLoading(false);
    setManualStatusSnapshot(null);
    setManualStatusError(null);
  };

  const handleManualActionTargetUserInputChange = () => {
    setManualActionContextHint(null);
    manualStatusRequestIdRef.current += 1;
    setManualStatusLoading(false);
    setManualStatusSnapshot(null);
    setManualStatusError(null);
  };

  const openReviewModal = (item: ContentReportQueueItemVo) => {
    setReviewingItem(item);
    form.setFieldsValue({
      isApproved: true,
      actionType: 0,
      durationHours: undefined,
      reviewRemark: '',
    });
  };

  const handleSubmitReview = async () => {
    if (!reviewingItem) {
      return;
    }

    try {
      const reviewedItem = reviewingItem;
      const values = await form.validateFields();
      setSubmittingReview(true);
      await reviewReport({
        reportId: reviewedItem.voReportId,
        isApproved: values.isApproved,
        actionType: values.actionType,
        durationHours: values.actionType === 0 ? null : values.durationHours,
        reviewRemark: values.reviewRemark,
      });

      message.success(t('moderation.reviewSuccess'));
      setReviewingItem(null);
      await loadQueue();
      if (values.isApproved && values.actionType > 0) {
        applyActionLogPreset({
          targetUserId: hasPositiveLongId(reviewedItem.voTargetUserId) ? String(reviewedItem.voTargetUserId) : undefined,
          sourceReportId: String(reviewedItem.voReportId),
          actionType: values.actionType === 1 ? 'Mute' : values.actionType === 2 ? 'Ban' : undefined,
          isActive: 'active',
          hint: t('moderation.hint.reviewResult', { reportId: reviewedItem.voReportId }),
        });
      } else {
        await loadLogs();
      }
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) {
        return;
      }

      log.error('ModerationPage', 'Failed to submit moderation review:', error);
      message.error(t('moderation.reviewFailed'));
    } finally {
      setSubmittingReview(false);
    }
  };

  const handleSubmitManualAction = async () => {
    try {
      const values = await manualActionForm.validateFields();
      const targetUserIdText = values.targetUserId?.trim() ?? '';
      const sourceReportIdText = values.sourceReportId?.trim() ?? '';
      const normalizedTargetUserId = toPositiveLongString(targetUserIdText);
      const normalizedSourceReportId = sourceReportIdText.length > 0
        ? toPositiveLongString(sourceReportIdText)
        : undefined;

      if (!normalizedTargetUserId) {
        message.error(t('moderation.invalidTargetUserId'));
        return;
      }

      if (sourceReportIdText.length > 0 && !normalizedSourceReportId) {
        message.error(t('moderation.invalidSourceReportId'));
        return;
      }

      setSubmittingManualAction(true);
      const result = await applyUserModerationAction({
        targetUserId: normalizedTargetUserId,
        actionType: values.actionType,
        durationHours: values.actionType === MANUAL_ACTION_TYPE.mute || values.actionType === MANUAL_ACTION_TYPE.ban
          ? values.durationHours ?? null
          : null,
        reason: toOptionalString(values.reason),
        sourceReportId: normalizedSourceReportId ?? null,
      });

      const actionText = getActionTypeText(result.voActionType, t);
      message.success(t('moderation.manualSuccess', { action: actionText }));
      manualActionForm.setFieldsValue({
        targetUserId: normalizedTargetUserId,
        sourceReportId: normalizedSourceReportId ?? '',
        actionType: undefined,
        durationHours: undefined,
        reason: '',
      });
      setManualActionContextHint(t('moderation.hint.executed', { action: actionText, actionId: result.voActionId }));
      void loadManualActionStatus(normalizedTargetUserId);
      applyActionLogPreset({
        targetUserId: String(result.voTargetUserId),
        sourceReportId: result.voSourceReportId ? String(result.voSourceReportId) : undefined,
        actionType: result.voActionType,
        isActive: result.voIsActive ? 'active' : 'inactive',
        hint: t('moderation.hint.executedLog', { action: actionText, actionId: result.voActionId }),
      });
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) {
        return;
      }

      log.error('ModerationPage', 'Failed to apply manual moderation action:', error);
      message.error(t('moderation.manualFailed'));
    } finally {
      setSubmittingManualAction(false);
    }
  };

  const queueColumns = createModerationQueueColumns({
    canReview,
    onOpenTarget: handleOpenTarget,
    onApplyActionLogPreset: applyActionLogPreset,
    onApplyManualActionPreset: applyManualActionPreset,
    onOpenReviewModal: openReviewModal,
  }, t, language);

  const logColumns = createModerationLogColumns({
    canReview,
    onOpenTarget: handleOpenTarget,
    onApplyQueuePreset: applyQueuePreset,
    onApplyManualActionPreset: applyManualActionPreset,
  }, t, language);
  const activeQueueFilterCount = [
    statusFilter !== -1 ? 'status' : undefined,
    queueTargetTypeFilter,
    queueReasonTypeFilter,
    queueNavigationStatusFilter,
    queueKeyword,
  ].filter(Boolean).length;
  const activeLogFilterCount = [
    logTargetUserId,
    logSourceReportId,
    logActionTypeFilter,
    logIsActiveFilter,
    logKeyword,
  ].filter(Boolean).length;
  const primaryQueueItem = queueItems[0] ?? null;
  const latestActionLog = logItems[0] ?? null;
  const activeActionLogCount = logItems.filter((item) => item.voIsActive).length;
  const primaryQueueTargetInput = primaryQueueItem
    ? buildQueueTargetDisplayInput(primaryQueueItem)
    : null;
  const primaryQueueNavigationLabel = primaryQueueItem
    ? resolveNavigationStatusLabel(primaryQueueItem.voTargetNavigationStatus, t)
    : { label: t('moderation.notSelected') };

  return (
    <div className="admin-feature-page">
      <ConsolePageHeader
        eyebrow={t('moderation.title')}
        title={t('moderation.title')}
        description={t('moderation.description')}
        icon={<SafetyOutlined />}
        status={(
          <ConsoleStatusChip tone={canReview ? 'success' : 'neutral'}>
            {t(canReview ? 'moderation.canReview' : 'moderation.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={(
          <Space wrap>
            {returnTo ? (
              <Button onClick={() => navigate(returnTo)}>
                {t('moderation.backToSource')}
              </Button>
            ) : null}
            <Button icon={<ReloadOutlined />} onClick={() => {
              void Promise.all([loadQueue(), loadLogs()]);
            }}>
              {t('moderation.refresh')}
            </Button>
          </Space>
        )}
      />

      <ConsoleMetricGrid label={t('moderation.metrics.label')}>
        <ConsoleMetricCard label={t('moderation.metrics.queue')} value={queueTotal} description={t('moderation.metrics.queueDescription')} tone="info" />
        <ConsoleMetricCard label={t('moderation.metrics.page')} value={queueItems.length} description={t('moderation.metrics.pageDescription')} />
        <ConsoleMetricCard label={t('moderation.metrics.logs')} value={logTotal} description={t('moderation.metrics.logsDescription')} tone="success" />
        <ConsoleMetricCard
          label={t('moderation.metrics.filters')}
          value={activeQueueFilterCount + activeLogFilterCount}
          description={t('moderation.metrics.filtersDescription')}
          tone={activeQueueFilterCount + activeLogFilterCount > 0 ? 'warning' : 'neutral'}
        />
      </ConsoleMetricGrid>

      <div className="admin-feature-banner">
        {t('moderation.banner')}
      </div>

      <section className="governance-task-flow" aria-label={t('moderation.flow.label')}>
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>{t('moderation.flow.queueTitle')}</strong>
          <p>{t('moderation.flow.queue', { total: queueTotal, visible: queueItems.length })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>{t('moderation.flow.evidenceTitle')}</strong>
          <p>{primaryQueueItem ? `${getTargetTypeLabel(primaryQueueItem.voTargetType, t)} · ${getReasonTypeLabel(primaryQueueItem.voReasonType, t)}` : t('moderation.flow.evidenceEmpty')}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>{t('moderation.flow.actionTitle')}</strong>
          <p>{t(canReview ? 'moderation.flow.actionAllowed' : 'moderation.flow.actionReadOnly')}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>{t('moderation.flow.logTitle')}</strong>
          <p>{t('moderation.flow.log', { total: logTotal, active: activeActionLogCount })}</p>
        </div>
      </section>

      <section className="moderation-mobile-operations" aria-label={t('moderation.mobile.label')}>
        <div>
          <span className="moderation-mobile-operations__eyebrow">{t('moderation.mobile.eyebrow')}</span>
          <h3>{t('moderation.mobile.title')}</h3>
          <p>
            {t('moderation.mobile.description')}
          </p>
        </div>
        <Space wrap>
          <Button size="small" onClick={focusQueueSection}>
            {t('moderation.mobile.queue')}
          </Button>
          {canReview ? (
            <Button size="small" onClick={focusManualActionSection}>
              {t('moderation.mobile.manual')}
            </Button>
          ) : null}
          <Button size="small" onClick={focusLogSection}>
            {t('moderation.mobile.logs')}
          </Button>
        </Space>
      </section>

      <div className={canReview ? 'governance-workbench' : 'governance-workbench governance-workbench--without-actions'}>
        <div className="governance-workbench__queue">
          <section className="admin-feature-card" ref={queueSectionRef}>
            <div className="admin-feature-header">
              <div>
                <h3>{t('moderation.queue.title')}</h3>
                <p className="admin-feature-subtle">{t('moderation.queue.description')}</p>
              </div>
              <Space wrap>
                <Select
                  value={statusFilter}
                  className="moderation-filter-control moderation-filter-control--sm"
                  options={getReviewStatusOptions(t)}
                  onChange={(value) => {
                    setQueueContextHint(null);
                    setStatusFilter(value);
                  }}
                />
                <Select
                  value={queueTargetTypeFilter}
                  className="moderation-filter-control moderation-filter-control--sm"
                  options={getTargetTypeOptions(t)}
                  allowClear
                  placeholder={t('moderation.queue.targetType')}
                  onChange={(value) => {
                    setQueueContextHint(null);
                    setQueueTargetTypeFilter(toOptionalString(value));
                  }}
                />
                <Select
                  value={queueReasonTypeFilter}
                  className="moderation-filter-control moderation-filter-control--sm"
                  options={getReasonTypeOptions(t)}
                  allowClear
                  placeholder={t('moderation.queue.reason')}
                  onChange={(value) => {
                    setQueueContextHint(null);
                    setQueueReasonTypeFilter(toOptionalString(value));
                  }}
                />
                <Select
                  value={queueNavigationStatusFilter}
                  className="moderation-filter-control moderation-filter-control--sm"
                  options={getTargetNavigationStatusOptions(t)}
                  allowClear
                  placeholder={t('moderation.queue.navigationStatus')}
                  onChange={(value) => {
                    setQueueContextHint(null);
                    setQueueNavigationStatusFilter(toOptionalString(value));
                  }}
                />
                <Input
                  allowClear
                  placeholder={t('moderation.queue.search')}
                  value={queueKeywordInput}
                  onChange={(event) => {
                    setQueueContextHint(null);
                    setQueueKeywordInput(event.target.value);
                  }}
                  onPressEnter={applyQueueKeywordSearch}
                  className="moderation-filter-control moderation-filter-control--xl"
                />
                <Button
                  variant="primary"
                  onClick={applyQueueKeywordSearch}
                >
                  {t('moderation.query')}
                </Button>
                <Button onClick={resetQueueFilters}>
                  {t('moderation.reset')}
                </Button>
              </Space>
            </div>

            {queueContextHint ? (
              <div className="admin-feature-banner moderation-section-banner">
                {queueContextHint}
              </div>
            ) : null}

            <Table<ContentReportQueueItemVo>
              rowKey="voReportId"
              columns={queueColumns}
              dataSource={queueItems}
              loading={loadingQueue}
              pagination={{
                current: queuePageIndex,
                pageSize: queuePageSize,
                total: queueTotal,
                showSizeChanger: true,
                showQuickJumper: true,
                onChange: (page, size) => {
                  void loadQueue(page, size);
                },
              }}
              scroll={{ x: 1580 }}
            />
          </section>
        </div>

        {canReview ? (
          <div className="governance-workbench__actions">
            <section className="admin-feature-rail" aria-label={t('moderation.evidence.label')}>
              <div className="admin-feature-rail__header">
                <div>
                  <span className="admin-feature-rail__eyebrow">{t('moderation.evidence.eyebrow')}</span>
                  <h3>{t('moderation.evidence.title')}</h3>
                </div>
                <ConsoleStatusChip tone={primaryQueueItem ? 'info' : 'neutral'}>
                  {primaryQueueNavigationLabel.label}
                </ConsoleStatusChip>
              </div>

              {primaryQueueItem && primaryQueueTargetInput ? (
                <>
                  <ModerationTargetDisplay
                    input={{
                      ...primaryQueueTargetInput,
                      showTargetUser: true,
                    }}
                  />
                  <div className="admin-feature-rail__list">
                    <div className="admin-feature-rail__item">
                      <span>{t('moderation.evidence.report')}</span>
                      <strong>#{primaryQueueItem.voReportId}</strong>
                    </div>
                    <div className="admin-feature-rail__item">
                      <span>{t('moderation.evidence.reporter')}</span>
                      <strong>{primaryQueueItem.voReporterUserName || `#${primaryQueueItem.voReporterUserId}`}</strong>
                    </div>
                    <div className="admin-feature-rail__item">
                      <span>{t('moderation.evidence.reason')}</span>
                      <strong>{getReasonTypeLabel(primaryQueueItem.voReasonType, t)}</strong>
                    </div>
                  </div>
                  <div className="admin-feature-rail__actions">
                    <Button size="small" onClick={() => handleOpenTarget(primaryQueueTargetInput)}>
                      {t('moderation.evidence.open')}
                    </Button>
                    <Button
                      size="small"
                      onClick={() => applyActionLogPreset({
                        targetUserId: hasPositiveLongId(primaryQueueItem.voTargetUserId) ? String(primaryQueueItem.voTargetUserId) : undefined,
                        sourceReportId: String(primaryQueueItem.voReportId),
                        hint: t('moderation.hint.evidenceLogs', { reportId: primaryQueueItem.voReportId }),
                      })}
                    >
                      {t('moderation.evidence.logs')}
                    </Button>
                  </div>
                </>
              ) : (
                <p className="admin-feature-rail__empty">
                  {t('moderation.evidence.empty')}
                </p>
              )}
            </section>

            <ManualModerationActionSection
              sectionRef={manualActionSectionRef}
              form={manualActionForm}
              contextHint={manualActionContextHint}
              submitting={submittingManualAction}
              statusLoading={manualStatusLoading}
              statusError={manualStatusError}
              statusSnapshot={manualStatusSnapshot}
              onResetForm={resetManualActionForm}
              onSubmit={() => {
                void handleSubmitManualAction();
              }}
              onRefreshStatus={refreshManualActionStatus}
              onTargetUserInputChange={handleManualActionTargetUserInputChange}
              onApplyActionLogPreset={applyActionLogPreset}
              onApplyManualActionPreset={applyManualActionPreset}
            />
          </div>
        ) : null}

        <div className="governance-workbench__detail">
          <section className="admin-feature-card" ref={logSectionRef}>
            <div className="admin-feature-header">
              <div>
                <h3>{t('moderation.logs.title')}</h3>
                <p className="admin-feature-subtle">{t('moderation.logs.description')}</p>
              </div>
              <Space wrap>
                <Input
                  placeholder={t('moderation.logs.targetUserId')}
                  value={logTargetUserIdInput}
                  onChange={(event) => {
                    setLogContextHint(null);
                    setLogTargetUserIdInput(event.target.value);
                  }}
                  className="moderation-filter-control moderation-filter-control--md"
                />
                <Input
                  placeholder={t('moderation.logs.sourceReportId')}
                  value={logSourceReportIdInput}
                  onChange={(event) => {
                    setLogContextHint(null);
                    setLogSourceReportIdInput(event.target.value);
                  }}
                  className="moderation-filter-control moderation-filter-control--md"
                />
                <Select
                  value={logActionTypeFilter}
                  className="moderation-filter-control moderation-filter-control--sm"
                  options={getActionLogActionTypeOptions(t)}
                  allowClear
                  placeholder={t('moderation.logs.action')}
                  onChange={(value) => {
                    setLogContextHint(null);
                    setLogActionTypeFilter(toOptionalString(value));
                  }}
                />
                <Select
                  value={logIsActiveFilter}
                  className="moderation-filter-control moderation-filter-control--sm"
                  options={getActionLogStatusOptions(t)}
                  allowClear
                  placeholder={t('moderation.logs.status')}
                  onChange={(value) => {
                    setLogContextHint(null);
                    setLogIsActiveFilter((value === 'active' || value === 'inactive') ? value : undefined);
                  }}
                />
                <Input
                  allowClear
                  placeholder={t('moderation.logs.search')}
                  value={logKeywordInput}
                  onChange={(event) => {
                    setLogContextHint(null);
                    setLogKeywordInput(event.target.value);
                  }}
                  onPressEnter={applyLogFilters}
                  className="moderation-filter-control moderation-filter-control--lg"
                />
                <Button
                  variant="primary"
                  onClick={applyLogFilters}
                >
                  {t('moderation.query')}
                </Button>
                <Button onClick={resetLogFilters}>
                  {t('moderation.reset')}
                </Button>
              </Space>
            </div>

            {logContextHint ? (
              <div className="admin-feature-banner moderation-section-banner">
                {logContextHint}
              </div>
            ) : null}

            {latestActionLog ? (
              <div className="admin-feature-inline-context">
                <span>{t('moderation.logs.latest', { id: latestActionLog.voActionId })}</span>
                <strong>{getActionTypeText(latestActionLog.voActionType, t)}</strong>
                <span>{t(latestActionLog.voIsActive ? 'moderation.action.active' : 'moderation.action.inactive')}</span>
                <span>{latestActionLog.voOperatorUserName}</span>
              </div>
            ) : null}

            <Table<UserModerationActionVo>
              rowKey="voActionId"
              columns={logColumns}
              dataSource={logItems}
              loading={loadingLogs}
              pagination={{
                current: logPageIndex,
                pageSize: logPageSize,
                total: logTotal,
                showSizeChanger: true,
                onChange: (page, size) => {
                  void loadLogs(page, size);
                },
              }}
              scroll={{ x: 1780 }}
            />
          </section>
        </div>
      </div>

      <Modal
        title={reviewingItem
          ? t('moderation.review.titleWithId', { id: reviewingItem.voReportId })
          : t('moderation.review.title')}
        open={!!reviewingItem}
        onOk={handleSubmitReview}
        onCancel={() => setReviewingItem(null)}
        confirmLoading={submittingReview}
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          {reviewingItem ? (
            <div className="moderation-review-preview">
              <div className="moderation-review-preview__label">{t('moderation.review.target')}</div>
              <ModerationTargetDisplay
                input={{
                  ...buildQueueTargetDisplayInput(reviewingItem),
                  showTargetUser: true,
                }}
              />
            </div>
          ) : null}

          <Form.Item
            name="isApproved"
            label={t('moderation.review.result')}
            rules={[{ required: true, message: t('moderation.review.resultRequired') }]}
          >
            <Select
              options={[
                { label: t('moderation.review.approve'), value: true },
                { label: t('moderation.review.reject'), value: false },
              ]}
            />
          </Form.Item>

          <Form.Item
            name="actionType"
            label={t('moderation.review.action')}
            rules={[{ required: true, message: t('moderation.review.actionRequired') }]}
          >
            <Select options={getActionOptions(t)} />
          </Form.Item>

          <Form.Item
            noStyle
            shouldUpdate={(prev, next) => prev.actionType !== next.actionType}
          >
            {({ getFieldValue }) => (
              <Form.Item
                name="durationHours"
                label={t('moderation.review.duration')}
                rules={getFieldValue('actionType') > 0
                  ? [{ required: true, message: t('moderation.review.durationRequired') }]
                  : []}
              >
                <InputNumber min={1} max={720} className="moderation-full-width-control" disabled={getFieldValue('actionType') === 0} />
              </Form.Item>
            )}
          </Form.Item>

          <Form.Item name="reviewRemark" label={t('moderation.review.remark')}>
            <Input.TextArea
              rows={4}
              maxLength={500}
              showCount
              placeholder={t('moderation.review.remarkPlaceholder')}
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

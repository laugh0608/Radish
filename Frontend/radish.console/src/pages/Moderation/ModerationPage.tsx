import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
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
  ACTION_LOG_ACTION_TYPE_OPTIONS,
  ACTION_LOG_STATUS_OPTIONS,
  ACTION_OPTIONS,
  MANUAL_ACTION_TYPE,
  REASON_TYPE_OPTIONS,
  REVIEW_STATUS_OPTIONS,
  TARGET_NAVIGATION_STATUS_OPTIONS,
  TARGET_TYPE_OPTIONS,
  buildManualModerationStatusSnapshot,
  buildQueueTargetDisplayInput,
  getActionTypeText,
  hasPositiveLongId,
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
  useDocumentTitle('内容治理');
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
    const target = resolveOpenTarget(input);
    if (!target) {
      message.error(resolveMissingTargetMessage(input.targetType, input.navigationMessage));
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
      log.error('ModerationPage', '加载审核队列失败:', error);
      message.error('加载审核队列失败');
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
      log.error('ModerationPage', '加载治理动作日志失败:', error);
      message.error('加载治理动作日志失败');
    } finally {
      setLoadingLogs(false);
    }
  };

  useEffect(() => {
    void loadQueue(1, queuePageSize);
  }, [statusFilter, queueTargetTypeFilter, queueReasonTypeFilter, queueNavigationStatusFilter, queueKeyword, queuePageSize]);

  useEffect(() => {
    void loadLogs(1, logPageSize);
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
      message.error('请输入有效的目标用户 ID');
      return;
    }

    if (nextSourceReportId.length > 0 && !normalizedSourceReportId) {
      message.error('请输入有效的关联举报单 ID');
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
      log.error('ModerationPage', '加载手动治理当前状态失败:', error);
      if (requestId !== manualStatusRequestIdRef.current) {
        return;
      }

      setManualStatusSnapshot(null);
      setManualStatusError('加载当前治理状态失败');
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
        hint: `已从排障入口带入举报单 #${querySourceReportId}，可继续回看原始审核记录与目标快照。`,
      });
      return;
    }

    if (querySection === 'manual') {
      applyManualActionPreset({
        targetUserId: queryTargetUserId,
        sourceReportId: querySourceReportId,
        hint: queryTargetUserId
          ? `已从排障入口带入用户 #${queryTargetUserId}，可继续执行禁言 / 封禁 / 解除动作。`
          : '已进入手动治理区，请先输入目标用户 ID。',
      });
      return;
    }

    applyActionLogPreset({
      targetUserId: queryTargetUserId,
      sourceReportId: querySourceReportId,
      hint: queryTargetUserId
        ? `已从排障入口带入用户 #${queryTargetUserId} 的治理动作日志。`
        : '已进入治理动作日志，可继续按用户或举报单筛选。',
    });
    // URL presets are applied once per query key; depending on non-memoized page commands would replay them on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [querySection, querySourceReportId, queryTargetUserId, returnTo]);

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

      message.success('审核完成');
      setReviewingItem(null);
      await loadQueue();
      if (values.isApproved && values.actionType > 0) {
        applyActionLogPreset({
          targetUserId: hasPositiveLongId(reviewedItem.voTargetUserId) ? String(reviewedItem.voTargetUserId) : undefined,
          sourceReportId: String(reviewedItem.voReportId),
          actionType: values.actionType === 1 ? 'Mute' : values.actionType === 2 ? 'Ban' : undefined,
          isActive: 'active',
          hint: `已带入举报单 #${reviewedItem.voReportId} 关联的治理动作日志，便于继续核对实际处罚是否已落下。`,
        });
      } else {
        await loadLogs();
      }
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) {
        return;
      }

      log.error('ModerationPage', '提交审核失败:', error);
      message.error('提交审核失败');
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
        message.error('请输入有效的目标用户 ID');
        return;
      }

      if (sourceReportIdText.length > 0 && !normalizedSourceReportId) {
        message.error('请输入有效的关联举报单 ID');
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

      const actionText = getActionTypeText(result.voActionType);
      message.success(`${actionText}已执行`);
      manualActionForm.setFieldsValue({
        targetUserId: normalizedTargetUserId,
        sourceReportId: normalizedSourceReportId ?? '',
        actionType: undefined,
        durationHours: undefined,
        reason: '',
      });
      setManualActionContextHint(`已执行${actionText}，目标用户与来源举报单已保留在表单中；下方日志已自动定位到动作单 #${result.voActionId}。`);
      void loadManualActionStatus(normalizedTargetUserId);
      applyActionLogPreset({
        targetUserId: String(result.voTargetUserId),
        sourceReportId: result.voSourceReportId ? String(result.voSourceReportId) : undefined,
        actionType: result.voActionType,
        isActive: result.voIsActive ? 'active' : 'inactive',
        hint: `已定位到刚执行的${actionText}动作单 #${result.voActionId}。`,
      });
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) {
        return;
      }

      log.error('ModerationPage', '执行手动治理动作失败:', error);
      message.error('执行手动治理动作失败');
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
  });

  const logColumns = createModerationLogColumns({
    canReview,
    onOpenTarget: handleOpenTarget,
    onApplyQueuePreset: applyQueuePreset,
    onApplyManualActionPreset: applyManualActionPreset,
  });
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

  return (
    <div className="admin-feature-page">
      <ConsolePageHeader
        eyebrow="CONTENT MODERATION"
        title="内容治理"
        description="举报队列、手动治理动作与治理日志统一在 Console 收口。"
        icon={<SafetyOutlined />}
        status={(
          <ConsoleStatusChip tone={canReview ? 'success' : 'neutral'}>
            {canReview ? '可审核处置' : '只读查看'}
          </ConsoleStatusChip>
        )}
        actions={(
          <Space wrap>
            {returnTo ? (
              <Button onClick={() => navigate(returnTo)}>
                返回来源
              </Button>
            ) : null}
            <Button icon={<ReloadOutlined />} onClick={() => {
              void Promise.all([loadQueue(), loadLogs()]);
            }}>
              刷新
            </Button>
          </Space>
        )}
      />

      <ConsoleMetricGrid label="内容治理工作台指标">
        <ConsoleMetricCard label="举报队列" value={queueTotal} description="当前筛选后的举报单" tone="info" />
        <ConsoleMetricCard label="本页举报" value={queueItems.length} description="当前页可见举报单" />
        <ConsoleMetricCard label="治理日志" value={logTotal} description="当前筛选后的动作记录" tone="success" />
        <ConsoleMetricCard
          label="筛选条件"
          value={activeQueueFilterCount + activeLogFilterCount}
          description="队列与日志合计筛选"
          tone={activeQueueFilterCount + activeLogFilterCount > 0 ? 'warning' : 'neutral'}
        />
      </ConsoleMetricGrid>

      <div className="admin-feature-banner">
        当前治理链路已统一接入帖子、评论、聊天室消息和商品举报，审核通过后可联动禁言 / 封禁；历史动作也支持继续处置或直接解除。
      </div>

      <div className={canReview ? 'governance-workbench' : 'governance-workbench governance-workbench--without-actions'}>
        <div className="governance-workbench__queue">
          <section className="admin-feature-card" ref={queueSectionRef}>
            <div className="admin-feature-header">
              <div>
                <h3>举报审核队列</h3>
                <p className="admin-feature-subtle">按状态、目标类型、举报原因、回看状态和关键词筛选举报单，并在审核时直接联动治理动作。</p>
              </div>
              <Space wrap>
                <Select
                  value={statusFilter}
                  style={{ width: 160 }}
                  options={REVIEW_STATUS_OPTIONS}
                  onChange={(value) => {
                    setQueueContextHint(null);
                    setStatusFilter(value);
                  }}
                />
                <Select
                  value={queueTargetTypeFilter}
                  style={{ width: 160 }}
                  options={TARGET_TYPE_OPTIONS}
                  allowClear
                  placeholder="目标类型"
                  onChange={(value) => {
                    setQueueContextHint(null);
                    setQueueTargetTypeFilter(toOptionalString(value));
                  }}
                />
                <Select
                  value={queueReasonTypeFilter}
                  style={{ width: 160 }}
                  options={REASON_TYPE_OPTIONS}
                  allowClear
                  placeholder="举报原因"
                  onChange={(value) => {
                    setQueueContextHint(null);
                    setQueueReasonTypeFilter(toOptionalString(value));
                  }}
                />
                <Select
                  value={queueNavigationStatusFilter}
                  style={{ width: 160 }}
                  options={TARGET_NAVIGATION_STATUS_OPTIONS}
                  allowClear
                  placeholder="回看状态"
                  onChange={(value) => {
                    setQueueContextHint(null);
                    setQueueNavigationStatusFilter(toOptionalString(value));
                  }}
                />
                <Input
                  allowClear
                  placeholder="搜索举报单 / 目标 / 快照 / 用户 / 原因补充"
                  value={queueKeywordInput}
                  onChange={(event) => {
                    setQueueContextHint(null);
                    setQueueKeywordInput(event.target.value);
                  }}
                  onPressEnter={applyQueueKeywordSearch}
                  style={{ width: 320 }}
                />
                <Button
                  variant="primary"
                  onClick={applyQueueKeywordSearch}
                >
                  查询
                </Button>
                <Button onClick={resetQueueFilters}>
                  重置
                </Button>
              </Space>
            </div>

            {queueContextHint ? (
              <div className="admin-feature-banner" style={{ marginTop: 16 }}>
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
                <h3>治理动作日志</h3>
                <p className="admin-feature-subtle">回看审核联动与手动执行后实际落下的治理动作，并支持从举报队列或动作记录回跳到对应上下文。</p>
              </div>
              <Space wrap>
                <Input
                  placeholder="按目标用户 ID 过滤"
                  value={logTargetUserIdInput}
                  onChange={(event) => {
                    setLogContextHint(null);
                    setLogTargetUserIdInput(event.target.value);
                  }}
                  style={{ width: 200 }}
                />
                <Input
                  placeholder="关联举报单 ID"
                  value={logSourceReportIdInput}
                  onChange={(event) => {
                    setLogContextHint(null);
                    setLogSourceReportIdInput(event.target.value);
                  }}
                  style={{ width: 200 }}
                />
                <Select
                  value={logActionTypeFilter}
                  style={{ width: 160 }}
                  options={ACTION_LOG_ACTION_TYPE_OPTIONS}
                  allowClear
                  placeholder="治理动作"
                  onChange={(value) => {
                    setLogContextHint(null);
                    setLogActionTypeFilter(toOptionalString(value));
                  }}
                />
                <Select
                  value={logIsActiveFilter}
                  style={{ width: 160 }}
                  options={ACTION_LOG_STATUS_OPTIONS}
                  allowClear
                  placeholder="动作状态"
                  onChange={(value) => {
                    setLogContextHint(null);
                    setLogIsActiveFilter((value === 'active' || value === 'inactive') ? value : undefined);
                  }}
                />
                <Input
                  allowClear
                  placeholder="搜索动作单 / 目标用户 / 原因 / 操作者"
                  value={logKeywordInput}
                  onChange={(event) => {
                    setLogContextHint(null);
                    setLogKeywordInput(event.target.value);
                  }}
                  onPressEnter={applyLogFilters}
                  style={{ width: 280 }}
                />
                <Button
                  variant="primary"
                  onClick={applyLogFilters}
                >
                  查询
                </Button>
                <Button onClick={resetLogFilters}>
                  重置
                </Button>
              </Space>
            </div>

            {logContextHint ? (
              <div className="admin-feature-banner" style={{ marginTop: 16 }}>
                {logContextHint}
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
        title={reviewingItem ? `审核举报单 #${reviewingItem.voReportId}` : '审核举报单'}
        open={!!reviewingItem}
        onOk={handleSubmitReview}
        onCancel={() => setReviewingItem(null)}
        confirmLoading={submittingReview}
        destroyOnHidden
      >
        <Form form={form} layout="vertical">
          {reviewingItem ? (
            <div className="moderation-review-preview">
              <div className="moderation-review-preview__label">审核目标</div>
              <ModerationTargetDisplay
                input={{
                  ...buildQueueTargetDisplayInput(reviewingItem),
                  showTargetUser: true,
                }}
              />
            </div>
          ) : null}

          <Form.Item name="isApproved" label="审核结果" rules={[{ required: true, message: '请选择审核结果' }]}>
            <Select
              options={[
                { label: '通过举报', value: true },
                { label: '驳回举报', value: false },
              ]}
            />
          </Form.Item>

          <Form.Item name="actionType" label="治理动作" rules={[{ required: true, message: '请选择治理动作' }]}>
            <Select options={ACTION_OPTIONS} />
          </Form.Item>

          <Form.Item
            noStyle
            shouldUpdate={(prev, next) => prev.actionType !== next.actionType}
          >
            {({ getFieldValue }) => (
              <Form.Item
                name="durationHours"
                label="持续时长（小时）"
                rules={getFieldValue('actionType') > 0
                  ? [{ required: true, message: '请输入动作时长' }]
                  : []}
              >
                <InputNumber min={1} max={720} style={{ width: '100%' }} disabled={getFieldValue('actionType') === 0} />
              </Form.Item>
            )}
          </Form.Item>

          <Form.Item name="reviewRemark" label="审核备注">
            <Input.TextArea rows={4} maxLength={500} showCount placeholder="补充审核说明或处理依据" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

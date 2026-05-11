import { useEffect, useRef, useState } from 'react';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  Button,
  Form,
  InputNumber,
  Space,
  Table,
  Tag,
  message,
  type TableColumnsType,
} from '@radish/ui';
import { ReloadOutlined } from '@radish/ui';
import {
  applyUserModerationAction,
  getActionLogs,
  getReviewQueue,
  reviewReport,
  type ContentReportQueueItemVo,
  type UserModerationActionVo,
} from '@/api/moderationApi';
import { getApiBaseUrl } from '@/config/env';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import './index.css';
import '../adminFeature.css';

const REVIEW_STATUS_OPTIONS = [
  { label: '全部状态', value: -1 },
  { label: '待审核', value: 0 },
  { label: '已通过', value: 1 },
  { label: '已驳回', value: 2 },
];

const TARGET_TYPE_OPTIONS = [
  { label: '帖子', value: 'Post' },
  { label: '评论', value: 'Comment' },
  { label: '轻回应', value: 'PostQuickReply' },
  { label: '聊天消息', value: 'ChatMessage' },
  { label: '商品', value: 'Product' },
];

const REASON_TYPE_OPTIONS = [
  { label: '垃圾内容', value: 'Spam' },
  { label: '辱骂骚扰', value: 'Abuse' },
  { label: '色情低俗', value: 'Pornography' },
  { label: '违法违规', value: 'Illegal' },
  { label: '欺诈误导', value: 'Fraud' },
  { label: '其他', value: 'Other' },
];

const TARGET_NAVIGATION_STATUS_OPTIONS = [
  { label: '可回看', value: 'Ready' },
  { label: '已降级', value: 'Fallback' },
  { label: '已失效', value: 'Unavailable' },
  { label: '暂不支持', value: 'Unsupported' },
];

const ACTION_OPTIONS = [
  { label: '不处罚', value: 0 },
  { label: '禁言', value: 1 },
  { label: '封禁', value: 2 },
];

const MANUAL_ACTION_TYPE = {
  mute: 1,
  ban: 2,
  unmute: 3,
  unban: 4,
} as const;

type ManualActionTypeValue = typeof MANUAL_ACTION_TYPE[keyof typeof MANUAL_ACTION_TYPE];

const MANUAL_ACTION_OPTIONS = [
  { label: '禁言', value: MANUAL_ACTION_TYPE.mute },
  { label: '封禁', value: MANUAL_ACTION_TYPE.ban },
  { label: '解除禁言', value: MANUAL_ACTION_TYPE.unmute },
  { label: '解除封禁', value: MANUAL_ACTION_TYPE.unban },
];

const ACTION_LOG_ACTION_TYPE_OPTIONS = [
  { label: '禁言', value: 'Mute' },
  { label: '封禁', value: 'Ban' },
  { label: '解除禁言', value: 'Unmute' },
  { label: '解除封禁', value: 'Unban' },
];

const ACTION_LOG_STATUS_OPTIONS = [
  { label: '生效中', value: 'active' },
  { label: '已结束', value: 'inactive' },
];

const TARGET_TYPE_LABEL_MAP: Record<string, string> = {
  Post: '帖子',
  Comment: '评论',
  PostQuickReply: '轻回应',
  ChatMessage: '聊天消息',
  Product: '商品',
  Unknown: '未知目标',
};

const REASON_TYPE_LABEL_MAP: Record<string, string> = {
  Spam: '垃圾内容',
  Abuse: '辱骂骚扰',
  Pornography: '色情低俗',
  Illegal: '违法违规',
  Fraud: '欺诈误导',
  Other: '其他',
};

function getTargetTypeLabel(value: string | null | undefined): string {
  return value ? (TARGET_TYPE_LABEL_MAP[value] ?? value) : '未知目标';
}

function getReasonTypeLabel(value: string | null | undefined): string {
  return value ? (REASON_TYPE_LABEL_MAP[value] ?? value) : '未分类';
}

function toOptionalString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : undefined;
}

function toPositiveLongString(value: string): string | undefined {
  const trimmed = value.trim();
  return /^[1-9]\d*$/.test(trimmed) ? trimmed : undefined;
}

const renderReportStatus = (value: string) => {
  switch (value) {
    case 'Approved':
      return <Tag color="success">已通过</Tag>;
    case 'Rejected':
      return <Tag color="error">已驳回</Tag>;
    default:
      return <Tag color="processing">待审核</Tag>;
  }
};

const renderActionType = (value: string) => {
  switch (value) {
    case 'Mute':
      return <Tag color="orange">禁言</Tag>;
    case 'Ban':
      return <Tag color="red">封禁</Tag>;
    case 'Unmute':
      return <Tag color="green">解除禁言</Tag>;
    case 'Unban':
      return <Tag color="cyan">解除封禁</Tag>;
    default:
      return <Tag>无动作</Tag>;
  }
};

function getActionTypeText(value: string | null | undefined): string {
  switch (value) {
    case 'Mute':
      return '禁言';
    case 'Ban':
      return '封禁';
    case 'Unmute':
      return '解除禁言';
    case 'Unban':
      return '解除封禁';
    default:
      return '治理动作';
  }
}

function getManualActionTypeText(value: ManualActionTypeValue | null | undefined): string {
  switch (value) {
    case MANUAL_ACTION_TYPE.mute:
      return '禁言';
    case MANUAL_ACTION_TYPE.ban:
      return '封禁';
    case MANUAL_ACTION_TYPE.unmute:
      return '解除禁言';
    case MANUAL_ACTION_TYPE.unban:
      return '解除封禁';
    default:
      return '治理动作';
  }
}

function buildDesktopChatTargetUrl(channelId: number, messageId: number): string {
  const url = new URL('/desktop', getApiBaseUrl());
  url.searchParams.set('app', 'chat');
  url.searchParams.set('channelId', String(channelId));
  url.searchParams.set('messageId', String(messageId));
  return url.toString();
}

function buildPublicForumTargetUrl(postId: number, commentId?: number | null): string {
  const url = new URL(`/forum/post/${postId}`, getApiBaseUrl());
  if (commentId && commentId > 0) {
    url.searchParams.set('commentId', String(commentId));
  }

  return url.toString();
}

function buildPublicShopTargetUrl(productId: number): string {
  return new URL(`/shop/product/${productId}`, getApiBaseUrl()).toString();
}

function canOpenChatTarget(targetType: string | null | undefined, channelId: number | null | undefined, messageId: number | null | undefined): boolean {
  return targetType === 'ChatMessage'
    && !!channelId
    && channelId > 0
    && !!messageId
    && messageId > 0;
}

interface ModerationTargetNavigationInput {
  targetType: string | null | undefined;
  targetContentId?: number | null;
  targetPostId?: number | null;
  targetCommentId?: number | null;
  targetChannelId?: number | null;
  targetMessageId?: number | null;
}

interface ModerationOpenTarget {
  label: string;
  url: string;
}

interface ModerationTargetNavigationStateInput extends ModerationTargetNavigationInput {
  navigationStatus?: string | null;
  navigationMessage?: string | null;
}

interface ModerationTargetDisplayInput extends ModerationTargetNavigationStateInput {
  snapshotTitle?: string | null;
  snapshotSummary?: string | null;
  snapshotIsPersisted?: boolean;
  targetUserId?: number | null;
  targetUserName?: string | null;
  showTargetUser?: boolean;
}

type ActionLogActiveFilter = 'active' | 'inactive';

interface ActionLogPreset {
  targetUserId?: string;
  sourceReportId?: string;
  actionType?: string;
  isActive?: ActionLogActiveFilter;
  keyword?: string;
  hint: string;
}

interface QueuePreset {
  status?: number;
  targetType?: string;
  reasonType?: string;
  navigationStatus?: string;
  keyword?: string;
  hint: string;
}

interface ManualActionPreset {
  targetUserId?: string;
  sourceReportId?: string;
  actionType?: ManualActionTypeValue;
  durationHours?: number | null;
  reason?: string;
  hint: string;
}

function resolveNavigationStatusLabel(status: string | null | undefined): { color: string; label: string } {
  switch (status) {
    case 'Fallback':
      return { color: 'warning', label: '已降级' };
    case 'Unavailable':
      return { color: 'default', label: '已失效' };
    case 'Unsupported':
      return { color: 'default', label: '暂不支持' };
    default:
      return { color: 'success', label: '可回看' };
  }
}

function renderTargetNavigationState(status: string | null | undefined, messageText: string | null | undefined) {
  const statusMeta = resolveNavigationStatusLabel(status);

  return (
    <div className="moderation-target__section">
      <div className="moderation-target__section-label">当前状态</div>
      <div className="moderation-target__state">
        <Tag color={statusMeta.color}>{statusMeta.label}</Tag>
        {messageText ? <div className="moderation-target__state-message">{messageText}</div> : null}
      </div>
    </div>
  );
}

function renderSnapshotSection(input: ModerationTargetDisplayInput) {
  const hasSnapshotTitle = !!input.snapshotTitle?.trim();
  const hasSnapshotSummary = !!input.snapshotSummary?.trim();

  if (!hasSnapshotTitle && !hasSnapshotSummary && !input.snapshotIsPersisted) {
    return null;
  }

  return (
    <div className="moderation-target__section">
      <div className="moderation-target__section-label">
        {input.snapshotIsPersisted ? '创建时快照' : '目标摘要（旧数据兼容）'}
      </div>
      {hasSnapshotTitle ? <div className="moderation-target__snapshot-title">{input.snapshotTitle}</div> : null}
      {hasSnapshotSummary ? <div className="moderation-target__snapshot-summary">{input.snapshotSummary}</div> : null}
      {!hasSnapshotTitle && !hasSnapshotSummary ? <div className="moderation-target__empty">未保留文本摘要</div> : null}
    </div>
  );
}

function renderModerationTarget(input: ModerationTargetDisplayInput) {
  return (
    <div className="moderation-target">
      <div className="moderation-target__identity">{getTargetTypeLabel(input.targetType)} #{input.targetContentId ?? '-'}</div>
      {input.targetType === 'Comment' && input.targetPostId ? (
        <div className="moderation-target__meta">
          帖子 #{input.targetPostId} · 评论 #{input.targetCommentId ?? input.targetContentId}
        </div>
      ) : null}
      {input.targetType === 'PostQuickReply' && input.targetPostId ? (
        <div className="moderation-target__meta">所属帖子 #{input.targetPostId}</div>
      ) : null}
      {input.targetType === 'ChatMessage' && input.targetChannelId ? (
        <div className="moderation-target__meta">
          频道 #{input.targetChannelId} · 消息 #{input.targetMessageId ?? input.targetContentId}
        </div>
      ) : null}
      {renderSnapshotSection(input)}
      {renderTargetNavigationState(input.navigationStatus, input.navigationMessage)}
      {input.showTargetUser ? (
        <div className="moderation-target__user">{input.targetUserName || `用户 ${input.targetUserId}`}</div>
      ) : null}
    </div>
  );
}

function buildQueueTargetDisplayInput(record: ContentReportQueueItemVo): ModerationTargetDisplayInput {
  return {
    targetType: record.voTargetType,
    targetContentId: record.voTargetContentId,
    targetPostId: record.voTargetPostId,
    targetCommentId: record.voTargetCommentId,
    targetChannelId: record.voTargetChannelId,
    targetMessageId: record.voTargetMessageId,
    navigationStatus: record.voTargetNavigationStatus,
    navigationMessage: record.voTargetNavigationMessage,
    snapshotTitle: record.voTargetSnapshotTitle,
    snapshotSummary: record.voTargetSnapshotSummary,
    snapshotIsPersisted: record.voTargetSnapshotIsPersisted,
    targetUserId: record.voTargetUserId,
    targetUserName: record.voTargetUserName,
  };
}

function buildQueueTargetNavigationInput(record: ContentReportQueueItemVo): ModerationTargetNavigationStateInput {
  return {
    targetType: record.voTargetType,
    targetContentId: record.voTargetContentId,
    targetPostId: record.voTargetPostId,
    targetCommentId: record.voTargetCommentId,
    targetChannelId: record.voTargetChannelId,
    targetMessageId: record.voTargetMessageId,
    navigationStatus: record.voTargetNavigationStatus,
    navigationMessage: record.voTargetNavigationMessage,
  };
}

function buildActionSourceTargetDisplayInput(record: UserModerationActionVo): ModerationTargetDisplayInput {
  return {
    targetType: record.voSourceReportTargetType,
    targetContentId: record.voSourceReportTargetContentId ?? null,
    targetPostId: record.voSourceReportTargetPostId,
    targetCommentId: record.voSourceReportTargetCommentId,
    targetChannelId: record.voSourceReportTargetChannelId,
    targetMessageId: record.voSourceReportTargetMessageId,
    navigationStatus: record.voSourceReportTargetNavigationStatus,
    navigationMessage: record.voSourceReportTargetNavigationMessage,
    snapshotTitle: record.voSourceReportTargetSnapshotTitle,
    snapshotSummary: record.voSourceReportTargetSnapshotSummary,
    snapshotIsPersisted: record.voSourceReportTargetSnapshotIsPersisted,
  };
}

function buildActionSourceTargetNavigationInput(record: UserModerationActionVo): ModerationTargetNavigationStateInput {
  return {
    targetType: record.voSourceReportTargetType,
    targetContentId: record.voSourceReportTargetContentId,
    targetPostId: record.voSourceReportTargetPostId,
    targetCommentId: record.voSourceReportTargetCommentId,
    targetChannelId: record.voSourceReportTargetChannelId,
    targetMessageId: record.voSourceReportTargetMessageId,
    navigationStatus: record.voSourceReportTargetNavigationStatus,
    navigationMessage: record.voSourceReportTargetNavigationMessage,
  };
}

function buildQueueManualActionReason(record: ContentReportQueueItemVo): string {
  const reasonLabel = getReasonTypeLabel(record.voReasonType);
  const reasonDetail = record.voReasonDetail?.trim();
  return reasonDetail
    ? `来源举报单 #${record.voReportId}：${reasonLabel}，${reasonDetail}`
    : `来源举报单 #${record.voReportId}：${reasonLabel}`;
}

function buildActionLogManualActionReason(record: UserModerationActionVo, actionType?: ManualActionTypeValue): string {
  if (actionType === MANUAL_ACTION_TYPE.unmute || actionType === MANUAL_ACTION_TYPE.unban) {
    return `参考动作单 #${record.voActionId}，人工复核后${getManualActionTypeText(actionType)}`;
  }

  return `参考动作单 #${record.voActionId}，继续对用户 #${record.voTargetUserId} 执行人工治理`;
}

function resolveOpenTarget(input: ModerationTargetNavigationStateInput): ModerationOpenTarget | null {
  const navigationStatus = input.navigationStatus ?? 'Ready';
  if (navigationStatus === 'Unavailable' || navigationStatus === 'Unsupported') {
    return null;
  }

  const targetType = input.targetType ?? null;
  if (canOpenChatTarget(targetType, input.targetChannelId, input.targetMessageId)) {
    return {
      label: '打开聊天定位',
      url: buildDesktopChatTargetUrl(Number(input.targetChannelId), Number(input.targetMessageId)),
    };
  }

  if (targetType === 'Post' && input.targetPostId && input.targetPostId > 0) {
    return {
      label: '打开帖子详情',
      url: buildPublicForumTargetUrl(Number(input.targetPostId)),
    };
  }

  if (targetType === 'Comment' && input.targetPostId && input.targetPostId > 0) {
    const targetCommentId = input.targetCommentId ?? input.targetContentId;
    const isFallback = navigationStatus === 'Fallback';
    return {
      label: isFallback ? '打开所属帖子' : '打开评论定位',
      url: buildPublicForumTargetUrl(Number(input.targetPostId), isFallback ? undefined : targetCommentId),
    };
  }

  if (targetType === 'PostQuickReply' && input.targetPostId && input.targetPostId > 0) {
    return {
      label: '打开所属帖子',
      url: buildPublicForumTargetUrl(Number(input.targetPostId)),
    };
  }

  if (targetType === 'Product' && input.targetContentId && input.targetContentId > 0) {
    return {
      label: '打开商品详情',
      url: buildPublicShopTargetUrl(Number(input.targetContentId)),
    };
  }

  return null;
}

function resolveMissingTargetMessage(targetType: string | null | undefined, navigationMessage?: string | null): string {
  if (navigationMessage && navigationMessage.trim().length > 0) {
    return navigationMessage;
  }

  switch (targetType) {
    case 'ChatMessage':
      return '当前举报项缺少聊天定位信息';
    case 'Post':
    case 'Comment':
    case 'PostQuickReply':
      return '当前举报项缺少论坛定位信息';
    case 'Product':
      return '当前举报项缺少商品定位信息';
    default:
      return '当前举报项暂不支持直接回看';
  }
}

export const ModerationPage = () => {
  useDocumentTitle('内容治理');

  const [form] = Form.useForm();
  const [manualActionForm] = Form.useForm();
  const queueSectionRef = useRef<HTMLElement | null>(null);
  const manualActionSectionRef = useRef<HTMLElement | null>(null);
  const logSectionRef = useRef<HTMLElement | null>(null);
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
      const targetUserId = Number(toPositiveLongString(targetUserIdText ?? '') ?? 0);
      const sourceReportId = Number(toPositiveLongString(sourceReportIdText ?? '') ?? 0);
      const actionType = overrides?.actionType ?? logActionTypeFilter;
      const isActiveFilter = overrides?.isActive ?? logIsActiveFilter;
      const keyword = overrides?.keyword ?? logKeyword;
      const page = await getActionLogs({
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        targetUserId: Number.isFinite(targetUserId) && targetUserId > 0 ? targetUserId : undefined,
        sourceReportId: Number.isFinite(sourceReportId) && sourceReportId > 0 ? sourceReportId : undefined,
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
  };

  const resetManualActionForm = () => {
    manualActionForm.resetFields();
    setManualActionContextHint(null);
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
          targetUserId: reviewedItem.voTargetUserId > 0 ? String(reviewedItem.voTargetUserId) : undefined,
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
        targetUserId: Number(normalizedTargetUserId),
        actionType: values.actionType,
        durationHours: values.actionType === MANUAL_ACTION_TYPE.mute || values.actionType === MANUAL_ACTION_TYPE.ban
          ? values.durationHours ?? null
          : null,
        reason: toOptionalString(values.reason),
        sourceReportId: normalizedSourceReportId ? Number(normalizedSourceReportId) : null,
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

  const queueColumns: TableColumnsType<ContentReportQueueItemVo> = [
    {
      title: '举报单',
      key: 'report',
      width: 110,
      render: (_, record) => `#${record.voReportId}`,
    },
    {
      title: '目标',
      key: 'target',
      width: 340,
      render: (_, record) => renderModerationTarget({
        ...buildQueueTargetDisplayInput(record),
        showTargetUser: true,
      }),
    },
    {
      title: '举报人',
      key: 'reporter',
      width: 180,
      render: (_, record) => `${record.voReporterUserName} (#${record.voReporterUserId})`,
    },
    {
      title: '原因',
      key: 'reason',
      render: (_, record) => (
        <div>
          <div>{getReasonTypeLabel(record.voReasonType)}</div>
          {record.voReasonDetail ? <div style={{ color: '#8c8c8c' }}>{record.voReasonDetail}</div> : null}
        </div>
      ),
    },
    {
      title: '状态',
      key: 'status',
      width: 160,
      render: (_, record) => (
        <Space direction="vertical" size={4}>
          {renderReportStatus(record.voStatus)}
          {renderActionType(record.voReviewActionType)}
        </Space>
      ),
    },
    {
      title: '提交时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
    },
    {
      title: '操作',
      key: 'actions',
      width: 460,
      render: (_, record) => {
        const openTarget = resolveOpenTarget(buildQueueTargetNavigationInput(record));

        return (
          <Space wrap>
            {openTarget ? (
              <Button
                size="small"
                onClick={() => handleOpenTarget(buildQueueTargetNavigationInput(record))}
                >
                  {openTarget.label}
                </Button>
              ) : record.voTargetNavigationStatus === 'Unavailable' || record.voTargetNavigationStatus === 'Unsupported' ? (
                <span style={{ color: '#8c8c8c' }}>
                  {record.voTargetNavigationStatus === 'Unsupported' ? '暂不支持回看' : '目标已失效'}
                </span>
              ) : null}
            {record.voTargetUserId > 0 ? (
              <Button
                size="small"
                onClick={() => {
                  applyActionLogPreset({
                    targetUserId: String(record.voTargetUserId),
                    hint: `已带入被举报用户 #${record.voTargetUserId} 的治理动作日志，便于核对该用户历史处罚记录。`,
                  });
                }}
              >
                查看目标动作
              </Button>
            ) : null}
            {canReview && record.voTargetUserId > 0 ? (
              <Button
                size="small"
                onClick={() => {
                  applyManualActionPreset({
                    targetUserId: String(record.voTargetUserId),
                    sourceReportId: String(record.voReportId),
                    actionType: record.voReviewActionType === 'Mute'
                      ? MANUAL_ACTION_TYPE.mute
                      : record.voReviewActionType === 'Ban'
                        ? MANUAL_ACTION_TYPE.ban
                        : undefined,
                    durationHours: record.voReviewActionType === 'Mute' || record.voReviewActionType === 'Ban'
                      ? record.voReviewDurationHours ?? undefined
                      : undefined,
                    reason: buildQueueManualActionReason(record),
                    hint: `已带入举报单 #${record.voReportId} 与被举报用户 #${record.voTargetUserId}，可继续执行手动禁言 / 封禁或补录解除动作。`,
                  });
                }}
              >
                手动处置
              </Button>
            ) : null}
            {record.voReviewActionType !== 'None' ? (
              <Button
                size="small"
                onClick={() => {
                  applyActionLogPreset({
                    targetUserId: record.voTargetUserId > 0 ? String(record.voTargetUserId) : undefined,
                    sourceReportId: String(record.voReportId),
                    hint: `已带入举报单 #${record.voReportId} 关联的治理动作日志。`,
                  });
                }}
              >
                查看关联动作
              </Button>
            ) : null}
            {record.voStatus === 'Pending' && canReview ? (
              <Button size="small" variant="primary" onClick={() => openReviewModal(record)}>
                审核
              </Button>
            ) : (
              <span style={{ color: '#8c8c8c' }}>{record.voReviewedByName || '已处理'}</span>
            )}
          </Space>
        );
      },
    },
  ];

  const logColumns: TableColumnsType<UserModerationActionVo> = [
    {
      title: '动作单',
      key: 'voActionId',
      width: 110,
      render: (_, record) => `#${record.voActionId}`,
    },
    {
      title: '目标用户',
      key: 'targetUser',
      width: 180,
      render: (_, record) => record.voTargetUserName || `用户 ${record.voTargetUserId}`,
    },
    {
      title: '动作',
      key: 'actionType',
      width: 140,
      render: (_, record) => renderActionType(record.voActionType),
    },
    {
      title: '来源举报',
      key: 'sourceReport',
      width: 360,
      render: (_, record) => {
        if (!record.voSourceReportId) {
          return <span style={{ color: '#8c8c8c' }}>-</span>;
        }

        return (
          record.voSourceReportTargetType
            ? (
              <div>
                <div>举报单 #{record.voSourceReportId}</div>
                {renderModerationTarget(buildActionSourceTargetDisplayInput(record))}
              </div>
            )
            : <div style={{ color: '#8c8c8c' }}>未保留目标快照</div>
        );
      },
    },
    {
      title: '原因',
      dataIndex: 'voReason',
      key: 'voReason',
    },
    {
      title: '操作者',
      dataIndex: 'voOperatorUserName',
      key: 'voOperatorUserName',
      width: 140,
    },
    {
      title: '状态',
      key: 'voIsActive',
      width: 100,
      render: (_, record) => <Tag color={record.voIsActive ? 'processing' : 'default'}>{record.voIsActive ? '生效中' : '已结束'}</Tag>,
    },
    {
      title: '操作',
      key: 'actions',
      width: 420,
      render: (_, record) => {
        const openTarget = resolveOpenTarget(buildActionSourceTargetNavigationInput(record));

        return (
          <Space wrap>
            {openTarget ? (
              <Button
                size="small"
                onClick={() => handleOpenTarget(buildActionSourceTargetNavigationInput(record))}
              >
                {openTarget.label}
              </Button>
            ) : (
              <span style={{ color: '#8c8c8c' }}>
                {record.voSourceReportTargetNavigationStatus === 'Unsupported'
                  ? '暂不支持回看'
                  : record.voSourceReportTargetNavigationStatus === 'Unavailable'
                    ? '目标已失效'
                    : '-'}
              </span>
            )}
            {record.voSourceReportId ? (
              <Button
                size="small"
                onClick={() => {
                  applyQueuePreset({
                    keyword: String(record.voSourceReportId),
                    hint: `已带入来源举报单 #${record.voSourceReportId}，便于回看原始审核记录与目标快照。`,
                  });
                }}
              >
                查看原举报
              </Button>
            ) : null}
            {canReview ? (
              <Button
                size="small"
                onClick={() => {
                  applyManualActionPreset({
                    targetUserId: String(record.voTargetUserId),
                    sourceReportId: record.voSourceReportId ? String(record.voSourceReportId) : undefined,
                    reason: buildActionLogManualActionReason(record),
                    hint: `已带入动作单 #${record.voActionId} 的目标用户${record.voSourceReportId ? ` 与来源举报单 #${record.voSourceReportId}` : ''}，可继续执行人工治理。`,
                  });
                }}
              >
                手动处置
              </Button>
            ) : null}
            {canReview && record.voIsActive && (record.voActionType === 'Mute' || record.voActionType === 'Ban') ? (
              <Button
                size="small"
                variant="primary"
                onClick={() => {
                  const actionType = record.voActionType === 'Mute'
                    ? MANUAL_ACTION_TYPE.unmute
                    : MANUAL_ACTION_TYPE.unban;
                  applyManualActionPreset({
                    targetUserId: String(record.voTargetUserId),
                    sourceReportId: record.voSourceReportId ? String(record.voSourceReportId) : undefined,
                    actionType,
                    reason: buildActionLogManualActionReason(record, actionType),
                    hint: `已根据动作单 #${record.voActionId} 带入${getManualActionTypeText(actionType)}建议，提交后会自动回跳到对应日志记录。`,
                  });
                }}
              >
                {record.voActionType === 'Mute' ? '解除禁言' : '解除封禁'}
              </Button>
            ) : null}
          </Space>
        );
      },
    },
    {
      title: '开始时间',
      dataIndex: 'voStartTime',
      key: 'voStartTime',
      width: 180,
    },
    {
      title: '结束时间',
      key: 'voEndTime',
      width: 180,
      render: (_, record) => record.voEndTime || '-',
    },
  ];

  return (
    <div className="admin-feature-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>内容治理</h2>
            <p className="admin-feature-subtle">举报队列、手动治理动作与治理日志统一在 Console 收口。</p>
          </div>
          <Button icon={<ReloadOutlined />} onClick={() => {
            void Promise.all([loadQueue(), loadLogs()]);
          }}>
            刷新
          </Button>
        </div>
      </section>

      <div className="admin-feature-banner">
        当前治理链路已统一接入帖子、评论、聊天室消息和商品举报，审核通过后可联动禁言 / 封禁；历史动作也支持继续处置或直接解除。
      </div>

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

      {canReview ? (
        <section className="admin-feature-card" ref={manualActionSectionRef}>
          <div className="admin-feature-header">
            <div>
              <h3>手动治理动作</h3>
              <p className="admin-feature-subtle">从举报队列或治理日志一键带入目标用户、来源举报单和解除建议，补齐人工禁言 / 封禁 / 解除动作闭环。</p>
            </div>
            <Space>
              <Button onClick={resetManualActionForm}>
                清空表单
              </Button>
              <Button variant="primary" disabled={submittingManualAction} onClick={() => {
                void handleSubmitManualAction();
              }}>
                {submittingManualAction ? '执行中...' : '执行治理动作'}
              </Button>
            </Space>
          </div>

          {manualActionContextHint ? (
            <div className="admin-feature-banner" style={{ marginTop: 16 }}>
              {manualActionContextHint}
            </div>
          ) : null}

          <Form form={manualActionForm} layout="vertical" className="moderation-manual-action-form">
            <div className="moderation-manual-action-form__grid">
              <Form.Item
                name="targetUserId"
                label="目标用户 ID"
                rules={[
                  { required: true, message: '请输入目标用户 ID' },
                  {
                    validator: (_, value) => {
                      if (typeof value !== 'string' || value.trim().length === 0) {
                        return Promise.resolve();
                      }

                      if (toPositiveLongString(value)) {
                        return Promise.resolve();
                      }

                      return Promise.reject(new Error('请输入有效的目标用户 ID'));
                    },
                  },
                ]}
              >
                <Input placeholder="输入目标用户 ID，或从上方队列 / 日志一键带入" />
              </Form.Item>

              <Form.Item
                name="sourceReportId"
                label="关联举报单 ID"
                rules={[
                  {
                    validator: (_, value) => {
                      if (value === undefined || value === null || String(value).trim().length === 0) {
                        return Promise.resolve();
                      }

                      return toPositiveLongString(String(value))
                        ? Promise.resolve()
                        : Promise.reject(new Error('请输入有效的关联举报单 ID'));
                    },
                  },
                ]}
              >
                <Input placeholder="可选，保留动作与举报单的关联" />
              </Form.Item>
            </div>

            <div className="moderation-manual-action-form__grid">
              <Form.Item name="actionType" label="治理动作" rules={[{ required: true, message: '请选择治理动作' }]}>
                <Select options={MANUAL_ACTION_OPTIONS} placeholder="选择禁言、封禁或解除动作" />
              </Form.Item>

              <Form.Item
                noStyle
                shouldUpdate={(prev, next) => prev.actionType !== next.actionType}
              >
                {({ getFieldValue }) => {
                  const currentActionType = getFieldValue('actionType') as ManualActionTypeValue | undefined;
                  if (currentActionType !== MANUAL_ACTION_TYPE.mute && currentActionType !== MANUAL_ACTION_TYPE.ban) {
                    return <div className="moderation-manual-action-form__field-placeholder" />;
                  }

                  return (
                    <Form.Item
                      name="durationHours"
                      label={currentActionType === MANUAL_ACTION_TYPE.mute ? '持续时长（小时）' : '持续时长（小时，可留空表示永久封禁）'}
                      rules={currentActionType === MANUAL_ACTION_TYPE.mute
                        ? [{ required: true, message: '请输入禁言时长' }]
                        : []}
                    >
                      <InputNumber min={1} max={720} style={{ width: '100%' }} placeholder={currentActionType === MANUAL_ACTION_TYPE.mute ? '例如 24' : '留空表示永久封禁'} />
                    </Form.Item>
                  );
                }}
              </Form.Item>
            </div>

            <Form.Item name="reason" label="动作原因">
              <Input.TextArea rows={4} maxLength={500} showCount placeholder="补充人工治理依据；若从上方队列或日志带入，会自动预填推荐说明。" />
            </Form.Item>

            <div className="moderation-manual-action-form__footnote">
              禁言必须填写时长，封禁可留空表示永久；解除禁言 / 解除封禁会记录新的治理动作单，并自动回跳到对应日志。
            </div>
          </Form>
        </section>
      ) : null}

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
              {renderModerationTarget({
                ...buildQueueTargetDisplayInput(reviewingItem),
                showTargetUser: true,
              })}
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

import { useEffect, useState } from 'react';
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
import { getActionLogs, getReviewQueue, reviewReport, type ContentReportQueueItemVo, type UserModerationActionVo } from '@/api/moderationApi';
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

const ACTION_OPTIONS = [
  { label: '不处罚', value: 0 },
  { label: '禁言', value: 1 },
  { label: '封禁', value: 2 },
];

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
      <div className="moderation-target__identity">{input.targetType} #{input.targetContentId ?? '-'}</div>
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
  const [loadingQueue, setLoadingQueue] = useState(false);
  const [loadingLogs, setLoadingLogs] = useState(false);
  const [queueItems, setQueueItems] = useState<ContentReportQueueItemVo[]>([]);
  const [queueTotal, setQueueTotal] = useState(0);
  const [queuePageIndex, setQueuePageIndex] = useState(1);
  const [queuePageSize, setQueuePageSize] = useState(20);
  const [statusFilter, setStatusFilter] = useState(-1);
  const [logItems, setLogItems] = useState<UserModerationActionVo[]>([]);
  const [logTotal, setLogTotal] = useState(0);
  const [logPageIndex, setLogPageIndex] = useState(1);
  const [logPageSize, setLogPageSize] = useState(10);
  const [logTargetUserId, setLogTargetUserId] = useState('');
  const [reviewingItem, setReviewingItem] = useState<ContentReportQueueItemVo | null>(null);
  const [submittingReview, setSubmittingReview] = useState(false);

  const canReview = usePermission(CONSOLE_PERMISSIONS.moderationReview);

  const handleOpenTarget = (input: ModerationTargetNavigationStateInput) => {
    const target = resolveOpenTarget(input);
    if (!target) {
      message.error(resolveMissingTargetMessage(input.targetType, input.navigationMessage));
      return;
    }

    window.open(target.url, '_blank', 'noopener');
  };

  const loadQueue = async (targetPageIndex = queuePageIndex, targetPageSize = queuePageSize) => {
    try {
      setLoadingQueue(true);
      const page = await getReviewQueue({
        status: statusFilter,
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
      });

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

  const loadLogs = async (targetPageIndex = logPageIndex, targetPageSize = logPageSize) => {
    try {
      setLoadingLogs(true);
      const targetUserId = Number(logTargetUserId);
      const page = await getActionLogs({
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        targetUserId: Number.isFinite(targetUserId) && targetUserId > 0 ? targetUserId : undefined,
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
  }, [statusFilter, queuePageSize]);

  useEffect(() => {
    void loadLogs(1, logPageSize);
  }, [logPageSize]);

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
      const values = await form.validateFields();
      setSubmittingReview(true);
      await reviewReport({
        reportId: reviewingItem.voReportId,
        isApproved: values.isApproved,
        actionType: values.actionType,
        durationHours: values.actionType === 0 ? null : values.durationHours,
        reviewRemark: values.reviewRemark,
      });

      message.success('审核完成');
      setReviewingItem(null);
      await Promise.all([loadQueue(), loadLogs()]);
    } catch (error) {
      log.error('ModerationPage', '提交审核失败:', error);
      message.error('提交审核失败');
    } finally {
      setSubmittingReview(false);
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
          <div>{record.voReasonType}</div>
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
      width: 260,
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
      width: 180,
      render: (_, record) => {
        const openTarget = resolveOpenTarget(buildActionSourceTargetNavigationInput(record));

        return openTarget ? (
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
            <p className="admin-feature-subtle">举报队列、审核动作与治理日志统一在 Console 收口。</p>
          </div>
          <Button icon={<ReloadOutlined />} onClick={() => {
            void Promise.all([loadQueue(), loadLogs()]);
          }}>
            刷新
          </Button>
        </div>
      </section>

      <div className="admin-feature-banner">
        当前治理链路已统一接入帖子、评论、聊天室消息和商品举报，审核通过后可继续联动禁言或封禁动作。
      </div>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>举报审核队列</h3>
            <p className="admin-feature-subtle">按状态查看举报单，并在审核时直接联动治理动作。</p>
          </div>
          <Space wrap>
            <Select
              value={statusFilter}
              style={{ width: 160 }}
              options={REVIEW_STATUS_OPTIONS}
              onChange={(value) => setStatusFilter(value)}
            />
          </Space>
        </div>

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
          scroll={{ x: 1460 }}
        />
      </section>

      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h3>治理动作日志</h3>
            <p className="admin-feature-subtle">回看审核联动后实际落下的禁言/封禁动作。</p>
          </div>
          <Space wrap>
            <Input
              placeholder="按目标用户 ID 过滤"
              value={logTargetUserId}
              onChange={(event) => setLogTargetUserId(event.target.value)}
              style={{ width: 200 }}
            />
            <Button
              variant="primary"
              onClick={() => {
                void loadLogs(1, logPageSize);
              }}
            >
              查询
            </Button>
          </Space>
        </div>

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
          scroll={{ x: 1640 }}
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

import {
  Button,
  Space,
  Tag,
  type TableColumnsType,
} from '@radish/ui';
import {
  type ContentReportQueueItemVo,
  type UserModerationActionVo,
} from '@/api/moderationApi';
import {
  MANUAL_ACTION_TYPE,
  buildActionLogManualActionReason,
  buildActionSourceTargetDisplayInput,
  buildActionSourceTargetNavigationInput,
  buildQueueManualActionReason,
  buildQueueTargetDisplayInput,
  buildQueueTargetNavigationInput,
  getManualActionTypeText,
  getReasonTypeLabel,
  renderActionType,
  renderModerationTarget,
  renderReportStatus,
  resolveOpenTarget,
  type ActionLogPreset,
  type ManualActionPreset,
  type ModerationTargetNavigationStateInput,
  type QueuePreset,
} from './moderationPageHelpers';

interface ModerationQueueColumnActions {
  canReview: boolean;
  onOpenTarget: (input: ModerationTargetNavigationStateInput) => void;
  onApplyActionLogPreset: (preset: ActionLogPreset) => void;
  onApplyManualActionPreset: (preset: ManualActionPreset) => void;
  onOpenReviewModal: (item: ContentReportQueueItemVo) => void;
}

interface ModerationLogColumnActions {
  canReview: boolean;
  onOpenTarget: (input: ModerationTargetNavigationStateInput) => void;
  onApplyQueuePreset: (preset: QueuePreset) => void;
  onApplyManualActionPreset: (preset: ManualActionPreset) => void;
}

const mutedTextStyle = { color: '#8c8c8c' };

export function createModerationQueueColumns(
  actions: ModerationQueueColumnActions
): TableColumnsType<ContentReportQueueItemVo> {
  return [
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
          {record.voReasonDetail ? <div style={mutedTextStyle}>{record.voReasonDetail}</div> : null}
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
        const targetNavigationInput = buildQueueTargetNavigationInput(record);
        const openTarget = resolveOpenTarget(targetNavigationInput);

        return (
          <Space wrap>
            {openTarget ? (
              <Button
                size="small"
                onClick={() => actions.onOpenTarget(targetNavigationInput)}
              >
                {openTarget.label}
              </Button>
            ) : record.voTargetNavigationStatus === 'Unavailable' || record.voTargetNavigationStatus === 'Unsupported' ? (
              <span style={mutedTextStyle}>
                {record.voTargetNavigationStatus === 'Unsupported' ? '暂不支持回看' : '目标已失效'}
              </span>
            ) : null}
            {record.voTargetUserId > 0 ? (
              <Button
                size="small"
                onClick={() => {
                  actions.onApplyActionLogPreset({
                    targetUserId: String(record.voTargetUserId),
                    hint: `已带入被举报用户 #${record.voTargetUserId} 的治理动作日志，便于核对该用户历史处罚记录。`,
                  });
                }}
              >
                查看目标动作
              </Button>
            ) : null}
            {actions.canReview && record.voTargetUserId > 0 ? (
              <Button
                size="small"
                onClick={() => {
                  actions.onApplyManualActionPreset({
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
                  actions.onApplyActionLogPreset({
                    targetUserId: record.voTargetUserId > 0 ? String(record.voTargetUserId) : undefined,
                    sourceReportId: String(record.voReportId),
                    hint: `已带入举报单 #${record.voReportId} 关联的治理动作日志。`,
                  });
                }}
              >
                查看关联动作
              </Button>
            ) : null}
            {record.voStatus === 'Pending' && actions.canReview ? (
              <Button size="small" variant="primary" onClick={() => actions.onOpenReviewModal(record)}>
                审核
              </Button>
            ) : (
              <span style={mutedTextStyle}>{record.voReviewedByName || '已处理'}</span>
            )}
          </Space>
        );
      },
    },
  ];
}

export function createModerationLogColumns(
  actions: ModerationLogColumnActions
): TableColumnsType<UserModerationActionVo> {
  return [
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
          return <span style={mutedTextStyle}>-</span>;
        }

        return (
          record.voSourceReportTargetType
            ? (
              <div>
                <div>举报单 #{record.voSourceReportId}</div>
                {renderModerationTarget(buildActionSourceTargetDisplayInput(record))}
              </div>
            )
            : <div style={mutedTextStyle}>未保留目标快照</div>
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
        const targetNavigationInput = buildActionSourceTargetNavigationInput(record);
        const openTarget = resolveOpenTarget(targetNavigationInput);

        return (
          <Space wrap>
            {openTarget ? (
              <Button
                size="small"
                onClick={() => actions.onOpenTarget(targetNavigationInput)}
              >
                {openTarget.label}
              </Button>
            ) : (
              <span style={mutedTextStyle}>
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
                  actions.onApplyQueuePreset({
                    keyword: String(record.voSourceReportId),
                    hint: `已带入来源举报单 #${record.voSourceReportId}，便于回看原始审核记录与目标快照。`,
                  });
                }}
              >
                查看原举报
              </Button>
            ) : null}
            {actions.canReview ? (
              <Button
                size="small"
                onClick={() => {
                  actions.onApplyManualActionPreset({
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
            {actions.canReview && record.voIsActive && (record.voActionType === 'Mute' || record.voActionType === 'Ban') ? (
              <Button
                size="small"
                variant="primary"
                onClick={() => {
                  const actionType = record.voActionType === 'Mute'
                    ? MANUAL_ACTION_TYPE.unmute
                    : MANUAL_ACTION_TYPE.unban;
                  actions.onApplyManualActionPreset({
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
}

import {
  Button,
  Space,
  Tag,
  formatLocalizedDateTime,
  type TableColumnsType,
} from '@radish/ui';
import type { TFunction } from 'i18next';
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
  hasPositiveLongId,
  resolveOpenTarget,
  type ActionLogPreset,
  type ManualActionPreset,
  type ModerationTargetNavigationStateInput,
  type QueuePreset,
} from './moderationPageHelpers';
import {
  ActionTypeTag,
  ModerationTargetDisplay,
  ReportStatusTag,
} from './moderationPageRenderers';

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

export function createModerationQueueColumns(
  actions: ModerationQueueColumnActions,
  t: TFunction,
  language: string,
): TableColumnsType<ContentReportQueueItemVo> {
  return [
    {
      title: t('moderation.column.report'),
      key: 'report',
      width: 110,
      render: (_, record) => `#${record.voReportId}`,
    },
    {
      title: t('moderation.column.target'),
      key: 'target',
      width: 340,
      render: (_, record) => (
        <ModerationTargetDisplay
          input={{
            ...buildQueueTargetDisplayInput(record),
            showTargetUser: true,
          }}
        />
      ),
    },
    {
      title: t('moderation.column.reporter'),
      key: 'reporter',
      width: 180,
      render: (_, record) => `${record.voReporterUserName} (#${record.voReporterUserId})`,
    },
    {
      title: t('moderation.column.reason'),
      key: 'reason',
      render: (_, record) => (
        <div>
          <div>{getReasonTypeLabel(record.voReasonType, t)}</div>
          {record.voReasonDetail ? <div className="moderation-table-muted">{record.voReasonDetail}</div> : null}
        </div>
      ),
    },
    {
      title: t('moderation.column.status'),
      key: 'status',
      width: 160,
      render: (_, record) => (
        <Space orientation="vertical" size={4}>
          <ReportStatusTag value={record.voStatus} />
          <ActionTypeTag value={record.voReviewActionType} />
        </Space>
      ),
    },
    {
      title: t('moderation.column.submittedAt'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (value: string) => formatLocalizedDateTime(value, language),
    },
    {
      title: t('moderation.column.action'),
      key: 'actions',
      width: 460,
      render: (_, record) => {
        const targetNavigationInput = buildQueueTargetNavigationInput(record);
        const openTarget = resolveOpenTarget(targetNavigationInput, t);

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
              <span className="moderation-table-muted">
                {t(record.voTargetNavigationStatus === 'Unsupported'
                  ? 'moderation.navigation.unsupported'
                  : 'moderation.navigation.targetUnavailable')}
              </span>
            ) : null}
            {hasPositiveLongId(record.voTargetUserId) ? (
              <Button
                size="small"
                onClick={() => {
                  actions.onApplyActionLogPreset({
                    targetUserId: String(record.voTargetUserId),
                    hint: t('moderation.hint.targetUserLogs', { userId: record.voTargetUserId }),
                  });
                }}
              >
                {t('moderation.column.viewTargetActions')}
              </Button>
            ) : null}
            {actions.canReview && hasPositiveLongId(record.voTargetUserId) ? (
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
                    reason: buildQueueManualActionReason(record, t),
                    hint: t('moderation.hint.reportManual', { reportId: record.voReportId, userId: record.voTargetUserId }),
                  });
                }}
              >
                {t('moderation.column.manual')}
              </Button>
            ) : null}
            {record.voReviewActionType !== 'None' ? (
              <Button
                size="small"
                onClick={() => {
                  actions.onApplyActionLogPreset({
                    targetUserId: hasPositiveLongId(record.voTargetUserId) ? String(record.voTargetUserId) : undefined,
                    sourceReportId: String(record.voReportId),
                    hint: t('moderation.hint.reportLogs', { reportId: record.voReportId }),
                  });
                }}
              >
                {t('moderation.column.viewRelatedActions')}
              </Button>
            ) : null}
            {record.voStatus === 'Pending' && actions.canReview ? (
              <Button size="small" variant="primary" onClick={() => actions.onOpenReviewModal(record)}>
                {t('moderation.column.review')}
              </Button>
            ) : (
              <span className="moderation-table-muted">{record.voReviewedByName || t('moderation.column.processed')}</span>
            )}
          </Space>
        );
      },
    },
  ];
}

export function createModerationLogColumns(
  actions: ModerationLogColumnActions,
  t: TFunction,
  language: string,
): TableColumnsType<UserModerationActionVo> {
  return [
    {
      title: t('moderation.column.actionRecord'),
      key: 'voActionId',
      width: 110,
      render: (_, record) => `#${record.voActionId}`,
    },
    {
      title: t('moderation.column.targetUser'),
      key: 'targetUser',
      width: 180,
      render: (_, record) => record.voTargetUserName || t('moderation.target.user', { id: record.voTargetUserId }),
    },
    {
      title: t('moderation.column.action'),
      key: 'actionType',
      width: 140,
      render: (_, record) => <ActionTypeTag value={record.voActionType} />,
    },
    {
      title: t('moderation.column.sourceReport'),
      key: 'sourceReport',
      width: 360,
      render: (_, record) => {
        if (!record.voSourceReportId) {
          return <span className="moderation-table-muted">-</span>;
        }

        return (
          record.voSourceReportTargetType
            ? (
              <div>
                <div>{t('moderation.column.reportRef', { id: record.voSourceReportId })}</div>
                <ModerationTargetDisplay input={buildActionSourceTargetDisplayInput(record)} />
              </div>
            )
            : <div className="moderation-table-muted">{t('moderation.column.noSnapshot')}</div>
        );
      },
    },
    {
      title: t('moderation.column.reason'),
      dataIndex: 'voReason',
      key: 'voReason',
    },
    {
      title: t('moderation.column.operator'),
      dataIndex: 'voOperatorUserName',
      key: 'voOperatorUserName',
      width: 140,
    },
    {
      title: t('moderation.column.status'),
      key: 'voIsActive',
      width: 100,
      render: (_, record) => <Tag color={record.voIsActive ? 'processing' : 'default'}>{t(record.voIsActive ? 'moderation.action.active' : 'moderation.action.inactive')}</Tag>,
    },
    {
      title: t('moderation.column.action'),
      key: 'actions',
      width: 420,
      render: (_, record) => {
        const targetNavigationInput = buildActionSourceTargetNavigationInput(record);
        const openTarget = resolveOpenTarget(targetNavigationInput, t);

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
              <span className="moderation-table-muted">
                {record.voSourceReportTargetNavigationStatus === 'Unsupported'
                  ? t('moderation.navigation.unsupported')
                  : record.voSourceReportTargetNavigationStatus === 'Unavailable'
                    ? t('moderation.navigation.targetUnavailable')
                    : '-'}
              </span>
            )}
            {record.voSourceReportId ? (
              <Button
                size="small"
                onClick={() => {
                  actions.onApplyQueuePreset({
                    keyword: String(record.voSourceReportId),
                    hint: t('moderation.hint.sourceReport', { reportId: record.voSourceReportId }),
                  });
                }}
              >
                {t('moderation.column.viewOriginalReport')}
              </Button>
            ) : null}
            {actions.canReview ? (
              <Button
                size="small"
                onClick={() => {
                  actions.onApplyManualActionPreset({
                    targetUserId: String(record.voTargetUserId),
                    sourceReportId: record.voSourceReportId ? String(record.voSourceReportId) : undefined,
                    reason: buildActionLogManualActionReason(record, t),
                    hint: t('moderation.hint.actionManual', {
                      actionId: record.voActionId,
                      source: record.voSourceReportId
                        ? t('moderation.hint.sourceSuffix', { reportId: record.voSourceReportId })
                        : '',
                    }),
                  });
                }}
              >
                {t('moderation.column.manual')}
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
                    reason: buildActionLogManualActionReason(record, t, actionType),
                    hint: t('moderation.hint.actionUndo', {
                      actionId: record.voActionId,
                      action: getManualActionTypeText(actionType, t),
                    }),
                  });
                }}
              >
                {t(record.voActionType === 'Mute' ? 'moderation.action.unmute' : 'moderation.action.unban')}
              </Button>
            ) : null}
          </Space>
        );
      },
    },
    {
      title: t('moderation.column.startedAt'),
      dataIndex: 'voStartTime',
      key: 'voStartTime',
      width: 180,
      render: (value: string) => formatLocalizedDateTime(value, language),
    },
    {
      title: t('moderation.column.endedAt'),
      key: 'voEndTime',
      width: 180,
      render: (_, record) => record.voEndTime ? formatLocalizedDateTime(record.voEndTime, language) : '-',
    },
  ];
}

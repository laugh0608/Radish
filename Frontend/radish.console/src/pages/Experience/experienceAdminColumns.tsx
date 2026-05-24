import {
  Button,
  Tag,
  type TableColumnsType,
} from '@radish/ui';
import dayjs from 'dayjs';
import {
  type ExpTransactionVo,
  type LevelConfigVo,
  type UserExpDailyLimitSnapshotVo,
  type UserExpDailyStatsVo,
  type UserExperienceGovernanceActionVo,
} from '@/api/experienceAdminApi';
import {
  formatDisplayTime,
  formatLimitValue,
  formatStatDate,
  formatTransactionAmount,
  getGovernanceActionTagColor,
  getGovernanceReviewResultTagColor,
} from './experienceAdminHelpers';

interface DailyStatsColumnActions {
  canFreeze: boolean;
  dailyLimits?: UserExpDailyLimitSnapshotVo | null;
  onDayReview: (record: UserExpDailyStatsVo) => void;
  onDayGovernanceReview: (record: UserExpDailyStatsVo) => void;
  onDayFreezeReason: (record: UserExpDailyStatsVo) => void;
}

const mutedTextStyle = { color: '#8c8c8c' };
const inlineGroupStyle = { display: 'flex', gap: 12, flexWrap: 'wrap' } as const;
const compactInlineGroupStyle = { display: 'flex', gap: 8, flexWrap: 'wrap' } as const;

export function createLevelColumns(): TableColumnsType<LevelConfigVo> {
  return [
    {
      title: '等级',
      key: 'level',
      width: 120,
      render: (_, record) => (
        <div>
          <div>L{record.voLevel}</div>
          <div style={mutedTextStyle}>{record.voLevelName}</div>
        </div>
      ),
    },
    {
      title: '累计经验',
      dataIndex: 'voExpCumulative',
      key: 'voExpCumulative',
      width: 140,
    },
    {
      title: '升级所需',
      dataIndex: 'voExpRequired',
      key: 'voExpRequired',
      width: 140,
    },
    {
      title: '状态',
      key: 'status',
      width: 100,
      render: (_, record) => <Tag color={record.voIsEnabled ? 'success' : 'default'}>{record.voIsEnabled ? '启用' : '禁用'}</Tag>,
    },
    {
      title: '说明',
      key: 'description',
      render: (_, record) => record.voDescription || '-',
    },
  ];
}

export function createDailyStatsColumns({
  canFreeze,
  dailyLimits,
  onDayReview,
  onDayGovernanceReview,
  onDayFreezeReason,
}: DailyStatsColumnActions): TableColumnsType<UserExpDailyStatsVo> {
  return [
    {
      title: '日期',
      key: 'date',
      width: 120,
      render: (_, record) => (
        <div>
          <div>{formatStatDate(record.voStatDate)}</div>
          <div style={mutedTextStyle}>{dayjs(record.voStatDate).format('ddd')}</div>
        </div>
      ),
    },
    {
      title: '总经验',
      dataIndex: 'voExpEarned',
      key: 'voExpEarned',
      width: 100,
      render: (value: number) => (
        <div>
          <div>{value}</div>
          {dailyLimits?.voDailyLimitEnabled && (
            <div style={mutedTextStyle}>
              上限 {formatLimitValue(value, dailyLimits.voMaxDailyExp, dailyLimits)}
            </div>
          )}
        </div>
      ),
    },
    {
      title: '来源拆分',
      key: 'sources',
      width: 360,
      render: (_, record) => (
        <div style={inlineGroupStyle}>
          <span>发帖 {formatLimitValue(record.voExpFromPost, dailyLimits?.voMaxExpFromPost ?? 0, dailyLimits)}</span>
          <span>评论 {formatLimitValue(record.voExpFromComment, dailyLimits?.voMaxExpFromComment ?? 0, dailyLimits)}</span>
          <span>点赞 {formatLimitValue(record.voExpFromLike, dailyLimits?.voMaxExpFromLike ?? 0, dailyLimits)}</span>
          <span>高亮 {formatLimitValue(record.voExpFromHighlight, dailyLimits?.voMaxExpFromHighlight ?? 0, dailyLimits)}</span>
          <span>登录 {formatLimitValue(record.voExpFromLogin, dailyLimits?.voMaxExpFromLogin ?? 0, dailyLimits)}</span>
        </div>
      ),
    },
    {
      title: '行为计数',
      key: 'counts',
      width: 320,
      render: (_, record) => (
        <div style={inlineGroupStyle}>
          <span>发帖 {record.voPostCount}</span>
          <span>评论 {record.voCommentCount}</span>
          <span>点赞 {record.voLikeGivenCount}</span>
          <span>被赞 {record.voLikeReceivedCount}</span>
        </div>
      ),
    },
    {
      title: '观察',
      key: 'observations',
      width: 360,
      render: (_, record) => {
        const observations = record.voObservations ?? [];
        const contextObservations = observations.filter((observation) => observation.voKind !== 'anomaly');
        const anomalyObservations = observations.filter((observation) => observation.voKind === 'anomaly');
        return (
          <div style={{ display: 'grid', gap: 10 }}>
            <div>
              <div style={{ color: '#8c8c8c', marginBottom: 6 }}>来源 / 状态</div>
              <div style={compactInlineGroupStyle}>
                {contextObservations.length > 0 ? contextObservations.map((observation) => (
                  <Tag key={observation.voRuleCode} color={observation.voTone} title={observation.voDescription ?? undefined}>
                    {observation.voLabel}
                  </Tag>
                )) : <span style={mutedTextStyle}>-</span>}
              </div>
            </div>
            <div>
              <div style={{ color: '#8c8c8c', marginBottom: 6 }}>异常判定</div>
              <div style={compactInlineGroupStyle}>
                {anomalyObservations.length > 0 ? anomalyObservations.map((observation) => (
                  <Tag key={observation.voRuleCode} color={observation.voTone} title={observation.voDescription ?? undefined}>
                    {observation.voLabel}
                  </Tag>
                )) : <span style={mutedTextStyle}>未命中</span>}
              </div>
            </div>
          </div>
        );
      },
    },
    {
      title: '复核动作',
      key: 'reviewActions',
      width: 320,
      render: (_, record) => {
        const hasAnomaly = (record.voObservations ?? []).some((observation) => observation.voKind === 'anomaly');
        if (!hasAnomaly) {
          return <span style={mutedTextStyle}>-</span>;
        }

        return (
          <div style={compactInlineGroupStyle}>
            <Button onClick={() => onDayReview(record)}>
              查看流水
            </Button>
            <Button
              variant="secondary"
              disabled={!canFreeze}
              onClick={() => onDayGovernanceReview(record)}
            >
              带入复核结论
            </Button>
            <Button
              variant="secondary"
              disabled={!canFreeze}
              onClick={() => onDayFreezeReason(record)}
            >
              带入冻结原因
            </Button>
          </div>
        );
      },
    },
  ];
}

export function createTransactionColumns(): TableColumnsType<ExpTransactionVo> {
  return [
    {
      title: '时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (value: string) => formatDisplayTime(value),
    },
    {
      title: '类型',
      key: 'type',
      width: 140,
      render: (_, record) => (
        <div>
          <div>{record.voExpTypeDisplay}</div>
          <div style={mutedTextStyle}>{record.voExpType}</div>
        </div>
      ),
    },
    {
      title: '变动量',
      dataIndex: 'voExpAmount',
      key: 'voExpAmount',
      width: 110,
      render: (value: number) => (
        <span style={{ color: value >= 0 ? '#389e0d' : '#cf1322', fontWeight: 600 }}>
          {formatTransactionAmount(value)}
        </span>
      ),
    },
    {
      title: '经验变化',
      key: 'expRange',
      width: 180,
      render: (_, record) => `${record.voExpBefore} -> ${record.voExpAfter}`,
    },
    {
      title: '等级变化',
      key: 'levelRange',
      width: 170,
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <span>{`L${record.voLevelBefore} -> L${record.voLevelAfter}`}</span>
          {record.voLevelAfter > record.voLevelBefore ? <Tag color="success">升级</Tag> : null}
        </div>
      ),
    },
    {
      title: '操作者',
      key: 'operator',
      width: 180,
      render: (_, record) => (
        <div>
          <div>{record.voOperatorName || 'System'}</div>
          <div style={mutedTextStyle}>ID: {record.voOperatorId}</div>
        </div>
      ),
    },
    {
      title: '备注',
      dataIndex: 'voRemark',
      key: 'voRemark',
      render: (value?: string | null) => value || '-',
    },
  ];
}

export function createGovernanceActionColumns(): TableColumnsType<UserExperienceGovernanceActionVo> {
  return [
    {
      title: '动作',
      key: 'actionType',
      width: 200,
      render: (_, record) => (
        <div style={{ display: 'grid', gap: 6 }}>
          <div>
            <Tag color={getGovernanceActionTagColor(record.voActionType)}>
              {record.voActionTypeDisplay}
            </Tag>
          </div>
          {record.voActionType === 'Freeze' && (
            <span style={mutedTextStyle}>
              到期：{record.voFrozenUntil ? formatDisplayTime(record.voFrozenUntil) : '永久冻结'}
            </span>
          )}
        </div>
      ),
    },
    {
      title: '复核结论',
      key: 'reviewResult',
      width: 180,
      render: (_, record) => record.voReviewResultDisplay ? (
        <Tag color={getGovernanceReviewResultTagColor(record.voReviewResult)}>
          {record.voReviewResultDisplay}
        </Tag>
      ) : <span style={mutedTextStyle}>-</span>,
    },
    {
      title: '证据快照',
      key: 'evidence',
      width: 360,
      render: (_, record) => (
        <div style={{ display: 'grid', gap: 6 }}>
          <span>{record.voEvidenceSummary || '-'}</span>
          {record.voRecommendationReason && (
            <span style={mutedTextStyle}>
              建议原因：{record.voRecommendationReason}
            </span>
          )}
        </div>
      ),
    },
    {
      title: '备注',
      dataIndex: 'voRemark',
      key: 'voRemark',
      width: 320,
      render: (value: string) => (
        <div style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
          {value}
        </div>
      ),
    },
    {
      title: '操作人 / 时间',
      key: 'operator',
      width: 220,
      render: (_, record) => (
        <div style={{ display: 'grid', gap: 6 }}>
          <span>{record.voOperatorName || 'System'}（ID: {record.voOperatorId}）</span>
          <span style={mutedTextStyle}>{formatDisplayTime(record.voCreateTime)}</span>
        </div>
      ),
    },
  ];
}

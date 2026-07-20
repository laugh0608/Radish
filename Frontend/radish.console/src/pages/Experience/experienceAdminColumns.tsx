import {
  Button,
  Tag,
  type TableColumnsType,
} from '@radish/ui';
import type { TFunction } from 'i18next';
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
  formatStatWeekday,
  formatTransactionAmount,
  getGovernanceActionTagColor,
  getGovernanceReviewResultTagColor,
} from './experienceAdminHelpers';

interface DailyStatsColumnActions {
  t: TFunction;
  language?: string;
  canFreeze: boolean;
  dailyLimits?: UserExpDailyLimitSnapshotVo | null;
  onDayReview: (record: UserExpDailyStatsVo) => void;
  onDayGovernanceReview: (record: UserExpDailyStatsVo) => void;
  onDayFreezeReason: (record: UserExpDailyStatsVo) => void;
}

export function createLevelColumns(t: TFunction, language?: string): TableColumnsType<LevelConfigVo> {
  return [
    {
      title: t('experience.table.level'),
      key: 'level',
      width: 120,
      render: (_, record) => (
        <div>
          <div>L{record.voLevel}</div>
          <div className="experience-table-muted">{record.voLevelName}</div>
        </div>
      ),
    },
    {
      title: t('experience.table.cumulativeExp'),
      dataIndex: 'voExpCumulative',
      key: 'voExpCumulative',
      width: 140,
      render: (value: number) => formatLimitValue(value, 0, null, language),
    },
    {
      title: t('experience.table.requiredExp'),
      dataIndex: 'voExpRequired',
      key: 'voExpRequired',
      width: 140,
      render: (value: number) => formatLimitValue(value, 0, null, language),
    },
    {
      title: t('experience.table.status'),
      key: 'status',
      width: 100,
      render: (_, record) => <Tag color={record.voIsEnabled ? 'success' : 'default'}>{record.voIsEnabled ? t('experience.common.enabled') : t('experience.common.disabled')}</Tag>,
    },
    {
      title: t('experience.table.description'),
      key: 'description',
      render: (_, record) => record.voDescription || '-',
    },
  ];
}

export function createDailyStatsColumns({
  t,
  language,
  canFreeze,
  dailyLimits,
  onDayReview,
  onDayGovernanceReview,
  onDayFreezeReason,
}: DailyStatsColumnActions): TableColumnsType<UserExpDailyStatsVo> {
  return [
    {
      title: t('experience.table.date'),
      key: 'date',
      width: 120,
      render: (_, record) => (
        <div>
          <div>{formatStatDate(record.voStatDate, language)}</div>
          <div className="experience-table-muted">{formatStatWeekday(record.voStatDate, language)}</div>
        </div>
      ),
    },
    {
      title: t('experience.table.totalExp'),
      dataIndex: 'voExpEarned',
      key: 'voExpEarned',
      width: 100,
      render: (value: number) => (
        <div>
          <div>{formatLimitValue(value, 0, null, language)}</div>
          {dailyLimits?.voDailyLimitEnabled && (
            <div className="experience-table-muted">
              {t('experience.table.limit', { value: formatLimitValue(value, dailyLimits.voMaxDailyExp, dailyLimits, language) })}
            </div>
          )}
        </div>
      ),
    },
    {
      title: t('experience.table.sources'),
      key: 'sources',
      width: 360,
      render: (_, record) => (
        <div className="experience-table-inline">
          <span>{t('experience.source.post')} {formatLimitValue(record.voExpFromPost, dailyLimits?.voMaxExpFromPost ?? 0, dailyLimits, language)}</span>
          <span>{t('experience.source.comment')} {formatLimitValue(record.voExpFromComment, dailyLimits?.voMaxExpFromComment ?? 0, dailyLimits, language)}</span>
          <span>{t('experience.source.like')} {formatLimitValue(record.voExpFromLike, dailyLimits?.voMaxExpFromLike ?? 0, dailyLimits, language)}</span>
          <span>{t('experience.source.highlight')} {formatLimitValue(record.voExpFromHighlight, dailyLimits?.voMaxExpFromHighlight ?? 0, dailyLimits, language)}</span>
          <span>{t('experience.source.login')} {formatLimitValue(record.voExpFromLogin, dailyLimits?.voMaxExpFromLogin ?? 0, dailyLimits, language)}</span>
        </div>
      ),
    },
    {
      title: t('experience.table.behaviorCounts'),
      key: 'counts',
      width: 320,
      render: (_, record) => (
        <div className="experience-table-inline">
          <span>{t('experience.source.post')} {formatLimitValue(record.voPostCount, 0, null, language)}</span>
          <span>{t('experience.source.comment')} {formatLimitValue(record.voCommentCount, 0, null, language)}</span>
          <span>{t('experience.source.like')} {formatLimitValue(record.voLikeGivenCount, 0, null, language)}</span>
          <span>{t('experience.source.receivedLike')} {formatLimitValue(record.voLikeReceivedCount, 0, null, language)}</span>
        </div>
      ),
    },
    {
      title: t('experience.table.observations'),
      key: 'observations',
      width: 360,
      render: (_, record) => {
        const observations = record.voObservations ?? [];
        const contextObservations = observations.filter((observation) => observation.voKind !== 'anomaly');
        const anomalyObservations = observations.filter((observation) => observation.voKind === 'anomaly');
        return (
          <div className="experience-table-observation">
            <div>
              <div className="experience-table-observation__label">{t('experience.table.sourceStatus')}</div>
              <div className="experience-table-inline experience-table-inline--compact">
                {contextObservations.length > 0 ? contextObservations.map((observation) => (
                  <Tag key={observation.voRuleCode} color={observation.voTone} title={observation.voDescription ?? undefined}>
                    {observation.voLabel}
                  </Tag>
                )) : <span className="experience-table-muted">-</span>}
              </div>
            </div>
            <div>
              <div className="experience-table-observation__label">{t('experience.table.anomaly')}</div>
              <div className="experience-table-inline experience-table-inline--compact">
                {anomalyObservations.length > 0 ? anomalyObservations.map((observation) => (
                  <Tag key={observation.voRuleCode} color={observation.voTone} title={observation.voDescription ?? undefined}>
                    {observation.voLabel}
                  </Tag>
                )) : <span className="experience-table-muted">{t('experience.common.noHit')}</span>}
              </div>
            </div>
          </div>
        );
      },
    },
    {
      title: t('experience.table.reviewActions'),
      key: 'reviewActions',
      width: 320,
      render: (_, record) => {
        const hasAnomaly = (record.voObservations ?? []).some((observation) => observation.voKind === 'anomaly');
        if (!hasAnomaly) {
          return <span className="experience-table-muted">-</span>;
        }

        return (
          <div className="experience-table-inline experience-table-inline--compact">
            <Button onClick={() => onDayReview(record)}>
              {t('experience.actions.viewTransactions')}
            </Button>
            <Button
              variant="secondary"
              disabled={!canFreeze}
              onClick={() => onDayGovernanceReview(record)}
            >
              {t('experience.actions.prefillReview')}
            </Button>
            <Button
              variant="secondary"
              disabled={!canFreeze}
              onClick={() => onDayFreezeReason(record)}
            >
              {t('experience.actions.prefillFreeze')}
            </Button>
          </div>
        );
      },
    },
  ];
}

export function createTransactionColumns(t: TFunction, language?: string): TableColumnsType<ExpTransactionVo> {
  return [
    {
      title: t('experience.table.time'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (value: string) => formatDisplayTime(value, language),
    },
    {
      title: t('experience.table.type'),
      key: 'type',
      width: 140,
      render: (_, record) => (
        <div>
          <div>{t(`experience.expType.${record.voExpType}`, { defaultValue: record.voExpTypeDisplay })}</div>
          <div className="experience-table-muted">{record.voExpType}</div>
        </div>
      ),
    },
    {
      title: t('experience.table.changeAmount'),
      dataIndex: 'voExpAmount',
      key: 'voExpAmount',
      width: 110,
      render: (value: number) => (
        <span className={value >= 0 ? 'experience-transaction-amount experience-transaction-amount--positive' : 'experience-transaction-amount experience-transaction-amount--negative'}>
          {formatTransactionAmount(value, language)}
        </span>
      ),
    },
    {
      title: t('experience.table.expChange'),
      key: 'expRange',
      width: 180,
      render: (_, record) => `${record.voExpBefore} -> ${record.voExpAfter}`,
    },
    {
      title: t('experience.table.levelChange'),
      key: 'levelRange',
      width: 170,
      render: (_, record) => (
        <div className="experience-table-inline experience-table-inline--compact experience-table-inline--center">
          <span>{`L${record.voLevelBefore} -> L${record.voLevelAfter}`}</span>
          {record.voLevelAfter > record.voLevelBefore ? <Tag color="success">{t('experience.common.levelUp')}</Tag> : null}
        </div>
      ),
    },
    {
      title: t('experience.table.operator'),
      key: 'operator',
      width: 180,
      render: (_, record) => (
        <div>
          <div>{record.voOperatorName || 'System'}</div>
          <div className="experience-table-muted">ID: {record.voOperatorId}</div>
        </div>
      ),
    },
    {
      title: t('experience.table.remark'),
      dataIndex: 'voRemark',
      key: 'voRemark',
      render: (value?: string | null) => value || '-',
    },
  ];
}

export function createGovernanceActionColumns(t: TFunction, language?: string): TableColumnsType<UserExperienceGovernanceActionVo> {
  return [
    {
      title: t('experience.table.action'),
      key: 'actionType',
      width: 200,
      render: (_, record) => (
        <div className="experience-table-stack">
          <div>
            <Tag color={getGovernanceActionTagColor(record.voActionType)}>
              {t(`experience.actionType.${record.voActionType}`, { defaultValue: record.voActionTypeDisplay })}
            </Tag>
          </div>
          {record.voActionType === 'Freeze' && (
            <span className="experience-table-muted">
              {t('experience.table.expiresAt', { value: record.voFrozenUntil ? formatDisplayTime(record.voFrozenUntil, language) : t('experience.common.permanentFreeze') })}
            </span>
          )}
        </div>
      ),
    },
    {
      title: t('experience.table.reviewResult'),
      key: 'reviewResult',
      width: 180,
      render: (_, record) => record.voReviewResultDisplay ? (
        <Tag color={getGovernanceReviewResultTagColor(record.voReviewResult)}>
          {t(`experience.reviewResult.${record.voReviewResult}`, { defaultValue: record.voReviewResultDisplay })}
        </Tag>
      ) : <span className="experience-table-muted">-</span>,
    },
    {
      title: t('experience.table.evidence'),
      key: 'evidence',
      width: 360,
      render: (_, record) => (
        <div className="experience-table-stack">
          <span>{record.voEvidenceSummary || '-'}</span>
          {record.voRecommendationReason && (
            <span className="experience-table-muted">
              {t('experience.table.recommendationReason', { value: record.voRecommendationReason })}
            </span>
          )}
        </div>
      ),
    },
    {
      title: t('experience.table.remark'),
      dataIndex: 'voRemark',
      key: 'voRemark',
      width: 320,
      render: (value: string) => (
        <div className="experience-table-prewrap">
          {value}
        </div>
      ),
    },
    {
      title: t('experience.table.operatorTime'),
      key: 'operator',
      width: 220,
      render: (_, record) => (
        <div className="experience-table-stack">
          <span>{record.voOperatorName || 'System'} ({t('experience.common.id')}: {record.voOperatorId})</span>
          <span className="experience-table-muted">{formatDisplayTime(record.voCreateTime, language)}</span>
        </div>
      ),
    },
  ];
}

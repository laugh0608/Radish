import { Button, Table, Tag } from '@radish/ui';
import {
  type UserExpAnomalyRuleSummaryVo,
  type UserExpDailyLimitSnapshotVo,
  type UserExpDailyStatsSummaryVo,
  type UserExpDailyStatsVo,
  type UserExpGovernanceRecommendationVo,
} from '@/api/experienceAdminApi';
import { createDailyStatsColumns } from './experienceAdminColumns';
import {
  formatFullStatDate,
  getRecommendationTagColor,
  getRuleSeverityLabel,
  getRuleSeverityTagColor,
  type StatsWindowDays,
} from './experienceAdminHelpers';

type ExperienceObservationSummaryProps = {
  loadedUserId: string | null;
  statsWindowDays: StatsWindowDays;
  loadingDailyStats: boolean;
  canFreeze: boolean;
  dailyStats: UserExpDailyStatsVo[];
  dailyStatsSummary: UserExpDailyStatsSummaryVo | null;
  anomalyRuleSummaries: UserExpAnomalyRuleSummaryVo[];
  governanceRecommendation: UserExpGovernanceRecommendationVo | null;
  dailyLimits: UserExpDailyLimitSnapshotVo | null;
  onStatsWindowDaysChange: (days: StatsWindowDays) => void;
  onRecommendationGovernanceReview: (recommendation: UserExpGovernanceRecommendationVo) => void;
  onRecommendationFreezeReason: (reason: string) => void;
  onRuleReview: (rule: UserExpAnomalyRuleSummaryVo) => void;
  onRuleGovernanceReview: (rule: UserExpAnomalyRuleSummaryVo) => void;
  onRuleFreezeReason: (rule: UserExpAnomalyRuleSummaryVo) => void;
  onDayReview: (record: UserExpDailyStatsVo) => void;
  onDayGovernanceReview: (record: UserExpDailyStatsVo) => void;
  onDayFreezeReason: (record: UserExpDailyStatsVo) => void;
};

export const ExperienceObservationSummary = ({
  loadedUserId,
  statsWindowDays,
  loadingDailyStats,
  canFreeze,
  dailyStats,
  dailyStatsSummary,
  anomalyRuleSummaries,
  governanceRecommendation,
  dailyLimits,
  onStatsWindowDaysChange,
  onRecommendationGovernanceReview,
  onRecommendationFreezeReason,
  onRuleReview,
  onRuleGovernanceReview,
  onRuleFreezeReason,
  onDayReview,
  onDayGovernanceReview,
  onDayFreezeReason,
}: ExperienceObservationSummaryProps) => {
  const dailyStatsColumns = createDailyStatsColumns({
    canFreeze,
    dailyLimits,
    onDayReview,
    onDayGovernanceReview,
    onDayFreezeReason,
  });

  return (
    <section className="admin-feature-card">
      <div className="admin-feature-header">
        <div>
          <h3>经验统计观察</h3>
          <p className="admin-feature-subtle">查看最近 7 / 30 天经验来源与异常判定，仅服务人工复核和冻结建议，不自动处罚。</p>
        </div>
        <div className="experience-inline-actions">
          <Button
            variant={statsWindowDays === 7 ? 'primary' : 'secondary'}
            disabled={!loadedUserId || loadingDailyStats}
            onClick={() => onStatsWindowDaysChange(7)}
          >
            最近 7 天
          </Button>
          <Button
            variant={statsWindowDays === 30 ? 'primary' : 'secondary'}
            disabled={!loadedUserId || loadingDailyStats}
            onClick={() => onStatsWindowDaysChange(30)}
          >
            最近 30 天
          </Button>
        </div>
      </div>

      {loadedUserId ? (
        <>
          <div className="admin-feature-metrics experience-section-gap-md">
            <div className="admin-feature-metric">
              <span>窗口总经验</span>
              <strong>{dailyStatsSummary ? dailyStatsSummary.voTotalExp : '--'}</strong>
            </div>
            <div className="admin-feature-metric">
              <span>日均经验</span>
              <strong>{dailyStatsSummary ? dailyStatsSummary.voAverageExp.toFixed(1) : '--'}</strong>
            </div>
            <div className="admin-feature-metric">
              <span>峰值单日</span>
              <strong>{dailyStatsSummary ? dailyStatsSummary.voPeakDayExp : '--'}</strong>
              <div className="experience-metric-note">
                {dailyStatsSummary?.voPeakStatDate
                  ? formatFullStatDate(dailyStatsSummary.voPeakStatDate)
                  : '--'}
              </div>
            </div>
            <div className="admin-feature-metric">
              <span>零增长天数</span>
              <strong>{dailyStatsSummary ? dailyStatsSummary.voZeroGainDays : '--'}</strong>
            </div>
            <div className="admin-feature-metric">
              <span>异常命中天数</span>
              <strong>{dailyStatsSummary ? dailyStatsSummary.voReviewDays : '--'}</strong>
            </div>
          </div>

          {dailyLimits && (
            <div className="admin-feature-banner experience-section-gap-sm">
              <div className="experience-inline-actions experience-inline-actions--center">
                <strong>当前经验阈值</strong>
                <Tag color={dailyLimits.voDailyLimitEnabled ? 'success' : 'default'}>
                  {dailyLimits.voDailyLimitEnabled ? '每日上限启用中' : '每日上限已停用'}
                </Tag>
              </div>
              <div className="experience-banner-list">
                <span>总经验 {dailyLimits.voMaxDailyExp}</span>
                <span>发帖 {dailyLimits.voMaxExpFromPost}</span>
                <span>评论 {dailyLimits.voMaxExpFromComment}</span>
                <span>点赞 {dailyLimits.voMaxExpFromLike}</span>
                <span>高亮 {dailyLimits.voMaxExpFromHighlight}</span>
                <span>登录 {dailyLimits.voMaxExpFromLogin}</span>
              </div>
            </div>
          )}

          {governanceRecommendation && (
            <div className="admin-feature-banner experience-section-gap-sm">
              <div className="experience-inline-actions experience-inline-actions--center">
                <strong>当前治理建议</strong>
                <Tag color={getRecommendationTagColor(governanceRecommendation.voLevel)}>
                  {governanceRecommendation.voTitle}
                </Tag>
              </div>
              <div className="experience-section-gap-xs">{governanceRecommendation.voReason}</div>
              <div className="experience-review-draft-reason">
                建议动作：{governanceRecommendation.voSuggestedAction}
              </div>
              <div className="experience-inline-actions experience-inline-actions--compact experience-section-gap-xs">
                <Button
                  variant="secondary"
                  disabled={!canFreeze}
                  onClick={() => onRecommendationGovernanceReview(governanceRecommendation)}
                >
                  带入复核结论
                </Button>
                {governanceRecommendation.voLevel !== 'normal' && (
                  <Button
                    variant="secondary"
                    disabled={!canFreeze}
                    onClick={() => onRecommendationFreezeReason(`经验异常待复核：${governanceRecommendation.voReason}`)}
                  >
                    带入冻结原因
                  </Button>
                )}
              </div>
            </div>
          )}

          {anomalyRuleSummaries.length > 0 && (
            <div className="experience-rule-summary">
              <div className="experience-rule-summary__title">异常规则摘要</div>
              <div className="experience-rule-grid">
                {anomalyRuleSummaries.map((rule) => (
                  <div
                    key={rule.voRuleCode}
                    className="experience-rule-card"
                  >
                    <div className="experience-rule-card__head">
                      <strong>{rule.voRuleLabel}</strong>
                      <Tag color={getRuleSeverityTagColor(rule.voSeverity)}>
                        {getRuleSeverityLabel(rule.voSeverity)}
                      </Tag>
                    </div>
                    <div className="experience-rule-card__description">{rule.voThresholdDescription}</div>
                    <div className="experience-rule-card__facts">
                      <span>窗口命中：{rule.voHitDays} 天</span>
                      <span>最强信号：{rule.voStrongestSignal}</span>
                      <span>最近命中：{rule.voLatestHitDate ? formatFullStatDate(rule.voLatestHitDate) : '-'}</span>
                    </div>
                    <div className="experience-rule-card__recommendation">
                      建议动作：{rule.voSuggestedAction}
                    </div>
                    <div className="experience-inline-actions experience-inline-actions--compact experience-section-gap-xs">
                      <Button onClick={() => onRuleReview(rule)}>
                        查看流水
                      </Button>
                      <Button
                        variant="secondary"
                        disabled={!canFreeze}
                        onClick={() => onRuleGovernanceReview(rule)}
                      >
                        带入复核结论
                      </Button>
                      <Button
                        variant="secondary"
                        disabled={!canFreeze}
                        onClick={() => onRuleFreezeReason(rule)}
                      >
                        带入冻结原因
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {dailyStatsSummary && (
            <div className="admin-feature-banner experience-section-gap-sm">
              {dailyStatsSummary.voNotices.map((notice) => (
                <div key={notice}>{notice}</div>
              ))}
            </div>
          )}

          <Table<UserExpDailyStatsVo>
            rowKey={(record) => `${record.voUserId}-${record.voStatDate}`}
            columns={dailyStatsColumns}
            dataSource={dailyStats}
            loading={loadingDailyStats}
            pagination={false}
            scroll={{ x: 1520 }}
            className="experience-table-spaced"
            locale={{
              emptyText: loadingDailyStats
                ? '统计加载中...'
                : `最近 ${statsWindowDays} 天暂无经验统计记录`,
            }}
          />
        </>
      ) : (
        <div className="experience-empty-hint">
          请先查询用户经验，再查看最近 {statsWindowDays} 天的统计观察。
        </div>
      )}
    </section>
  );
};

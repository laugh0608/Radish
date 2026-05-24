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
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
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
          <div className="admin-feature-metrics" style={{ marginTop: 20 }}>
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
              <div style={{ marginTop: 6, color: '#8c8c8c' }}>
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
            <div className="admin-feature-banner" style={{ marginTop: 16 }}>
              <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
                <strong>当前经验阈值</strong>
                <Tag color={dailyLimits.voDailyLimitEnabled ? 'success' : 'default'}>
                  {dailyLimits.voDailyLimitEnabled ? '每日上限启用中' : '每日上限已停用'}
                </Tag>
              </div>
              <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginTop: 12 }}>
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
            <div className="admin-feature-banner" style={{ marginTop: 16 }}>
              <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
                <strong>当前治理建议</strong>
                <Tag color={getRecommendationTagColor(governanceRecommendation.voLevel)}>
                  {governanceRecommendation.voTitle}
                </Tag>
              </div>
              <div style={{ marginTop: 12 }}>{governanceRecommendation.voReason}</div>
              <div style={{ marginTop: 8, color: '#8c8c8c' }}>
                建议动作：{governanceRecommendation.voSuggestedAction}
              </div>
              <div style={{ marginTop: 12, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
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
            <div style={{ marginTop: 16 }}>
              <div style={{ marginBottom: 12, fontWeight: 600 }}>异常规则摘要</div>
              <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
                gap: 12,
              }}>
                {anomalyRuleSummaries.map((rule) => (
                  <div
                    key={rule.voRuleCode}
                    style={{
                      border: '1px solid rgba(5, 5, 5, 0.08)',
                      borderRadius: 12,
                      padding: 16,
                      background: '#fff',
                    }}
                  >
                    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                      <strong>{rule.voRuleLabel}</strong>
                      <Tag color={getRuleSeverityTagColor(rule.voSeverity)}>
                        {getRuleSeverityLabel(rule.voSeverity)}
                      </Tag>
                    </div>
                    <div style={{ marginTop: 10, color: '#595959' }}>{rule.voThresholdDescription}</div>
                    <div style={{ marginTop: 10, display: 'grid', gap: 6 }}>
                      <span>窗口命中：{rule.voHitDays} 天</span>
                      <span>最强信号：{rule.voStrongestSignal}</span>
                      <span>最近命中：{rule.voLatestHitDate ? formatFullStatDate(rule.voLatestHitDate) : '-'}</span>
                    </div>
                    <div style={{ marginTop: 10, color: '#8c8c8c' }}>
                      建议动作：{rule.voSuggestedAction}
                    </div>
                    <div style={{ marginTop: 12, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
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
            <div className="admin-feature-banner" style={{ marginTop: 16 }}>
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
            style={{ marginTop: 20 }}
            locale={{
              emptyText: loadingDailyStats
                ? '统计加载中...'
                : `最近 ${statsWindowDays} 天暂无经验统计记录`,
            }}
          />
        </>
      ) : (
        <div style={{ marginTop: 20, color: '#8c8c8c' }}>
          请先查询用户经验，再查看最近 {statsWindowDays} 天的统计观察。
        </div>
      )}
    </section>
  );
};

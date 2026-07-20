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
import { useTranslation } from 'react-i18next';
import { formatConsoleInteger, formatConsoleNumber } from '@/utils/localeFormatters';

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
  const { t, i18n } = useTranslation();
  const dailyStatsColumns = createDailyStatsColumns({
    t,
    language: i18n.resolvedLanguage,
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
          <h3>{t('experience.observation.title')}</h3>
          <p className="admin-feature-subtle">{t('experience.observation.description')}</p>
        </div>
        <div className="experience-inline-actions">
          <Button
            variant={statsWindowDays === 7 ? 'primary' : 'secondary'}
            disabled={!loadedUserId || loadingDailyStats}
            onClick={() => onStatsWindowDaysChange(7)}
          >
            {t('experience.observation.window', { count: 7 })}
          </Button>
          <Button
            variant={statsWindowDays === 30 ? 'primary' : 'secondary'}
            disabled={!loadedUserId || loadingDailyStats}
            onClick={() => onStatsWindowDaysChange(30)}
          >
            {t('experience.observation.window', { count: 30 })}
          </Button>
        </div>
      </div>

      {loadedUserId ? (
        <>
          <div className="admin-feature-metrics experience-section-gap-md">
            <div className="admin-feature-metric">
              <span>{t('experience.observation.totalExp')}</span>
              <strong>{dailyStatsSummary ? formatConsoleInteger(dailyStatsSummary.voTotalExp, i18n.resolvedLanguage) : '--'}</strong>
            </div>
            <div className="admin-feature-metric">
              <span>{t('experience.observation.averageExp')}</span>
              <strong>{dailyStatsSummary ? formatConsoleNumber(dailyStatsSummary.voAverageExp, i18n.resolvedLanguage) : '--'}</strong>
            </div>
            <div className="admin-feature-metric">
              <span>{t('experience.observation.peakDay')}</span>
              <strong>{dailyStatsSummary ? formatConsoleInteger(dailyStatsSummary.voPeakDayExp, i18n.resolvedLanguage) : '--'}</strong>
              <div className="experience-metric-note">
                {dailyStatsSummary?.voPeakStatDate
                  ? formatFullStatDate(dailyStatsSummary.voPeakStatDate, i18n.resolvedLanguage)
                  : '--'}
              </div>
            </div>
            <div className="admin-feature-metric">
              <span>{t('experience.observation.zeroDays')}</span>
              <strong>{dailyStatsSummary ? formatConsoleInteger(dailyStatsSummary.voZeroGainDays, i18n.resolvedLanguage) : '--'}</strong>
            </div>
            <div className="admin-feature-metric">
              <span>{t('experience.observation.reviewDays')}</span>
              <strong>{dailyStatsSummary ? formatConsoleInteger(dailyStatsSummary.voReviewDays, i18n.resolvedLanguage) : '--'}</strong>
            </div>
          </div>

          {dailyLimits && (
            <div className="admin-feature-banner experience-section-gap-sm">
              <div className="experience-inline-actions experience-inline-actions--center">
                <strong>{t('experience.observation.thresholds')}</strong>
                <Tag color={dailyLimits.voDailyLimitEnabled ? 'success' : 'default'}>
                  {dailyLimits.voDailyLimitEnabled ? t('experience.observation.limitEnabled') : t('experience.observation.limitDisabled')}
                </Tag>
              </div>
              <div className="experience-banner-list">
                <span>{t('experience.table.totalExp')} {formatConsoleInteger(dailyLimits.voMaxDailyExp, i18n.resolvedLanguage)}</span>
                <span>{t('experience.source.post')} {formatConsoleInteger(dailyLimits.voMaxExpFromPost, i18n.resolvedLanguage)}</span>
                <span>{t('experience.source.comment')} {formatConsoleInteger(dailyLimits.voMaxExpFromComment, i18n.resolvedLanguage)}</span>
                <span>{t('experience.source.like')} {formatConsoleInteger(dailyLimits.voMaxExpFromLike, i18n.resolvedLanguage)}</span>
                <span>{t('experience.source.highlight')} {formatConsoleInteger(dailyLimits.voMaxExpFromHighlight, i18n.resolvedLanguage)}</span>
                <span>{t('experience.source.login')} {formatConsoleInteger(dailyLimits.voMaxExpFromLogin, i18n.resolvedLanguage)}</span>
              </div>
            </div>
          )}

          {governanceRecommendation && (
            <div className="admin-feature-banner experience-section-gap-sm">
              <div className="experience-inline-actions experience-inline-actions--center">
                <strong>{t('experience.observation.recommendation')}</strong>
                <Tag color={getRecommendationTagColor(governanceRecommendation.voLevel)}>
                  {governanceRecommendation.voTitle}
                </Tag>
              </div>
              <div className="experience-section-gap-xs">{governanceRecommendation.voReason}</div>
              <div className="experience-review-draft-reason">
                {t('experience.observation.suggestedAction', { value: governanceRecommendation.voSuggestedAction })}
              </div>
              <div className="experience-inline-actions experience-inline-actions--compact experience-section-gap-xs">
                <Button
                  variant="secondary"
                  disabled={!canFreeze}
                  onClick={() => onRecommendationGovernanceReview(governanceRecommendation)}
                >
                  {t('experience.actions.prefillReview')}
                </Button>
                {governanceRecommendation.voLevel !== 'normal' && (
                  <Button
                    variant="secondary"
                    disabled={!canFreeze}
                    onClick={() => onRecommendationFreezeReason(`经验异常待复核：${governanceRecommendation.voReason}`)}
                  >
                    {t('experience.actions.prefillFreeze')}
                  </Button>
                )}
              </div>
            </div>
          )}

          {anomalyRuleSummaries.length > 0 && (
            <div className="experience-rule-summary">
              <div className="experience-rule-summary__title">{t('experience.observation.ruleSummary')}</div>
              <div className="experience-rule-grid">
                {anomalyRuleSummaries.map((rule) => (
                  <div
                    key={rule.voRuleCode}
                    className="experience-rule-card"
                  >
                    <div className="experience-rule-card__head">
                      <strong>{rule.voRuleLabel}</strong>
                      <Tag color={getRuleSeverityTagColor(rule.voSeverity)}>
                        {getRuleSeverityLabel(t, rule.voSeverity)}
                      </Tag>
                    </div>
                    <div className="experience-rule-card__description">{rule.voThresholdDescription}</div>
                    <div className="experience-rule-card__facts">
                      <span>{t('experience.observation.hitDays', { count: rule.voHitDays })}</span>
                      <span>{t('experience.observation.strongestSignal', { value: rule.voStrongestSignal })}</span>
                      <span>{t('experience.observation.latestHit', { value: rule.voLatestHitDate ? formatFullStatDate(rule.voLatestHitDate, i18n.resolvedLanguage) : '-' })}</span>
                    </div>
                    <div className="experience-rule-card__recommendation">
                      {t('experience.observation.suggestedAction', { value: rule.voSuggestedAction })}
                    </div>
                    <div className="experience-inline-actions experience-inline-actions--compact experience-section-gap-xs">
                      <Button onClick={() => onRuleReview(rule)}>
                        {t('experience.actions.viewTransactions')}
                      </Button>
                      <Button
                        variant="secondary"
                        disabled={!canFreeze}
                        onClick={() => onRuleGovernanceReview(rule)}
                      >
                        {t('experience.actions.prefillReview')}
                      </Button>
                      <Button
                        variant="secondary"
                        disabled={!canFreeze}
                        onClick={() => onRuleFreezeReason(rule)}
                      >
                        {t('experience.actions.prefillFreeze')}
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
                ? t('experience.observation.loading')
                : t('experience.observation.empty', { count: statsWindowDays }),
            }}
          />
        </>
      ) : (
        <div className="experience-empty-hint">
          {t('experience.observation.queryFirst', { count: statsWindowDays })}
        </div>
      )}
    </section>
  );
};

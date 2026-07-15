import { useTranslation } from 'react-i18next';
import {
  addCoinValues,
  compareCoinValues,
  divideCoinValue,
  formatCoinAmount,
  formatCoinNumber,
  formatCoinRatio,
  formatStatisticsCategory,
  subtractCoinValues,
  toCoinChartNumber,
} from '../../utils';
import type { StatisticsData } from '../../types';
import styles from './StatisticsOverview.module.css';

interface StatisticsOverviewProps {
  data: StatisticsData | null;
  loading: boolean;
  error: string | null;
  displayMode: 'carrot' | 'white';
  timeRange: 'month' | 'quarter' | 'year';
}

/**
 * 统计概览组件
 */
export const StatisticsOverview = ({
  data,
  loading,
  error,
  displayMode,
  timeRange
}: StatisticsOverviewProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.statistics.loading')}</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <h3>{t('pit.common.loadFailed')}</h3>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  if (!data) {
    return (
      <div className={styles.container}>
        <div className={styles.empty}>
          <div className={styles.emptyIcon}>📊</div>
          <h3>{t('pit.statistics.empty')}</h3>
          <p>{t('pit.statistics.emptyDescription')}</p>
        </div>
      </div>
    );
  }

  // 从趋势数据中计算收入和支出总额
  const trendData = data.voTrendData || [];
  const totalIncome = addCoinValues(...trendData.map((item) => item.voIncome));
  const totalExpense = addCoinValues(...trendData.map((item) => item.voExpense));
  const netProfit = subtractCoinValues(totalIncome, totalExpense);
  const netProfitIsPositive = compareCoinValues(netProfit, 0) >= 0;

  // 分类统计数据
  const categoryStats = data.voCategoryStats || [];

  const timeRangeText = t(`pit.statistics.period.${timeRange}`);

  return (
    <div className={styles.container}>
      {/* 核心指标卡片 */}
      <div className={styles.metricsCards}>
        <div className={`${styles.metricCard} ${styles.income}`}>
          <div className={styles.metricIcon}>📈</div>
          <div className={styles.metricContent}>
            <div className={styles.metricLabel}>{t('pit.statistics.periodIncome', { range: timeRangeText })}</div>
            <div className={styles.metricValue}>
              {formatCoinAmount(totalIncome, language, t, displayMode)}
            </div>
            <div className={styles.metricChange}>
              <span className={styles.changeText}>{t('pit.statistics.currentPeriod')}</span>
            </div>
          </div>
        </div>

        <div className={`${styles.metricCard} ${styles.expense}`}>
          <div className={styles.metricIcon}>📉</div>
          <div className={styles.metricContent}>
            <div className={styles.metricLabel}>{t('pit.statistics.periodExpense', { range: timeRangeText })}</div>
            <div className={styles.metricValue}>
              {formatCoinAmount(totalExpense, language, t, displayMode)}
            </div>
            <div className={styles.metricChange}>
              <span className={styles.changeText}>{t('pit.statistics.currentPeriod')}</span>
            </div>
          </div>
        </div>

        <div className={`${styles.metricCard} ${styles.profit} ${netProfitIsPositive ? styles.positive : styles.negative}`}>
          <div className={styles.metricIcon}>{netProfitIsPositive ? '💰' : '⚠️'}</div>
          <div className={styles.metricContent}>
            <div className={styles.metricLabel}>{t('pit.statistics.periodNet', { range: timeRangeText })}</div>
            <div className={styles.metricValue}>
              {netProfitIsPositive ? '+' : ''}
              {formatCoinAmount(netProfit, language, t, displayMode)}
            </div>
            <div className={styles.metricChange}>
              <span className={styles.changeText}>{t('pit.statistics.currentPeriod')}</span>
            </div>
          </div>
        </div>
      </div>

      {/* 快速统计 */}
      <div className={styles.quickStats}>
        <h4 className={styles.quickStatsTitle}>{t('pit.statistics.quick')}</h4>
        <div className={styles.quickStatsGrid}>
          <div className={styles.quickStatItem}>
            <div className={styles.quickStatIcon}>🎯</div>
            <div className={styles.quickStatContent}>
              <div className={styles.quickStatValue}>
                {formatCoinNumber(categoryStats.reduce((sum, cat) => sum + cat.voCount, 0), language)}
              </div>
              <div className={styles.quickStatLabel}>{t('pit.statistics.totalTransactions')}</div>
            </div>
          </div>

          <div className={styles.quickStatItem}>
            <div className={styles.quickStatIcon}>📅</div>
            <div className={styles.quickStatContent}>
              <div className={styles.quickStatValue}>
                {formatCoinAmount(
                  trendData.length > 0 ? divideCoinValue(totalIncome, trendData.length) : 0,
                  language,
                  t,
                  displayMode,
                  false,
                )}
              </div>
              <div className={styles.quickStatLabel}>{t('pit.statistics.dailyAverage')}</div>
            </div>
          </div>

          <div className={styles.quickStatItem}>
            <div className={styles.quickStatIcon}>🏆</div>
            <div className={styles.quickStatContent}>
              <div className={styles.quickStatValue}>
                {categoryStats.length > 0
                  ? formatStatisticsCategory(categoryStats[0].voCategory, t)
                  : t('pit.common.none')}
              </div>
              <div className={styles.quickStatLabel}>{t('pit.statistics.mainSource')}</div>
            </div>
          </div>

          <div className={styles.quickStatItem}>
            <div className={styles.quickStatIcon}>💡</div>
            <div className={styles.quickStatContent}>
              <div className={styles.quickStatValue}>
                {formatCoinRatio(
                  compareCoinValues(totalExpense, 0) > 0
                    ? toCoinChartNumber(totalIncome) / toCoinChartNumber(totalExpense)
                    : 0,
                  language,
                )}
              </div>
              <div className={styles.quickStatLabel}>{t('pit.statistics.ratio')}</div>
            </div>
          </div>
        </div>
      </div>

      {/* 分类排行 */}
      <div className={styles.categoryRanking}>
        <h4 className={styles.categoryTitle}>{t('pit.statistics.categoryRanking')}</h4>
        <div className={styles.categoryList}>
          {categoryStats.slice(0, 5).map((category, index) => (
            <div key={category.voCategory} className={styles.categoryItem}>
              <div className={styles.categoryRank}>#{index + 1}</div>
              <div className={styles.categoryInfo}>
                <div className={styles.categoryName}>{formatStatisticsCategory(category.voCategory, t)}</div>
                <div className={styles.categoryCount}>
                  {t('pit.statistics.transactionCount', {
                    count: category.voCount,
                    value: formatCoinNumber(category.voCount, language),
                  })}
                </div>
              </div>
              <div className={styles.categoryAmount}>
                {formatCoinAmount(category.voAmount, language, t, displayMode)}
              </div>
              <div className={styles.categoryProgress}>
                <div
                  className={styles.categoryProgressBar}
                  style={{
                    width: `${categoryStats[0] && compareCoinValues(categoryStats[0].voAmount, 0) > 0
                      ? (toCoinChartNumber(category.voAmount) / toCoinChartNumber(categoryStats[0].voAmount)) * 100
                      : 0}%`
                  }}
                ></div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

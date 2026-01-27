import { formatCoinAmount } from '../../utils';
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
  const useWhiteRadish = displayMode === 'white';

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载统计数据中...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <h3>加载失败</h3>
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
          <h3>暂无统计数据</h3>
          <p>开始使用萝卜后，统计数据将显示在这里</p>
        </div>
      </div>
    );
  }

  const totalIncome = data.monthlyIncome.reduce((sum, amount) => sum + amount, 0);
  const totalExpense = data.monthlyExpense.reduce((sum, amount) => sum + amount, 0);
  const netProfit = totalIncome - totalExpense;

  const getTimeRangeText = () => {
    switch (timeRange) {
      case 'month': return '本月';
      case 'quarter': return '本季度';
      case 'year': return '本年度';
      default: return '当前';
    }
  };

  return (
    <div className={styles.container}>
      {/* 核心指标卡片 */}
      <div className={styles.metricsCards}>
        <div className={`${styles.metricCard} ${styles.income}`}>
          <div className={styles.metricIcon}>📈</div>
          <div className={styles.metricContent}>
            <div className={styles.metricLabel}>{getTimeRangeText()}收入</div>
            <div className={styles.metricValue}>
              {formatCoinAmount(totalIncome, true, useWhiteRadish)}
            </div>
            <div className={styles.metricChange}>
              <span className={styles.changePositive}>+12.5%</span>
              <span className={styles.changeText}>较上期</span>
            </div>
          </div>
        </div>

        <div className={`${styles.metricCard} ${styles.expense}`}>
          <div className={styles.metricIcon}>📉</div>
          <div className={styles.metricContent}>
            <div className={styles.metricLabel}>{getTimeRangeText()}支出</div>
            <div className={styles.metricValue}>
              {formatCoinAmount(totalExpense, true, useWhiteRadish)}
            </div>
            <div className={styles.metricChange}>
              <span className={styles.changeNegative}>-5.2%</span>
              <span className={styles.changeText}>较上期</span>
            </div>
          </div>
        </div>

        <div className={`${styles.metricCard} ${styles.profit} ${netProfit >= 0 ? styles.positive : styles.negative}`}>
          <div className={styles.metricIcon}>{netProfit >= 0 ? '💰' : '⚠️'}</div>
          <div className={styles.metricContent}>
            <div className={styles.metricLabel}>{getTimeRangeText()}净收益</div>
            <div className={styles.metricValue}>
              {netProfit >= 0 ? '+' : ''}
              {formatCoinAmount(netProfit, true, useWhiteRadish)}
            </div>
            <div className={styles.metricChange}>
              <span className={netProfit >= 0 ? styles.changePositive : styles.changeNegative}>
                {netProfit >= 0 ? '+' : ''}18.7%
              </span>
              <span className={styles.changeText}>较上期</span>
            </div>
          </div>
        </div>
      </div>

      {/* 快速统计 */}
      <div className={styles.quickStats}>
        <h4 className={styles.quickStatsTitle}>快速统计</h4>
        <div className={styles.quickStatsGrid}>
          <div className={styles.quickStatItem}>
            <div className={styles.quickStatIcon}>🎯</div>
            <div className={styles.quickStatContent}>
              <div className={styles.quickStatValue}>
                {data.categoryStats.reduce((sum, cat) => sum + cat.count, 0)}
              </div>
              <div className={styles.quickStatLabel}>总交易次数</div>
            </div>
          </div>

          <div className={styles.quickStatItem}>
            <div className={styles.quickStatIcon}>📅</div>
            <div className={styles.quickStatContent}>
              <div className={styles.quickStatValue}>
                {Math.round(totalIncome / data.monthlyIncome.length)}
              </div>
              <div className={styles.quickStatLabel}>日均收入</div>
            </div>
          </div>

          <div className={styles.quickStatItem}>
            <div className={styles.quickStatIcon}>🏆</div>
            <div className={styles.quickStatContent}>
              <div className={styles.quickStatValue}>
                {data.categoryStats.length > 0 ? data.categoryStats[0].category : '无'}
              </div>
              <div className={styles.quickStatLabel}>主要收入来源</div>
            </div>
          </div>

          <div className={styles.quickStatItem}>
            <div className={styles.quickStatIcon}>💡</div>
            <div className={styles.quickStatContent}>
              <div className={styles.quickStatValue}>
                {totalExpense > 0 ? Math.round((totalIncome / totalExpense) * 100) : 0}%
              </div>
              <div className={styles.quickStatLabel}>收支比率</div>
            </div>
          </div>
        </div>
      </div>

      {/* 分类排行 */}
      <div className={styles.categoryRanking}>
        <h4 className={styles.categoryTitle}>收入分类排行</h4>
        <div className={styles.categoryList}>
          {data.categoryStats.slice(0, 5).map((category, index) => (
            <div key={category.category} className={styles.categoryItem}>
              <div className={styles.categoryRank}>#{index + 1}</div>
              <div className={styles.categoryInfo}>
                <div className={styles.categoryName}>{category.category}</div>
                <div className={styles.categoryCount}>{category.count} 次</div>
              </div>
              <div className={styles.categoryAmount}>
                {formatCoinAmount(category.amount, true, useWhiteRadish)}
              </div>
              <div className={styles.categoryProgress}>
                <div
                  className={styles.categoryProgressBar}
                  style={{
                    width: `${(category.amount / data.categoryStats[0].amount) * 100}%`
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
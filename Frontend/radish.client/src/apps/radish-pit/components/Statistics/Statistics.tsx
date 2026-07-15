import { lazy, Suspense, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useStatistics } from '../../hooks';
import { StatisticsOverview } from './StatisticsOverview';
import styles from './Statistics.module.css';

const IncomeExpenseChart = lazy(() =>
  import('./IncomeExpenseChart').then((module) => ({ default: module.IncomeExpenseChart }))
);
const CategoryBreakdown = lazy(() =>
  import('./CategoryBreakdown').then((module) => ({ default: module.CategoryBreakdown }))
);
const TrendAnalysis = lazy(() =>
  import('./TrendAnalysis').then((module) => ({ default: module.TrendAnalysis }))
);

type StatisticsTab = 'overview' | 'chart' | 'category' | 'trend';
type TimeRange = 'month' | 'quarter' | 'year';

/**
 * 收支统计组件
 */
export const Statistics = () => {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState<StatisticsTab>('overview');
  const [timeRange, setTimeRange] = useState<TimeRange>('month');
  const [displayMode, setDisplayMode] = useState<'carrot' | 'white'>('carrot');

  const { data, loading, error, refetch } = useStatistics(timeRange);

  const handleTabChange = (tab: StatisticsTab) => {
    setActiveTab(tab);
  };

  const handleTimeRangeChange = (range: TimeRange) => {
    setTimeRange(range);
  };

  const toggleDisplayMode = () => {
    setDisplayMode(prev => prev === 'carrot' ? 'white' : 'carrot');
  };

  const renderTabContent = () => {
    switch (activeTab) {
      case 'overview':
        return (
          <StatisticsOverview
            data={data}
            loading={loading}
            error={error}
            displayMode={displayMode}
            timeRange={timeRange}
          />
        );
      case 'chart':
        return (
          <IncomeExpenseChart
            data={data}
            loading={loading}
            error={error}
            displayMode={displayMode}
            timeRange={timeRange}
          />
        );
      case 'category':
        return (
          <CategoryBreakdown
            data={data}
            loading={loading}
            error={error}
            displayMode={displayMode}
          />
        );
      case 'trend':
        return (
          <TrendAnalysis
            data={data}
            loading={loading}
            error={error}
            displayMode={displayMode}
            timeRange={timeRange}
          />
        );
      default:
        return null;
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>{t('pit.statistics.title')}</h2>
          <p className={styles.subtitle}>{t('pit.statistics.description')}</p>
        </div>
        <div className={styles.headerRight}>
          <div className={styles.timeRangeSelector}>
            <button
              className={`${styles.timeRangeButton} ${timeRange === 'month' ? styles.active : ''}`}
              onClick={() => handleTimeRangeChange('month')}
            >
              {t('pit.statistics.range.month')}
            </button>
            <button
              className={`${styles.timeRangeButton} ${timeRange === 'quarter' ? styles.active : ''}`}
              onClick={() => handleTimeRangeChange('quarter')}
            >
              {t('pit.statistics.range.quarter')}
            </button>
            <button
              className={`${styles.timeRangeButton} ${timeRange === 'year' ? styles.active : ''}`}
              onClick={() => handleTimeRangeChange('year')}
            >
              {t('pit.statistics.range.year')}
            </button>
          </div>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={t('pit.currency.switchTo', {
              mode: t(displayMode === 'carrot' ? 'pit.currency.white' : 'pit.currency.carrot'),
            })}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {t(displayMode === 'carrot' ? 'pit.currency.carrot' : 'pit.currency.white')}
          </button>
          <button className={styles.refreshButton} onClick={refetch} title={t('pit.common.refresh')}>
            🔄
          </button>
        </div>
      </div>

      <div className={styles.navigation}>
        <div className={styles.tabList}>
          <button
            className={`${styles.tab} ${activeTab === 'overview' ? styles.active : ''}`}
            onClick={() => handleTabChange('overview')}
          >
            <span className={styles.tabIcon}>📊</span>
            <span className={styles.tabText}>{t('pit.statistics.tab.overview')}</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'chart' ? styles.active : ''}`}
            onClick={() => handleTabChange('chart')}
          >
            <span className={styles.tabIcon}>📈</span>
            <span className={styles.tabText}>{t('pit.statistics.tab.chart')}</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'category' ? styles.active : ''}`}
            onClick={() => handleTabChange('category')}
          >
            <span className={styles.tabIcon}>🏷️</span>
            <span className={styles.tabText}>{t('pit.statistics.tab.category')}</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'trend' ? styles.active : ''}`}
            onClick={() => handleTabChange('trend')}
          >
            <span className={styles.tabIcon}>📉</span>
            <span className={styles.tabText}>{t('pit.statistics.tab.trend')}</span>
          </button>
        </div>
      </div>

      <div className={styles.content}>
        <Suspense fallback={<div>{t('pit.statistics.moduleLoading')}</div>}>
          {renderTabContent()}
        </Suspense>
      </div>
    </div>
  );
};

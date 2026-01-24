import { useState } from 'react';
import { useStatistics } from '../../hooks';
import { StatisticsOverview } from './StatisticsOverview';
import { IncomeExpenseChart } from './IncomeExpenseChart';
import { CategoryBreakdown } from './CategoryBreakdown';
import { TrendAnalysis } from './TrendAnalysis';
import styles from './Statistics.module.css';

type StatisticsTab = 'overview' | 'chart' | 'category' | 'trend';
type TimeRange = 'month' | 'quarter' | 'year';

/**
 * 收支统计组件
 */
export const Statistics = () => {
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
      {/* 页面标题和操作 */}
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>收支统计</h2>
          <p className={styles.subtitle}>分析您的萝卜币收入和支出情况</p>
        </div>
        <div className={styles.headerRight}>
          <div className={styles.timeRangeSelector}>
            <button
              className={`${styles.timeRangeButton} ${timeRange === 'month' ? styles.active : ''}`}
              onClick={() => handleTimeRangeChange('month')}
            >
              月度
            </button>
            <button
              className={`${styles.timeRangeButton} ${timeRange === 'quarter' ? styles.active : ''}`}
              onClick={() => handleTimeRangeChange('quarter')}
            >
              季度
            </button>
            <button
              className={`${styles.timeRangeButton} ${timeRange === 'year' ? styles.active : ''}`}
              onClick={() => handleTimeRangeChange('year')}
            >
              年度
            </button>
          </div>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={`切换到${displayMode === 'carrot' ? '白萝卜' : '胡萝卜'}显示`}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {displayMode === 'carrot' ? '胡萝卜' : '白萝卜'}
          </button>
          <button className={styles.refreshButton} onClick={refetch} title="刷新数据">
            🔄
          </button>
        </div>
      </div>

      {/* 导航标签 */}
      <div className={styles.navigation}>
        <div className={styles.tabList}>
          <button
            className={`${styles.tab} ${activeTab === 'overview' ? styles.active : ''}`}
            onClick={() => handleTabChange('overview')}
          >
            <span className={styles.tabIcon}>📊</span>
            <span className={styles.tabText}>统计概览</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'chart' ? styles.active : ''}`}
            onClick={() => handleTabChange('chart')}
          >
            <span className={styles.tabIcon}>📈</span>
            <span className={styles.tabText}>收支图表</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'category' ? styles.active : ''}`}
            onClick={() => handleTabChange('category')}
          >
            <span className={styles.tabIcon}>🏷️</span>
            <span className={styles.tabText}>分类统计</span>
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'trend' ? styles.active : ''}`}
            onClick={() => handleTabChange('trend')}
          >
            <span className={styles.tabIcon}>📉</span>
            <span className={styles.tabText}>趋势分析</span>
          </button>
        </div>
      </div>

      {/* 内容区域 */}
      <div className={styles.content}>
        {renderTabContent()}
      </div>
    </div>
  );
};
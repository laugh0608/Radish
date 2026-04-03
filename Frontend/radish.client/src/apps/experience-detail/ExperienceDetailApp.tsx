import { useState, useEffect, lazy, Suspense } from 'react';
import { log } from '@/utils/logger';
import { ExperienceBar } from '@radish/ui/experience-bar';
import { Icon } from '@radish/ui/icon';
import { experienceApi, type ExperienceData, type ExpTransactionData } from '@/api/experience';
import styles from './ExperienceDetailApp.module.css';

const LineChart = lazy(() =>
  import('@radish/ui/line-chart').then((module) => ({ default: module.LineChart }))
);

const PieChart = lazy(() =>
  import('@radish/ui/pie-chart').then((module) => ({ default: module.PieChart }))
);

export const ExperienceDetailApp = () => {
  const [experience, setExperience] = useState<ExperienceData | null>(null);
  const [transactions, setTransactions] = useState<ExpTransactionData[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pageIndex, setPageIndex] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [days, setDays] = useState<7 | 30>(7);
  const pageSize = 20;

  useEffect(() => {
    void loadData();
  }, [pageIndex]);

  const loadData = async () => {
    setLoading(true);
    setError(null);

    try {
      const expResult = await experienceApi.getMyExperience();
      if (!expResult) {
        setExperience(null);
        setTransactions([]);
        setTotalPages(1);
        setError('暂时无法获取等级数据，请稍后重试');
        return;
      }

      setExperience(expResult);

      try {
        const transResult = await experienceApi.getTransactions({ pageIndex, pageSize });
        if (transResult) {
          setTransactions(transResult.data);
          setTotalPages(transResult.pageCount);
        } else {
          setTransactions([]);
          setTotalPages(1);
        }
      } catch (transErr) {
        log.warn('经验值交易记录 API 不可用:', transErr);
        setTransactions([]);
        setTotalPages(1);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载数据失败');
      log.error('加载经验值详情失败:', err);
    } finally {
      setLoading(false);
    }
  };

  // 从交易记录计算每日统计数据
  const getDailyStats = () => {
    if (transactions.length === 0) {
      // 如果没有交易记录，返回空数组
      return [];
    }

    const stats: Record<string, number> = {};
    const today = new Date();

    // 初始化最近 N 天的数据
    for (let i = days - 1; i >= 0; i--) {
      const date = new Date(today);
      date.setDate(date.getDate() - i);
      const dateKey = `${date.getMonth() + 1}/${date.getDate()}`;
      stats[dateKey] = 0;
    }

    // 统计交易记录中的经验值
    transactions.forEach(tx => {
      const txDate = new Date(tx.voCreateTime);
      const dateKey = `${txDate.getMonth() + 1}/${txDate.getDate()}`;

      // 只统计最近 N 天的数据
      if (Object.prototype.hasOwnProperty.call(stats, dateKey)) {
        stats[dateKey] += tx.voExpAmount;
      }
    });

    // 转换为数组格式
    return Object.entries(stats).map(([date, exp]) => ({
      date,
      exp
    }));
  };

  // 计算经验值来源分布
  const getExpSourceDistribution = () => {
    const sources: Record<string, number> = {};

    transactions.forEach(tx => {
      const type = tx.voExpType || '其他';
      sources[type] = (sources[type] || 0) + tx.voExpAmount;
    });

    const colors = ['#667eea', '#764ba2', '#f093fb', '#4facfe', '#43e97b'];

    return Object.entries(sources).map(([name, value], index) => ({
      name: getExpTypeDisplay(name),
      value,
      color: colors[index % colors.length]
    }));
  };

  const getExpTypeDisplay = (type: string): string => {
    const typeMap: Record<string, string> = {
      'POST_CREATE': '发帖',
      'FIRST_POST': '首次发帖',
      'COMMENT_CREATE': '评论',
      'FIRST_COMMENT': '首次评论',
      'POST_LIKED': '帖子被点赞',
      'COMMENT_LIKED': '评论被点赞',
      'LIKE_POST': '点赞帖子',
      'LIKE_COMMENT': '点赞评论',
      'GIVE_LIKE': '给出点赞',
      'RECEIVE_LIKE': '收到点赞',
      'GOD_COMMENT': '神评',
      'SOFA': '沙发',
      'ADMIN_ADJUST': '管理员调整',
      'DAILY_LOGIN': '每日登录',
      'WEEKLY_LOGIN': '每周登录',
      'PROFILE_COMPLETE': '完善资料',
      'PENALTY': '违规扣分'
    };
    return typeMap[type] || type;
  };

  const handlePrevPage = () => {
    if (pageIndex > 1) {
      setPageIndex(pageIndex - 1);
    }
  };

  const handleNextPage = () => {
    if (pageIndex < totalPages) {
      setPageIndex(pageIndex + 1);
    }
  };

  const dailyStats = getDailyStats();
  const sourceDistribution = getExpSourceDistribution();
  const resolvedLevelName = experience?.voCurrentLevelName?.trim() || (experience ? `等级 ${experience.voCurrentLevel}` : '');
  const totalProgress = experience
    ? Math.min(1, Math.max(0, experience.voTotalExp / Math.max(experience.voTotalExp + experience.voExpToNextLevel, 1)))
    : 0;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.title}>
          <Icon icon="mdi:star-circle" size={32} />
          等级
        </h1>
      </div>

      {loading && (
        <div className={styles.loading}>
          <Icon icon="mdi:loading" size={32} className={styles.spinner} />
          <p>加载中...</p>
        </div>
      )}

      {error && (
        <div className={styles.error}>
          <Icon icon="mdi:alert-circle" size={24} />
          <p>{error}</p>
          <button onClick={() => void loadData()} className={styles.retryButton}>
            重试
          </button>
        </div>
      )}

      {!loading && !error && !experience && (
        <div className={styles.empty}>
          <Icon icon="mdi:star-off-outline" size={56} />
          <p>当前没有可展示的等级数据</p>
          <button onClick={() => void loadData()} className={styles.retryButton}>
            重新获取
          </button>
        </div>
      )}

      {!loading && !error && experience && (
        <>
          {/* 经验条区域 */}
          <div className={styles.experienceBarSection}>
            <div className={styles.expBarGroup}>
              {/* 当前等级进度条 */}
              <div className={styles.expBarItem}>
                <div className={styles.expBarLabel}>当前等级进度</div>
                <ExperienceBar
                  data={{
                    ...experience,
                    voNextLevelExp: experience.voLevelProgress > 0
                      ? Math.round(experience.voCurrentExp / experience.voLevelProgress)
                      : experience.voCurrentExp + experience.voExpToNextLevel
                  }}
                  size="medium"
                  showLevel={true}
                  showProgress={true}
                  showTooltip={true}
                  animated={true}
                />
              </div>

              {/* 总进度条（到满级） */}
              <div className={styles.expBarItem}>
                <div className={styles.expBarLabel}>总进度（到满级）</div>
                <ExperienceBar
                  data={{
                    ...experience,
                    voCurrentLevelName: `${resolvedLevelName} → 满级`,
                    voCurrentExp: experience.voTotalExp,
                    voNextLevelExp: Math.max(experience.voTotalExp + experience.voExpToNextLevel, 1),
                    voLevelProgress: totalProgress
                  }}
                  size="medium"
                  showLevel={true}
                  showProgress={true}
                  showTooltip={false}
                  animated={true}
                />
              </div>
            </div>
          </div>

          {/* 经验值概览 */}
          <div className={styles.overview}>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>当前等级</div>
              <div className={styles.statValue} style={{ color: experience.voThemeColor || 'var(--theme-brand-primary)' }}>
                Lv.{experience.voCurrentLevel} {resolvedLevelName}
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>当前经验值</div>
              <div className={styles.statValue}>{Number(experience.voCurrentExp).toLocaleString()}</div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>累计经验值</div>
              <div className={styles.statValue}>{Number(experience.voTotalExp).toLocaleString()}</div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>距下一级</div>
              <div className={styles.statValue}>{Number(experience.voExpToNextLevel).toLocaleString()}</div>
            </div>
          </div>

          {/* 图表区域 - 两列布局 */}
          <div className={styles.chartContainer}>
            {/* 经验值趋势图 */}
            <div className={styles.chartSection}>
              <div className={styles.chartHeader}>
                <h2 className={styles.chartTitle}>经验值趋势</h2>
                <div className={styles.chartControls}>
                  <button
                    className={`${styles.controlButton} ${days === 7 ? styles.active : ''}`}
                    onClick={() => setDays(7)}
                  >
                    最近7天
                  </button>
                  <button
                    className={`${styles.controlButton} ${days === 30 ? styles.active : ''}`}
                    onClick={() => setDays(30)}
                  >
                    最近30天
                  </button>
                </div>
              </div>
              <Suspense fallback={<div className={styles.empty}>图表加载中...</div>}>
                <LineChart
                  data={dailyStats}
                  lines={[
                    { dataKey: 'exp', name: '经验值', color: '#667eea', strokeWidth: 2 }
                  ]}
                  xAxisKey="date"
                  loading={loading}
                  error={null}
                  height={220}
                  showGrid={true}
                  showLegend={true}
                />
              </Suspense>
            </div>

            {/* 经验值来源分布 */}
            {sourceDistribution.length > 0 && (
              <div className={`${styles.chartSection} ${styles.pieChart}`}>
                <h2 className={styles.chartTitle}>经验值来源</h2>
                <Suspense fallback={<div className={styles.empty}>图表加载中...</div>}>
                  <PieChart
                    data={sourceDistribution}
                    loading={loading}
                    error={null}
                    height={300}
                    showLegend={true}
                    innerRadius={0}
                    outerRadius={100}
                    showLabel={true}
                  />
                </Suspense>
              </div>
            )}
          </div>

          {/* 经验值明细列表 */}
          <div className={styles.transactionSection}>
            <h2 className={styles.sectionTitle}>经验值明细</h2>
            {transactions.length === 0 ? (
              <div className={styles.empty}>
                <Icon icon="mdi:file-document-outline" size={64} />
                <p>暂无经验值记录</p>
              </div>
            ) : (
              <>
                <div className={styles.transactionList}>
                  {transactions.map((tx) => {
                    const isPositive = tx.voExpAmount > 0;
                    const typeLabel = getExpTypeDisplay(tx.voExpTypeDisplay || tx.voExpType);
                    const timeLabel = new Date(tx.voCreateTime).toLocaleString('zh-CN', {
                      month: '2-digit',
                      day: '2-digit',
                      hour: '2-digit',
                      minute: '2-digit'
                    });
                    return (
                      <div
                        key={tx.voId}
                        className={`${styles.transactionItem} ${isPositive ? styles.positive : styles.negative}`}
                      >
                        <div className={`${styles.txBadge} ${isPositive ? styles.txBadgePositive : styles.txBadgeNegative}`}>
                          <Icon icon={isPositive ? "mdi:plus" : "mdi:minus"} size={18} />
                        </div>
                        <div className={styles.txInfo}>
                          <div className={styles.txTitleRow}>
                            <div className={styles.txType}>{typeLabel}</div>
                            {tx.voIsLevelUp && <span className={styles.txLevelUp}>升级</span>}
                          </div>
                          <div className={styles.txMeta}>
                            <span className={styles.txTime}>{timeLabel}</span>
                            <span className={styles.txLevelChange}>
                              Lv.{tx.voLevelBefore} → Lv.{tx.voLevelAfter}
                            </span>
                          </div>
                          {tx.voRemark && <div className={styles.txRemark}>{tx.voRemark}</div>}
                        </div>
                        <div className={styles.txAmountWrap}>
                          <div className={styles.txAmount}>
                            {isPositive ? '+' : ''}{tx.voExpAmount}
                          </div>
                          <div className={styles.txBalance}>
                            余额 {Number(tx.voExpAfter).toLocaleString()}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>

                <div className={styles.pagination}>
                  <button
                    onClick={handlePrevPage}
                    disabled={pageIndex === 1}
                    className={styles.pageButton}
                  >
                    <Icon icon="mdi:chevron-left" size={20} />
                    上一页
                  </button>

                  <span className={styles.pageInfo}>
                    第 {pageIndex} / {totalPages} 页
                  </span>

                  <button
                    onClick={handleNextPage}
                    disabled={pageIndex >= totalPages}
                    className={styles.pageButton}
                  >
                    下一页
                    <Icon icon="mdi:chevron-right" size={20} />
                  </button>
                </div>
              </>
            )}
          </div>
        </>
      )}
    </div>
  );
};

import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { LineChart, PieChart, ExperienceBar } from '@radish/ui';
import { experienceApi, type ExperienceData, type ExpTransactionData } from '@/api/experience';
import { Icon } from '@radish/ui';
import styles from './ExperienceDetailApp.module.css';

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
      // 加载经验值信息
      const expResult = await experienceApi.getMyExperience();
      setExperience(expResult);

      // 尝试加载交易记录（如果 API 不存在则忽略）
      try {
        const transResult = await experienceApi.getTransactions({ pageIndex, pageSize });
        if (transResult) {
          setTransactions(transResult.data);
          setTotalPages(transResult.pageCount);
        }
      } catch (transErr) {
        log.warn('经验值交易记录 API 不可用:', transErr);
        // 忽略交易记录加载失败，只显示经验值信息
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载数据失败');
      log.error('加载经验值详情失败:', err);
    } finally {
      setLoading(false);
    }
  };

  // 模拟每日统计数据（实际应该从 API 获取）
  const getDailyStats = () => {
    const stats = [];
    const today = new Date();

    for (let i = days - 1; i >= 0; i--) {
      const date = new Date(today);
      date.setDate(date.getDate() - i);

      stats.push({
        date: `${date.getMonth() + 1}/${date.getDate()}`,
        exp: Math.floor(Math.random() * 100) + 20 // 模拟数据
      });
    }

    return stats;
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
      'GOD_COMMENT': '神评',
      'SOFA': '沙发',
      'ADMIN_ADJUST': '管理员调整'
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

      {!loading && !error && experience && (
        <>
          {/* 经验条 */}
          <div className={styles.experienceBarSection}>
            <ExperienceBar
              data={{
                ...experience,
                voLevelName: experience.voCurrentLevelName,  // 映射字段名
                voNextLevelExp: experience.voCurrentExp + experience.voExpToNextLevel  // 计算总经验值
              }}
              size="large"
              showLevel={true}
              showProgress={true}
              showTooltip={true}
              animated={true}
            />
          </div>

          {/* 经验值概览 */}
          <div className={styles.overview}>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>当前等级</div>
              <div className={styles.statValue} style={{ color: experience.voThemeColor || '#3b82f6' }}>
                Lv.{experience.voCurrentLevel} {experience.voCurrentLevelName}
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
            <LineChart
              data={dailyStats}
              lines={[
                { dataKey: 'exp', name: '经验值', color: '#667eea', strokeWidth: 2 }
              ]}
              xAxisKey="date"
              loading={loading}
              error={null}
              height={300}
              showGrid={true}
              showLegend={true}
            />
          </div>

          {/* 经验值来源分布 */}
          {sourceDistribution.length > 0 && (
            <div className={styles.chartSection}>
              <h2 className={styles.chartTitle}>经验值来源</h2>
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
            </div>
          )}

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
                  {transactions.map((tx) => (
                    <div key={tx.voId} className={styles.transactionItem}>
                      <div className={styles.txIcon}>
                        <Icon icon="mdi:plus-circle" size={24} />
                      </div>
                      <div className={styles.txInfo}>
                        <div className={styles.txType}>
                          {tx.voExpTypeDisplay || getExpTypeDisplay(tx.voExpType)}
                        </div>
                        <div className={styles.txTime}>
                          {new Date(tx.voCreateTime).toLocaleString('zh-CN', {
                            year: 'numeric',
                            month: '2-digit',
                            day: '2-digit',
                            hour: '2-digit',
                            minute: '2-digit',
                            second: '2-digit'
                          })}
                        </div>
                        {tx.voRemark && (
                          <div className={styles.txRemark}>{tx.voRemark}</div>
                        )}
                      </div>
                      <div className={styles.txAmount}>
                        {tx.voExpAmount > 0 ? '+' : ''}{tx.voExpAmount}
                      </div>
                    </div>
                  ))}
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

import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import {
  LineChart,
  Line,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer
} from 'recharts';
import { experienceApi, type UserExperience, type ExpTransaction } from '@radish/ui';
import { Icon } from '@radish/ui';
import styles from './ExperienceDetailApp.module.css';

export const ExperienceDetailApp = () => {
  const [experience, setExperience] = useState<UserExperience | null>(null);
  const [transactions, setTransactions] = useState<ExpTransaction[]>([]);
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
      // 并行加载经验值信息和交易记录
      const [expResult, transResult] = await Promise.all([
        experienceApi.getMyExperience(),
        experienceApi.getTransactions({ pageIndex, pageSize })
      ]);

      setExperience(expResult);
      setTransactions(transResult.data);
      setTotalPages(transResult.pageCount);
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
      const type = tx.expType || '其他';
      sources[type] = (sources[type] || 0) + tx.expAmount;
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
          <Icon icon="mdi:chart-line" size={32} />
          经验值详情
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
          {/* 经验值概览 */}
          <div className={styles.overview}>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>当前等级</div>
              <div className={styles.statValue} style={{ color: experience.themeColor }}>
                Lv.{experience.currentLevel} {experience.currentLevelName}
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>当前经验值</div>
              <div className={styles.statValue}>{Number(experience.currentExp).toLocaleString()}</div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>累计经验值</div>
              <div className={styles.statValue}>{Number(experience.totalExp).toLocaleString()}</div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>距下一级</div>
              <div className={styles.statValue}>{Number(experience.expToNextLevel).toLocaleString()}</div>
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
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={dailyStats}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="date" />
                <YAxis />
                <Tooltip />
                <Legend />
                <Line
                  type="monotone"
                  dataKey="exp"
                  stroke="#667eea"
                  strokeWidth={2}
                  name="经验值"
                />
              </LineChart>
            </ResponsiveContainer>
          </div>

          {/* 经验值来源分布 */}
          {sourceDistribution.length > 0 && (
            <div className={styles.chartSection}>
              <h2 className={styles.chartTitle}>经验值来源</h2>
              <ResponsiveContainer width="100%" height={300}>
                <PieChart>
                  <Pie
                    data={sourceDistribution}
                    cx="50%"
                    cy="50%"
                    labelLine={false}
                    label={({ name, percent }) => `${name} ${((percent || 0) * 100).toFixed(0)}%`}
                    outerRadius={100}
                    fill="#8884d8"
                    dataKey="value"
                  >
                    {sourceDistribution.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
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
                    <div key={tx.id} className={styles.transactionItem}>
                      <div className={styles.txIcon}>
                        <Icon icon="mdi:plus-circle" size={24} />
                      </div>
                      <div className={styles.txInfo}>
                        <div className={styles.txType}>{getExpTypeDisplay(tx.expType)}</div>
                        <div className={styles.txTime}>
                          {new Date(tx.createTime).toLocaleString('zh-CN')}
                        </div>
                        {tx.remark && (
                          <div className={styles.txRemark}>{tx.remark}</div>
                        )}
                      </div>
                      <div className={styles.txAmount}>
                        +{tx.expAmount}
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

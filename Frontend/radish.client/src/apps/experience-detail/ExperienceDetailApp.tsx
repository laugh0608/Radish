import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { ExperienceBar } from '@radish/ui/experience-bar';
import { Icon } from '@radish/ui/icon';
import { experienceApi, type ExperienceData, type ExpTransactionData } from '@/api/experience';
import {
  addExperienceValues,
  buildExperienceBarData,
  buildExperienceDailyStats,
  buildExperienceSourceStats,
  createExperienceBarPresentation,
  formatExperienceDateTime,
  formatExperienceNumber,
  formatExperienceSignedNumber,
  formatExperienceType,
} from '@/experience/experiencePresentation';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import styles from './ExperienceDetailApp.module.css';

const LineChart = lazy(() =>
  import('@radish/ui/line-chart').then((module) => ({ default: module.LineChart }))
);

const PieChart = lazy(() =>
  import('@radish/ui/pie-chart').then((module) => ({ default: module.PieChart }))
);

const EXPERIENCE_TRANSACTION_PAGE_SIZE = 20;
const EXPERIENCE_SOURCE_COLORS = ['#667eea', '#764ba2', '#f093fb', '#4facfe', '#43e97b'];

interface ExperienceDetailAppProps {
  pageIndex?: number;
  onPageIndexChange?: (pageIndex: number) => void;
  onDataLoaded?: (snapshot: ExperienceDetailSnapshot) => void;
}

export interface ExperienceDetailSnapshot {
  experience: ExperienceData | null;
  transactions: ExpTransactionData[];
  totalPages: number;
}

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.trim() ? error.message : fallback;
}

export const ExperienceDetailApp = ({
  pageIndex: controlledPageIndex,
  onPageIndexChange,
  onDataLoaded,
}: ExperienceDetailAppProps = {}) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const chartNow = useMemo(() => new Date(), []);
  const experienceBarPresentation = useMemo(
    () => createExperienceBarPresentation(t, language, displayTimeZone),
    [displayTimeZone, language, t],
  );
  const [experience, setExperience] = useState<ExperienceData | null>(null);
  const [transactions, setTransactions] = useState<ExpTransactionData[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [transactionsError, setTransactionsError] = useState<string | null>(null);
  const [internalPageIndex, setInternalPageIndex] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [days, setDays] = useState<7 | 30>(7);
  const pageIndex = controlledPageIndex ?? internalPageIndex;
  const onDataLoadedRef = useRef(onDataLoaded);

  useEffect(() => {
    onDataLoadedRef.current = onDataLoaded;
  }, [onDataLoaded]);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    setTransactionsError(null);

    try {
      const expResult = await experienceApi.getMyExperience(t);
      setExperience(expResult);

      try {
        const transResult = await experienceApi.getTransactions({
          pageIndex,
          pageSize: EXPERIENCE_TRANSACTION_PAGE_SIZE,
        }, t);
        const normalizedTotalPages = Math.max(transResult.pageCount, 1);
        setTransactions(transResult.data);
        setTotalPages(normalizedTotalPages);
        onDataLoadedRef.current?.({
          experience: expResult,
          transactions: transResult.data,
          totalPages: normalizedTotalPages,
        });
      } catch (transactionError) {
        const message = getErrorMessage(transactionError, t('experience.transactions.loadFailed'));
        log.warn('经验值交易记录 API 不可用:', transactionError);
        setTransactions([]);
        setTotalPages(1);
        setTransactionsError(message);
        onDataLoadedRef.current?.({ experience: expResult, transactions: [], totalPages: 1 });
      }
    } catch (loadError) {
      setExperience(null);
      setTransactions([]);
      setTotalPages(1);
      setError(getErrorMessage(loadError, t('experience.loadFailed')));
      onDataLoadedRef.current?.({ experience: null, transactions: [], totalPages: 1 });
      log.error('加载经验值详情失败:', loadError);
    } finally {
      setLoading(false);
    }
  }, [pageIndex, t]);

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const updatePageIndex = (nextPageIndex: number) => {
    if (onPageIndexChange) {
      onPageIndexChange(nextPageIndex);
      return;
    }

    setInternalPageIndex(nextPageIndex);
  };

  const dailyStats = useMemo(
    () => buildExperienceDailyStats(transactions, days, chartNow, language, displayTimeZone),
    [chartNow, days, displayTimeZone, language, transactions],
  );
  const sourceDistribution = useMemo(
    () => buildExperienceSourceStats(transactions, t).map((item, index) => ({
      ...item,
      color: EXPERIENCE_SOURCE_COLORS[index % EXPERIENCE_SOURCE_COLORS.length],
    })),
    [t, transactions],
  );
  const resolvedLevelName = experience?.voCurrentLevelName?.trim()
    || (experience ? t('experience.levelFallback', { level: experience.voCurrentLevel }) : '');
  const resolvedNextLevelName = experience?.voNextLevelName?.trim()
    || (experience ? t('experience.levelFallback', { level: experience.voNextLevel }) : '');
  const cumulativeThreshold = experience
    ? addExperienceValues(experience.voTotalExp, experience.voExpToNextLevel)
    : '0';
  const cumulativeThresholdNumber = Number(cumulativeThreshold);
  const totalProgress = experience && cumulativeThresholdNumber > 0
    ? Math.min(1, Math.max(0, Number(experience.voTotalExp) / cumulativeThresholdNumber))
    : 0;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.title}>
          <Icon icon="mdi:star-circle" size={32} />
          {t('experience.title')}
        </h1>
      </div>

      {loading && (
        <div className={styles.loading}>
          <Icon icon="mdi:loading" size={32} className={styles.spinner} />
          <p>{t('experience.loading')}</p>
        </div>
      )}

      {error && (
        <div className={styles.error}>
          <Icon icon="mdi:alert-circle" size={24} />
          <p>{error}</p>
          <button type="button" onClick={() => void loadData()} className={styles.retryButton}>
            {t('common.retry')}
          </button>
        </div>
      )}

      {!loading && !error && !experience && (
        <div className={styles.empty}>
          <Icon icon="mdi:star-off-outline" size={56} />
          <p>{t('experience.empty')}</p>
          <button type="button" onClick={() => void loadData()} className={styles.retryButton}>
            {t('experience.reload')}
          </button>
        </div>
      )}

      {!loading && !error && experience && (
        <>
          <div className={styles.experienceBarSection}>
            <div className={styles.expBarGroup}>
              <div className={styles.expBarItem}>
                <div className={styles.expBarLabel}>{t('experience.currentLevelProgress')}</div>
                <ExperienceBar
                  data={buildExperienceBarData(experience)}
                  size="medium"
                  showLevel={true}
                  showProgress={true}
                  showTooltip={true}
                  animated={true}
                  presentation={experienceBarPresentation}
                />
              </div>

              <div className={styles.expBarItem}>
                <div className={styles.expBarLabel}>{t('experience.cumulativeProgress')}</div>
                <ExperienceBar
                  data={{
                    ...buildExperienceBarData(experience),
                    voCurrentLevelName: t('experience.cumulativeLevel', {
                      current: resolvedLevelName,
                      next: resolvedNextLevelName,
                    }),
                    voCurrentExp: experience.voTotalExp,
                    voNextLevelExp: cumulativeThreshold,
                    voLevelProgress: totalProgress,
                  }}
                  size="medium"
                  showLevel={true}
                  showProgress={true}
                  showTooltip={false}
                  animated={true}
                  presentation={experienceBarPresentation}
                />
              </div>
            </div>
          </div>

          <div className={styles.overview}>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>{t('experience.stat.currentLevel')}</div>
              <div className={styles.statValue} style={{ color: experience.voThemeColor || 'var(--theme-brand-primary)' }}>
                Lv.{experience.voCurrentLevel}
                {experience.voCurrentLevelName?.trim() ? ` ${experience.voCurrentLevelName}` : ''}
              </div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>{t('experience.stat.currentExp')}</div>
              <div className={styles.statValue}>{formatExperienceNumber(experience.voCurrentExp, language)}</div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>{t('experience.stat.totalExp')}</div>
              <div className={styles.statValue}>{formatExperienceNumber(experience.voTotalExp, language)}</div>
            </div>
            <div className={styles.statCard}>
              <div className={styles.statLabel}>{t('experience.stat.toNextLevel')}</div>
              <div className={styles.statValue}>{formatExperienceNumber(experience.voExpToNextLevel, language)}</div>
            </div>
          </div>

          {!transactionsError && (
            <div className={styles.chartContainer}>
              <div className={styles.chartSection}>
                <div className={styles.chartHeader}>
                  <h2 className={styles.chartTitle}>{t('experience.chart.trendTitle')}</h2>
                  <div className={styles.chartControls}>
                    <button
                      type="button"
                      className={`${styles.controlButton} ${days === 7 ? styles.active : ''}`}
                      onClick={() => setDays(7)}
                    >
                      {t('experience.chart.rangeDays', { count: 7 })}
                    </button>
                    <button
                      type="button"
                      className={`${styles.controlButton} ${days === 30 ? styles.active : ''}`}
                      onClick={() => setDays(30)}
                    >
                      {t('experience.chart.rangeDays', { count: 30 })}
                    </button>
                  </div>
                </div>
                <Suspense fallback={<div className={styles.empty}>{t('experience.chart.loading')}</div>}>
                  <LineChart
                    data={dailyStats}
                    lines={[
                      { dataKey: 'exp', name: t('experience.chart.expSeries'), color: '#667eea', strokeWidth: 2 },
                    ]}
                    xAxisKey="date"
                    loading={false}
                    error={null}
                    height={220}
                    showGrid={true}
                    showLegend={true}
                    valueFormatter={(value) => formatExperienceNumber(value, language)}
                  />
                </Suspense>
              </div>

              {sourceDistribution.length > 0 && (
                <div className={`${styles.chartSection} ${styles.pieChart}`}>
                  <h2 className={styles.chartTitle}>{t('experience.chart.sourceTitle')}</h2>
                  <Suspense fallback={<div className={styles.empty}>{t('experience.chart.loading')}</div>}>
                    <PieChart
                      data={sourceDistribution}
                      loading={false}
                      error={null}
                      height={300}
                      showLegend={true}
                      innerRadius={0}
                      outerRadius={100}
                      showLabel={true}
                      valueFormatter={(value) => formatExperienceNumber(value, language)}
                      percentageFormatter={experienceBarPresentation.formatPercentage}
                    />
                  </Suspense>
                </div>
              )}
            </div>
          )}

          <div className={styles.transactionSection}>
            <h2 className={styles.sectionTitle}>{t('experience.transactions.title')}</h2>
            {transactionsError ? (
              <div className={styles.error}>
                <Icon icon="mdi:alert-circle" size={24} />
                <p>{transactionsError}</p>
                <button type="button" onClick={() => void loadData()} className={styles.retryButton}>
                  {t('common.retry')}
                </button>
              </div>
            ) : transactions.length === 0 ? (
              <div className={styles.empty}>
                <Icon icon="mdi:file-document-outline" size={64} />
                <p>{t('experience.transactions.empty')}</p>
              </div>
            ) : (
              <>
                <div className={styles.transactionList}>
                  {transactions.map((transaction) => {
                    const isPositive = transaction.voExpAmount > 0;
                    const typeLabel = formatExperienceType(transaction.voExpType, t);
                    const timeLabel = formatExperienceDateTime(
                      transaction.voCreateTime,
                      displayTimeZone,
                      language,
                    );
                    return (
                      <div
                        key={transaction.voId}
                        className={`${styles.transactionItem} ${isPositive ? styles.positive : styles.negative}`}
                      >
                        <div className={`${styles.txBadge} ${isPositive ? styles.txBadgePositive : styles.txBadgeNegative}`}>
                          <Icon icon={isPositive ? 'mdi:plus' : 'mdi:minus'} size={18} />
                        </div>
                        <div className={styles.txInfo}>
                          <div className={styles.txTitleRow}>
                            <div className={styles.txType}>{typeLabel}</div>
                            {transaction.voIsLevelUp && (
                              <span className={styles.txLevelUp}>{t('experience.transaction.levelUp')}</span>
                            )}
                          </div>
                          <div className={styles.txMeta}>
                            <span className={styles.txTime}>{timeLabel}</span>
                            <span className={styles.txLevelChange}>
                              Lv.{transaction.voLevelBefore} → Lv.{transaction.voLevelAfter}
                            </span>
                          </div>
                          {transaction.voRemark && <div className={styles.txRemark}>{transaction.voRemark}</div>}
                        </div>
                        <div className={styles.txAmountWrap}>
                          <div className={styles.txAmount}>
                            {formatExperienceSignedNumber(transaction.voExpAmount, language)}
                          </div>
                          <div className={styles.txBalance}>
                            {t('experience.transaction.balance', {
                              value: formatExperienceNumber(transaction.voExpAfter, language),
                            })}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>

                <div className={styles.pagination}>
                  <button
                    type="button"
                    onClick={() => pageIndex > 1 && updatePageIndex(pageIndex - 1)}
                    disabled={pageIndex === 1}
                    className={styles.pageButton}
                  >
                    <Icon icon="mdi:chevron-left" size={20} />
                    {t('common.previousPage')}
                  </button>

                  <span className={styles.pageInfo}>
                    {t('experience.pagination.pageInfo', {
                      current: formatExperienceNumber(pageIndex, language),
                      total: formatExperienceNumber(totalPages, language),
                    })}
                  </span>

                  <button
                    type="button"
                    onClick={() => pageIndex < totalPages && updatePageIndex(pageIndex + 1)}
                    disabled={pageIndex >= totalPages}
                    className={styles.pageButton}
                  >
                    {t('common.nextPage')}
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

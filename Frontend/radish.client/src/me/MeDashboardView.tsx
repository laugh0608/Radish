import type { MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { ExperienceBar, type ExperienceBarPresentation } from '@radish/ui/experience-bar';
import { Icon } from '@radish/ui/icon';
import { WebTaskRailDisclosure } from '@/components/web-shell';
import {
  absoluteCoinValue,
  formatCoinNumber,
  formatTransactionType,
  resolveTransactionDirection,
} from '@/coin/coinPresentation';
import {
  buildExperienceBarData,
  formatExperienceNumber,
  formatExperienceSignedNumber,
  formatExperienceType,
} from '@/experience/experiencePresentation';
import {
  resolvePetGrowthStageTranslationKey,
  resolvePetMoodTranslationKey,
} from '@/pet/petPresentation';
import { logout } from '@/services/auth';
import type { MeDashboardData } from './meDashboardModel';
import { buildMePath, type MeRoute } from './meRouteState';
import styles from './MeApp.module.css';

interface MeDashboardViewProps {
  data: MeDashboardData;
  loading: boolean;
  language: string;
  userId: string;
  displayName: string;
  accountName: string;
  resolvedAvatarUrl: string | null;
  loadedAtLabel: string | null;
  selfProfilePath: string | null;
  experienceBarPresentation: ExperienceBarPresentation;
  formatDisplayDateTime: (value: string | number | Date | null | undefined) => string;
  getBrowseHistoryHref: (item: MeDashboardData['browseHistory'][number]) => string | null;
  onNavigate: (route: MeRoute) => void;
  onRefresh: () => void;
  onRememberSelfProfileSource: (event: MouseEvent<HTMLAnchorElement>) => void;
  onRememberPublicSource: (event: MouseEvent<HTMLAnchorElement>, href: string) => void;
}

function shouldHandleRouteLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

export const MeDashboardView = ({
  data,
  loading,
  language,
  userId,
  displayName,
  accountName,
  resolvedAvatarUrl,
  loadedAtLabel,
  selfProfilePath,
  experienceBarPresentation,
  formatDisplayDateTime,
  getBrowseHistoryHref,
  onNavigate,
  onRefresh,
  onRememberSelfProfileSource,
  onRememberPublicSource,
}: MeDashboardViewProps) => {
  const { t } = useTranslation();
  const experience = data.experience;
  const balance = data.balance;
  const pet = data.pet;
  const recentBrowseItem = data.browseHistory[0] ?? null;
  const recentBrowseHref = recentBrowseItem ? getBrowseHistoryHref(recentBrowseItem) : null;

  const handleRouteLink = (event: MouseEvent<HTMLAnchorElement>, route: MeRoute) => {
    if (!shouldHandleRouteLink(event)) {
      return;
    }

    event.preventDefault();
    onNavigate(route);
  };

  return (
    <>
      <section className={styles.identityPanel}>
        <div className={styles.avatar} aria-hidden="true">
          {resolvedAvatarUrl ? (
            <img src={resolvedAvatarUrl} alt="" className={styles.avatarImage} />
          ) : (
            <span>{displayName.charAt(0).toUpperCase()}</span>
          )}
        </div>
        <div className={styles.identityBody}>
          <p className={styles.kicker}>{t('me.identityKicker')}</p>
          <h1 className={styles.title}>{displayName}</h1>
          <div className={styles.identityMeta}>
            <span>@{accountName}</span>
            {loadedAtLabel ? <span>{t('me.refreshedAt', { time: loadedAtLabel })}</span> : null}
          </div>
          {data.errors.profile ? (
            <p className={styles.identityError} role="status">{data.errors.profile}</p>
          ) : null}
        </div>
        <div className={styles.identityActions} aria-label={t('me.actionsLabel')}>
          <div className={styles.primaryActionSlot}>
            {selfProfilePath ? (
              <a
                className={styles.primaryButton}
                href={selfProfilePath}
                onClick={onRememberSelfProfileSource}
              >
                <Icon icon="mdi:account-arrow-right-outline" size={18} />
                <span>{t('me.openPublicProfile')}</span>
              </a>
            ) : (
              <button type="button" className={styles.primaryButton} disabled>
                <Icon icon="mdi:account-arrow-right-outline" size={18} />
                <span>{t('me.openPublicProfile')}</span>
              </button>
            )}
          </div>
          <div className={styles.secondaryActionGroup}>
            <a
              className={styles.secondaryButton}
              href={buildMePath({ kind: 'content', tab: 'posts', page: 1 })}
              onClick={(event) => handleRouteLink(event, { kind: 'content', tab: 'posts', page: 1 })}
            >
              <Icon icon="mdi:file-document-edit-outline" size={18} />
              <span>{t('me.openContent')}</span>
            </a>
            <a className={styles.secondaryButton} href="/circle">
              <Icon icon="mdi:account-group-outline" size={18} />
              <span>{t('me.openCircle')}</span>
            </a>
            <a
              className={styles.secondaryButton}
              href={buildMePath({ kind: 'attachments', businessType: 'All', keyword: '', page: 1 })}
              onClick={(event) => handleRouteLink(event, {
                kind: 'attachments',
                businessType: 'All',
                keyword: '',
                page: 1,
              })}
            >
              <Icon icon="mdi:paperclip" size={18} />
              <span>{t('me.openAttachments')}</span>
            </a>
            <a className={styles.secondaryButton} href="/notifications">
              <Icon icon="mdi:bell-outline" size={18} />
              <span>{t('me.openNotifications')}</span>
            </a>
            <a className={styles.secondaryButton} href="/pet">
              <Icon icon="mdi:leaf" size={18} />
              <span>{t('me.openPet')}</span>
            </a>
            <button type="button" className={styles.secondaryButton} onClick={logout}>
              <Icon icon="mdi:logout" size={18} />
              <span>{t('auth.logout')}</span>
            </button>
          </div>
        </div>
      </section>

      <div className={styles.toolbar}>
        <div>
          <h2>{t('me.overviewTitle')}</h2>
          <p>{t('me.overviewDescription')}</p>
        </div>
        <button type="button" className={styles.refreshButton} onClick={onRefresh} disabled={loading}>
          <Icon icon={loading ? 'mdi:loading' : 'mdi:refresh'} size={18} className={loading ? styles.spin : undefined} />
          <span>{loading ? t('me.refreshing') : t('me.refresh')}</span>
        </button>
      </div>

      <div className={styles.dashboardWorkspace}>
        <section className={`${styles.detailPanel} ${styles.revisitPanel}`}>
          <div className={styles.panelHeader}>
            <h3>{t('me.revisitTitle')}</h3>
            <a
              href={buildMePath({ kind: 'history', page: 1 })}
              onClick={(event) => handleRouteLink(event, { kind: 'history', page: 1 })}
            >
              {t('me.openHistory')}
            </a>
          </div>
          <p className={styles.revisitDescription}>{t('me.revisitDescription')}</p>
          {data.errors.browse ? (
            <p className={styles.errorText}>{data.errors.browse}</p>
          ) : recentBrowseItem ? (
            <div className={styles.revisitTask}>
              <span className={styles.itemIcon} data-tone="positive">
                <Icon icon="mdi:history" size={18} />
              </span>
              <div className={styles.itemBody}>
                {recentBrowseHref ? (
                  <a href={recentBrowseHref} onClick={(event) => onRememberPublicSource(event, recentBrowseHref)}>
                    {recentBrowseItem.voTitle}
                  </a>
                ) : (
                  <strong>{recentBrowseItem.voTitle}</strong>
                )}
                <span>{recentBrowseItem.voTargetTypeDisplay} · {formatDisplayDateTime(recentBrowseItem.voLastViewTime)}</span>
              </div>
              <span className={styles.viewCount}>{t('me.viewCount', { count: recentBrowseItem.voViewCount })}</span>
            </div>
          ) : (
            <p className={styles.emptyText}>{t('me.revisitEmpty')}</p>
          )}
        </section>

        <aside className={styles.dashboardRail} aria-label={t('me.overviewTitle')}>
          <WebTaskRailDisclosure
            label={t('me.overviewTitle')}
            summary={t('me.overviewDescription')}
          >
            <section className={styles.summaryGrid}>
              <article className={styles.summaryCard}>
                <div className={styles.cardHeader}>
                  <Icon icon="mdi:star-circle-outline" size={22} />
                  <h3>{t('me.experienceTitle')}</h3>
                </div>
                {data.errors.experience ? (
                  <p className={styles.errorText}>{data.errors.experience}</p>
                ) : experience ? (
                  <>
                    <ExperienceBar
                      data={buildExperienceBarData(experience)}
                      size="medium"
                      showLevel={true}
                      showProgress={true}
                      showTooltip={true}
                      animated={true}
                      presentation={experienceBarPresentation}
                    />
                    <div className={styles.metricRow}>
                      <span>{t('me.totalExp')}</span>
                      <strong>{formatExperienceNumber(experience.voTotalExp, language)}</strong>
                    </div>
                    <div className={styles.metricRow}>
                      <span>{t('me.nextLevel')}</span>
                      <strong>{formatExperienceNumber(experience.voExpToNextLevel, language)}</strong>
                    </div>
                  </>
                ) : (
                  <p className={styles.emptyText}>{t('me.experienceEmpty')}</p>
                )}
              </article>

              <article className={styles.summaryCard}>
                <div className={styles.cardHeader}>
                  <Icon icon="mdi:wallet-outline" size={22} />
                  <h3>{t('me.assetTitle')}</h3>
                </div>
                {data.errors.assets ? (
                  <p className={styles.errorText}>{data.errors.assets}</p>
                ) : balance ? (
                  <>
                    <div className={styles.balanceValue}>{formatCoinNumber(balance.voBalance, language)} {t('me.carrotUnit')}</div>
                    <div className={styles.metricRow}>
                      <span>{t('me.frozenBalance')}</span>
                      <strong>{formatCoinNumber(balance.voFrozenBalance, language)} {t('me.carrotUnit')}</strong>
                    </div>
                    <div className={styles.metricRow}>
                      <span>{t('me.totalEarned')}</span>
                      <strong>{formatCoinNumber(balance.voTotalEarned, language)}</strong>
                    </div>
                  </>
                ) : (
                  <p className={styles.emptyText}>{t('me.assetEmpty')}</p>
                )}
              </article>

              <article className={styles.summaryCard}>
                <div className={styles.cardHeader}>
                  <Icon icon="mdi:history" size={22} />
                  <h3>{t('me.revisitTitle')}</h3>
                </div>
                {data.errors.browse ? (
                  <p className={styles.errorText}>{data.errors.browse}</p>
                ) : data.browseHistory.length > 0 ? (
                  <>
                    <div className={styles.balanceValue}>{data.browseHistory.length}</div>
                    <p className={styles.emptyText}>{t('me.revisitDescription')}</p>
                  </>
                ) : (
                  <p className={styles.emptyText}>{t('me.revisitEmpty')}</p>
                )}
              </article>

              <article className={styles.summaryCard}>
                <div className={styles.cardHeader}>
                  <Icon icon="mdi:leaf" size={22} />
                  <h3>{t('me.petTitle')}</h3>
                </div>
                {data.errors.pet ? (
                  <p className={styles.errorText}>{data.errors.pet}</p>
                ) : pet ? (
                  <>
                    <div className={styles.balanceValue}>{pet.voName}</div>
                    <div className={styles.metricRow}>
                      <span>{t('me.petMood')}</span>
                      <strong>{t(resolvePetMoodTranslationKey(pet.voMood))}</strong>
                    </div>
                    <div className={styles.metricRow}>
                      <span>{t('me.petStage')}</span>
                      <strong>{t(resolvePetGrowthStageTranslationKey(pet.voGrowthStage))}</strong>
                    </div>
                  </>
                ) : (
                  <p className={styles.emptyText}>{t('me.petEmpty')}</p>
                )}
              </article>
            </section>
          </WebTaskRailDisclosure>
        </aside>

        <section className={styles.detailGrid}>
        <article className={styles.detailPanel}>
          <div className={styles.panelHeader}>
            <h3>{t('me.recentExperience')}</h3>
            <a
              href={buildMePath({ kind: 'experience', page: 1 })}
              onClick={(event) => handleRouteLink(event, { kind: 'experience', page: 1 })}
            >
              {t('me.openExperienceDetail')}
            </a>
          </div>
          {data.errors.experienceTransactions ? (
            <p className={styles.errorText}>{data.errors.experienceTransactions}</p>
          ) : data.expTransactions.length > 0 ? (
            <div className={styles.itemList}>
              {data.expTransactions.map((transaction) => (
                <div key={transaction.voId} className={styles.listItem}>
                  <div className={styles.itemIcon} data-tone={transaction.voExpAmount >= 0 ? 'positive' : 'negative'}>
                    <Icon icon={transaction.voExpAmount >= 0 ? 'mdi:plus' : 'mdi:minus'} size={16} />
                  </div>
                  <div className={styles.itemBody}>
                    <strong>{formatExperienceType(transaction.voExpType, t)}</strong>
                    <span>{formatDisplayDateTime(transaction.voCreateTime)}</span>
                  </div>
                  <div className={styles.itemAmount}>
                    {formatExperienceSignedNumber(transaction.voExpAmount, language)}
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className={styles.emptyText}>{t('me.recentExperienceEmpty')}</p>
          )}
        </article>

        <article className={styles.detailPanel}>
          <div className={styles.panelHeader}>
            <h3>{t('me.recentAssets')}</h3>
            <a
              href={buildMePath({ kind: 'assets-transactions' })}
              onClick={(event) => handleRouteLink(event, { kind: 'assets-transactions' })}
            >
              {t('me.openFullWallet')}
            </a>
          </div>
          {data.errors.assets ? (
            <p className={styles.errorText}>{data.errors.assets}</p>
          ) : data.coinTransactions.length > 0 ? (
            <div className={styles.itemList}>
              {data.coinTransactions.map((transaction) => {
                const direction = resolveTransactionDirection(transaction, userId);
                return (
                  <div key={transaction.voId} className={styles.listItem}>
                    <div className={styles.itemIcon} data-tone={direction === 'in' ? 'positive' : 'negative'}>
                      <Icon icon={direction === 'in' ? 'mdi:arrow-up' : 'mdi:arrow-down'} size={16} />
                    </div>
                    <div className={styles.itemBody}>
                      <strong>{formatTransactionType(transaction.voTransactionType, t)}</strong>
                      <span>{formatDisplayDateTime(transaction.voCreateTime)}</span>
                    </div>
                    <div className={styles.itemAmount}>
                      {direction === 'in' ? '+' : '-'}{formatCoinNumber(absoluteCoinValue(transaction.voAmount), language)}
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <p className={styles.emptyText}>{t('me.recentAssetsEmpty')}</p>
          )}
        </article>

        <article className={styles.detailPanel}>
          <div className={styles.panelHeader}>
            <h3>{t('me.recentBrowse')}</h3>
            <a
              href={buildMePath({ kind: 'history', page: 1 })}
              onClick={(event) => handleRouteLink(event, { kind: 'history', page: 1 })}
            >
              {t('me.openHistory')}
            </a>
          </div>
          {data.errors.browse ? (
            <p className={styles.errorText}>{data.errors.browse}</p>
          ) : data.browseHistory.length > 0 ? (
            <div className={styles.itemList}>
              {data.browseHistory.map((item) => {
                const href = getBrowseHistoryHref(item);
                return (
                  <div key={item.voId} className={styles.browseItem}>
                    <div className={styles.itemBody}>
                      {href ? (
                        <a href={href} onClick={(event) => onRememberPublicSource(event, href)}>
                          {item.voTitle}
                        </a>
                      ) : (
                        <strong>{item.voTitle}</strong>
                      )}
                      <span>{item.voTargetTypeDisplay} · {formatDisplayDateTime(item.voLastViewTime)}</span>
                    </div>
                    <span className={styles.viewCount}>{t('me.viewCount', { count: item.voViewCount })}</span>
                  </div>
                );
              })}
            </div>
          ) : (
            <p className={styles.emptyText}>{t('me.recentBrowseEmpty')}</p>
          )}
        </article>
        </section>
      </div>
    </>
  );
};

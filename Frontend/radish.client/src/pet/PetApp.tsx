import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import {
  carePet,
  claimPet,
  getMyPet,
  getPetLogs,
  updatePetProfile,
  type PetCareActionState,
  type PetCareActionType,
  type PetProfile,
  type PetStatLog,
} from '@/api/pet';
import { getApiBaseUrl } from '@/config/env';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { redirectToLogin } from '@/services/auth';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import { buildPetReturnPath } from '@/services/authReturnPath';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { DEFAULT_TIME_ZONE, formatDateTimeByTimeZone, getBrowserTimeZoneId } from '@/utils/dateTime';
import { log } from '@/utils/logger';
import {
  getCooldownDisplayParts,
  getPetLogStatDeltas,
  resolvePetActionAvailability,
  resolvePetStatLevel,
  resolvePetStatusInsight,
  type PetStatDelta,
  type PetStatKey,
} from './petPresentation';
import { buildPetPath } from './petRouteState';
import styles from './PetApp.module.css';

interface PetPageData {
  pet: PetProfile | null;
  logs: PetStatLog[];
  loadedAt: string | null;
}

interface FeedbackNotice {
  kind: 'success';
  titleKey: string;
  descriptionKey: string;
  values?: Record<string, number | string>;
  deltas?: PetStatDelta[];
}

interface LoadPetDataOptions {
  silent?: boolean;
}

const initialPageData: PetPageData = {
  pet: null,
  logs: [],
  loadedAt: null,
};

const actionIcons: Record<string, string> = {
  feed: 'mdi:food-apple-outline',
  clean: 'mdi:sparkles',
  play: 'mdi:hand-heart-outline',
  rest: 'mdi:sleep',
};

const statLabelKeys: Record<PetStatKey, string> = {
  voSatiety: 'pet.stat.satiety',
  voCleanliness: 'pet.stat.cleanliness',
  voEnergy: 'pet.stat.energy',
};

const statKeys = [
  { key: 'voSatiety', labelKey: statLabelKeys.voSatiety, icon: 'mdi:food-apple-outline' },
  { key: 'voCleanliness', labelKey: statLabelKeys.voCleanliness, icon: 'mdi:sparkles' },
  { key: 'voEnergy', labelKey: statLabelKeys.voEnergy, icon: 'mdi:lightning-bolt-outline' },
] as const;

const actionPreviewKeys: Record<string, string> = {
  feed: 'pet.care.preview.feed',
  clean: 'pet.care.preview.clean',
  play: 'pet.care.preview.play',
  rest: 'pet.care.preview.rest',
};

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.trim() ? error.message : fallback;
}

function clampPercent(value: number): number {
  return Math.max(0, Math.min(100, Number.isFinite(value) ? value : 0));
}

function buildIdempotencyKey(actionType: PetCareActionType): string {
  const randomPart = typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `web:${actionType}:${randomPart}`;
}

function formatSignedValue(value: number): string {
  return value > 0 ? `+${value}` : `${value}`;
}

export const PetApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [pageData, setPageData] = useState<PetPageData>(initialPageData);
  const [loading, setLoading] = useState(false);
  const [claimName, setClaimName] = useState('');
  const [profileName, setProfileName] = useState('');
  const [isPublic, setIsPublic] = useState(false);
  const [savingProfile, setSavingProfile] = useState(false);
  const [claiming, setClaiming] = useState(false);
  const [activeAction, setActiveAction] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<FeedbackNotice | null>(null);
  const [nowTick, setNowTick] = useState(() => Date.now());

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((err) => {
        log.warn('PetApp', '电子宠物登录态初始化失败', err);
        return null;
      })
      .finally(() => {
        if (!cancelled) {
          setAuthReady(true);
        }
      });

    return () => {
      cancelled = true;
      cleanup();
    };
  }, [apiBaseUrl]);

  useEffect(() => {
    document.title = `${t('pet.title')} · Radish`;
  }, [t]);

  useEffect(() => {
    const canonicalPath = buildPetPath();
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== canonicalPath) {
      window.history.replaceState(window.history.state, '', canonicalPath);
    }
  }, []);

  useEffect(() => {
    if (!authReady || loggedIn || redirecting) {
      return;
    }

    setRedirecting(true);
    redirectToLogin({
      returnPath: buildPetReturnPath()
    });
  }, [authReady, loggedIn, redirecting]);

  const loadPetData = useCallback(async (options: LoadPetDataOptions = {}) => {
    if (!loggedIn) {
      return;
    }

    setLoading(true);
    if (!options.silent) {
      setError(null);
      setFeedback(null);
    }
    try {
      const pet = await getMyPet();
      const logs = pet ? (await getPetLogs(1, 8)).voItems : [];
      setPageData({
        pet,
        logs,
        loadedAt: new Date().toISOString(),
      });
      setNowTick(Date.now());
      setProfileName(pet?.voName ?? '');
      setIsPublic(pet?.voIsPublic ?? false);
    } catch (err) {
      const message = getErrorMessage(err, t('pet.error.load'));
      if (!options.silent) {
        setError(message);
      }
      log.warn('PetApp', '加载电子宠物失败', err);
    } finally {
      setLoading(false);
    }
  }, [loggedIn, t]);

  useEffect(() => {
    if (authReady && loggedIn) {
      void loadPetData();
    }
  }, [authReady, loadPetData, loggedIn]);

  useEffect(() => {
    const actions = pageData.pet?.voCareActions ?? [];
    const hasCooldown = actions.some(action => resolvePetActionAvailability(action, Date.now()).kind === 'cooldown');
    if (!hasCooldown) {
      return;
    }

    const timerId = window.setInterval(() => {
      setNowTick(Date.now());
    }, 30000);
    return () => window.clearInterval(timerId);
  }, [pageData.pet?.voCareActions]);

  const handleClaim = useCallback(async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setClaiming(true);
    setError(null);
    setFeedback(null);
    try {
      const pet = await claimPet({ name: claimName.trim() || undefined });
      setPageData({
        pet,
        logs: [],
        loadedAt: new Date().toISOString(),
      });
      setProfileName(pet.voName);
      setIsPublic(pet.voIsPublic);
      setClaimName('');
      setNowTick(Date.now());
      setFeedback({
        kind: 'success',
        titleKey: 'pet.feedback.claimed.title',
        descriptionKey: 'pet.feedback.claimed.description',
        values: { name: pet.voName },
      });
    } catch (err) {
      const message = getErrorMessage(err, t('pet.error.claim'));
      setError(message);
      log.warn('PetApp', '领取电子宠物失败', err);
    } finally {
      setClaiming(false);
    }
  }, [claimName, t]);

  const handleSaveProfile = useCallback(async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!pageData.pet) {
      return;
    }

    setSavingProfile(true);
    setError(null);
    setFeedback(null);
    try {
      const pet = await updatePetProfile({
        name: profileName.trim() || pageData.pet.voName,
        isPublic,
      });
      setPageData(current => ({
        ...current,
        pet,
        loadedAt: new Date().toISOString(),
      }));
      setProfileName(pet.voName);
      setIsPublic(pet.voIsPublic);
      setNowTick(Date.now());
      setFeedback({
        kind: 'success',
        titleKey: 'pet.feedback.profileSaved.title',
        descriptionKey: 'pet.feedback.profileSaved.description',
      });
    } catch (err) {
      const message = getErrorMessage(err, t('pet.error.save'));
      setError(message);
      log.warn('PetApp', '保存电子宠物资料失败', err);
    } finally {
      setSavingProfile(false);
    }
  }, [isPublic, pageData.pet, profileName, t]);

  const handleCare = useCallback(async (action: PetCareActionState) => {
    const availability = resolvePetActionAvailability(action, nowTick);
    if (availability.kind !== 'ready' || activeAction) {
      return;
    }

    setActiveAction(action.voActionType);
    setError(null);
    setFeedback(null);
    try {
      const result = await carePet({
        actionType: action.voActionType,
        idempotencyKey: buildIdempotencyKey(action.voActionType),
      });
      setPageData(current => ({
        pet: result.voPet,
        logs: [result.voLog, ...current.logs.filter(item => item.voId !== result.voLog.voId)].slice(0, 8),
        loadedAt: new Date().toISOString(),
      }));
      setProfileName(result.voPet.voName);
      setIsPublic(result.voPet.voIsPublic);
      setNowTick(Date.now());
      setFeedback({
        kind: 'success',
        titleKey: 'pet.feedback.care.title',
        descriptionKey: 'pet.feedback.care.description',
        values: {
          action: result.voLog.voActionName,
          message: result.voMessage || result.voLog.voMessage,
          growth: result.voLog.voGrowthDelta,
        },
        deltas: getPetLogStatDeltas(result.voLog),
      });
    } catch (err) {
      const message = getErrorMessage(err, t('pet.error.care'));
      setError(message);
      log.warn('PetApp', '照顾电子宠物失败', err);
      void loadPetData({ silent: true });
    } finally {
      setActiveAction(null);
    }
  }, [activeAction, loadPetData, nowTick, t]);

  const formatCooldownLabel = useCallback((cooldownMs: number) => {
    const parts = getCooldownDisplayParts(cooldownMs);
    if (!parts) {
      return null;
    }

    return t('pet.care.cooldownRemaining', {
      value: parts.value,
      unit: t(`pet.care.cooldownUnit.${parts.unit}`),
    });
  }, [t]);

  const renderFeedback = () => {
    if (!feedback) {
      return null;
    }

    return (
      <section className={styles.feedbackBanner} data-kind={feedback.kind} aria-live="polite">
        <Icon icon="mdi:check-circle-outline" size={20} />
        <div className={styles.feedbackBody}>
          <strong>{t(feedback.titleKey, feedback.values)}</strong>
          <span>{t(feedback.descriptionKey, feedback.values)}</span>
          {feedback.deltas?.length ? (
            <div className={styles.feedbackDeltas}>
              {feedback.deltas.map(delta => (
                <em key={delta.statKey}>
                  {t(statLabelKeys[delta.statKey])} {formatSignedValue(delta.value)}
                </em>
              ))}
            </div>
          ) : null}
        </div>
      </section>
    );
  };

  const renderStatusPanel = (title: string, description: string, icon = 'mdi:leaf') => (
    <section className={styles.statusPanel}>
      <Icon icon={icon} size={24} />
      <div>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
    </section>
  );

  const renderClaimPanel = () => (
    <section className={styles.claimPanel}>
      <div className={styles.petPortrait} data-mood="calm" aria-hidden="true">
        <span>萝</span>
      </div>
      <div className={styles.claimBody}>
        <p className={styles.kicker}>{t('pet.claim.kicker')}</p>
        <h1>{t('pet.claim.title')}</h1>
        <form className={styles.claimForm} onSubmit={handleClaim}>
          <label>
            <span>{t('pet.nameLabel')}</span>
            <input
              value={claimName}
              maxLength={40}
              onChange={(event) => setClaimName(event.target.value)}
              placeholder={t('pet.claim.placeholder')}
            />
          </label>
          <button type="submit" className={styles.primaryButton} disabled={claiming}>
            <Icon icon={claiming ? 'mdi:loading' : 'mdi:leaf'} size={18} className={claiming ? styles.spin : undefined} />
            <span>{claiming ? t('pet.claiming') : t('pet.claim.action')}</span>
          </button>
        </form>
      </div>
    </section>
  );

  const renderPetDashboard = (pet: PetProfile) => {
    const insight = resolvePetStatusInsight(pet);
    const loadedAtLabel = pageData.loadedAt
      ? formatDateTimeByTimeZone(pageData.loadedAt, displayTimeZone)
      : null;

    return (
      <>
        <section className={styles.heroPanel}>
          <div className={styles.petPortrait} data-mood={pet.voMood} data-intent={insight.intent} aria-hidden="true">
            <span>萝</span>
          </div>
          <div className={styles.heroBody}>
            <p className={styles.kicker}>{t('pet.hero.kicker')}</p>
            <h1>{pet.voName}</h1>
            <div className={styles.heroMeta}>
              <span>{pet.voGrowthStageName}</span>
              <span>{pet.voMoodDisplay}</span>
              <span>{t('pet.growthValue', { value: pet.voGrowthValue })}</span>
              {loadedAtLabel ? <span>{t('pet.refreshedAt', { time: loadedAtLabel })}</span> : null}
            </div>
          </div>
          <div className={styles.heroActions}>
            <a className={styles.secondaryButton} href="/me">
              <Icon icon="mdi:account-heart-outline" size={18} />
              <span>{t('pet.openMe')}</span>
            </a>
            <button type="button" className={styles.secondaryButton} onClick={() => void loadPetData()} disabled={loading}>
              <Icon icon={loading ? 'mdi:loading' : 'mdi:refresh'} size={18} className={loading ? styles.spin : undefined} />
              <span>{loading ? t('pet.refreshing') : t('pet.refresh')}</span>
            </button>
          </div>
        </section>

        <section className={styles.insightPanel} data-intent={insight.intent}>
          <div className={styles.insightIcon}>
            <Icon icon={insight.icon} size={22} />
          </div>
          <div>
            <h2>{t(insight.titleKey)}</h2>
            <p>{t(insight.descriptionKey)}</p>
          </div>
        </section>

        <section className={styles.statGrid}>
          {statKeys.map((stat) => {
            const value = clampPercent(pet[stat.key]);
            const level = resolvePetStatLevel(value);
            return (
              <article className={styles.statCard} data-level={level} key={stat.key}>
                <div className={styles.statHeader}>
                  <Icon icon={stat.icon} size={20} />
                  <span>{t(stat.labelKey)}</span>
                  <strong>{value}</strong>
                </div>
                <div className={styles.statTrack} aria-hidden="true">
                  <span style={{ width: `${value}%` }} />
                </div>
                <p className={styles.statLevel}>{t(`pet.stat.level.${level}`)}</p>
              </article>
            );
          })}
        </section>

        <section className={styles.actionPanel}>
          <div className={styles.sectionHeader}>
            <div>
              <h2>{t('pet.care.title')}</h2>
              <p>{t('pet.care.description')}</p>
            </div>
          </div>
          <div className={styles.actionGrid}>
            {pet.voCareActions.map((action) => {
              const pending = activeAction === action.voActionType;
              const availability = resolvePetActionAvailability(action, nowTick);
              const cooldownLabel = availability.kind === 'cooldown'
                ? formatCooldownLabel(availability.cooldownMs)
                : null;
              const nextLabel = action.voNextAvailableAt
                ? formatDateTimeByTimeZone(action.voNextAvailableAt, displayTimeZone)
                : null;
              const availabilityKey = pending ? 'pending' : availability.kind;

              return (
                <button
                  type="button"
                  key={action.voActionType}
                  className={styles.actionButton}
                  data-availability={availabilityKey}
                  disabled={availability.kind !== 'ready' || !!activeAction}
                  onClick={() => void handleCare(action)}
                >
                  <Icon
                    icon={pending ? 'mdi:loading' : actionIcons[action.voActionType] ?? 'mdi:hand-heart-outline'}
                    size={22}
                    className={pending ? styles.spin : undefined}
                  />
                  <strong>{action.voActionName}</strong>
                  <span>
                    {action.voRemainingToday > 0
                      ? t('pet.care.remaining', { count: action.voRemainingToday })
                      : t('pet.care.usedUp')}
                  </span>
                  <small className={styles.actionStatus}>{t(`pet.care.availability.${availabilityKey}`)}</small>
                  {cooldownLabel ? <small>{cooldownLabel}</small> : null}
                  {availability.kind === 'cooldown' && nextLabel ? <small>{t('pet.care.nextAt', { time: nextLabel })}</small> : null}
                  <small className={styles.actionPreview}>{t(actionPreviewKeys[action.voActionType] ?? 'pet.care.preview.default')}</small>
                </button>
              );
            })}
          </div>
        </section>

        <section className={styles.contentGrid}>
          <article className={styles.profilePanel}>
            <div className={styles.sectionHeader}>
              <h2>{t('pet.profile.title')}</h2>
            </div>
            <form className={styles.profileForm} onSubmit={handleSaveProfile}>
              <label>
                <span>{t('pet.nameLabel')}</span>
                <input
                  value={profileName}
                  maxLength={40}
                  onChange={(event) => setProfileName(event.target.value)}
                />
              </label>
              <label className={styles.switchRow}>
                <input
                  type="checkbox"
                  checked={isPublic}
                  onChange={(event) => setIsPublic(event.target.checked)}
                />
                <span>{t('pet.profile.public')}</span>
              </label>
              <button type="submit" className={styles.primaryButton} disabled={savingProfile}>
                <Icon icon={savingProfile ? 'mdi:loading' : 'mdi:content-save-outline'} size={18} className={savingProfile ? styles.spin : undefined} />
                <span>{savingProfile ? t('pet.profile.saving') : t('pet.profile.save')}</span>
              </button>
            </form>
          </article>

          <article className={styles.logPanel}>
            <div className={styles.sectionHeader}>
              <h2>{t('pet.logs.title')}</h2>
            </div>
            {pageData.logs.length > 0 ? (
              <div className={styles.logList}>
                {pageData.logs.map((item) => (
                  <div className={styles.logItem} key={item.voId}>
                    <div className={styles.logIcon}>
                      <Icon icon={actionIcons[item.voActionType] ?? 'mdi:history'} size={18} />
                    </div>
                    <div>
                      <strong>{item.voMessage || item.voActionName}</strong>
                      <span>{formatDateTimeByTimeZone(item.voCreateTime, displayTimeZone)}</span>
                    </div>
                    <em>{t('pet.logs.growthDelta', { value: item.voGrowthDelta })}</em>
                  </div>
                ))}
              </div>
            ) : (
              <p className={styles.emptyText}>{t('pet.logs.empty')}</p>
            )}
          </article>
        </section>
      </>
    );
  };

  const renderContent = () => {
    if (!authReady || !loggedIn) {
      return renderStatusPanel(
        t('pet.title'),
        t(redirecting ? 'pet.auth.redirecting' : 'pet.auth.loading'),
        'mdi:account-clock-outline'
      );
    }

    if (loading && !pageData.loadedAt) {
      return renderStatusPanel(t('pet.loadingTitle'), t('pet.loadingDescription'), 'mdi:progress-clock');
    }

    return (
      <>
        {error ? <p className={styles.errorBanner}>{error}</p> : null}
        {renderFeedback()}
        {pageData.pet ? renderPetDashboard(pageData.pet) : renderClaimPanel()}
      </>
    );
  };

  return (
    <div className={styles.page}>
      <PublicShellHeader
        brandMark="萝"
        brandName={t('pet.title')}
        brandSubline={t('pet.shellSubline')}
        discoverLabel={t('public.shell.discoverAction')}
        circleLabel={t('public.shell.circleAction')}
        desktopLabel={t('public.shell.desktopAction')}
        onBrandClick={() => {
          window.location.href = buildPetPath();
        }}
        onNavigateToDiscover={() => {
          window.location.href = '/discover';
        }}
      />

      <main className={styles.main}>
        {renderContent()}
      </main>
    </div>
  );
};

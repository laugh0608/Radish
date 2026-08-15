import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@radish/ui/button';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { Icon } from '@radish/ui/icon';
import { Input } from '@radish/ui/input';
import { Modal } from '@radish/ui/modal';
import { Select } from '@radish/ui/select';
import { getBalance, type UserBalance } from '@/api/coin';
import {
  getMyProfile,
  updateMyProfile,
  type LongId,
  type MyProfileInfo,
} from '@/api/user';
import { useUserStore } from '@/stores/userStore';
import { getIntlLocale } from '@/locales/language';
import { log } from '@/utils/logger';
import { buildTimeZoneOptions, formatDateTimeByTimeZone, resolveTimeZoneId } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import {
  resolveFailedSnapshotState,
  type ProfileAuthorityState,
  type ProfileStats,
} from '../profileAuthority';
import { reuseInFlightRequest } from '../requestDedup';
import styles from './UserInfoCard.module.css';

const AvatarUploadModal = lazy(() =>
  import('./AvatarUploadModal').then((module) => ({ default: module.AvatarUploadModal }))
);

interface UserInfoCardProps {
  userId: LongId;
  userName: string;
  stats?: ProfileStats;
  statsState: ProfileAuthorityState;
  statsError?: string;
  onRetryStats: () => void;
  timeState: ProfileAuthorityState;
  timeError?: string;
  onRetryTime: () => void;
  apiBaseUrl: string;
  displayTimeZone: string;
  systemTimeZone: string;
  displayTimeFormat: string;
  savingTimeZone?: boolean;
  onTimeZoneChange: (timeZoneId: string) => Promise<void>;
  onDirtyChange: (dirty: boolean) => void;
  onBusyChange: (busy: boolean) => void;
}

function resolveUrl(apiBaseUrl: string, url: string | null | undefined): string | null {
  return resolveMediaUrl(url, apiBaseUrl);
}

function formatCoinAmount(
  amount: number | string | null | undefined,
  language: string,
  translate: (key: string) => string
): string {
  const normalized = String(amount ?? 0).trim();
  const value = /^-?\d+$/.test(normalized) ? BigInt(normalized) : 0n;
  const negative = value < 0n;
  const abs = negative ? -value : value;
  const whiteRadish = abs / 1000n;
  const carrot = abs % 1000n;
  const formatter = new Intl.NumberFormat(getIntlLocale(language));
  const parts: string[] = [];

  if (whiteRadish > 0n) {
    parts.push(`${formatter.format(whiteRadish)} ${translate('profile.coin.whiteRadish')}`);
  }
  if (carrot > 0n || parts.length === 0) {
    parts.push(`${formatter.format(carrot)} ${translate('profile.coin.carrot')}`);
  }

  const result = parts.join(' ');
  return negative ? `-${result}` : result;
}

function buildOwnProfileRequestKey(apiBaseUrl: string, userId: LongId): string {
  return `own-profile|${apiBaseUrl}|${userId}`;
}

export const UserInfoCard = ({
  userId,
  userName,
  stats,
  statsState,
  statsError,
  onRetryStats,
  timeState,
  timeError,
  onRetryTime,
  apiBaseUrl,
  displayTimeZone,
  systemTimeZone,
  displayTimeFormat,
  savingTimeZone = false,
  onTimeZoneChange,
  onDirtyChange,
  onBusyChange,
}: UserInfoCardProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const { setUser, tenantId, roles, permissions } = useUserStore();
  const profileRef = useRef<MyProfileInfo | null>(null);
  const balanceRef = useRef<UserBalance | null>(null);
  const editingRef = useRef(false);
  const loadRequestIdRef = useRef(0);

  const [profile, setProfile] = useState<MyProfileInfo | null>(null);
  const [coinBalance, setCoinBalance] = useState<UserBalance | null>(null);
  const [profileState, setProfileState] = useState<ProfileAuthorityState>('loading');
  const [balanceState, setBalanceState] = useState<ProfileAuthorityState>('loading');
  const [profileError, setProfileError] = useState<string>();
  const [balanceError, setBalanceError] = useState<string>();
  const [refreshing, setRefreshing] = useState(false);
  const [savingProfile, setSavingProfile] = useState(false);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [isAvatarModalOpen, setIsAvatarModalOpen] = useState(false);
  const [avatarBusy, setAvatarBusy] = useState(false);
  const [avatarLoadError, setAvatarLoadError] = useState(false);
  const [editUserName, setEditUserName] = useState('');
  const [editUserEmail, setEditUserEmail] = useState('');
  const [editAge, setEditAge] = useState('');
  const [editAddress, setEditAddress] = useState('');
  const [customTimeZone, setCustomTimeZone] = useState(displayTimeZone);
  const [timeZoneError, setTimeZoneError] = useState<string | null>(null);

  const presetTimeZoneOptions = useMemo(
    () => buildTimeZoneOptions(systemTimeZone, displayTimeZone),
    [displayTimeZone, systemTimeZone],
  );

  const applyProfileSnapshot = useCallback((nextProfile: MyProfileInfo, replaceDraft: boolean) => {
    const currentDisplayName = resolveVisibleUserDisplayName(nextProfile, userName);
    profileRef.current = nextProfile;
    setProfile(nextProfile);
    setProfileState('ready');
    setProfileError(undefined);
    setUser({
      userId: String(userId),
      displayName: currentDisplayName,
      userName: currentDisplayName,
      displayHandle: nextProfile.voDisplayHandle || undefined,
      publicId: nextProfile.voPublicId || undefined,
      publicIndex: typeof nextProfile.voPublicIndex === 'number'
        ? String(nextProfile.voPublicIndex)
        : nextProfile.voPublicIndex || undefined,
      nickname: currentDisplayName,
      tenantId,
      roles: roles || ['User'],
      permissions: permissions || [],
      avatarUrl: nextProfile.voAvatarUrl || undefined,
      avatarThumbnailUrl: nextProfile.voAvatarThumbnailUrl || undefined,
    });

    if (replaceDraft || !editingRef.current) {
      setEditUserName(currentDisplayName);
      setEditUserEmail(nextProfile.voUserEmail || '');
      setEditAge(String(nextProfile.voAge ?? ''));
      setEditAddress(nextProfile.voAddress || '');
    }
  }, [permissions, roles, setUser, tenantId, userId, userName]);

  const loadProfile = useCallback(async (reuseInFlight = false, replaceDraft = false) => {
    const requestId = ++loadRequestIdRef.current;
    const hadProfile = profileRef.current !== null;
    const hadBalance = balanceRef.current !== null;
    setRefreshing(true);
    if (!hadProfile) {
      setProfileState('loading');
    }
    if (!hadBalance) {
      setBalanceState('loading');
    }

    const request = async () => Promise.allSettled([
      getMyProfile(t),
      getBalance(t),
    ]);
    const [profileResult, balanceResult] = reuseInFlight
      ? await reuseInFlightRequest(buildOwnProfileRequestKey(apiBaseUrl, userId), request)
      : await request();

    if (requestId !== loadRequestIdRef.current) {
      return;
    }

    if (profileResult.status === 'fulfilled') {
      applyProfileSnapshot(profileResult.value, replaceDraft);
    } else {
      const errorMessage = profileResult.reason instanceof Error
        ? profileResult.reason.message
        : t('profile.authority.profileLoadFailed');
      log.error('UserInfoCard', '加载用户资料失败:', profileResult.reason);
      setProfileError(errorMessage);
      setProfileState(resolveFailedSnapshotState(hadProfile));
    }

    if (balanceResult.status === 'fulfilled') {
      balanceRef.current = balanceResult.value;
      setCoinBalance(balanceResult.value);
      setBalanceState('ready');
      setBalanceError(undefined);
    } else {
      const errorMessage = balanceResult.reason instanceof Error
        ? balanceResult.reason.message
        : t('profile.authority.balanceLoadFailed');
      log.error('UserInfoCard', '加载余额摘要失败:', balanceResult.reason);
      setBalanceError(errorMessage);
      setBalanceState(resolveFailedSnapshotState(hadBalance));
    }

    setRefreshing(false);
  }, [apiBaseUrl, applyProfileSnapshot, t, userId]);

  useEffect(() => {
    void loadProfile(true, true);
    return () => {
      loadRequestIdRef.current += 1;
    };
  }, [loadProfile]);

  useEffect(() => {
    editingRef.current = isEditOpen;
  }, [isEditOpen]);

  useEffect(() => {
    setCustomTimeZone(displayTimeZone);
    setTimeZoneError(null);
  }, [displayTimeZone]);

  useEffect(() => {
    setAvatarLoadError(false);
  }, [profile?.voAvatarThumbnailUrl, profile?.voAvatarUrl]);

  const authoritativeDisplayName = profile
    ? resolveVisibleUserDisplayName(profile, userName)
    : '';
  const profileDirty = isEditOpen && profile !== null && (
    editUserName !== authoritativeDisplayName ||
    editUserEmail !== (profile.voUserEmail || '') ||
    editAge !== String(profile.voAge ?? '') ||
    editAddress !== (profile.voAddress || '')
  );
  const timeZoneDirty = timeState === 'ready' && customTimeZone !== displayTimeZone;
  const combinedDirty = profileDirty || timeZoneDirty;
  const combinedBusy = savingProfile || savingTimeZone || avatarBusy;

  useEffect(() => {
    onDirtyChange(combinedDirty);
  }, [combinedDirty, onDirtyChange]);

  useEffect(() => {
    onBusyChange(combinedBusy);
  }, [combinedBusy, onBusyChange]);

  useEffect(() => () => {
    onDirtyChange(false);
    onBusyChange(false);
  }, [onBusyChange, onDirtyChange]);

  const avatarSrc = useMemo(() => {
    const url = profile?.voAvatarThumbnailUrl || profile?.voAvatarUrl;
    return resolveUrl(apiBaseUrl, url) || undefined;
  }, [apiBaseUrl, profile?.voAvatarThumbnailUrl, profile?.voAvatarUrl]);
  const avatarImageSrc = avatarLoadError ? undefined : avatarSrc;
  const profileDisplayName = profile
    ? resolveVisibleUserDisplayName(profile, userName)
    : userName;
  const profileDisplayHandle = profile
    ? resolveVisibleUserHandle(profile, profileDisplayName)
    : null;

  const handleOpenEdit = () => {
    if (!profile || profileState !== 'ready' || combinedBusy) {
      return;
    }

    setEditUserName(resolveVisibleUserDisplayName(profile, userName));
    setEditUserEmail(profile.voUserEmail || '');
    setEditAge(String(profile.voAge ?? ''));
    setEditAddress(profile.voAddress || '');
    setSaveError(null);
    setIsEditOpen(true);
  };

  const closeEdit = () => {
    if (savingProfile) {
      return;
    }
    if (profileDirty && !window.confirm(t('profile.info.discardConfirm'))) {
      return;
    }

    setIsEditOpen(false);
    setConfirmOpen(false);
    setSaveError(null);
  };

  const handleSave = async () => {
    if (!profileDirty || profileState !== 'ready' || savingProfile) {
      setConfirmOpen(false);
      return;
    }

    setConfirmOpen(false);
    setSaveError(null);
    const ageNumber = editAge.trim() ? Number(editAge.trim()) : undefined;

    try {
      setSavingProfile(true);
      await updateMyProfile({
        userName: editUserName.trim() || undefined,
        userEmail: editUserEmail.trim() || undefined,
        age: Number.isFinite(ageNumber) ? ageNumber : undefined,
        address: editAddress.trim() || undefined,
      }, t);
      setIsEditOpen(false);
      await loadProfile(false, true);
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : t('profile.info.saveFailed'));
    } finally {
      setSavingProfile(false);
    }
  };

  const handleApplyTimeZone = async () => {
    if (timeState !== 'ready' || savingTimeZone || !timeZoneDirty) {
      return;
    }

    const selectedTimeZone = resolveTimeZoneId(customTimeZone, systemTimeZone);
    setTimeZoneError(null);
    try {
      await onTimeZoneChange(selectedTimeZone);
    } catch (error) {
      setTimeZoneError(error instanceof Error ? error.message : t('profile.info.timeZoneSaveFailed'));
    }
  };

  const handleAvatarUploadSuccess = async () => {
    setIsAvatarModalOpen(false);
    await loadProfile(false, false);
  };

  const renderAuthorityState = (
    state: ProfileAuthorityState,
    error: string | undefined,
    retry: () => void,
    scope: 'profile' | 'balance' | 'stats' | 'time',
  ) => {
    if (state === 'ready') {
      return null;
    }

    return (
      <div className={`${styles.authorityState} ${styles[`authorityState_${state}`]}`} role={state === 'loading' ? 'status' : 'alert'}>
        <div>
          <strong>{t(`profile.authority.${scope}.${state}Title`)}</strong>
          <span>{error || t(`profile.authority.${scope}.${state}Description`)}</span>
        </div>
        {state !== 'loading' ? (
          <Button size="small" variant="secondary" onClick={retry} disabled={refreshing}>
            {t('profile.authority.retry')}
          </Button>
        ) : null}
      </div>
    );
  };

  return (
    <div className={styles.card}>
      {renderAuthorityState(profileState, profileError, () => void loadProfile(false, true), 'profile')}

      {profile ? (
        <>
          <div className={styles.header}>
            <button
              type="button"
              className={styles.avatarSection}
              onClick={() => setIsAvatarModalOpen(true)}
              disabled={profileState !== 'ready' || combinedBusy}
            >
              <div className={styles.avatar}>
                {avatarImageSrc ? (
                  <img
                    className={styles.avatarImg}
                    src={avatarImageSrc}
                    alt={profileDisplayName}
                    onError={() => setAvatarLoadError(true)}
                  />
                ) : (
                  <Icon icon="mdi:account-circle" size={80} />
                )}
              </div>
              <span className={styles.avatarHint}>{t('profile.info.changeAvatar')}</span>
            </button>

            <div className={styles.info}>
              <h2 className={styles.userName}>{profileDisplayName}</h2>
              <p className={styles.userId}>ID: {userId}</p>
              <div className={styles.profileMeta}>
                {profileDisplayHandle ? (
                  <div className={styles.metaItem}>
                    <Icon icon="mdi:identifier" size={16} />
                    <span>{profileDisplayHandle}</span>
                  </div>
                ) : null}
                <div className={styles.metaItem}>
                  <Icon icon="mdi:email" size={16} />
                  <span>{profile.voUserEmail || t('profile.info.emailUnset')}</span>
                </div>
                <div className={styles.metaItem}>
                  <Icon icon="mdi:wallet" size={16} />
                  <span>
                    {balanceState === 'ready'
                      ? formatCoinAmount(coinBalance?.voBalance, language, (key) => t(key))
                      : t(`profile.authority.state.${balanceState}`)}
                  </span>
                </div>
                {profile.voAddress ? (
                  <div className={styles.metaItem}>
                    <Icon icon="mdi:map-marker" size={16} />
                    <span>{profile.voAddress}</span>
                  </div>
                ) : null}
                {profile.voCreateTime ? (
                  <div className={styles.metaItem}>
                    <Icon icon="mdi:clock-outline" size={16} />
                    <span>{formatDateTimeByTimeZone(profile.voCreateTime, displayTimeZone)}</span>
                  </div>
                ) : null}
              </div>
            </div>

            <div className={styles.headerActions}>
              <Button variant="secondary" size="small" onClick={handleOpenEdit} disabled={profileState !== 'ready' || combinedBusy}>
                {t('profile.info.editProfile')}
              </Button>
              <div className={styles.timeZonePanel}>
                <div className={styles.timeZoneTitle}>{t('profile.info.timeZoneTitle')}</div>
                {renderAuthorityState(timeState, timeError, onRetryTime, 'time')}
                <Select
                  options={presetTimeZoneOptions}
                  value={customTimeZone}
                  disabled={timeState !== 'ready' || savingTimeZone}
                  onChange={(event) => {
                    setCustomTimeZone(event.target.value);
                    setTimeZoneError(null);
                  }}
                />
                {timeZoneError ? <div className={styles.timeZoneError}>{timeZoneError}</div> : null}
                <Button size="small" onClick={() => void handleApplyTimeZone()} disabled={timeState !== 'ready' || savingTimeZone || !timeZoneDirty}>
                  {savingTimeZone ? t('profile.info.timeZoneSaving') : t('profile.info.timeZoneSave')}
                </Button>
                <div className={styles.timeZoneHint}>
                  <div>{t('profile.info.systemTimeZone', { value: timeState === 'loading' || timeState === 'unavailable' ? '--' : systemTimeZone })}</div>
                  <div>{t('profile.info.displayTimeZone', { value: timeState === 'loading' || timeState === 'unavailable' ? '--' : displayTimeZone })}</div>
                  <div>{t('profile.info.displayFormat', { value: timeState === 'loading' || timeState === 'unavailable' ? '--' : displayTimeFormat })}</div>
                </div>
              </div>
            </div>
          </div>

          {renderAuthorityState(balanceState, balanceError, () => void loadProfile(false, false), 'balance')}
          {renderAuthorityState(statsState, statsError, onRetryStats, 'stats')}

          {statsState === 'ready' && stats ? (
            <div className={styles.stats}>
              <div className={styles.statItem}>
                <Icon icon="mdi:file-document" size={24} />
                <div className={styles.statValue}>{stats.voPostCount}</div>
                <div className={styles.statLabel}>{t('profile.stats.postsLabel')}</div>
              </div>
              <div className={styles.statItem}>
                <Icon icon="mdi:comment" size={24} />
                <div className={styles.statValue}>{stats.voCommentCount}</div>
                <div className={styles.statLabel}>{t('profile.stats.commentsLabel')}</div>
              </div>
              <div className={styles.statItem}>
                <Icon icon="mdi:heart" size={24} />
                <div className={styles.statValue}>{stats.voTotalLikeCount}</div>
                <div className={styles.statLabel}>{t('profile.stats.likesLabel')}</div>
              </div>
            </div>
          ) : null}
        </>
      ) : null}

      <Modal
        isOpen={isEditOpen}
        onClose={closeEdit}
        closeLabel={t('common.close')}
        title={t('profile.info.editDialogTitle')}
        closeDisabled={savingProfile}
        closeOnOverlayClick={!savingProfile}
        closeOnEscape={!savingProfile}
      >
        <div className={styles.editForm}>
          <Input label={t('profile.info.form.userName')} value={editUserName} onChange={(event) => setEditUserName(event.target.value)} fullWidth disabled={savingProfile} />
          <Input label={t('profile.info.form.email')} value={editUserEmail} onChange={(event) => setEditUserEmail(event.target.value)} fullWidth disabled={savingProfile} />
          <Input label={t('profile.info.form.age')} value={editAge} onChange={(event) => setEditAge(event.target.value)} fullWidth disabled={savingProfile} />
          <Input label={t('profile.info.form.address')} value={editAddress} onChange={(event) => setEditAddress(event.target.value)} fullWidth disabled={savingProfile} />
          {profileDirty ? <div className={styles.dirtyNote}>{t('profile.info.unsaved')}</div> : null}
          {saveError ? <div className={styles.saveError}>{saveError}</div> : null}
          <div className={styles.editActions}>
            <Button variant="secondary" onClick={closeEdit} disabled={savingProfile}>{t('common.cancel')}</Button>
            <Button onClick={() => setConfirmOpen(true)} disabled={!profileDirty || savingProfile}>
              {savingProfile ? t('profile.info.saving') : t('common.save')}
            </Button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={confirmOpen}
        title={t('profile.info.confirmSaveTitle')}
        message={t('profile.info.confirmSaveMessage')}
        confirmText={t('common.confirm')}
        cancelText={t('common.cancel')}
        onCancel={() => setConfirmOpen(false)}
        onConfirm={() => void handleSave()}
      />

      {isAvatarModalOpen ? (
        <Suspense fallback={null}>
          <AvatarUploadModal
            isOpen={isAvatarModalOpen}
            onClose={() => setIsAvatarModalOpen(false)}
            onSuccess={handleAvatarUploadSuccess}
            onBusyChange={setAvatarBusy}
          />
        </Suspense>
      ) : null}
    </div>
  );
};

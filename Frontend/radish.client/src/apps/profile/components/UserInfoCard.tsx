import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { log } from '@/utils/logger';
import { apiGet, configureApiClient } from '@radish/http';
import type { ApiResponse } from '@radish/http';
import { Button } from '@radish/ui/button';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { Icon } from '@radish/ui/icon';
import { Input } from '@radish/ui/input';
import { Modal } from '@radish/ui/modal';
import { Select } from '@radish/ui/select';
import { useUserStore } from '@/stores/userStore';
import { tokenService } from '@/services/tokenService';
import { buildTimeZoneOptions, formatDateTimeByTimeZone, resolveTimeZoneId } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import type { LongId } from '@/api/user';
import { getBalance, type UserBalance } from '@/api/coin';
import { getIntlLocale } from '@/locales/language';
import { reuseInFlightRequest } from '../requestDedup';
import styles from './UserInfoCard.module.css';

const AvatarUploadModal = lazy(() =>
  import('./AvatarUploadModal').then((module) => ({ default: module.AvatarUploadModal }))
);

interface UserStats {
  voPostCount: number;
  voCommentCount: number;
  voTotalLikeCount: number;
  voPostLikeCount: number;
  voCommentLikeCount: number;
}

interface UserInfoCardProps {
  userId: LongId;
  userName: string;
  stats?: UserStats;
  loading?: boolean;
  apiBaseUrl: string;
  displayTimeZone: string;
  systemTimeZone: string;
  displayTimeFormat: string;
  savingTimeZone?: boolean;
  onTimeZoneChange: (timeZoneId: string) => Promise<void>;
}

interface ProfileInfo {
  voUserId: LongId;
  voPublicId?: string | null;
  voPublicIndex?: string | number | null;
  voDisplayName?: string | null;
  voDisplayHandle?: string | null;
  voUserName: string;
  voUserEmail: string;
  voSex: number;
  voAge: number;
  voBirth?: string | null;
  voAddress: string;
  voCreateTime: string;
  voAvatarAttachmentId?: string | null;
  voAvatarUrl?: string | null;
  voAvatarThumbnailUrl?: string | null;
}

function getAuthHeader(): string | null {
  const token = tokenService.getAccessToken();
  return token ? `Bearer ${token}` : null;
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
  if (whiteRadish > 0n) parts.push(`${formatter.format(whiteRadish)} ${translate('profile.coin.whiteRadish')}`);
  if (carrot > 0n || parts.length === 0) parts.push(`${formatter.format(carrot)} ${translate('profile.coin.carrot')}`);

  const result = parts.join(' ');
  return negative ? `-${result}` : result;
}

interface OwnProfileBundle {
  profile: ProfileInfo | null;
  coinBalance: UserBalance | null;
}

function buildOwnProfileRequestKey(apiBaseUrl: string, userId: LongId): string {
  return `own-profile|${apiBaseUrl}|${userId}`;
}

async function fetchOwnProfileBundle(
  apiBaseUrl: string,
  userId: LongId,
  t: TFunction,
  reuseInFlight: boolean
): Promise<OwnProfileBundle> {
  const loadBundle = async () => {
    const [profileResult, coinBalanceResult] = await Promise.allSettled([
      apiGet<ProfileInfo>('/api/v1/User/GetMyProfile', { withAuth: true }),
      getBalance(t)
    ]);

    return {
      profile: profileResult.status === 'fulfilled' && profileResult.value.ok
        ? profileResult.value.data ?? null
        : null,
      coinBalance: coinBalanceResult.status === 'fulfilled'
        ? coinBalanceResult.value
        : null
    };
  };

  if (!reuseInFlight) {
    return loadBundle();
  }

  return reuseInFlightRequest(buildOwnProfileRequestKey(apiBaseUrl, userId), loadBundle);
}

export const UserInfoCard = ({
  userId,
  userName,
  stats,
  loading = false,
  apiBaseUrl,
  displayTimeZone,
  systemTimeZone,
  displayTimeFormat,
  savingTimeZone = false,
  onTimeZoneChange
}: UserInfoCardProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const { setUser, tenantId, roles, permissions } = useUserStore();

  const [profile, setProfile] = useState<ProfileInfo | null>(null);
  const [coinBalance, setCoinBalance] = useState<UserBalance | null>(null);
  const [loadingProfile, setLoadingProfile] = useState(true);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [isAvatarModalOpen, setIsAvatarModalOpen] = useState(false);
  const [avatarLoadError, setAvatarLoadError] = useState(false);

  const [editUserName, setEditUserName] = useState('');
  const [editUserEmail, setEditUserEmail] = useState('');
  const [editAge, setEditAge] = useState('');
  const [editAddress, setEditAddress] = useState('');
  const [customTimeZone, setCustomTimeZone] = useState(displayTimeZone);
  const [timeZoneError, setTimeZoneError] = useState<string | null>(null);
  const loadProfileRequestIdRef = useRef(0);

  const presetTimeZoneOptions = useMemo(() => {
    return buildTimeZoneOptions(systemTimeZone, displayTimeZone);
  }, [displayTimeZone, systemTimeZone]);

  useEffect(() => {
    configureApiClient({
      baseUrl: apiBaseUrl,
    });
  }, [apiBaseUrl]);

  useEffect(() => {
    void loadProfile(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiBaseUrl, userId]);

  useEffect(() => {
    return () => {
      loadProfileRequestIdRef.current += 1;
    };
  }, []);

  useEffect(() => {
    setCustomTimeZone(displayTimeZone);
    setTimeZoneError(null);
  }, [displayTimeZone]);

  useEffect(() => {
    setAvatarLoadError(false);
  }, [profile?.voAvatarThumbnailUrl, profile?.voAvatarUrl]);

  const loadProfile = async (reuseInFlight: boolean = false) => {
    const requestId = ++loadProfileRequestIdRef.current;
    setLoadingProfile(true);

    try {
      const result = await fetchOwnProfileBundle(apiBaseUrl, userId, t, reuseInFlight);
      if (requestId !== loadProfileRequestIdRef.current) {
        return;
      }

      if (result.profile) {
        const currentProfile = result.profile;
        const currentDisplayName = resolveVisibleUserDisplayName(currentProfile, userName);
        log.debug('UserInfoCard', '头像信息:', {
          avatarAttachmentId: currentProfile.voAvatarAttachmentId,
          avatarUrl: currentProfile.voAvatarUrl,
          avatarThumbnailUrl: currentProfile.voAvatarThumbnailUrl
        });
        setProfile(currentProfile);

        // 更新全局 userStore，使 Dock 栏能实时刷新头像
        setUser({
          userId: String(userId),
          displayName: currentDisplayName,
          userName: currentDisplayName,
          displayHandle: currentProfile.voDisplayHandle || undefined,
          publicId: currentProfile.voPublicId || undefined,
          publicIndex: typeof currentProfile.voPublicIndex === 'number'
            ? String(currentProfile.voPublicIndex)
            : currentProfile.voPublicIndex || undefined,
          nickname: currentDisplayName,
          tenantId: tenantId,
          roles: roles || ['User'],
          permissions: permissions || [],
          avatarUrl: currentProfile.voAvatarUrl || undefined,
          avatarThumbnailUrl: currentProfile.voAvatarThumbnailUrl || undefined
        });

        setEditUserName(currentDisplayName);
        setEditUserEmail(currentProfile.voUserEmail || '');
        setEditAge(String(currentProfile.voAge ?? ''));
        setEditAddress(currentProfile.voAddress || '');
      }

      if (result.coinBalance) {
        setCoinBalance(result.coinBalance);
      }
    } catch (error) {
      if (requestId !== loadProfileRequestIdRef.current) {
        return;
      }

      log.error('UserInfoCard', '加载用户资料失败:', error);
    } finally {
      if (requestId === loadProfileRequestIdRef.current) {
        setLoadingProfile(false);
      }
    }
  };

  const avatarSrc = useMemo(() => {
    const url = profile?.voAvatarThumbnailUrl || profile?.voAvatarUrl;
    const resolved = resolveUrl(apiBaseUrl, url);

    if (resolved) {
      log.debug('UserInfoCard', '解析后的头像 URL:', resolved);
    } else {
      log.debug('UserInfoCard', '没有头像 URL');
    }

    return resolved || undefined;
  }, [apiBaseUrl, profile?.voAvatarThumbnailUrl, profile?.voAvatarUrl]);

  const avatarImageSrc = avatarLoadError ? undefined : avatarSrc;
  const profileDisplayName = resolveVisibleUserDisplayName(profile ?? { voUserName: userName }, userName);
  const profileDisplayHandle = profile
    ? resolveVisibleUserHandle(profile, profileDisplayName)
    : null;

  const handleOpenEdit = () => {
    if (profile) {
      setEditUserName(resolveVisibleUserDisplayName(profile, userName));
      setEditUserEmail(profile.voUserEmail || '');
      setEditAge(String(profile.voAge ?? ''));
      setEditAddress(profile.voAddress || '');
    }
    setIsEditOpen(true);
  };

  const handleAvatarClick = () => {
    setIsAvatarModalOpen(true);
  };

  const handleAvatarUploadSuccess = async () => {
    setIsAvatarModalOpen(false);
    await loadProfile();
  };

  const handleAvatarError = () => {
    log.error('UserInfoCard', '头像加载失败:', avatarSrc);
    setAvatarLoadError(true);
  };

  const handleSave = async () => {
    setConfirmOpen(false);
    setSaveError(null);

    const ageNum = editAge.trim() ? Number(editAge.trim()) : undefined;

    try {
      const authHeader = getAuthHeader();
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      };
      if (authHeader) {
        headers.Authorization = authHeader;
      }

      const res = await fetch(`${apiBaseUrl}/api/v1/User/UpdateMyProfile`, {
        method: 'POST',
        headers,
        body: JSON.stringify({
          userName: editUserName.trim() || undefined,
          userEmail: editUserEmail.trim() || undefined,
          age: Number.isFinite(ageNum) ? ageNum : undefined,
          address: editAddress.trim() || undefined
        })
      });

      const json = (await res.json()) as ApiResponse<unknown>;

      if (!res.ok || !json.isSuccess) {
        setSaveError(json.messageInfo || `保存失败: HTTP ${res.status}`);
        return;
      }

      setIsEditOpen(false);
      await loadProfile();
    } catch (e) {
      setSaveError(e instanceof Error ? e.message : String(e));
    }
  };

  const handleApplyTimeZone = async () => {
    const selectedTimeZone = resolveTimeZoneId(customTimeZone, systemTimeZone);
    setTimeZoneError(null);

    try {
      await onTimeZoneChange(selectedTimeZone);
    } catch (error) {
      const message = error instanceof Error ? error.message : t('profile.info.timeZoneSaveFailed');
      setTimeZoneError(message);
    }
  };

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <div className={styles.avatarSection} onClick={handleAvatarClick}>
          <div className={styles.avatar}>
            {avatarImageSrc ? (
              <img
                className={styles.avatarImg}
                src={avatarImageSrc}
                alt={profileDisplayName}
                onError={handleAvatarError}
              />
            ) : (
              <Icon icon="mdi:account-circle" size={80} />
            )}
          </div>
          <div className={styles.avatarHint}>{t('profile.info.changeAvatar')}</div>
        </div>
        <div className={styles.info}>
          <h2 className={styles.userName}>{profileDisplayName}</h2>
          <p className={styles.userId}>ID: {userId}</p>
          <div className={styles.profileMeta}>
            {profileDisplayHandle && (
              <div className={styles.metaItem}>
                <Icon icon="mdi:identifier" size={16} />
                <span>{profileDisplayHandle}</span>
              </div>
            )}
            <div className={styles.metaItem}>
              <Icon icon="mdi:email" size={16} />
              <span>{profile?.voUserEmail || t('profile.info.emailUnset')}</span>
            </div>
            <div className={styles.metaItem}>
              <Icon icon="mdi:wallet" size={16} />
              <span>{formatCoinAmount(coinBalance?.voBalance, language, (key) => t(key))}</span>
            </div>
            {profile?.voAddress && (
              <div className={styles.metaItem}>
                <Icon icon="mdi:map-marker" size={16} />
                <span>{profile.voAddress}</span>
              </div>
            )}
            {profile?.voCreateTime && (
              <div className={styles.metaItem}>
                <Icon icon="mdi:clock-outline" size={16} />
                <span>{formatDateTimeByTimeZone(profile.voCreateTime, displayTimeZone)}</span>
              </div>
            )}
          </div>
        </div>
        <div className={styles.headerActions}>
          <Button variant="secondary" size="small" onClick={handleOpenEdit}>
            {t('profile.info.editProfile')}
          </Button>
          <div className={styles.timeZonePanel}>
            <div className={styles.timeZoneTitle}>{t('profile.info.timeZoneTitle')}</div>
            <Select
              options={presetTimeZoneOptions}
              value={customTimeZone}
              onChange={(event) => {
                setCustomTimeZone(event.target.value);
                setTimeZoneError(null);
              }}
            />
            {timeZoneError && <div className={styles.timeZoneError}>{timeZoneError}</div>}
            <Button size="small" onClick={() => void handleApplyTimeZone()} disabled={savingTimeZone}>
              {savingTimeZone ? t('profile.info.timeZoneSaving') : t('profile.info.timeZoneSave')}
            </Button>
            <div className={styles.timeZoneHint}>
              <div>{t('profile.info.systemTimeZone', { value: systemTimeZone })}</div>
              <div>{t('profile.info.displayTimeZone', { value: displayTimeZone })}</div>
              <div>{t('profile.info.displayFormat', { value: displayTimeFormat })}</div>
            </div>
          </div>
        </div>
      </div>

      {(loading || loadingProfile) && (
        <div className={styles.loading}>{t('common.loading')}</div>
      )}

      {!loading && !loadingProfile && stats && (
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
      )}

      <Modal
        isOpen={isEditOpen}
        onClose={() => {
          setIsEditOpen(false);
          setSaveError(null);
        }}
        title={t('profile.info.editDialogTitle')}
      >
        <div className={styles.editForm}>
          <Input label={t('profile.info.form.userName')} value={editUserName} onChange={(e) => setEditUserName(e.target.value)} fullWidth />
          <Input label={t('profile.info.form.email')} value={editUserEmail} onChange={(e) => setEditUserEmail(e.target.value)} fullWidth />
          <Input label={t('profile.info.form.age')} value={editAge} onChange={(e) => setEditAge(e.target.value)} fullWidth />
          <Input label={t('profile.info.form.address')} value={editAddress} onChange={(e) => setEditAddress(e.target.value)} fullWidth />

          {saveError && <div className={styles.saveError}>{saveError}</div>}

          <div className={styles.editActions}>
            <Button
              variant="secondary"
              onClick={() => {
                setIsEditOpen(false);
                setSaveError(null);
              }}
            >
              {t('common.cancel')}
            </Button>
            <Button onClick={() => setConfirmOpen(true)}>{t('common.save')}</Button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={confirmOpen}
        title={t('profile.info.confirmSaveTitle')}
        message={t('profile.info.confirmSaveMessage')}
        onCancel={() => setConfirmOpen(false)}
        onConfirm={handleSave}
      />

      {isAvatarModalOpen && (
        <Suspense fallback={null}>
          <AvatarUploadModal
            isOpen={isAvatarModalOpen}
            onClose={() => setIsAvatarModalOpen(false)}
            onSuccess={handleAvatarUploadSuccess}
            apiBaseUrl={apiBaseUrl}
          />
        </Suspense>
      )}
    </div>
  );
};

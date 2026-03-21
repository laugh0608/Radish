import { lazy, Suspense, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  userId: number;
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
  voUserId: number;
  voUserName: string;
  voUserEmail: string;
  voRealName: string;
  voSex: number;
  voAge: number;
  voBirth?: string | null;
  voAddress: string;
  voCreateTime: string;
  voAvatarAttachmentId?: number | string | null;
  voAvatarUrl?: string | null;
  voAvatarThumbnailUrl?: string | null;
}

interface CoinBalanceInfo {
  voUserId: number | string;
  voBalance: number | string;
  voBalanceDisplay: string;
  voFrozenBalance: number | string;
  voFrozenBalanceDisplay: string;
  voTotalEarned: number | string;
  voTotalSpent: number | string;
  voTotalTransferredIn: number | string;
  voTotalTransferredOut: number | string;
  voCreateTime: string;
  voModifyTime?: string | null;
}

function getAuthHeader(): string | null {
  const token = tokenService.getAccessToken();
  return token ? `Bearer ${token}` : null;
}

function resolveUrl(apiBaseUrl: string, url: string | null | undefined): string | null {
  if (!url) return null;
  if (/^https?:\/\//i.test(url)) return url;
  if (url.startsWith('/')) return `${apiBaseUrl}${url}`;
  return `${apiBaseUrl}/${url}`;
}

function formatCoinAmount(
  amount: number | string | null | undefined,
  translate: (key: string) => string
): string {
  const parsed = typeof amount === 'string' ? Number(amount) : amount;
  const value = Number.isFinite(parsed) ? (parsed as number) : 0;
  const negative = value < 0;
  const abs = Math.abs(value);

  const whiteRadish = Math.floor(abs / 1000);
  const carrot = abs % 1000;

  const parts: string[] = [];
  if (whiteRadish > 0) parts.push(`${whiteRadish} ${translate('profile.coin.whiteRadish')}`);
  if (carrot > 0 || parts.length === 0) parts.push(`${carrot} ${translate('profile.coin.carrot')}`);

  const result = parts.join(' ');
  return negative ? `-${result}` : result;
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
  const { t } = useTranslation();
  const { setUser, tenantId, roles, permissions } = useUserStore();

  const [profile, setProfile] = useState<ProfileInfo | null>(null);
  const [coinBalance, setCoinBalance] = useState<CoinBalanceInfo | null>(null);
  const [loadingProfile, setLoadingProfile] = useState(true);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [isAvatarModalOpen, setIsAvatarModalOpen] = useState(false);
  const [avatarLoadError, setAvatarLoadError] = useState(false);

  const [editUserName, setEditUserName] = useState('');
  const [editUserEmail, setEditUserEmail] = useState('');
  const [editRealName, setEditRealName] = useState('');
  const [editAge, setEditAge] = useState('');
  const [editAddress, setEditAddress] = useState('');
  const [customTimeZone, setCustomTimeZone] = useState(displayTimeZone);
  const [timeZoneError, setTimeZoneError] = useState<string | null>(null);

  const presetTimeZoneOptions = useMemo(() => {
    return buildTimeZoneOptions(systemTimeZone, displayTimeZone);
  }, [displayTimeZone, systemTimeZone]);

  useEffect(() => {
    configureApiClient({
      baseUrl: apiBaseUrl,
    });
  }, [apiBaseUrl]);

  useEffect(() => {
    void loadProfile();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  useEffect(() => {
    setCustomTimeZone(displayTimeZone);
    setTimeZoneError(null);
  }, [displayTimeZone]);

  useEffect(() => {
    setAvatarLoadError(false);
  }, [profile?.voAvatarThumbnailUrl, profile?.voAvatarUrl]);

  const loadProfile = async () => {
    setLoadingProfile(true);
    try {
      const [profileResult, coinBalanceResult] = await Promise.allSettled([
        apiGet<ProfileInfo>('/api/v1/User/GetMyProfile', { withAuth: true }),
        apiGet<CoinBalanceInfo>('/api/v1/Coin/GetBalance', { withAuth: true })
      ]);

      if (profileResult.status === 'fulfilled') {
        const profileRes = profileResult.value;

        log.debug('UserInfoCard', 'GetMyProfile 响应:', profileRes);

        if (profileRes.ok && profileRes.data) {
          const profile = profileRes.data;
          log.debug('UserInfoCard', '头像信息:', {
            avatarAttachmentId: profile.voAvatarAttachmentId,
            avatarUrl: profile.voAvatarUrl,
            avatarThumbnailUrl: profile.voAvatarThumbnailUrl
          });
          setProfile(profile);

          // 更新全局 userStore，使 Dock 栏能实时刷新头像
          setUser({
            userId: profile.voUserId,
            userName: profile.voUserName,
            tenantId: tenantId,
            roles: roles || ['User'],
            permissions: permissions || [],
            avatarUrl: profile.voAvatarUrl || undefined,
            avatarThumbnailUrl: profile.voAvatarThumbnailUrl || undefined
          });

          setEditUserName(profile.voUserName || userName);
          setEditUserEmail(profile.voUserEmail || '');
          setEditRealName(profile.voRealName || '');
          setEditAge(String(profile.voAge ?? ''));
          setEditAddress(profile.voAddress || '');
        }
      }

      if (coinBalanceResult.status === 'fulfilled') {
        const coinBalanceRes = coinBalanceResult.value;

        if (coinBalanceRes.ok && coinBalanceRes.data) {
          setCoinBalance(coinBalanceRes.data);
        }
      }
    } catch (error) {
      log.error('UserInfoCard', '加载用户资料失败:', error);
    } finally {
      setLoadingProfile(false);
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

  const handleOpenEdit = () => {
    if (profile) {
      setEditUserName(profile.voUserName || userName);
      setEditUserEmail(profile.voUserEmail || '');
      setEditRealName(profile.voRealName || '');
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
          realName: editRealName.trim() || undefined,
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
                alt={userName}
                onError={handleAvatarError}
              />
            ) : (
              <Icon icon="mdi:account-circle" size={80} />
            )}
          </div>
          <div className={styles.avatarHint}>{t('profile.info.changeAvatar')}</div>
        </div>
        <div className={styles.info}>
          <h2 className={styles.userName}>{profile?.voUserName || userName}</h2>
          <p className={styles.userId}>ID: {userId}</p>
          <div className={styles.profileMeta}>
            <div className={styles.metaItem}>
              <Icon icon="mdi:email" size={16} />
              <span>{profile?.voUserEmail || t('profile.info.emailUnset')}</span>
            </div>
            <div className={styles.metaItem}>
              <Icon icon="mdi:wallet" size={16} />
              <span>{formatCoinAmount(coinBalance?.voBalance, (key) => t(key))}</span>
            </div>
            {profile?.voRealName && (
              <div className={styles.metaItem}>
                <Icon icon="mdi:account" size={16} />
                <span>{profile.voRealName}</span>
              </div>
            )}
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
          <Input label={t('profile.info.form.realName')} value={editRealName} onChange={(e) => setEditRealName(e.target.value)} fullWidth />
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

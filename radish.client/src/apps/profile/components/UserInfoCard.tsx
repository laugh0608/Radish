import { useEffect, useMemo, useState } from 'react';
import { log } from '@/utils/logger';
import { Button, ConfirmDialog, Icon, Input, Modal } from '@radish/ui';
import { useTranslation } from 'react-i18next';
import { useUserStore } from '@/stores/userStore';
import { AvatarUploadModal } from './AvatarUploadModal';
import styles from './UserInfoCard.module.css';

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
  userId: number | string;
  balance: number | string;
  balanceDisplay: string;
  frozenBalance: number | string;
  frozenBalanceDisplay: string;
  totalEarned: number | string;
  totalSpent: number | string;
  totalTransferredIn: number | string;
  totalTransferredOut: number | string;
  createTime: string;
  modifyTime?: string | null;
}

interface ApiResponse<T> {
  isSuccess: boolean;
  messageInfo?: string;
  responseData?: T;
}

function resolveUrl(apiBaseUrl: string, url: string | null | undefined): string | null {
  if (!url) return null;
  if (/^https?:\/\//i.test(url)) return url;
  if (url.startsWith('/')) return `${apiBaseUrl}${url}`;
  return `${apiBaseUrl}/${url}`;
}

function formatCoinAmount(amount: number | string | null | undefined): string {
  const parsed = typeof amount === 'string' ? Number(amount) : amount;
  const value = Number.isFinite(parsed) ? (parsed as number) : 0;
  const negative = value < 0;
  const abs = Math.abs(value);

  const whiteRadish = Math.floor(abs / 1000);
  const carrot = abs % 1000;

  const parts: string[] = [];
  if (whiteRadish > 0) parts.push(`${whiteRadish} 白萝卜`);
  if (carrot > 0 || parts.length === 0) parts.push(`${carrot} 胡萝卜`);

  const result = parts.join(' ');
  return negative ? `-${result}` : result;
}

async function readJsonIfPossible<T>(res: Response): Promise<T | null> {
  const contentType = res.headers.get('content-type') || '';
  if (!contentType.toLowerCase().includes('json')) return null;

  try {
    return (await res.json()) as T;
  } catch {
    return null;
  }
}

export const UserInfoCard = ({ userId, userName, stats, loading = false, apiBaseUrl }: UserInfoCardProps) => {
  const { t } = useTranslation();
  const { setUser, tenantId, roles } = useUserStore();

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

  const getAuthHeader = () => {
    if (typeof window === 'undefined') return undefined;
    const token = window.localStorage.getItem('access_token');
    return token ? `Bearer ${token}` : undefined;
  };

  useEffect(() => {
    void loadProfile();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  const loadProfile = async () => {
    setLoadingProfile(true);
    try {
      const [profileResult, coinBalanceResult] = await Promise.allSettled([
        fetch(`${apiBaseUrl}/api/v1/User/GetMyProfile`, {
          headers: {
            Accept: 'application/json',
            ...(getAuthHeader() ? { Authorization: getAuthHeader() } : {})
          }
        }),
        fetch(`${apiBaseUrl}/api/v1/Coin/GetBalance`, {
          headers: {
            Accept: 'application/json',
            ...(getAuthHeader() ? { Authorization: getAuthHeader() } : {})
          }
        })
      ]);

      if (profileResult.status === 'fulfilled') {
        const profileRes = profileResult.value;
        const profileJson = await readJsonIfPossible<ApiResponse<ProfileInfo>>(profileRes);

        log.debug('UserInfoCard', 'GetMyProfile 响应:', profileJson);

        if (profileRes.ok && profileJson?.isSuccess && profileJson.responseData) {
          const profile = profileJson.responseData;
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
        const coinBalanceJson = await readJsonIfPossible<ApiResponse<CoinBalanceInfo>>(coinBalanceRes);

        if (coinBalanceRes.ok && coinBalanceJson?.isSuccess && coinBalanceJson.responseData) {
          setCoinBalance(coinBalanceJson.responseData);
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
      const res = await fetch(`${apiBaseUrl}/api/v1/User/UpdateMyProfile`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
          ...(getAuthHeader() ? { Authorization: getAuthHeader() } : {})
        },
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

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <div className={styles.avatarSection} onClick={handleAvatarClick}>
          <div className={styles.avatar}>
            {avatarSrc ? (
              <img
                className={styles.avatarImg}
                src={avatarSrc}
                alt={userName}
                onError={handleAvatarError}
              />
            ) : (
              <Icon icon="mdi:account-circle" size={80} />
            )}
          </div>
          <div className={styles.avatarHint}>点击更换头像</div>
        </div>
        <div className={styles.info}>
          <h2 className={styles.userName}>{profile?.voUserName || userName}</h2>
          <p className={styles.userId}>ID: {userId}</p>
          <div className={styles.profileMeta}>
            <div className={styles.metaItem}>
              <Icon icon="mdi:email" size={16} />
              <span>{profile?.voUserEmail || '未设置邮箱'}</span>
            </div>
            <div className={styles.metaItem}>
              <Icon icon="mdi:wallet" size={16} />
              <span>{formatCoinAmount(coinBalance?.balance)}</span>
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
          </div>
        </div>
        <div className={styles.headerActions}>
          <Button variant="secondary" size="small" onClick={handleOpenEdit}>
            编辑资料
          </Button>
        </div>
      </div>

      {(loading || loadingProfile) && (
        <div className={styles.loading}>加载中...</div>
      )}

      {!loading && !loadingProfile && stats && (
        <div className={styles.stats}>
          <div className={styles.statItem}>
            <Icon icon="mdi:file-document" size={24} />
            <div className={styles.statValue}>{stats.voPostCount}</div>
            <div className={styles.statLabel}>帖子</div>
          </div>
          <div className={styles.statItem}>
            <Icon icon="mdi:comment" size={24} />
            <div className={styles.statValue}>{stats.voCommentCount}</div>
            <div className={styles.statLabel}>评论</div>
          </div>
          <div className={styles.statItem}>
            <Icon icon="mdi:heart" size={24} />
            <div className={styles.statValue}>{stats.voTotalLikeCount}</div>
            <div className={styles.statLabel}>获赞</div>
          </div>
        </div>
      )}

      <Modal
        isOpen={isEditOpen}
        onClose={() => {
          setIsEditOpen(false);
          setSaveError(null);
        }}
        title="编辑个人资料"
      >
        <div className={styles.editForm}>
          <Input label="昵称" value={editUserName} onChange={(e) => setEditUserName(e.target.value)} fullWidth />
          <Input label="邮箱" value={editUserEmail} onChange={(e) => setEditUserEmail(e.target.value)} fullWidth />
          <Input label="真实姓名" value={editRealName} onChange={(e) => setEditRealName(e.target.value)} fullWidth />
          <Input label="年龄" value={editAge} onChange={(e) => setEditAge(e.target.value)} fullWidth />
          <Input label="地址" value={editAddress} onChange={(e) => setEditAddress(e.target.value)} fullWidth />

          {saveError && <div className={styles.saveError}>{saveError}</div>}

          <div className={styles.editActions}>
            <Button
              variant="secondary"
              onClick={() => {
                setIsEditOpen(false);
                setSaveError(null);
              }}
            >
              取消
            </Button>
            <Button onClick={() => setConfirmOpen(true)}>保存</Button>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={confirmOpen}
        title="确认保存"
        message="确定要保存个人资料修改吗？"
        onCancel={() => setConfirmOpen(false)}
        onConfirm={handleSave}
      />

      <AvatarUploadModal
        isOpen={isAvatarModalOpen}
        onClose={() => setIsAvatarModalOpen(false)}
        onSuccess={handleAvatarUploadSuccess}
        apiBaseUrl={apiBaseUrl}
      />
    </div>
  );
};

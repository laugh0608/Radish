import { useEffect, useMemo, useState } from 'react';
import { log } from '@/utils/logger';
import { Button, ConfirmDialog, FileUpload, Icon, Input, Modal, ExperienceBar } from '@radish/ui';
import { experienceApi, type ExperienceData } from '@/api/experience';
import type { UploadResult } from '@radish/ui';
import { uploadImage } from '@/api/attachment';
import { useTranslation } from 'react-i18next';
import styles from './UserInfoCard.module.css';

interface UserStats {
  postCount: number;
  commentCount: number;
  totalLikeCount: number;
  postLikeCount: number;
  commentLikeCount: number;
}

interface UserInfoCardProps {
  userId: number;
  userName: string;
  stats?: UserStats;
  loading?: boolean;
  apiBaseUrl: string;
}

interface ProfileInfo {
  userId: number;
  userName: string;
  userEmail: string;
  realName: string;
  sex: number;
  age: number;
  birth?: string | null;
  address: string;
  createTime: string;
  avatarAttachmentId?: number | string | null;
  avatarUrl?: string | null;
  avatarThumbnailUrl?: string | null;
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

  const [profile, setProfile] = useState<ProfileInfo | null>(null);
  const [coinBalance, setCoinBalance] = useState<CoinBalanceInfo | null>(null);
  const [experience, setExperience] = useState<ExperienceData | null>(null);
  const [loadingProfile, setLoadingProfile] = useState(true);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

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
    void loadExperience();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  const loadExperience = async () => {
    try {
      const exp = await experienceApi.getMyExperience();
      setExperience(exp);
    } catch (error) {
      log.error('加载经验值失败:', error);
    }
  };

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

        if (profileRes.ok && profileJson?.isSuccess && profileJson.responseData) {
          setProfile(profileJson.responseData);

          setEditUserName(profileJson.responseData.userName || userName);
          setEditUserEmail(profileJson.responseData.userEmail || '');
          setEditRealName(profileJson.responseData.realName || '');
          setEditAge(String(profileJson.responseData.age ?? ''));
          setEditAddress(profileJson.responseData.address || '');
        }
      }

      if (coinBalanceResult.status === 'fulfilled') {
        const coinBalanceRes = coinBalanceResult.value;
        const coinBalanceJson = await readJsonIfPossible<ApiResponse<CoinBalanceInfo>>(coinBalanceRes);

        if (coinBalanceRes.ok && coinBalanceJson?.isSuccess && coinBalanceJson.responseData) {
          setCoinBalance(coinBalanceJson.responseData);
        }
      }
    } catch {
      // ignore
    } finally {
      setLoadingProfile(false);
    }
  };

  const avatarSrc = useMemo(() => {
    const url = profile?.avatarThumbnailUrl || profile?.avatarUrl;
    return resolveUrl(apiBaseUrl, url) || undefined;
  }, [apiBaseUrl, profile?.avatarThumbnailUrl, profile?.avatarUrl]);

  const handleOpenEdit = () => {
    if (profile) {
      setEditUserName(profile.userName || userName);
      setEditUserEmail(profile.userEmail || '');
      setEditRealName(profile.realName || '');
      setEditAge(String(profile.age ?? ''));
      setEditAddress(profile.address || '');
    }
    setIsEditOpen(true);
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

  const handleUploadAvatar = async (file: File) => {
    const result = await uploadImage(
      {
        file,
        businessType: 'Avatar',
        generateThumbnail: true,
        generateMultipleSizes: false,
        addWatermark: false,
        removeExif: true
      },
      t
    );

    await fetch(`${apiBaseUrl}/api/v1/User/SetMyAvatar`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        ...(getAuthHeader() ? { Authorization: getAuthHeader() } : {})
      },
      body: JSON.stringify({ attachmentId: result.id })
    });

    await loadProfile();

    const uploadResult: UploadResult = {
      id: result.id,
      originalName: result.originalName,
      url: result.url,
      thumbnailUrl: result.thumbnailUrl,
      fileSize: result.fileSize,
      mimeType: result.mimeType
    };

    return uploadResult;
  };

  const handleRemoveAvatar = async () => {
    try {
      await fetch(`${apiBaseUrl}/api/v1/User/SetMyAvatar`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
          ...(getAuthHeader() ? { Authorization: getAuthHeader() } : {})
        },
        body: JSON.stringify({ attachmentId: 0 })
      });

      await loadProfile();
    } catch {
      // ignore
    }
  };

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <div className={styles.avatar}>
          {avatarSrc ? (
            <img className={styles.avatarImg} src={avatarSrc} alt={userName} />
          ) : (
            <Icon icon="mdi:account-circle" size={64} />
          )}
        </div>
        <div className={styles.info}>
          <h2 className={styles.userName}>{profile?.userName || userName}</h2>
          <p className={styles.userId}>ID: {userId}</p>
          <div className={styles.profileMeta}>
            <span className={styles.metaItem}>邮箱：{profile?.userEmail || '-'}</span>
            <span className={styles.metaItem}>余额：{formatCoinAmount(coinBalance?.balance)}</span>
          </div>
        </div>
        <div className={styles.headerActions}>
          <Button variant="secondary" size="small" onClick={handleOpenEdit}>
            编辑资料
          </Button>
        </div>
      </div>

      <div className={styles.avatarUpload}>
        <FileUpload
          accept="image/*"
          maxSize={2 * 1024 * 1024}
          placeholder="上传头像（2MB以内）"
          onUpload={handleUploadAvatar}
          showPreview={false}
        />
        {avatarSrc && (
          <Button variant="secondary" size="small" onClick={handleRemoveAvatar} style={{ marginTop: '8px' }}>
            移除头像
          </Button>
        )}
      </div>

      {/* 经验值条 */}
      {experience && (
        <div style={{ marginBottom: '24px' }}>
          <ExperienceBar
            data={experience as any}
            size="medium"
            showLevel={true}
            showProgress={true}
            showTooltip={true}
            animated={true}
          />
        </div>
      )}

      {(loading || loadingProfile) && (
        <div className={styles.loading}>加载中...</div>
      )}

      {!loading && !loadingProfile && stats && (
        <div className={styles.stats}>
          <div className={styles.statItem}>
            <Icon icon="mdi:file-document" size={24} />
            <div className={styles.statValue}>{stats.postCount}</div>
            <div className={styles.statLabel}>帖子</div>
          </div>
          <div className={styles.statItem}>
            <Icon icon="mdi:comment" size={24} />
            <div className={styles.statValue}>{stats.commentCount}</div>
            <div className={styles.statLabel}>评论</div>
          </div>
          <div className={styles.statItem}>
            <Icon icon="mdi:heart" size={24} />
            <div className={styles.statValue}>{stats.totalLikeCount}</div>
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
    </div>
  );
};

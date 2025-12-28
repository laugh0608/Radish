import { useEffect, useMemo, useState } from 'react';
import { Button, ConfirmDialog, FileUpload, Icon, Input, Modal } from '@radish/ui';
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

interface PointsInfo {
  userId: number;
  balance: number;
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

export const UserInfoCard = ({ userId, userName, stats, loading = false, apiBaseUrl }: UserInfoCardProps) => {
  const { t } = useTranslation();

  const [profile, setProfile] = useState<ProfileInfo | null>(null);
  const [points, setPoints] = useState<PointsInfo | null>(null);
  const [loadingProfile, setLoadingProfile] = useState(true);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const [editUserName, setEditUserName] = useState('');
  const [editUserEmail, setEditUserEmail] = useState('');
  const [editRealName, setEditRealName] = useState('');
  const [editAge, setEditAge] = useState('');
  const [editAddress, setEditAddress] = useState('');

  const authHeader = useMemo(() => {
    if (typeof window === 'undefined') return undefined;
    const token = window.localStorage.getItem('access_token');
    return token ? `Bearer ${token}` : undefined;
  }, []);

  useEffect(() => {
    void loadProfile();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  const loadProfile = async () => {
    setLoadingProfile(true);
    try {
      const [profileRes, pointsRes] = await Promise.all([
        fetch(`${apiBaseUrl}/api/v1/User/GetMyProfile`, {
          headers: {
            Accept: 'application/json',
            ...(authHeader ? { Authorization: authHeader } : {})
          }
        }),
        fetch(`${apiBaseUrl}/api/v1/User/GetMyPoints`, {
          headers: {
            Accept: 'application/json',
            ...(authHeader ? { Authorization: authHeader } : {})
          }
        })
      ]);

      const profileJson = (await profileRes.json()) as ApiResponse<ProfileInfo>;
      const pointsJson = (await pointsRes.json()) as ApiResponse<PointsInfo>;

      if (profileRes.ok && profileJson.isSuccess && profileJson.responseData) {
        setProfile(profileJson.responseData);

        setEditUserName(profileJson.responseData.userName || userName);
        setEditUserEmail(profileJson.responseData.userEmail || '');
        setEditRealName(profileJson.responseData.realName || '');
        setEditAge(String(profileJson.responseData.age ?? ''));
        setEditAddress(profileJson.responseData.address || '');
      }

      if (pointsRes.ok && pointsJson.isSuccess && pointsJson.responseData) {
        setPoints(pointsJson.responseData);
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
          ...(authHeader ? { Authorization: authHeader } : {})
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
        ...(authHeader ? { Authorization: authHeader } : {})
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
          ...(authHeader ? { Authorization: authHeader } : {})
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
            <span className={styles.metaItem}>积分：{points?.balance ?? 0}</span>
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

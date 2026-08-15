import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useUser } from '@/hooks/useUser';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { useUnsavedChangesGuard } from '@/hooks/useUnsavedChangesGuard';
import {
  AntInput as Input,
  AntModal as Modal,
  Avatar,
  Space,
  message,
  Tag,
  isSupportedAttachmentImageFile,
} from '@radish/ui';
import { Form, Upload, Button } from 'antd';
import type { UploadProps } from 'antd';
import {
  UserOutlined,
  EditOutlined,
  SettingOutlined,
} from '@radish/ui';
import { SaveOutlined, CameraOutlined, ReloadOutlined } from '@ant-design/icons';
import { getApiBaseUrl, getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { userApi, type MyProfileInfo } from '@/api/user';
import { tokenService } from '@/services/tokenService';
import { formatConsoleDateTime } from '@/utils/localeFormatters';
import '../adminFeature.css';
import './UserProfile.css';

type AuthorityState = 'loading' | 'ready' | 'unavailable' | 'stale';

interface UserProfileData extends MyProfileInfo {
  voRoles: string[];
}

function isProfileDraftDirty(values: Record<string, unknown>, profile: UserProfileData | null): boolean {
  if (!profile) {
    return false;
  }

  return String(values.voUserName ?? '') !== resolveVisibleUserDisplayName(profile, profile.voUserName)
    || String(values.voUserEmail ?? '') !== (profile.voUserEmail || '')
    || String(values.voAge ?? '') !== (profile.voAge ? String(profile.voAge) : '')
    || String(values.voAddress ?? '') !== (profile.voAddress || '');
}

export const UserProfile = () => {
  const { t, i18n } = useTranslation();
  useDocumentTitle(t('profile.documentTitle'));
  const { user, loading: userLoading, refreshUser } = useUser();
  const [form] = Form.useForm();
  const profileRef = useRef<UserProfileData | null>(null);
  const editingRef = useRef(false);
  const loadRequestIdRef = useRef(0);
  const [authorityState, setAuthorityState] = useState<AuthorityState>('loading');
  const [reading, setReading] = useState(false);
  const [savingProfile, setSavingProfile] = useState(false);
  const [avatarBusy, setAvatarBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [profileData, setProfileData] = useState<UserProfileData | null>(null);
  const [loadError, setLoadError] = useState<string>();

  useUnsavedChangesGuard(dirty, t('profile.dirty.leaveConfirm'));

  useEffect(() => {
    editingRef.current = editing;
  }, [editing]);

  const setProfileFormValues = useCallback((profile: UserProfileData) => {
    form.setFieldsValue({
      voUserName: resolveVisibleUserDisplayName(profile, profile.voUserName),
      voUserEmail: profile.voUserEmail,
      voAge: profile.voAge || undefined,
      voAddress: profile.voAddress,
    });
  }, [form]);

  const applyProfileSnapshot = useCallback((profile: UserProfileData, replaceDraft: boolean) => {
    profileRef.current = profile;
    setProfileData(profile);
    setAuthorityState('ready');
    setLoadError(undefined);
    if (replaceDraft || !editingRef.current) {
      setProfileFormValues(profile);
      setDirty(false);
    }
  }, [setProfileFormValues]);

  const loadProfile = useCallback(async (replaceDraft = true) => {
    if (!user) {
      return;
    }

    const requestId = ++loadRequestIdRef.current;
    const hadSnapshot = profileRef.current !== null;
    setReading(true);
    setLoadError(undefined);
    if (!hadSnapshot) {
      setAuthorityState('loading');
    }

    try {
      const response = await userApi.getMyProfile();
      if (!response.ok || !response.data) {
        throw new Error(response.message || t('profile.feedback.loadFailed'));
      }
      if (requestId !== loadRequestIdRef.current) {
        return;
      }

      applyProfileSnapshot({
        ...response.data,
        voRoles: user.roles || [],
      }, replaceDraft);
    } catch (error) {
      if (requestId !== loadRequestIdRef.current) {
        return;
      }

      log.error('UserProfile', '加载个人信息失败:', error);
      const errorMessage = error instanceof Error ? error.message : t('profile.feedback.loadFailed');
      setLoadError(errorMessage);
      setAuthorityState(hadSnapshot ? 'stale' : 'unavailable');
      if (!hadSnapshot) {
        setProfileData(null);
      }
      message.error(errorMessage);
    } finally {
      if (requestId === loadRequestIdRef.current) {
        setReading(false);
      }
    }
  }, [applyProfileSnapshot, t, user]);

  useEffect(() => {
    if (user) {
      void loadProfile(true);
    } else if (!userLoading) {
      profileRef.current = null;
      setProfileData(null);
    }

    return () => {
      loadRequestIdRef.current += 1;
    };
  }, [user, userLoading, loadProfile]);

  const handleReload = () => {
    if (!dirty) {
      void loadProfile(true);
      return;
    }

    Modal.confirm({
      title: t('profile.dirty.reloadTitle'),
      content: t('profile.dirty.reloadDescription'),
      okText: t('profile.dirty.discard'),
      cancelText: t('profile.dirty.continue'),
      onOk: () => {
        setDirty(false);
        setEditing(false);
        void loadProfile(true);
      },
    });
  };

  const handleSave = async () => {
    if (authorityState !== 'ready' || !dirty || savingProfile || avatarBusy) {
      return;
    }

    try {
      const values = await form.validateFields();
      setSavingProfile(true);
      const age = values.voAge === undefined || values.voAge === null || values.voAge === ''
        ? undefined
        : Number(values.voAge);
      const response = await userApi.updateMyProfile({
        userName: values.voUserName?.trim(),
        userEmail: values.voUserEmail?.trim(),
        age: Number.isFinite(age) ? age : undefined,
        address: values.voAddress?.trim(),
      });

      if (!response.ok) {
        throw new Error(response.message || t('profile.feedback.updateFailed'));
      }

      setEditing(false);
      setDirty(false);
      setAuthorityState('stale');
      await refreshUser();
      await loadProfile(true);
      message.success(t('profile.feedback.updated'));
    } catch (error) {
      log.error('UserProfile', '更新个人信息失败:', error);
      message.error(error instanceof Error ? error.message : t('profile.feedback.updateFailed'));
    } finally {
      setSavingProfile(false);
    }
  };

  const handleCancel = () => {
    if (savingProfile) {
      return;
    }

    const cancel = () => {
      if (profileRef.current) {
        setProfileFormValues(profileRef.current);
      }
      setDirty(false);
      setEditing(false);
    };

    if (!dirty) {
      cancel();
      return;
    }

    Modal.confirm({
      title: t('profile.dirty.cancelTitle'),
      content: t('profile.dirty.cancelDescription'),
      okText: t('profile.dirty.discard'),
      cancelText: t('profile.dirty.continue'),
      onOk: cancel,
    });
  };

  const persistUploadedAvatar = async (fileId: string, avatarUrl: string) => {
    try {
      const response = await userApi.setMyAvatar(fileId);
      if (!response.ok) {
        throw new Error(response.message || t('profile.feedback.avatarSaveFailed'));
      }

      if (profileRef.current) {
        const nextProfile = { ...profileRef.current, voAvatarUrl: avatarUrl };
        profileRef.current = nextProfile;
        setProfileData(nextProfile);
      }
      setAuthorityState('stale');
      await refreshUser();
      await loadProfile(false);
      message.success(t('profile.feedback.avatarUpdated'));
    } catch (error) {
      log.error('UserProfile', '保存头像失败:', error);
      message.error(error instanceof Error ? error.message : t('profile.feedback.avatarSaveFailed'));
    } finally {
      setAvatarBusy(false);
    }
  };

  const uploadProps: UploadProps = {
    name: 'file',
    action: `${getApiBaseUrl()}/api/v1/Attachment/UploadImage`,
    headers: {
      authorization: `Bearer ${tokenService.getAccessToken()}`,
    },
    data: {
      businessType: 'Avatar',
      generateThumbnail: true,
    },
    disabled: authorityState !== 'ready' || savingProfile || avatarBusy,
    beforeUpload: (file) => {
      if (!isSupportedAttachmentImageFile(file)) {
        message.error(t('profile.feedback.imageOnly'));
        return false;
      }
      if (file.size / 1024 / 1024 >= 2) {
        message.error(t('profile.feedback.imageTooLarge'));
        return false;
      }
      return true;
    },
    onChange: (info) => {
      if (info.file.status === 'uploading') {
        setAvatarBusy(true);
        return;
      }
      if (info.file.status === 'error') {
        setAvatarBusy(false);
        log.error('UserProfile', '头像上传失败:', info.file.error);
        message.error(t('profile.feedback.avatarUploadFailed'));
        return;
      }
      if (info.file.status !== 'done') {
        return;
      }

      const response = info.file.response;
      const fileId = response?.responseData?.voId;
      const avatarUrl = response?.responseData?.voUrl
        || response?.responseData?.VoUrl
        || response?.responseData?.url;
      if (!response?.isSuccess || !fileId || !avatarUrl) {
        setAvatarBusy(false);
        message.error(t(fileId ? 'profile.feedback.avatarUploadFailed' : 'profile.feedback.avatarMissingFile'));
        return;
      }

      void persistUploadedAvatar(String(fileId), avatarUrl);
    },
  };

  if (userLoading) {
    return (
      <div className="admin-feature-page user-profile-page">
        <section className="admin-feature-card">
          <div className="admin-feature-header">
            <div>
              <h2><UserOutlined /> {t('profile.title')}</h2>
              <p className="admin-feature-subtle">{t('profile.loading.user')}</p>
            </div>
            <Tag color="processing">{t('profile.loading.tag')}</Tag>
          </div>
        </section>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="admin-feature-page user-profile-page">
        <section className="admin-feature-card">
          <div className="user-profile-empty">
            <p>{t('profile.empty.message')}</p>
            <Button onClick={() => window.location.reload()}>{t('profile.empty.refresh')}</Button>
          </div>
        </section>
      </div>
    );
  }

  if (!profileData) {
    return (
      <div className="admin-feature-page user-profile-page">
        <section className="admin-feature-card">
          {authorityState === 'unavailable' ? (
            <div className="user-profile-empty" role="alert">
              <h2><UserOutlined /> {t('profile.unavailable.title')}</h2>
              <p>{loadError || t('profile.feedback.loadFailed')}</p>
              <Button onClick={() => void loadProfile()} loading={reading}>{t('profile.unavailable.retry')}</Button>
            </div>
          ) : (
            <div className="admin-feature-header">
              <div>
                <h2><UserOutlined /> {t('profile.title')}</h2>
                <p className="admin-feature-subtle">{t('profile.loading.form')}</p>
              </div>
              <Tag color="processing">{t('profile.loading.tag')}</Tag>
            </div>
          )}
        </section>
      </div>
    );
  }

  const profileDisplayName = resolveVisibleUserDisplayName(
    profileData,
    profileData.voUserId ? t('profile.userFallback', { id: profileData.voUserId }) : '--',
  );
  const profileDisplayHandle = resolveVisibleUserHandle(profileData, profileDisplayName);
  const writesAreAuthoritative = authorityState === 'ready';
  const profileBusy = reading || savingProfile || avatarBusy;

  return (
    <div className="admin-feature-page user-profile-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div className="profile-heading">
            <Avatar
              size={72}
              src={getAvatarUrl(profileData.voAvatarUrl)}
              icon={<UserOutlined />}
              className="profile-avatar"
            />
            <div>
              <h2><UserOutlined /> {t('profile.title')}</h2>
              <p className="admin-feature-subtle">{t('profile.description')}</p>
            </div>
          </div>
          <div className="profile-actions">
            <Button icon={<ReloadOutlined />} onClick={handleReload} loading={reading} disabled={savingProfile || avatarBusy}>
              {t('profile.action.reload')}
            </Button>
            {!editing ? (
              <Button
                icon={<EditOutlined />}
                onClick={() => setEditing(true)}
                disabled={!writesAreAuthoritative || profileBusy}
              >
                {t('profile.action.edit')}
              </Button>
            ) : (
              <Space>
                <Button onClick={handleCancel} disabled={savingProfile}>{t('profile.action.cancel')}</Button>
                <Button
                  type="primary"
                  icon={<SaveOutlined />}
                  loading={savingProfile}
                  disabled={!writesAreAuthoritative || avatarBusy || !dirty}
                  onClick={() => void handleSave()}
                >
                  {t('profile.action.save')}
                </Button>
              </Space>
            )}
          </div>
        </div>
      </section>

      {authorityState === 'stale' ? (
        <div className="self-service-authority self-service-authority--stale" role="alert">
          <div>
            <strong>{t('profile.authority.staleTitle')}</strong>
            <span>{t('profile.authority.staleDescription')}</span>
          </div>
          <Button onClick={handleReload} loading={reading}>{t('profile.action.reloadAuthority')}</Button>
        </div>
      ) : null}
      {dirty ? (
        <div className="self-service-authority self-service-authority--dirty" role="status">
          <strong>{t('profile.dirty.title')}</strong>
          <span>{t('profile.dirty.description')}</span>
        </div>
      ) : null}

      <section className="admin-feature-metrics" aria-label={t('profile.metrics.label')}>
        <div className="admin-feature-metric">{t('profile.metrics.displayName')}<strong>{profileDisplayName}</strong></div>
        <div className="admin-feature-metric">{t('profile.metrics.handle')}<strong>{profileDisplayHandle || '--'}</strong></div>
        <div className="admin-feature-metric">{t('profile.metrics.roles')}<strong>{profileData.voRoles.length}</strong></div>
        <div className="admin-feature-metric">{t('profile.metrics.userId')}<strong>{profileData.voUserId}</strong></div>
      </section>

      <div className="admin-settings-layout user-profile-layout">
        <aside className="admin-settings-nav user-profile-summary">
          <h3>{t('profile.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('profile.summary.description')}</p>
          <div className="avatar-section">
            <Avatar size={80} src={getAvatarUrl(profileData.voAvatarUrl)} icon={<UserOutlined />} className="profile-avatar" />
            <Upload {...uploadProps} showUploadList={false}>
              <Button icon={<CameraOutlined />} size="small" className="avatar-upload-btn" loading={avatarBusy} disabled={!writesAreAuthoritative || savingProfile || avatarBusy}>
                {t('profile.summary.changeAvatar')}
              </Button>
            </Upload>
          </div>
        </aside>

        <main className="admin-settings-main user-profile-primary-task">
          <section className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div className="admin-setting-section__title-main">
                <SettingOutlined />
                <h3>{t('profile.basic.title')}</h3>
              </div>
              <Tag color={dirty ? 'warning' : undefined}>{editing ? t('profile.basic.editing') : t('profile.basic.readOnly')}</Tag>
            </div>

            <Form
              form={form}
              layout="vertical"
              disabled={!editing || savingProfile}
              onValuesChange={(_, values) => {
                if (editing) {
                  setDirty(isProfileDraftDirty(values, profileRef.current));
                }
              }}
            >
              <Form.Item name="voUserName" label={t('profile.form.displayName')} rules={[{ required: true, message: t('profile.form.displayNameRequired') }]}>
                <Input placeholder={t('profile.form.displayNamePlaceholder')} />
              </Form.Item>
              <Form.Item
                name="voUserEmail"
                label={t('profile.form.email')}
                rules={[
                  { required: true, message: t('profile.form.emailRequired') },
                  { type: 'email', message: t('profile.form.emailInvalid') },
                ]}
              >
                <Input placeholder={t('profile.form.emailPlaceholder')} />
              </Form.Item>
              <Form.Item
                name="voAge"
                label={t('profile.form.age')}
                rules={[{
                  validator(_, value) {
                    if (value === undefined || value === null || value === '') {
                      return Promise.resolve();
                    }
                    const age = Number(value);
                    return Number.isInteger(age) && age >= 0
                      ? Promise.resolve()
                      : Promise.reject(new Error(t('profile.form.ageInvalid')));
                  },
                }]}
              >
                <Input placeholder={t('profile.form.agePlaceholder')} />
              </Form.Item>
              <Form.Item name="voAddress" label={t('profile.form.address')} rules={[{ max: 2000, message: t('profile.form.addressLength') }]}>
                <Input.TextArea rows={3} placeholder={t('profile.form.addressPlaceholder')} />
              </Form.Item>
            </Form>
          </section>
        </main>

        <aside className="admin-settings-aside user-profile-account-summary">
          <h3>{t('profile.account.title')}</h3>
          <p className="admin-feature-subtle">{t('profile.account.description')}</p>
          <div className="admin-settings-aside__list">
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('profile.account.authority')}</span>
              <span className="admin-settings-aside__value">{t(`profile.authority.state.${authorityState}`)}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('profile.account.userId')}</span>
              <span className="admin-settings-aside__value">{profileData.voUserId}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('profile.account.roles')}</span>
              <span className="admin-settings-aside__value">{profileData.voRoles.join(', ') || t('profile.account.noRoles')}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('profile.account.registered')}</span>
              <span className="admin-settings-aside__value">{formatConsoleDateTime(profileData.voCreateTime, i18n.resolvedLanguage ?? i18n.language)}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('profile.account.lastLogin')}</span>
              <span className="admin-settings-aside__value">{t('profile.account.unavailable')}</span>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
};

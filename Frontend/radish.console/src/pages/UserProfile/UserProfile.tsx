import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useUser } from '@/hooks/useUser';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  AntInput as Input,
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
import { SaveOutlined, CameraOutlined } from '@ant-design/icons';
import { getApiBaseUrl, getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { userApi, type MyProfileInfo } from '@/api/user';
import { tokenService } from '@/services/tokenService';
import { formatConsoleDateTime } from '@/utils/localeFormatters';
import '../adminFeature.css';
import './UserProfile.css';

interface UserProfileData extends MyProfileInfo {
  voRoles: string[];
}

export const UserProfile = () => {
  const { t, i18n } = useTranslation();
  useDocumentTitle(t('profile.documentTitle'));
  const { user, loading: userLoading, refreshUser } = useUser();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [profileData, setProfileData] = useState<UserProfileData | null>(null);

  const setProfileFormValues = useCallback((profile: UserProfileData) => {
    form.setFieldsValue({
      voUserName: resolveVisibleUserDisplayName(profile, profile.voUserName),
      voUserEmail: profile.voUserEmail,
      voAge: profile.voAge || undefined,
      voAddress: profile.voAddress,
    });
  }, [form]);

  const loadProfile = useCallback(async () => {
    if (!user) return;

    try {
      setLoading(true);
      const response = await userApi.getMyProfile();
      if (!response.ok || !response.data) {
        throw new Error(response.message || t('profile.feedback.loadFailed'));
      }

      const nextProfile: UserProfileData = {
        ...response.data,
        voRoles: user.roles || [],
      };
      setProfileData(nextProfile);
    } catch (error) {
      log.error('UserProfile', '加载个人信息失败:', error);
      message.error(error instanceof Error ? error.message : t('profile.feedback.loadFailed'));
      setProfileData(null);
    } finally {
      setLoading(false);
    }
  }, [user, t]);

  useEffect(() => {
    if (user) {
      void loadProfile();
    } else if (!userLoading) {
      setProfileData(null);
    }
  }, [user, userLoading, loadProfile]);

  useEffect(() => {
    if (profileData) {
      setProfileFormValues(profileData);
    }
  }, [profileData, setProfileFormValues]);

  // 保存个人信息
  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);
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

      setProfileData(prev => prev ? {
        ...prev,
        voDisplayName: values.voUserName,
        voUserName: values.voUserName,
        voUserEmail: values.voUserEmail,
        voAge: values.voAge || 0,
        voAddress: values.voAddress || '',
      } : null);

      await refreshUser();
      await loadProfile();
      message.success(t('profile.feedback.updated'));
      setEditing(false);
    } catch (error) {
      log.error('UserProfile', '更新个人信息失败:', error);
      message.error(error instanceof Error ? error.message : t('profile.feedback.updateFailed'));
    } finally {
      setLoading(false);
    }
  };

  // 取消编辑
  const handleCancel = () => {
    if (profileData) {
      setProfileFormValues(profileData);
    }
    setEditing(false);
  };

  // 头像上传
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
    beforeUpload: (file) => {
      const isImage = isSupportedAttachmentImageFile(file);
      if (!isImage) {
        message.error(t('profile.feedback.imageOnly'));
        return false;
      }
      const isLt2M = file.size / 1024 / 1024 < 2;
      if (!isLt2M) {
        message.error(t('profile.feedback.imageTooLarge'));
        return false;
      }
      return true;
    },
    onChange: (info) => {
      if (info.file.status === 'uploading') {
        log.debug('UserProfile', '正在上传头像...');
      } else if (info.file.status === 'done') {
        const response = info.file.response;
        log.debug('UserProfile', '头像上传响应:', response);

        if (response?.isSuccess && response?.responseData) {
          // 获取文件ID和URL
          const fileId = response.responseData.voId;
          const avatarUrl = response.responseData.voUrl ||
                           response.responseData.VoUrl ||
                           response.responseData.url;

          if (fileId && avatarUrl) {
            // 调用后端接口保存头像关联（fileId 是字符串类型的雪花ID）
            userApi.setMyAvatar(String(fileId))
              .then(setAvatarResponse => {
                if (setAvatarResponse.ok) {
                  // 更新本地状态
                  setProfileData(prev => prev ? {
                    ...prev,
                    voAvatarUrl: avatarUrl,
                  } : null);

                  // 刷新UserContext中的用户信息，更新右上角导航栏头像
                  void refreshUser();

                  message.success(t('profile.feedback.avatarUpdated'));
                  log.debug('UserProfile', '头像已保存到数据库');
                } else {
                  message.error(setAvatarResponse.message || t('profile.feedback.avatarSaveFailed'));
                  log.error('UserProfile', '保存头像失败:', setAvatarResponse.message);
                }
              })
              .catch(error => {
                message.error(t('profile.feedback.avatarSaveFailed'));
                log.error('UserProfile', '保存头像异常:', error);
              });
          } else {
            message.error(t('profile.feedback.avatarMissingFile'));
          }
        } else {
          message.error(response?.messageInfo || t('profile.feedback.avatarUploadFailed'));
        }
      } else if (info.file.status === 'error') {
        log.error('UserProfile', '头像上传失败:', info.file.error);
        message.error(t('profile.feedback.avatarUploadFailed'));
      }
    },
  };

  if (userLoading) {
    return (
      <div className="admin-feature-page user-profile-page">
        <section className="admin-feature-card">
          <div className="admin-feature-header">
            <div>
              <h2>
                <UserOutlined /> {t('profile.title')}
              </h2>
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
            <Button onClick={() => window.location.reload()}>
              {t('profile.empty.refresh')}
            </Button>
          </div>
        </section>
      </div>
    );
  }

  if (!profileData) {
    return (
      <div className="admin-feature-page user-profile-page">
        <section className="admin-feature-card">
          <div className="admin-feature-header">
            <div>
              <h2>
                <UserOutlined /> {t('profile.title')}
              </h2>
              <p className="admin-feature-subtle">{t('profile.loading.form')}</p>
            </div>
            <Tag color="processing">{t('profile.loading.tag')}</Tag>
          </div>
        </section>
      </div>
    );
  }

  const profileDisplayName = resolveVisibleUserDisplayName(
    profileData,
    profileData.voUserId ? t('profile.userFallback', { id: profileData.voUserId }) : '--',
  );
  const profileDisplayHandle = resolveVisibleUserHandle(profileData, profileDisplayName);

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
              <h2>
                <UserOutlined /> {t('profile.title')}
              </h2>
              <p className="admin-feature-subtle">{t('profile.description')}</p>
            </div>
          </div>
          <div className="profile-actions">
            {!editing ? (
              <Button
                icon={<EditOutlined />}
                onClick={() => setEditing(true)}
              >
                {t('profile.action.edit')}
              </Button>
            ) : (
              <Space>
                <Button onClick={handleCancel}>
                  {t('profile.action.cancel')}
                </Button>
                <Button
                  type="primary"
                  icon={<SaveOutlined />}
                  loading={loading}
                  onClick={handleSave}
                >
                  {t('profile.action.save')}
                </Button>
              </Space>
            )}
          </div>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label={t('profile.metrics.label')}>
        <div className="admin-feature-metric">
          {t('profile.metrics.displayName')}
          <strong>{profileDisplayName}</strong>
        </div>
        <div className="admin-feature-metric">
          {t('profile.metrics.handle')}
          <strong>{profileDisplayHandle || '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          {t('profile.metrics.roles')}
          <strong>{profileData.voRoles.length}</strong>
        </div>
        <div className="admin-feature-metric">
          {t('profile.metrics.userId')}
          <strong>{profileData.voUserId}</strong>
        </div>
      </section>

      <div className="admin-settings-layout user-profile-layout">
        <aside className="admin-settings-nav">
          <h3>{t('profile.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('profile.summary.description')}</p>
          <div className="avatar-section">
            <Avatar
              size={80}
              src={getAvatarUrl(profileData.voAvatarUrl)}
              icon={<UserOutlined />}
              className="profile-avatar"
            />
            <Upload {...uploadProps} showUploadList={false}>
              <Button
                icon={<CameraOutlined />}
                size="small"
                className="avatar-upload-btn"
              >
                {t('profile.summary.changeAvatar')}
              </Button>
            </Upload>
          </div>
        </aside>

        <main className="admin-settings-main">
          <section className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div className="admin-setting-section__title-main">
                <SettingOutlined />
                <h3>{t('profile.basic.title')}</h3>
              </div>
              <Tag>{editing ? t('profile.basic.editing') : t('profile.basic.readOnly')}</Tag>
            </div>

            <Form
              form={form}
              layout="vertical"
              disabled={!editing}
            >
              <Form.Item
                name="voUserName"
                label={t('profile.form.displayName')}
                rules={[
                  { required: true, message: t('profile.form.displayNameRequired') },
                  { min: 2, max: 50, message: t('profile.form.displayNameLength') },
                ]}
              >
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
                rules={[
                  {
                    validator(_, value) {
                      if (value === undefined || value === null || value === '') {
                        return Promise.resolve();
                      }

                      const age = Number(value);
                      if (Number.isInteger(age) && age >= 0) {
                        return Promise.resolve();
                      }

                      return Promise.reject(new Error(t('profile.form.ageInvalid')));
                    },
                  },
                ]}
              >
                <Input placeholder={t('profile.form.agePlaceholder')} />
              </Form.Item>

              <Form.Item
                name="voAddress"
                label={t('profile.form.address')}
                rules={[{ max: 2000, message: t('profile.form.addressLength') }]}
              >
                <Input.TextArea rows={3} placeholder={t('profile.form.addressPlaceholder')} />
              </Form.Item>
            </Form>
          </section>
        </main>

        <aside className="admin-settings-aside">
          <h3>{t('profile.account.title')}</h3>
          <p className="admin-feature-subtle">{t('profile.account.description')}</p>
          <div className="admin-settings-aside__list">
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
              <span className="admin-settings-aside__value">
                {formatConsoleDateTime(profileData.voCreateTime, i18n.resolvedLanguage ?? i18n.language)}
              </span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('profile.account.lastLogin')}</span>
              <span className="admin-settings-aside__value">{t('profile.account.noLoginRecord')}</span>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
};

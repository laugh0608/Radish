import { useCallback, useEffect, useState } from 'react';
import { useUser } from '@/hooks/useUser';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  AntInput as Input,
  Avatar,
  Space,
  message,
  Tag,
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
import '../adminFeature.css';
import './UserProfile.css';

interface UserProfileData extends MyProfileInfo {
  voRoles: string[];
}

export const UserProfile = () => {
  useDocumentTitle('个人信息');
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
        throw new Error(response.message || '加载个人信息失败');
      }

      const nextProfile: UserProfileData = {
        ...response.data,
        voRoles: user.roles || [],
      };
      setProfileData(nextProfile);
      setProfileFormValues(nextProfile);
    } catch (error) {
      log.error('UserProfile', '加载个人信息失败:', error);
      message.error(error instanceof Error ? error.message : '加载个人信息失败');
      setProfileData(null);
    } finally {
      setLoading(false);
    }
  }, [user, setProfileFormValues]);

  useEffect(() => {
    if (user) {
      void loadProfile();
    } else if (!userLoading) {
      setProfileData(null);
    }
  }, [user, userLoading, loadProfile]);

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
        throw new Error(response.message || '更新个人信息失败');
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
      message.success('个人信息更新成功');
      setEditing(false);
    } catch (error) {
      log.error('UserProfile', '更新个人信息失败:', error);
      message.error(error instanceof Error ? error.message : '更新个人信息失败');
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
      const isImage = file.type.startsWith('image/');
      if (!isImage) {
        message.error('只能上传图片文件！');
        return false;
      }
      const isLt2M = file.size / 1024 / 1024 < 2;
      if (!isLt2M) {
        message.error('图片大小不能超过 2MB！');
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

                  message.success('头像更新成功');
                  log.debug('UserProfile', '头像已保存到数据库');
                } else {
                  message.error(setAvatarResponse.message || '保存头像失败');
                  log.error('UserProfile', '保存头像失败:', setAvatarResponse.message);
                }
              })
              .catch(error => {
                message.error('保存头像失败');
                log.error('UserProfile', '保存头像异常:', error);
              });
          } else {
            message.error('头像上传失败：未获取到文件信息');
          }
        } else {
          message.error(response?.messageInfo || '头像上传失败');
        }
      } else if (info.file.status === 'error') {
        log.error('UserProfile', '头像上传失败:', info.file.error);
        message.error('头像上传失败');
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
                <UserOutlined /> 个人信息
              </h2>
              <p className="admin-feature-subtle">正在加载当前登录用户资料。</p>
            </div>
            <Tag color="processing">加载中</Tag>
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
            <p>无法获取用户信息，请重新登录</p>
            <Button onClick={() => window.location.reload()}>
              刷新页面
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
                <UserOutlined /> 个人信息
              </h2>
              <p className="admin-feature-subtle">正在加载个人资料表单。</p>
            </div>
            <Tag color="processing">加载中</Tag>
          </div>
        </section>
      </div>
    );
  }

  const profileDisplayName = resolveVisibleUserDisplayName(profileData, profileData.voUserId ? `用户 ${profileData.voUserId}` : '--');
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
                <UserOutlined /> 个人信息
              </h2>
              <p className="admin-feature-subtle">维护当前登录账号的基础资料和头像。</p>
            </div>
          </div>
          <div className="profile-actions">
            {!editing ? (
              <Button
                icon={<EditOutlined />}
                onClick={() => setEditing(true)}
              >
                编辑信息
              </Button>
            ) : (
              <Space>
                <Button onClick={handleCancel}>
                  取消
                </Button>
                <Button
                  type="primary"
                  icon={<SaveOutlined />}
                  loading={loading}
                  onClick={handleSave}
                >
                  保存
                </Button>
              </Space>
            )}
          </div>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label="个人资料指标">
        <div className="admin-feature-metric">
          展示名称
          <strong>{profileDisplayName}</strong>
        </div>
        <div className="admin-feature-metric">
          公开句柄
          <strong>{profileDisplayHandle || '--'}</strong>
        </div>
        <div className="admin-feature-metric">
          角色数量
          <strong>{profileData.voRoles.length}</strong>
        </div>
        <div className="admin-feature-metric">
          用户 ID
          <strong>{profileData.voUserId}</strong>
        </div>
      </section>

      <div className="admin-settings-layout user-profile-layout">
        <aside className="admin-settings-nav">
          <h3>资料摘要</h3>
          <p className="admin-feature-subtle">当前登录账号的身份和头像状态。</p>
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
                更换头像
              </Button>
            </Upload>
          </div>
        </aside>

        <main className="admin-settings-main">
          <section className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div className="admin-setting-section__title-main">
                <SettingOutlined />
                <h3>基础资料</h3>
              </div>
              <Tag>{editing ? '编辑中' : '只读'}</Tag>
            </div>

            <Form
              form={form}
              layout="vertical"
              disabled={!editing}
            >
              <Form.Item
                name="voUserName"
                label="展示名称"
                rules={[
                  { required: true, message: '请输入展示名称' },
                  { min: 2, max: 50, message: '展示名称长度为2-50个字符' },
                ]}
              >
                <Input placeholder="请输入展示名称" />
              </Form.Item>

              <Form.Item
                name="voUserEmail"
                label="邮箱"
                rules={[
                  { required: true, message: '请输入邮箱' },
                  { type: 'email', message: '请输入有效的邮箱地址' },
                ]}
              >
                <Input placeholder="请输入邮箱" />
              </Form.Item>

              <Form.Item
                name="voAge"
                label="年龄"
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

                      return Promise.reject(new Error('年龄必须是非负整数'));
                    },
                  },
                ]}
              >
                <Input placeholder="请输入年龄" />
              </Form.Item>

              <Form.Item
                name="voAddress"
                label="地址"
                rules={[{ max: 2000, message: '地址长度不能超过2000个字符' }]}
              >
                <Input.TextArea rows={3} placeholder="请输入地址" />
              </Form.Item>
            </Form>
          </section>
        </main>

        <aside className="admin-settings-aside">
          <h3>账号摘要</h3>
          <p className="admin-feature-subtle">用于核对当前登录账号的角色和注册信息。</p>
          <div className="admin-settings-aside__list">
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">用户 ID</span>
              <span className="admin-settings-aside__value">{profileData.voUserId}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">角色</span>
              <span className="admin-settings-aside__value">{profileData.voRoles.join(', ') || '无角色'}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">注册时间</span>
              <span className="admin-settings-aside__value">
                {new Date(profileData.voCreateTime).toLocaleString('zh-CN')}
              </span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">最后登录</span>
              <span className="admin-settings-aside__value">暂无记录</span>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
};

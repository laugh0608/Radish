import { useState, useEffect } from 'react';
import { useUser } from '@/contexts/UserContext';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  AntInput as Input,
  Avatar,
  Space,
  message,
} from '@radish/ui';
import { Card, Form, Divider, Upload, Button } from 'antd';
import type { UploadProps } from 'antd';
import {
  UserOutlined,
  EditOutlined,
} from '@radish/ui';
import { SaveOutlined, CameraOutlined } from '@ant-design/icons';
import { getApiBaseUrl, getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';
import { userApi, type MyProfileInfo } from '@/api/user';
import { tokenService } from '@/services/tokenService';
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

  const setProfileFormValues = (profile: UserProfileData) => {
    form.setFieldsValue({
      voUserName: profile.voUserName,
      voUserEmail: profile.voUserEmail,
      voRealName: profile.voRealName,
      voAge: profile.voAge || undefined,
      voAddress: profile.voAddress,
    });
  };

  const loadProfile = async () => {
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
  };

  useEffect(() => {
    if (user) {
      void loadProfile();
    } else if (!userLoading) {
      setProfileData(null);
    }
  }, [user, userLoading]);

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
        realName: values.voRealName?.trim(),
        age: Number.isFinite(age) ? age : undefined,
        address: values.voAddress?.trim(),
      });

      if (!response.ok) {
        throw new Error(response.message || '更新个人信息失败');
      }

      setProfileData(prev => prev ? {
        ...prev,
        voUserName: values.voUserName,
        voUserEmail: values.voUserEmail,
        voRealName: values.voRealName || '',
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
      <div className="user-profile-page">
        <Card title="个人信息" loading>
          <div style={{ height: '200px' }} />
        </Card>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="user-profile-page">
        <Card title="个人信息">
          <div style={{ textAlign: 'center', padding: '40px' }}>
            <p>无法获取用户信息，请重新登录</p>
            <Button onClick={() => window.location.reload()}>
              刷新页面
            </Button>
          </div>
        </Card>
      </div>
    );
  }

  if (!profileData) {
    return (
      <div className="user-profile-page">
        <Card title="个人信息" loading>
          <div style={{ height: '200px' }} />
        </Card>
      </div>
    );
  }

  return (
    <div className="user-profile-page">
      <Card title="个人信息" className="profile-card">
        <div className="profile-header">
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
          <div className="profile-info">
            <h3>{profileData.voUserName}</h3>
            <p className="profile-roles">
              角色: {profileData.voRoles.join(', ')}
            </p>
            <p className="profile-id">
              用户ID: {profileData.voUserId}
            </p>
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

        <Divider />

        <Form
          form={form}
          layout="vertical"
          disabled={!editing}
        >
          <Form.Item
            name="voUserName"
            label="用户名"
            rules={[
              { required: true, message: '请输入用户名' },
              { min: 2, max: 50, message: '用户名长度为2-50个字符' },
            ]}
          >
            <Input placeholder="请输入用户名" />
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
            name="voRealName"
            label="真实姓名"
            rules={[{ max: 50, message: '真实姓名长度不能超过50个字符' }]}
          >
            <Input placeholder="请输入真实姓名" />
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

        <Divider />

        <div className="profile-stats">
          <div className="stat-item">
            <span className="stat-label">注册时间:</span>
            <span className="stat-value">
              {new Date(profileData.voCreateTime).toLocaleString('zh-CN')}
            </span>
          </div>
          <div className="stat-item">
            <span className="stat-label">最后登录:</span>
            <span className="stat-value">
              暂无记录
            </span>
          </div>
        </div>
      </Card>
    </div>
  );
};

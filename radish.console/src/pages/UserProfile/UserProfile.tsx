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
import './UserProfile.css';

interface UserProfileData {
  voId: number;
  voUserName: string;
  voEmail: string;
  voAvatarUrl?: string;
  voCreateTime: string;
  voLastLoginTime?: string;
  voRoles: string[];
}

export const UserProfile = () => {
  useDocumentTitle('个人信息');
  const { user, loading: userLoading, refreshUser } = useUser();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [profileData, setProfileData] = useState<UserProfileData | null>(null);

  // 模拟加载用户详细信息
  useEffect(() => {
    if (user) {
      const mockProfileData: UserProfileData = {
        voId: user.voUserId,
        voUserName: user.voUserName,
        voEmail: 'admin@radish.com', // 模拟邮箱
        voAvatarUrl: user.voAvatarUrl,
        voCreateTime: '2024-01-01T00:00:00Z',
        voLastLoginTime: new Date().toISOString(),
        voRoles: user.roles || ['Admin'],
      };
      setProfileData(mockProfileData);
      form.setFieldsValue({
        voUserName: mockProfileData.voUserName,
        voEmail: mockProfileData.voEmail,
      });
    } else if (!userLoading) {
      // 如果用户加载完成但没有用户信息，显示错误状态
      setProfileData(null);
    }
  }, [user, userLoading, form]);

  // 保存个人信息
  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      // TODO: 调用更新个人信息的 API
      await new Promise(resolve => setTimeout(resolve, 1000)); // 模拟 API 调用

      setProfileData(prev => prev ? {
        ...prev,
        voUserName: values.voUserName,
        voEmail: values.voEmail,
      } : null);

      message.success('个人信息更新成功');
      setEditing(false);
    } catch (error) {
      log.error('UserProfile', '更新个人信息失败:', error);
      message.error('更新个人信息失败');
    } finally {
      setLoading(false);
    }
  };

  // 取消编辑
  const handleCancel = () => {
    if (profileData) {
      form.setFieldsValue({
        voUserName: profileData.voUserName,
        voEmail: profileData.voEmail,
      });
    }
    setEditing(false);
  };

  // 头像上传
  const uploadProps: UploadProps = {
    name: 'file',
    action: `${getApiBaseUrl()}/api/v1/Attachment/UploadImage`,
    headers: {
      authorization: `Bearer ${localStorage.getItem('access_token')}`,
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
          // 根据后端返回的数据结构获取 URL
          const avatarUrl = response.responseData.voUrl ||
                           response.responseData.VoUrl ||
                           response.responseData.url;

          if (avatarUrl) {
            setProfileData(prev => prev ? {
              ...prev,
              voAvatarUrl: avatarUrl,
            } : null);

            // 刷新UserContext中的用户信息，更新右上角导航栏头像
            refreshUser();

            message.success('头像更新成功');
          } else {
            message.error('头像上传失败：未获取到文件URL');
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
              用户ID: {profileData.voId}
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
            name="voEmail"
            label="邮箱"
            rules={[
              { required: true, message: '请输入邮箱' },
              { type: 'email', message: '请输入有效的邮箱地址' },
            ]}
          >
            <Input placeholder="请输入邮箱" />
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
              {profileData.voLastLoginTime
                ? new Date(profileData.voLastLoginTime).toLocaleString('zh-CN')
                : '未知'
              }
            </span>
          </div>
        </div>
      </Card>
    </div>
  );
};
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
import { log } from '@/utils/logger';
import './UserProfile.css';

interface UserProfileData {
  id: number;
  userName: string;
  email: string;
  avatarUrl?: string;
  createTime: string;
  lastLoginTime?: string;
  roles: string[];
}

export const UserProfile = () => {
  useDocumentTitle('个人信息');
  const { user } = useUser();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [profileData, setProfileData] = useState<UserProfileData | null>(null);

  // 模拟加载用户详细信息
  useEffect(() => {
    if (user) {
      const mockProfileData: UserProfileData = {
        id: user.voUserId,
        userName: user.voUserName,
        email: 'admin@radish.com', // 模拟邮箱
        avatarUrl: user.voAvatarUrl,
        createTime: '2024-01-01T00:00:00Z',
        lastLoginTime: new Date().toISOString(),
        roles: user.roles || ['Admin'],
      };
      setProfileData(mockProfileData);
      form.setFieldsValue({
        userName: mockProfileData.userName,
        email: mockProfileData.email,
      });
    }
  }, [user, form]);

  // 保存个人信息
  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      // TODO: 调用更新个人信息的 API
      await new Promise(resolve => setTimeout(resolve, 1000)); // 模拟 API 调用

      setProfileData(prev => prev ? {
        ...prev,
        userName: values.userName,
        email: values.email,
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
        userName: profileData.userName,
        email: profileData.email,
      });
    }
    setEditing(false);
  };

  // 头像上传
  const uploadProps: UploadProps = {
    name: 'file',
    action: '/api/v1/Attachment/UploadImage', // 使用现有的图片上传接口
    headers: {
      authorization: `Bearer ${localStorage.getItem('access_token')}`,
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
      if (info.file.status === 'done') {
        const response = info.file.response;
        if (response?.isSuccess && response?.responseData?.url) {
          setProfileData(prev => prev ? {
            ...prev,
            avatarUrl: response.responseData.url,
          } : null);
          message.success('头像更新成功');
        } else {
          message.error('头像上传失败');
        }
      } else if (info.file.status === 'error') {
        message.error('头像上传失败');
      }
    },
  };

  if (!profileData) {
    return <div>加载中...</div>;
  }

  return (
    <div className="user-profile-page">
      <Card title="个人信息" className="profile-card">
        <div className="profile-header">
          <div className="avatar-section">
            <Avatar
              size={80}
              src={profileData.avatarUrl}
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
            <h3>{profileData.userName}</h3>
            <p className="profile-roles">
              角色: {profileData.roles.join(', ')}
            </p>
            <p className="profile-id">
              用户ID: {profileData.id}
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
            name="userName"
            label="用户名"
            rules={[
              { required: true, message: '请输入用户名' },
              { min: 2, max: 50, message: '用户名长度为2-50个字符' },
            ]}
          >
            <Input placeholder="请输入用户名" />
          </Form.Item>

          <Form.Item
            name="email"
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
              {new Date(profileData.createTime).toLocaleString('zh-CN')}
            </span>
          </div>
          <div className="stat-item">
            <span className="stat-label">最后登录:</span>
            <span className="stat-value">
              {profileData.lastLoginTime
                ? new Date(profileData.lastLoginTime).toLocaleString('zh-CN')
                : '未知'
              }
            </span>
          </div>
        </div>
      </Card>
    </div>
  );
};
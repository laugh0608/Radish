import { useState } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  AntInput as Input,
  Switch,
  AntSelect as Select,
  message,
  Space,
} from '@radish/ui';
import { Card, Form, Divider, Modal, Button } from 'antd';
import {
  SettingOutlined,
  LockOutlined,
  EyeOutlined,
} from '@radish/ui';
import { SaveOutlined, BellOutlined } from '@ant-design/icons';
import { log } from '@/utils/logger';
import './Settings.css';

interface SettingsData {
  // 通知设置
  emailNotifications: boolean;
  browserNotifications: boolean;
  systemNotifications: boolean;

  // 界面设置
  theme: 'light' | 'dark' | 'auto';
  language: 'zh-CN' | 'en-US';
  pageSize: number;

  // 安全设置
  twoFactorAuth: boolean;
  sessionTimeout: number;
}

export const Settings = () => {
  useDocumentTitle('设置');
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [passwordModalVisible, setPasswordModalVisible] = useState(false);
  const [passwordForm] = Form.useForm();
  const [passwordLoading, setPasswordLoading] = useState(false);

  // 默认设置
  const [settings, setSettings] = useState<SettingsData>({
    emailNotifications: true,
    browserNotifications: true,
    systemNotifications: false,
    theme: 'light',
    language: 'zh-CN',
    pageSize: 20,
    twoFactorAuth: false,
    sessionTimeout: 30,
  });

  // 初始化表单值
  useState(() => {
    form.setFieldsValue(settings);
  });

  // 保存设置
  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      // TODO: 调用保存设置的 API
      await new Promise(resolve => setTimeout(resolve, 1000)); // 模拟 API 调用

      setSettings(values);
      message.success('设置保存成功');

      // 应用主题设置
      if (values.theme !== settings.theme) {
        applyTheme(values.theme);
      }
    } catch (error) {
      log.error('Settings', '保存设置失败:', error);
      message.error('保存设置失败');
    } finally {
      setLoading(false);
    }
  };

  // 应用主题
  const applyTheme = (theme: string) => {
    // TODO: 实现主题切换逻辑
    log.info('Settings', '应用主题:', theme);
  };

  // 修改密码
  const handleChangePassword = async () => {
    try {
      const values = await passwordForm.validateFields();
      setPasswordLoading(true);

      // TODO: 调用修改密码的 API
      await new Promise(resolve => setTimeout(resolve, 1000)); // 模拟 API 调用

      message.success('密码修改成功');
      setPasswordModalVisible(false);
      passwordForm.resetFields();
    } catch (error) {
      log.error('Settings', '修改密码失败:', error);
      message.error('修改密码失败');
    } finally {
      setPasswordLoading(false);
    }
  };

  // 重置设置
  const handleReset = () => {
    Modal.confirm({
      title: '重置设置',
      content: '确定要重置所有设置为默认值吗？此操作不可撤销。',
      onOk: () => {
        const defaultSettings: SettingsData = {
          emailNotifications: true,
          browserNotifications: true,
          systemNotifications: false,
          theme: 'light',
          language: 'zh-CN',
          pageSize: 20,
          twoFactorAuth: false,
          sessionTimeout: 30,
        };
        setSettings(defaultSettings);
        form.setFieldsValue(defaultSettings);
        message.success('设置已重置为默认值');
      },
    });
  };

  return (
    <div className="settings-page">
      <div className="settings-header">
        <h2>
          <SettingOutlined /> 设置
        </h2>
        <Space>
          <Button onClick={handleReset}>
            重置默认
          </Button>
          <Button
            type="primary"
            icon={<SaveOutlined />}
            loading={loading}
            onClick={handleSave}
          >
            保存设置
          </Button>
        </Space>
      </div>

      <div className="settings-content">
        <Form
          form={form}
          layout="vertical"
          initialValues={settings}
        >
          {/* 通知设置 */}
          <Card title={<><BellOutlined /> 通知设置</>} className="settings-card">
            <Form.Item
              name="emailNotifications"
              label="邮件通知"
              valuePropName="checked"
            >
              <Switch checkedChildren="开启" unCheckedChildren="关闭" />
            </Form.Item>
            <Form.Item
              name="browserNotifications"
              label="浏览器通知"
              valuePropName="checked"
            >
              <Switch checkedChildren="开启" unCheckedChildren="关闭" />
            </Form.Item>
            <Form.Item
              name="systemNotifications"
              label="系统通知"
              valuePropName="checked"
            >
              <Switch checkedChildren="开启" unCheckedChildren="关闭" />
            </Form.Item>
          </Card>

          {/* 界面设置 */}
          <Card title={<><EyeOutlined /> 界面设置</>} className="settings-card">
            <Form.Item
              name="theme"
              label="主题"
            >
              <Select
                options={[
                  { label: '浅色主题', value: 'light' },
                  { label: '深色主题', value: 'dark' },
                  { label: '跟随系统', value: 'auto' },
                ]}
              />
            </Form.Item>
            <Form.Item
              name="language"
              label="语言"
            >
              <Select
                options={[
                  { label: '简体中文', value: 'zh-CN' },
                  { label: 'English', value: 'en-US' },
                ]}
              />
            </Form.Item>
            <Form.Item
              name="pageSize"
              label="每页显示条数"
            >
              <Select
                options={[
                  { label: '10 条/页', value: 10 },
                  { label: '20 条/页', value: 20 },
                  { label: '50 条/页', value: 50 },
                  { label: '100 条/页', value: 100 },
                ]}
              />
            </Form.Item>
          </Card>

          {/* 安全设置 */}
          <Card title={<><LockOutlined /> 安全设置</>} className="settings-card">
            <Form.Item
              name="twoFactorAuth"
              label="双因素认证"
              valuePropName="checked"
            >
              <Switch checkedChildren="开启" unCheckedChildren="关闭" />
            </Form.Item>
            <Form.Item
              name="sessionTimeout"
              label="会话超时时间（分钟）"
            >
              <Select
                options={[
                  { label: '15 分钟', value: 15 },
                  { label: '30 分钟', value: 30 },
                  { label: '60 分钟', value: 60 },
                  { label: '120 分钟', value: 120 },
                ]}
              />
            </Form.Item>

            <Divider />

            <div className="password-section">
              <h4>密码管理</h4>
              <p>定期更换密码可以提高账户安全性</p>
              <Button
                icon={<LockOutlined />}
                onClick={() => setPasswordModalVisible(true)}
              >
                修改密码
              </Button>
            </div>
          </Card>
        </Form>
      </div>

      {/* 修改密码弹窗 */}
      <Modal
        title="修改密码"
        open={passwordModalVisible}
        onOk={handleChangePassword}
        onCancel={() => {
          setPasswordModalVisible(false);
          passwordForm.resetFields();
        }}
        confirmLoading={passwordLoading}
        width={500}
      >
        <Form
          form={passwordForm}
          layout="vertical"
        >
          <Form.Item
            name="currentPassword"
            label="当前密码"
            rules={[
              { required: true, message: '请输入当前密码' },
            ]}
          >
            <Input.Password placeholder="请输入当前密码" />
          </Form.Item>
          <Form.Item
            name="newPassword"
            label="新密码"
            rules={[
              { required: true, message: '请输入新密码' },
              { min: 6, message: '密码长度至少6位' },
              {
                pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[a-zA-Z\d@$!%*?&]{6,}$/,
                message: '密码必须包含大小写字母和数字',
              },
            ]}
          >
            <Input.Password placeholder="请输入新密码" />
          </Form.Item>
          <Form.Item
            name="confirmPassword"
            label="确认新密码"
            dependencies={['newPassword']}
            rules={[
              { required: true, message: '请确认新密码' },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('newPassword') === value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(new Error('两次输入的密码不一致'));
                },
              }),
            ]}
          >
            <Input.Password placeholder="请再次输入新密码" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};
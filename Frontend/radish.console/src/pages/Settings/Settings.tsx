import { useCallback, useEffect, useState } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  AntInput as Input,
  Switch,
  AntSelect as Select,
  message,
  Space,
  Form,
  Divider,
  AntModal as Modal,
  AntButton as Button,
  Tag,
} from '@radish/ui';
import {
  SettingOutlined,
  LockOutlined,
  EyeOutlined,
} from '@radish/ui';
import { SaveOutlined, BellOutlined } from '@ant-design/icons';
import { userApi, type UserTimePreferenceVo } from '@/api/user';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './Settings.css';

interface SettingsData {
  timeZoneId: string;
  emailNotifications: boolean;
  browserNotifications: boolean;
  systemNotifications: boolean;
  theme: 'light';
  language: 'zh-CN';
  pageSize: number;
  twoFactorAuth: boolean;
  sessionTimeout: number;
}

const DEFAULT_SETTINGS: SettingsData = {
  timeZoneId: 'Asia/Shanghai',
  emailNotifications: true,
  browserNotifications: true,
  systemNotifications: false,
  theme: 'light',
  language: 'zh-CN',
  pageSize: 20,
  twoFactorAuth: false,
  sessionTimeout: 30,
};

const TIME_ZONE_OPTIONS = [
  { label: 'Asia/Shanghai', value: 'Asia/Shanghai' },
  { label: 'Asia/Tokyo', value: 'Asia/Tokyo' },
  { label: 'Asia/Singapore', value: 'Asia/Singapore' },
  { label: 'UTC', value: 'UTC' },
  { label: 'Europe/London', value: 'Europe/London' },
  { label: 'America/Los_Angeles', value: 'America/Los_Angeles' },
  { label: 'America/New_York', value: 'America/New_York' },
];

function buildSettings(preference?: UserTimePreferenceVo | null): SettingsData {
  return {
    ...DEFAULT_SETTINGS,
    timeZoneId: preference?.voTimeZoneId || preference?.voSystemDefaultTimeZoneId || DEFAULT_SETTINGS.timeZoneId,
  };
}

export const Settings = () => {
  useDocumentTitle('设置');
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [initializing, setInitializing] = useState(false);
  const [passwordModalVisible, setPasswordModalVisible] = useState(false);
  const [passwordForm] = Form.useForm();
  const [passwordLoading, setPasswordLoading] = useState(false);
  const [timePreference, setTimePreference] = useState<UserTimePreferenceVo | null>(null);
  const [settings, setSettings] = useState<SettingsData>(DEFAULT_SETTINGS);

  const loadSettings = useCallback(async () => {
    try {
      setInitializing(true);
      const response = await userApi.getMyTimePreference();
      if (!response.ok || !response.data) {
        throw new Error(response.message || '加载设置失败');
      }

      const nextSettings = buildSettings(response.data);
      setTimePreference(response.data);
      setSettings(nextSettings);
      form.setFieldsValue(nextSettings);
    } catch (error) {
      log.error('Settings', '加载设置失败:', error);
      message.error(error instanceof Error ? error.message : '加载设置失败');
    } finally {
      setInitializing(false);
    }
  }, [form]);

  useEffect(() => {
    void loadSettings();
  }, [loadSettings]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const response = await userApi.updateMyTimePreference(values.timeZoneId);
      if (!response.ok || !response.data) {
        throw new Error(response.message || '保存设置失败');
      }

      const nextSettings = buildSettings(response.data);
      setTimePreference(response.data);
      setSettings(nextSettings);
      form.setFieldsValue(nextSettings);
      message.success('设置保存成功');
    } catch (error) {
      log.error('Settings', '保存设置失败:', error);
      message.error(error instanceof Error ? error.message : '保存设置失败');
    } finally {
      setLoading(false);
    }
  };

  const handleChangePassword = async () => {
    try {
      const values = await passwordForm.validateFields();
      setPasswordLoading(true);

      const response = await userApi.changeMyLoginPassword({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
        confirmPassword: values.confirmPassword,
      });

      if (!response.ok) {
        throw new Error(response.message || '修改密码失败');
      }

      message.success('密码修改成功');
      setPasswordModalVisible(false);
      passwordForm.resetFields();
    } catch (error) {
      log.error('Settings', '修改密码失败:', error);
      message.error(error instanceof Error ? error.message : '修改密码失败');
    } finally {
      setPasswordLoading(false);
    }
  };

  const handleReset = () => {
    Modal.confirm({
      title: '重置设置',
      content: '确定要将时区恢复为系统默认值吗？',
      onOk: async () => {
        const fallbackTimeZone = timePreference?.voSystemDefaultTimeZoneId || DEFAULT_SETTINGS.timeZoneId;
        try {
          setLoading(true);
          const response = await userApi.updateMyTimePreference(fallbackTimeZone);
          if (!response.ok || !response.data) {
            throw new Error(response.message || '重置设置失败');
          }

          const nextSettings = buildSettings(response.data);
          setTimePreference(response.data);
          setSettings(nextSettings);
          form.setFieldsValue(nextSettings);
          message.success('设置已重置为系统默认值');
        } catch (error) {
          log.error('Settings', '重置设置失败:', error);
          message.error(error instanceof Error ? error.message : '重置设置失败');
        } finally {
          setLoading(false);
        }
      },
    });
  };

  return (
    <div className="admin-feature-page settings-page">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>
              <SettingOutlined /> 设置
            </h2>
            <p className="admin-feature-subtle">管理个人偏好、安全凭证和后续通知策略入口。</p>
          </div>
          <Space>
            <Button onClick={handleReset} disabled={initializing || loading}>
              重置默认
            </Button>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              loading={loading}
              disabled={initializing}
              onClick={handleSave}
            >
              保存设置
            </Button>
          </Space>
        </div>
      </section>

      <div className="admin-settings-layout">
        <aside className="admin-settings-nav" aria-label="设置分组">
          <h3>设置分组</h3>
          <p className="admin-feature-subtle">按配置影响范围进入对应分组。</p>
          <nav className="admin-settings-nav__list">
            <a className="admin-settings-nav__item" href="#settings-notifications">
              <BellOutlined /> 通知设置
            </a>
            <a className="admin-settings-nav__item" href="#settings-interface">
              <EyeOutlined /> 界面设置
            </a>
            <a className="admin-settings-nav__item" href="#settings-security">
              <LockOutlined /> 安全设置
            </a>
          </nav>
        </aside>

        <Form
          form={form}
          layout="vertical"
          initialValues={settings}
          className="admin-settings-main"
        >
          <section id="settings-notifications" className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div>
                <div className="admin-setting-section__title-main">
                  <BellOutlined />
                  <h3>通知设置</h3>
                </div>
                <p className="admin-feature-subtle">通知策略当前作为后置能力展示，保存时不写入通知配置。</p>
              </div>
              <Tag>后置</Tag>
            </div>

            <Form.Item name="emailNotifications" label="邮件通知" valuePropName="checked">
              <Switch checkedChildren="开启" unCheckedChildren="关闭" disabled />
            </Form.Item>
            <Form.Item name="browserNotifications" label="浏览器通知" valuePropName="checked">
              <Switch checkedChildren="开启" unCheckedChildren="关闭" disabled />
            </Form.Item>
            <Form.Item name="systemNotifications" label="系统通知" valuePropName="checked">
              <Switch checkedChildren="开启" unCheckedChildren="关闭" disabled />
            </Form.Item>
          </section>

          <section id="settings-interface" className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div>
                <div className="admin-setting-section__title-main">
                  <EyeOutlined />
                  <h3>界面设置</h3>
                </div>
                <p className="admin-feature-subtle">当前只开放时区偏好，其余界面选项保留为只读能力占位。</p>
              </div>
            </div>

            <Form.Item
              name="timeZoneId"
              label="时区"
              rules={[{ required: true, message: '请选择时区' }]}
            >
              <Select options={TIME_ZONE_OPTIONS} />
            </Form.Item>
            <div className="settings-meta">
              <span>系统默认：{timePreference?.voSystemDefaultTimeZoneId || DEFAULT_SETTINGS.timeZoneId}</span>
              <span>展示格式：{timePreference?.voDisplayFormat || 'yyyy-MM-dd HH:mm:ss'}</span>
            </div>

            <Divider />

            <Form.Item name="theme" label="主题">
              <Select
                disabled
                options={[{ label: '浅色主题', value: 'light' }]}
              />
            </Form.Item>
            <Form.Item name="language" label="语言">
              <Select
                disabled
                options={[{ label: '简体中文', value: 'zh-CN' }]}
              />
            </Form.Item>
            <Form.Item name="pageSize" label="每页显示条数">
              <Select
                disabled
                options={[{ label: '20 条/页', value: 20 }]}
              />
            </Form.Item>
          </section>

          <section id="settings-security" className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div>
                <div className="admin-setting-section__title-main">
                  <LockOutlined />
                  <h3>安全设置</h3>
                </div>
                <p className="admin-feature-subtle">高风险账户动作集中在本分组，避免与偏好配置混排。</p>
              </div>
            </div>

            <Form.Item name="twoFactorAuth" label="双因素认证" valuePropName="checked">
              <Switch checkedChildren="开启" unCheckedChildren="关闭" disabled />
            </Form.Item>
            <Form.Item name="sessionTimeout" label="会话超时时间（分钟）">
              <Select
                disabled
                options={[{ label: '30 分钟', value: 30 }]}
              />
            </Form.Item>

            <Divider />

            <div className="password-section">
              <h4>密码管理</h4>
              <p>定期更换密码可以提高账户安全性。</p>
              <Button
                icon={<LockOutlined />}
                onClick={() => setPasswordModalVisible(true)}
              >
                修改密码
              </Button>
            </div>
          </section>
        </Form>

        <aside className="admin-settings-aside">
          <h3>当前影响范围</h3>
          <p className="admin-feature-subtle">本页仅保存个人时区偏好和密码变更，不调整系统级治理策略。</p>
          <div className="admin-settings-aside__list">
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">当前时区</span>
              <span className="admin-settings-aside__value">{settings.timeZoneId}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">系统默认</span>
              <span className="admin-settings-aside__value">
                {timePreference?.voSystemDefaultTimeZoneId || DEFAULT_SETTINGS.timeZoneId}
              </span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">展示格式</span>
              <span className="admin-settings-aside__value">
                {timePreference?.voDisplayFormat || 'yyyy-MM-dd HH:mm:ss'}
              </span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">通知能力</span>
              <span className="admin-settings-aside__value">后续接入</span>
            </div>
          </div>
        </aside>
      </div>

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
        forceRender
      >
        <Form form={passwordForm} layout="vertical">
          <Form.Item
            name="currentPassword"
            label="当前密码"
            rules={[{ required: true, message: '请输入当前密码' }]}
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

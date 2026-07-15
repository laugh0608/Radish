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
import { useTranslation } from 'react-i18next';
import { normalizeLanguage, type SupportedLanguage } from '@/locales/language';

interface SettingsData {
  timeZoneId: string;
  emailNotifications: boolean;
  browserNotifications: boolean;
  systemNotifications: boolean;
  theme: 'light';
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
  const { t, i18n } = useTranslation();
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh';
  useDocumentTitle(t('console.route.settings'));
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
        throw new Error(t('settings.personal.feedback.loadFailed'));
      }

      const nextSettings = buildSettings(response.data);
      setTimePreference(response.data);
      setSettings(nextSettings);
      form.setFieldsValue(nextSettings);
    } catch (error) {
      log.error('Settings', '加载设置失败:', error);
      message.error(error instanceof Error ? error.message : t('settings.personal.feedback.loadFailed'));
    } finally {
      setInitializing(false);
    }
  }, [form, t]);

  useEffect(() => {
    void loadSettings();
  }, [loadSettings]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const response = await userApi.updateMyTimePreference(values.timeZoneId);
      if (!response.ok || !response.data) {
        throw new Error(t('settings.personal.feedback.saveFailed'));
      }

      const nextSettings = buildSettings(response.data);
      setTimePreference(response.data);
      setSettings(nextSettings);
      form.setFieldsValue(nextSettings);
      message.success(t('settings.personal.feedback.saveSuccess'));
    } catch (error) {
      log.error('Settings', '保存设置失败:', error);
      message.error(error instanceof Error ? error.message : t('settings.personal.feedback.saveFailed'));
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
        throw new Error(t('settings.personal.feedback.passwordFailed'));
      }

      message.success(t('settings.personal.feedback.passwordSuccess'));
      setPasswordModalVisible(false);
      passwordForm.resetFields();
    } catch (error) {
      log.error('Settings', '修改密码失败:', error);
      message.error(error instanceof Error ? error.message : t('settings.personal.feedback.passwordFailed'));
    } finally {
      setPasswordLoading(false);
    }
  };

  const handleReset = () => {
    Modal.confirm({
      title: t('settings.personal.feedback.resetTitle'),
      content: t('settings.personal.feedback.resetContent'),
      onOk: async () => {
        const fallbackTimeZone = timePreference?.voSystemDefaultTimeZoneId || DEFAULT_SETTINGS.timeZoneId;
        try {
          setLoading(true);
          const response = await userApi.updateMyTimePreference(fallbackTimeZone);
          if (!response.ok || !response.data) {
            throw new Error(t('settings.personal.feedback.resetFailed'));
          }

          const nextSettings = buildSettings(response.data);
          setTimePreference(response.data);
          setSettings(nextSettings);
          form.setFieldsValue(nextSettings);
          message.success(t('settings.personal.feedback.resetSuccess'));
        } catch (error) {
          log.error('Settings', '重置设置失败:', error);
          message.error(error instanceof Error ? error.message : t('settings.personal.feedback.resetFailed'));
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
              <SettingOutlined /> {t('settings.personal.title')}
            </h2>
            <p className="admin-feature-subtle">{t('settings.personal.description')}</p>
          </div>
          <Space>
            <Button onClick={handleReset} disabled={initializing || loading}>
              {t('settings.personal.resetDefault')}
            </Button>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              loading={loading}
              disabled={initializing}
              onClick={handleSave}
            >
              {t('settings.personal.save')}
            </Button>
          </Space>
        </div>
      </section>

      <div className="admin-settings-layout">
        <aside className="admin-settings-nav" aria-label={t('settings.personal.navLabel')}>
          <h3>{t('settings.personal.navTitle')}</h3>
          <p className="admin-feature-subtle">{t('settings.personal.navDescription')}</p>
          <nav className="admin-settings-nav__list">
            <a className="admin-settings-nav__item" href="#settings-notifications">
              <BellOutlined /> {t('settings.personal.notifications.title')}
            </a>
            <a className="admin-settings-nav__item" href="#settings-interface">
              <EyeOutlined /> {t('settings.personal.interface.title')}
            </a>
            <a className="admin-settings-nav__item" href="#settings-security">
              <LockOutlined /> {t('settings.personal.security.title')}
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
                  <h3>{t('settings.personal.notifications.title')}</h3>
                </div>
                <p className="admin-feature-subtle">{t('settings.personal.notifications.description')}</p>
              </div>
              <Tag>{t('settings.personal.notifications.deferred')}</Tag>
            </div>

            <Form.Item name="emailNotifications" label={t('settings.personal.notifications.email')} valuePropName="checked">
              <Switch checkedChildren={t('settings.personal.state.on')} unCheckedChildren={t('settings.personal.state.off')} disabled />
            </Form.Item>
            <Form.Item name="browserNotifications" label={t('settings.personal.notifications.browser')} valuePropName="checked">
              <Switch checkedChildren={t('settings.personal.state.on')} unCheckedChildren={t('settings.personal.state.off')} disabled />
            </Form.Item>
            <Form.Item name="systemNotifications" label={t('settings.personal.notifications.system')} valuePropName="checked">
              <Switch checkedChildren={t('settings.personal.state.on')} unCheckedChildren={t('settings.personal.state.off')} disabled />
            </Form.Item>
          </section>

          <section id="settings-interface" className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div>
                <div className="admin-setting-section__title-main">
                  <EyeOutlined />
                  <h3>{t('settings.personal.interface.title')}</h3>
                </div>
                <p className="admin-feature-subtle">{t('settings.personal.interface.description')}</p>
              </div>
            </div>

            <Form.Item
              name="timeZoneId"
              label={t('settings.personal.interface.timeZone')}
              rules={[{ required: true, message: t('settings.personal.interface.timeZoneRequired') }]}
            >
              <Select options={TIME_ZONE_OPTIONS} />
            </Form.Item>
            <div className="settings-meta">
              <span>{t('settings.personal.interface.systemDefault', { value: timePreference?.voSystemDefaultTimeZoneId || DEFAULT_SETTINGS.timeZoneId })}</span>
              <span>{t('settings.personal.interface.displayFormat', { value: timePreference?.voDisplayFormat || 'yyyy-MM-dd HH:mm:ss' })}</span>
            </div>

            <Divider />

            <Form.Item name="theme" label={t('settings.personal.interface.theme')}>
              <Select
                disabled
                options={[{ label: t('settings.personal.interface.lightTheme'), value: 'light' }]}
              />
            </Form.Item>
            <Form.Item label={t('settings.language.label')}>
              <Select
                value={language}
                options={[
                  { label: t('lang.zh'), value: 'zh' },
                  { label: t('lang.en'), value: 'en' },
                ]}
                onChange={(value: SupportedLanguage) => void i18n.changeLanguage(value)}
              />
            </Form.Item>
            <Form.Item name="pageSize" label={t('settings.personal.interface.pageSize')}>
              <Select
                disabled
                options={[{ label: t('settings.personal.interface.pageSizeValue', { count: 20 }), value: 20 }]}
              />
            </Form.Item>
          </section>

          <section id="settings-security" className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div>
                <div className="admin-setting-section__title-main">
                  <LockOutlined />
                  <h3>{t('settings.personal.security.title')}</h3>
                </div>
                <p className="admin-feature-subtle">{t('settings.personal.security.description')}</p>
              </div>
            </div>

            <Form.Item name="twoFactorAuth" label={t('settings.personal.security.twoFactor')} valuePropName="checked">
              <Switch checkedChildren={t('settings.personal.state.on')} unCheckedChildren={t('settings.personal.state.off')} disabled />
            </Form.Item>
            <Form.Item name="sessionTimeout" label={t('settings.personal.security.sessionTimeout')}>
              <Select
                disabled
                options={[{ label: t('settings.personal.security.sessionTimeoutValue', { count: 30 }), value: 30 }]}
              />
            </Form.Item>

            <Divider />

            <div className="password-section">
              <h4>{t('settings.personal.security.passwordTitle')}</h4>
              <p>{t('settings.personal.security.passwordDescription')}</p>
              <Button
                icon={<LockOutlined />}
                onClick={() => setPasswordModalVisible(true)}
              >
                {t('settings.personal.security.changePassword')}
              </Button>
            </div>
          </section>
        </Form>

        <aside className="admin-settings-aside">
          <h3>{t('settings.personal.scope.title')}</h3>
          <p className="admin-feature-subtle">{t('settings.personal.scope.description')}</p>
          <div className="admin-settings-aside__list">
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('settings.personal.scope.currentTimeZone')}</span>
              <span className="admin-settings-aside__value">{settings.timeZoneId}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('settings.personal.scope.systemDefault')}</span>
              <span className="admin-settings-aside__value">
                {timePreference?.voSystemDefaultTimeZoneId || DEFAULT_SETTINGS.timeZoneId}
              </span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('settings.personal.scope.displayFormat')}</span>
              <span className="admin-settings-aside__value">
                {timePreference?.voDisplayFormat || 'yyyy-MM-dd HH:mm:ss'}
              </span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('settings.personal.scope.notifications')}</span>
              <span className="admin-settings-aside__value">{t('settings.personal.scope.planned')}</span>
            </div>
          </div>
        </aside>
      </div>

      <Modal
        title={t('settings.personal.password.title')}
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
            label={t('settings.personal.password.current')}
            rules={[{ required: true, message: t('settings.personal.password.currentRequired') }]}
          >
            <Input.Password placeholder={t('settings.personal.password.currentPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="newPassword"
            label={t('settings.personal.password.new')}
            rules={[
              { required: true, message: t('settings.personal.password.newRequired') },
              { min: 6, message: t('settings.personal.password.minLength') },
              {
                pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[a-zA-Z\d@$!%*?&]{6,}$/,
                message: t('settings.personal.password.pattern'),
              },
            ]}
          >
            <Input.Password placeholder={t('settings.personal.password.newPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="confirmPassword"
            label={t('settings.personal.password.confirm')}
            dependencies={['newPassword']}
            rules={[
              { required: true, message: t('settings.personal.password.confirmRequired') },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  if (!value || getFieldValue('newPassword') === value) {
                    return Promise.resolve();
                  }
                  return Promise.reject(new Error(t('settings.personal.password.mismatch')));
                },
              }),
            ]}
          >
            <Input.Password placeholder={t('settings.personal.password.confirmPlaceholder')} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

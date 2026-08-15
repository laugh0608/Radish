import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { useUnsavedChangesGuard } from '@/hooks/useUnsavedChangesGuard';
import {
  AntInput as Input,
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
import { SaveOutlined, BellOutlined, ReloadOutlined } from '@ant-design/icons';
import { userApi, type UserTimePreferenceVo } from '@/api/user';
import { normalizeLanguage, type SupportedLanguage } from '@/locales/language';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './Settings.css';

type AuthorityState = 'loading' | 'ready' | 'unavailable' | 'stale';

interface SettingsData {
  timeZoneId?: string;
}

const TIME_ZONE_OPTIONS = [
  { label: 'Asia/Shanghai', value: 'Asia/Shanghai' },
  { label: 'Asia/Tokyo', value: 'Asia/Tokyo' },
  { label: 'Asia/Singapore', value: 'Asia/Singapore' },
  { label: 'UTC', value: 'UTC' },
  { label: 'Europe/London', value: 'Europe/London' },
  { label: 'America/Los_Angeles', value: 'America/Los_Angeles' },
  { label: 'America/New_York', value: 'America/New_York' },
];

export const Settings = () => {
  const { t, i18n } = useTranslation();
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh';
  useDocumentTitle(t('console.route.settings'));

  const [form] = Form.useForm<SettingsData>();
  const [passwordForm] = Form.useForm();
  const preferenceRef = useRef<UserTimePreferenceVo | null>(null);
  const loadRequestIdRef = useRef(0);
  const [authorityState, setAuthorityState] = useState<AuthorityState>('loading');
  const [loadError, setLoadError] = useState<string>();
  const [reading, setReading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [timeZoneDirty, setTimeZoneDirty] = useState(false);
  const [passwordModalVisible, setPasswordModalVisible] = useState(false);
  const [passwordBusy, setPasswordBusy] = useState(false);
  const [passwordDirty, setPasswordDirty] = useState(false);
  const [timePreference, setTimePreference] = useState<UserTimePreferenceVo | null>(null);

  const hasUnsavedChanges = timeZoneDirty || passwordDirty;
  useUnsavedChangesGuard(hasUnsavedChanges, t('settings.personal.dirty.leaveConfirm'));

  const applyPreference = useCallback((preference: UserTimePreferenceVo) => {
    preferenceRef.current = preference;
    setTimePreference(preference);
    form.setFieldsValue({ timeZoneId: preference.voTimeZoneId });
    setTimeZoneDirty(false);
    setAuthorityState('ready');
    setLoadError(undefined);
  }, [form]);

  const loadSettings = useCallback(async () => {
    const requestId = ++loadRequestIdRef.current;
    const hadSnapshot = preferenceRef.current !== null;
    setReading(true);
    setLoadError(undefined);
    if (!hadSnapshot) {
      setAuthorityState('loading');
    }

    try {
      const response = await userApi.getMyTimePreference();
      if (!response.ok || !response.data) {
        throw new Error(response.message || t('settings.personal.feedback.loadFailed'));
      }

      if (requestId !== loadRequestIdRef.current) {
        return;
      }

      applyPreference(response.data);
    } catch (error) {
      if (requestId !== loadRequestIdRef.current) {
        return;
      }

      log.error('Settings', '加载设置失败:', error);
      const errorMessage = error instanceof Error ? error.message : t('settings.personal.feedback.loadFailed');
      setLoadError(errorMessage);
      setAuthorityState(hadSnapshot ? 'stale' : 'unavailable');
      message.error(errorMessage);
    } finally {
      if (requestId === loadRequestIdRef.current) {
        setReading(false);
      }
    }
  }, [applyPreference, t]);

  useEffect(() => {
    void loadSettings();
    return () => {
      loadRequestIdRef.current += 1;
    };
  }, [loadSettings]);

  const handleReload = () => {
    if (!timeZoneDirty) {
      void loadSettings();
      return;
    }

    Modal.confirm({
      title: t('settings.personal.dirty.reloadTitle'),
      content: t('settings.personal.dirty.reloadDescription'),
      okText: t('settings.personal.dirty.discard'),
      cancelText: t('settings.personal.dirty.continue'),
      onOk: () => {
        setTimeZoneDirty(false);
        void loadSettings();
      },
    });
  };

  const handleSave = async () => {
    if (authorityState !== 'ready' || saving || resetting || !timeZoneDirty) {
      return;
    }

    try {
      const values = await form.validateFields();
      setSaving(true);
      const response = await userApi.updateMyTimePreference(values.timeZoneId!);
      if (!response.ok || !response.data) {
        throw new Error(response.message || t('settings.personal.feedback.saveFailed'));
      }

      applyPreference(response.data);
      message.success(t('settings.personal.feedback.saveSuccess'));
    } catch (error) {
      log.error('Settings', '保存设置失败:', error);
      message.error(error instanceof Error ? error.message : t('settings.personal.feedback.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const handleChangePassword = async () => {
    if (passwordBusy) {
      return;
    }

    try {
      const values = await passwordForm.validateFields();
      setPasswordBusy(true);
      const response = await userApi.changeMyLoginPassword({
        currentPassword: values.currentPassword,
        newPassword: values.newPassword,
        confirmPassword: values.confirmPassword,
      });

      if (!response.ok) {
        throw new Error(response.message || t('settings.personal.feedback.passwordFailed'));
      }

      message.success(t('settings.personal.feedback.passwordSuccess'));
      setPasswordModalVisible(false);
      setPasswordDirty(false);
      passwordForm.resetFields();
    } catch (error) {
      log.error('Settings', '修改密码失败:', error);
      message.error(error instanceof Error ? error.message : t('settings.personal.feedback.passwordFailed'));
    } finally {
      setPasswordBusy(false);
    }
  };

  const closePasswordModal = () => {
    if (passwordBusy) {
      return;
    }

    const close = () => {
      setPasswordModalVisible(false);
      setPasswordDirty(false);
      passwordForm.resetFields();
    };

    if (!passwordDirty) {
      close();
      return;
    }

    Modal.confirm({
      title: t('settings.personal.password.discardTitle'),
      content: t('settings.personal.password.discardDescription'),
      okText: t('settings.personal.dirty.discard'),
      cancelText: t('settings.personal.dirty.continue'),
      onOk: close,
    });
  };

  const handleReset = () => {
    if (authorityState !== 'ready' || !timePreference || saving || resetting) {
      return;
    }

    Modal.confirm({
      title: t('settings.personal.feedback.resetTitle'),
      content: t('settings.personal.feedback.resetContent'),
      onOk: async () => {
        try {
          setResetting(true);
          const response = await userApi.updateMyTimePreference(timePreference.voSystemDefaultTimeZoneId);
          if (!response.ok || !response.data) {
            throw new Error(response.message || t('settings.personal.feedback.resetFailed'));
          }

          applyPreference(response.data);
          message.success(t('settings.personal.feedback.resetSuccess'));
        } catch (error) {
          log.error('Settings', '重置设置失败:', error);
          message.error(error instanceof Error ? error.message : t('settings.personal.feedback.resetFailed'));
        } finally {
          setResetting(false);
        }
      },
    });
  };

  const writesAreAuthoritative = authorityState === 'ready';
  const preferenceBusy = reading || saving || resetting;

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
          <Space wrap>
            <Button icon={<ReloadOutlined />} onClick={handleReload} loading={reading} disabled={saving || resetting}>
              {t('settings.personal.reload')}
            </Button>
            <Button onClick={handleReset} loading={resetting} disabled={!writesAreAuthoritative || saving}>
              {t('settings.personal.resetDefault')}
            </Button>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              loading={saving}
              disabled={!writesAreAuthoritative || resetting || !timeZoneDirty}
              onClick={() => void handleSave()}
            >
              {t('settings.personal.save')}
            </Button>
          </Space>
        </div>
      </section>

      {authorityState === 'loading' ? (
        <div className="self-service-authority self-service-authority--loading" role="status">
          <strong>{t('settings.personal.authority.loadingTitle')}</strong>
          <span>{t('settings.personal.authority.loadingDescription')}</span>
        </div>
      ) : null}
      {authorityState === 'unavailable' ? (
        <div className="self-service-authority self-service-authority--unavailable" role="alert">
          <div>
            <strong>{t('settings.personal.authority.unavailableTitle')}</strong>
            <span>{loadError || t('settings.personal.feedback.loadFailed')}</span>
          </div>
          <Button onClick={() => void loadSettings()} loading={reading}>{t('settings.personal.authority.retry')}</Button>
        </div>
      ) : null}
      {authorityState === 'stale' ? (
        <div className="self-service-authority self-service-authority--stale" role="alert">
          <div>
            <strong>{t('settings.personal.authority.staleTitle')}</strong>
            <span>{t('settings.personal.authority.staleDescription')}</span>
          </div>
          <Button onClick={handleReload} loading={reading}>{t('settings.personal.authority.reload')}</Button>
        </div>
      ) : null}
      {timeZoneDirty ? (
        <div className="self-service-authority self-service-authority--dirty" role="status">
          <strong>{t('settings.personal.dirty.title')}</strong>
          <span>{t('settings.personal.dirty.description')}</span>
        </div>
      ) : null}

      <div className="admin-settings-layout">
        <aside className="admin-settings-nav" aria-label={t('settings.personal.navLabel')}>
          <h3>{t('settings.personal.navTitle')}</h3>
          <p className="admin-feature-subtle">{t('settings.personal.navDescription')}</p>
          <nav className="admin-settings-nav__list">
            <a className="admin-settings-nav__item" href="#settings-interface">
              <EyeOutlined /> {t('settings.personal.interface.title')}
            </a>
            <a className="admin-settings-nav__item" href="#settings-security">
              <LockOutlined /> {t('settings.personal.security.title')}
            </a>
            <a className="admin-settings-nav__item" href="#settings-deferred">
              <BellOutlined /> {t('settings.personal.deferred.title')}
            </a>
          </nav>
        </aside>

        <Form
          form={form}
          layout="vertical"
          className="admin-settings-main"
          onValuesChange={(_, values) => {
            const authoritativeTimeZone = preferenceRef.current?.voTimeZoneId;
            setTimeZoneDirty(
              authorityState === 'ready' &&
              typeof values.timeZoneId === 'string' &&
              values.timeZoneId !== authoritativeTimeZone,
            );
          }}
        >
          <section id="settings-interface" className="admin-setting-section settings-primary-task">
            <div className="admin-setting-section__title">
              <div>
                <div className="admin-setting-section__title-main">
                  <EyeOutlined />
                  <h3>{t('settings.personal.interface.title')}</h3>
                </div>
                <p className="admin-feature-subtle">{t('settings.personal.interface.description')}</p>
              </div>
              <Tag color={authorityState === 'ready' ? 'success' : authorityState === 'stale' ? 'warning' : 'default'}>
                {t(`settings.personal.authority.state.${authorityState}`)}
              </Tag>
            </div>

            <Form.Item
              name="timeZoneId"
              label={t('settings.personal.interface.timeZone')}
              rules={[{ required: true, message: t('settings.personal.interface.timeZoneRequired') }]}
            >
              <Select options={TIME_ZONE_OPTIONS} disabled={!writesAreAuthoritative || preferenceBusy} />
            </Form.Item>
            <div className="settings-meta">
              <span>{t('settings.personal.interface.systemDefault', { value: timePreference?.voSystemDefaultTimeZoneId || '--' })}</span>
              <span>{t('settings.personal.interface.displayFormat', { value: timePreference?.voDisplayFormat || '--' })}</span>
            </div>

            <Divider />

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
            <p className="settings-local-authority-note">{t('settings.personal.interface.languageAuthority')}</p>
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

            <div className="password-section">
              <h4>{t('settings.personal.security.passwordTitle')}</h4>
              <p>{t('settings.personal.security.passwordDescription')}</p>
              <Button icon={<LockOutlined />} onClick={() => setPasswordModalVisible(true)} disabled={passwordBusy}>
                {t('settings.personal.security.changePassword')}
              </Button>
            </div>
          </section>

          <section id="settings-deferred" className="admin-setting-section">
            <div className="admin-setting-section__title">
              <div>
                <div className="admin-setting-section__title-main">
                  <BellOutlined />
                  <h3>{t('settings.personal.deferred.title')}</h3>
                </div>
                <p className="admin-feature-subtle">{t('settings.personal.deferred.description')}</p>
              </div>
              <Tag>{t('settings.personal.notifications.deferred')}</Tag>
            </div>
            <div className="settings-deferred-list">
              {(['notifications', 'theme', 'pageSize', 'twoFactor', 'session'] as const).map((capability) => (
                <div className="settings-deferred-item" key={capability}>
                  <strong>{t(`settings.personal.deferred.${capability}.title`)}</strong>
                  <span>{t(`settings.personal.deferred.${capability}.description`)}</span>
                </div>
              ))}
            </div>
          </section>
        </Form>

        <aside className="admin-settings-aside">
          <h3>{t('settings.personal.scope.title')}</h3>
          <p className="admin-feature-subtle">{t('settings.personal.scope.description')}</p>
          <div className="admin-settings-aside__list">
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('settings.personal.scope.authority')}</span>
              <span className="admin-settings-aside__value">{t(`settings.personal.authority.state.${authorityState}`)}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('settings.personal.scope.currentTimeZone')}</span>
              <span className="admin-settings-aside__value">{timePreference?.voTimeZoneId || '--'}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('settings.personal.scope.systemDefault')}</span>
              <span className="admin-settings-aside__value">{timePreference?.voSystemDefaultTimeZoneId || '--'}</span>
            </div>
            <div className="admin-settings-aside__item">
              <span className="admin-settings-aside__label">{t('settings.personal.scope.displayFormat')}</span>
              <span className="admin-settings-aside__value">{timePreference?.voDisplayFormat || '--'}</span>
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
        onOk={() => void handleChangePassword()}
        onCancel={closePasswordModal}
        confirmLoading={passwordBusy}
        width={500}
        forceRender
        maskClosable={false}
      >
        <Form form={passwordForm} layout="vertical" onValuesChange={() => setPasswordDirty(true)}>
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

import { useEffect, useState } from 'react';
import { Grid } from 'antd';
import { useTranslation } from 'react-i18next';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  BottomSheet,
  Button,
  Form,
  Switch,
  message,
} from '@radish/ui';
import { clientApi } from '@/api/clients';
import type {
  ClientSecretResult,
  CreateClientRequest,
  OidcClient,
  UpdateClientRequest,
} from '@/types/oidc';
import { log } from '@/utils/logger';

interface ApplicationFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  application?: OidcClient;
  canSubmit: boolean;
  onCancel: () => void;
  onSuccess: (secretResult?: ClientSecretResult) => void;
}

interface ApplicationFormValues {
  clientId: string;
  displayName: string;
  description?: string;
  developerName?: string;
  developerEmail?: string;
  clientType: 'public' | 'confidential';
  grantTypes: string[];
  scopes: string[];
  consentType: string;
  requirePkce: boolean;
  redirectUris?: string;
  postLogoutRedirectUris?: string;
}

const parseUriLines = (value?: string) => Array.from(new Set(
  value?.split('\n').map((line) => line.trim()).filter(Boolean) ?? [],
));

const formatUriLines = (values?: string[]) => values?.join('\n') ?? '';

export const ApplicationForm = ({
  visible,
  mode,
  application,
  canSubmit,
  onCancel,
  onSuccess,
}: ApplicationFormProps) => {
  const { t } = useTranslation();
  const [form] = Form.useForm<ApplicationFormValues>();
  const [submitting, setSubmitting] = useState(false);
  const [isDirty, setIsDirty] = useState(false);
  const screens = Grid.useBreakpoint();
  const isMobile = !screens.md;
  const grantTypes = Form.useWatch('grantTypes', form) ?? [];
  const clientType = Form.useWatch('clientType', form) ?? 'public';
  const requiresRedirectUri = grantTypes.includes('authorization_code');

  useEffect(() => {
    if (!visible) {
      form.resetFields();
      setIsDirty(false);
      return;
    }

    if (mode === 'edit' && application) {
      form.setFieldsValue({
        clientId: application.clientId,
        displayName: application.displayName ?? '',
        description: application.description ?? '',
        developerName: application.developerName ?? '',
        developerEmail: application.developerEmail ?? '',
        clientType: application.clientType,
        grantTypes: application.grantTypes,
        scopes: application.scopes,
        consentType: application.consentType ?? 'explicit',
        requirePkce: application.requirePkce,
        redirectUris: formatUriLines(application.redirectUris),
        postLogoutRedirectUris: formatUriLines(application.postLogoutRedirectUris),
      });
      setIsDirty(false);
      return;
    }

    form.setFieldsValue({
      clientId: '',
      displayName: '',
      description: '',
      developerName: '',
      developerEmail: '',
      clientType: 'public',
      grantTypes: ['authorization_code', 'refresh_token'],
      scopes: ['openid', 'profile', 'offline_access', 'radish-api'],
      consentType: 'explicit',
      requirePkce: true,
      redirectUris: '',
      postLogoutRedirectUris: '',
    });
    setIsDirty(false);
  }, [application, form, mode, visible]);

  useEffect(() => {
    if (!visible || !isDirty) return;

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [isDirty, visible]);

  const closeForm = () => {
    setIsDirty(false);
    form.resetFields();
    onCancel();
  };

  const handleRequestCancel = () => {
    if (submitting) {
      message.warning(t('applications.form.closeBusy'));
      return;
    }

    if (!isDirty) {
      closeForm();
      return;
    }

    Modal.confirm({
      title: t('applications.form.discardTitle'),
      content: t('applications.form.discardDescription'),
      okText: t('applications.form.discardConfirm'),
      cancelText: t('applications.form.continueEditing'),
      okButtonProps: { danger: true },
      onOk: closeForm,
    });
  };

  const handleSubmit = async () => {
    if (submitting) return;
    if (!canSubmit) {
      message.error(t('applications.feedback.permissionDenied'));
      return;
    }

    let values: ApplicationFormValues;
    try {
      values = await form.validateFields();
    } catch {
      return;
    }

    if (values.clientType === 'public' && values.grantTypes.includes('client_credentials')) {
      form.setFields([{ name: 'grantTypes', errors: [t('applications.form.publicClientCredentials')] }]);
      return;
    }

    const redirectUris = parseUriLines(values.redirectUris);
    const postLogoutRedirectUris = parseUriLines(values.postLogoutRedirectUris);

    try {
      setSubmitting(true);
      if (mode === 'create') {
        const request: CreateClientRequest = {
          clientId: values.clientId.trim(),
          displayName: values.displayName.trim(),
          description: values.description?.trim(),
          developerName: values.developerName?.trim(),
          developerEmail: values.developerEmail?.trim(),
          clientType: values.clientType,
          grantTypes: values.grantTypes,
          scopes: values.scopes,
          consentType: values.consentType,
          requirePkce: values.requirePkce,
          redirectUris,
          postLogoutRedirectUris,
        };
        const result = await clientApi.createClient(request);
        if (!result.ok || !result.data) {
          throw new Error(result.message || t('applications.feedback.createFailed'));
        }

        setIsDirty(false);
        message.success(t('applications.feedback.created'));
        onSuccess(result.data.clientSecret ? result.data : undefined);
        return;
      }

      if (!application) {
        throw new Error(t('applications.feedback.detailUnavailable'));
      }

      const request: Omit<UpdateClientRequest, 'id'> = {
        displayName: values.displayName.trim(),
        description: values.description?.trim(),
        developerName: values.developerName?.trim(),
        developerEmail: values.developerEmail?.trim(),
        grantTypes: values.grantTypes,
        scopes: values.scopes,
        consentType: values.consentType,
        requirePkce: values.requirePkce,
        redirectUris,
        postLogoutRedirectUris,
      };
      const result = await clientApi.updateClient(application.id, request);
      if (!result.ok) {
        throw new Error(result.message || t('applications.feedback.updateFailed'));
      }

      setIsDirty(false);
      message.success(t('applications.feedback.updated'));
      onSuccess();
    } catch (error) {
      log.error('ApplicationForm', '提交应用表单失败', error);
      message.error(error instanceof Error
        ? error.message
        : t(mode === 'create' ? 'applications.feedback.createFailed' : 'applications.feedback.updateFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const formContent = (
    <Form<ApplicationFormValues>
      form={form}
      layout="vertical"
      autoComplete="off"
      onValuesChange={() => setIsDirty(true)}
      className="applications-form"
    >
      <div className="applications-form__grid">
        <Form.Item
          label={t('applications.form.clientId')}
          name="clientId"
          rules={[
            { required: true, message: t('applications.form.clientIdRequired') },
            { max: 100, message: t('applications.form.clientIdMax') },
          ]}
        >
          <Input placeholder={t('applications.form.clientIdPlaceholder')} disabled={mode === 'edit'} />
        </Form.Item>

        <Form.Item
          label={t('applications.form.displayName')}
          name="displayName"
          rules={[
            { required: true, message: t('applications.form.displayNameRequired') },
            { max: 200, message: t('applications.form.displayNameMax') },
          ]}
        >
          <Input placeholder={t('applications.form.displayNamePlaceholder')} />
        </Form.Item>

        <Form.Item label={t('applications.form.clientType')} name="clientType">
          <Select
            disabled={mode === 'edit'}
            options={[
              { value: 'public', label: t('applications.clientType.public') },
              { value: 'confidential', label: t('applications.clientType.confidential') },
            ]}
          />
        </Form.Item>

        <Form.Item label={t('applications.form.consentType')} name="consentType">
          <Select
            options={['explicit', 'implicit', 'external', 'systematic'].map((value) => ({
              value,
              label: t(`applications.consentType.${value}`),
            }))}
          />
        </Form.Item>
      </div>

      <Form.Item
        label={t('applications.form.grantTypes')}
        name="grantTypes"
        rules={[{ required: true, message: t('applications.form.grantTypesRequired') }]}
      >
        <Select
          mode="multiple"
          options={['authorization_code', 'refresh_token', 'client_credentials', 'password'].map((value) => ({
            value,
            label: t(`applications.grantType.${value}`),
            disabled: clientType === 'public' && value === 'client_credentials',
          }))}
        />
      </Form.Item>

      <Form.Item
        label={t('applications.form.scopes')}
        name="scopes"
        rules={[{ required: true, message: t('applications.form.scopesRequired') }]}
      >
        <Select
          mode="multiple"
          options={['openid', 'profile', 'email', 'offline_access', 'radish-api'].map((value) => ({
            value,
            label: value,
          }))}
        />
      </Form.Item>

      <Form.Item label={t('applications.form.requirePkce')} name="requirePkce" valuePropName="checked">
        <Switch
          checkedChildren={t('applications.form.enabled')}
          unCheckedChildren={t('applications.form.disabled')}
        />
      </Form.Item>

      <Form.Item
        label={t('applications.form.redirectUris')}
        name="redirectUris"
        rules={[{
          required: requiresRedirectUri,
          message: t('applications.form.redirectUrisRequired'),
        }]}
      >
        <Input.TextArea rows={3} placeholder={t('applications.form.redirectUrisPlaceholder')} />
      </Form.Item>

      <Form.Item label={t('applications.form.postLogoutRedirectUris')} name="postLogoutRedirectUris">
        <Input.TextArea rows={2} placeholder={t('applications.form.postLogoutRedirectUrisPlaceholder')} />
      </Form.Item>

      <Form.Item
        label={t('applications.form.description')}
        name="description"
        rules={[{ max: 1000, message: t('applications.form.descriptionMax') }]}
      >
        <Input.TextArea rows={3} placeholder={t('applications.form.descriptionPlaceholder')} />
      </Form.Item>

      <div className="applications-form__grid">
        <Form.Item label={t('applications.form.developerName')} name="developerName">
          <Input placeholder={t('applications.form.developerNamePlaceholder')} />
        </Form.Item>

        <Form.Item
          label={t('applications.form.developerEmail')}
          name="developerEmail"
          rules={[{ type: 'email', message: t('applications.form.developerEmailInvalid') }]}
        >
          <Input type="email" placeholder="developer@example.com" />
        </Form.Item>
      </div>
    </Form>
  );

  const title = t(mode === 'create' ? 'applications.modal.createTitle' : 'applications.modal.editTitle');
  if (isMobile) {
    return (
      <BottomSheet
        isOpen={visible}
        onClose={handleRequestCancel}
        closeLabel={t('applications.form.close')}
        title={title}
        height="92%"
        closeOnOverlayClick={false}
        closeOnEscape={!submitting}
        className="applications-form-sheet"
        footer={(
          <div className="applications-form__mobile-actions">
            <Button disabled={submitting} onClick={handleRequestCancel}>{t('applications.form.cancel')}</Button>
            <Button variant="primary" disabled={!canSubmit || submitting} onClick={() => void handleSubmit()}>
              {t(submitting ? 'applications.form.submitting' : 'applications.form.submit')}
            </Button>
          </div>
        )}
      >
        {formContent}
      </BottomSheet>
    );
  }

  return (
    <Modal
      title={title}
      open={visible}
      onOk={() => void handleSubmit()}
      onCancel={handleRequestCancel}
      confirmLoading={submitting}
      okButtonProps={{ disabled: !canSubmit }}
      okText={t('applications.form.submit')}
      cancelText={t('applications.form.cancel')}
      width={760}
      maskClosable={false}
      keyboard={false}
      destroyOnHidden
      forceRender
    >
      {formContent}
    </Modal>
  );
};

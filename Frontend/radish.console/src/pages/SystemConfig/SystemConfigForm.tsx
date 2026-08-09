import { useCallback, useState, useEffect } from 'react';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  AntSelect as Select,
  Button,
  Descriptions,
  InputNumber,
  Tag,
  message,
} from '@radish/ui';
import { ApiResponseError } from '@radish/http';
import {
  updateConfig,
  getConfigById,
  type SystemConfigVo,
} from '@/api/systemConfigApi';
import { log } from '@/utils/logger';
import {
  getSystemConfigCategoryLabel,
  getSystemConfigImpact,
} from './systemConfigPresentation';

interface SystemConfigFormProps {
  visible: boolean;
  configId?: number;
  onCancel: () => void;
  onSuccess: () => void;
}

const hasNumberConstraint = (value?: number | null): value is number => (
  typeof value === 'number' && Number.isFinite(value)
);

const formatNumberConstraint = (config: SystemConfigVo, t: TFunction) => {
  const parts: string[] = [];

  if (config.voRequiresInteger) {
    parts.push(t('systemConfig.form.numberInteger'));
  }

  if (hasNumberConstraint(config.voMinNumberValue) && hasNumberConstraint(config.voMaxNumberValue)) {
    parts.push(t('systemConfig.form.numberRange', { min: config.voMinNumberValue, max: config.voMaxNumberValue }));
  } else if (hasNumberConstraint(config.voMinNumberValue)) {
    parts.push(t('systemConfig.form.numberMin', { min: config.voMinNumberValue }));
  } else if (hasNumberConstraint(config.voMaxNumberValue)) {
    parts.push(t('systemConfig.form.numberMax', { max: config.voMaxNumberValue }));
  }

  return parts.length > 0 ? parts.join(t('systemConfig.form.constraintSeparator')) : t('systemConfig.form.numberNoConstraint');
};

const normalizeFormValue = (config: SystemConfigVo) => {
  if (config.voType !== 'number') {
    return config.voEffectiveValue;
  }

  const numberValue = Number(config.voEffectiveValue);
  return Number.isFinite(numberValue) ? numberValue : config.voEffectiveValue;
};

const validateNumberValue = (config: SystemConfigVo, value: unknown, t: TFunction) => {
  if (value === undefined || value === null || value === '') {
    return;
  }

  const numberValue = Number(value);
  if (!Number.isFinite(numberValue)) {
    throw new Error(t('systemConfig.form.invalidNumber'));
  }

  if (config.voRequiresInteger && !Number.isInteger(numberValue)) {
    throw new Error(t('systemConfig.form.integerRequired'));
  }

  if (hasNumberConstraint(config.voMinNumberValue) && numberValue < config.voMinNumberValue) {
    throw new Error(t('systemConfig.form.belowMin', { min: config.voMinNumberValue }));
  }

  if (hasNumberConstraint(config.voMaxNumberValue) && numberValue > config.voMaxNumberValue) {
    throw new Error(t('systemConfig.form.aboveMax', { max: config.voMaxNumberValue }));
  }
};

export const SystemConfigForm = ({
  visible,
  configId,
  onCancel,
  onSuccess,
}: SystemConfigFormProps) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [config, setConfig] = useState<SystemConfigVo>();
  const [loading, setLoading] = useState(false);
  const [initialLoading, setInitialLoading] = useState(false);
  const [loadError, setLoadError] = useState<string>();
  const [dirty, setDirty] = useState(false);
  const [versionConflict, setVersionConflict] = useState(false);
  const watchedValue = Form.useWatch('value', form);

  const loadConfigDetail = useCallback(async (id: number) => {
    try {
      setInitialLoading(true);
      setLoadError(undefined);
      setVersionConflict(false);
      setConfig(undefined);
      form.resetFields();
      const nextConfig = await getConfigById(id, t);
      setConfig(nextConfig);
      form.setFieldsValue({
        value: normalizeFormValue(nextConfig),
        reason: '',
        confirmRiskLevel: '',
        confirmKey: '',
      });
      setDirty(false);
    } catch (error) {
      log.error('SystemConfigForm', '加载系统设置详情失败:', error);
      const errorMessage = error instanceof Error ? error.message : t('systemConfig.feedback.loadDetailFailed');
      setLoadError(errorMessage);
      message.error(errorMessage);
    } finally {
      setInitialLoading(false);
    }
  }, [form, t]);

  const handleSubmit = async () => {
    if (!configId || !config) {
      message.error(t('systemConfig.feedback.notReady'));
      return;
    }

    try {
      const values = await form.validateFields();
      setLoading(true);

      let processedValue = String(values.value);
      if (config.voType === 'number') {
        processedValue = String(Number(values.value));
      }

      await updateConfig(configId, {
        value: processedValue,
        isEnabled: true,
        reason: String(values.reason).trim(),
        confirmRiskLevel: values.confirmRiskLevel?.trim(),
        confirmKey: values.confirmKey?.trim(),
        expectedVersion: config.voVersion,
      }, t);
      message.success(t('systemConfig.feedback.updateSuccess'));
      setDirty(false);
      onSuccess();
    } catch (error) {
      log.error('SystemConfigForm', '提交系统设置失败:', error);
      const isConflict = error instanceof ApiResponseError &&
        (error.httpStatus === 409 || error.statusCode === 409 || error.code === 'SystemConfig.VersionConflict');
      if (isConflict) {
        setVersionConflict(true);
        message.warning(t('systemConfig.feedback.versionConflict'));
      } else {
        message.error(error instanceof Error ? error.message : t('systemConfig.feedback.updateFailed'));
      }
    } finally {
      setLoading(false);
    }
  };

  const closeForm = () => {
    form.resetFields();
    setConfig(undefined);
    setLoadError(undefined);
    setDirty(false);
    setVersionConflict(false);
    onCancel();
  };

  const handleCancel = () => {
    if (!dirty) {
      closeForm();
      return;
    }

    Modal.confirm({
      title: t('systemConfig.form.discardTitle'),
      content: t('systemConfig.form.discardDescription'),
      okText: t('systemConfig.form.discardConfirm'),
      cancelText: t('roles.common.cancel'),
      onOk: closeForm,
    });
  };

  useEffect(() => {
    if (visible && configId) {
      void loadConfigDetail(configId);
    } else {
      form.resetFields();
      setConfig(undefined);
      setLoadError(undefined);
      setDirty(false);
      setVersionConflict(false);
    }
  }, [visible, configId, form, loadConfigDetail]);

  useEffect(() => {
    if (!visible || !dirty) {
      return;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [dirty, visible]);

  const renderValueInput = () => {
    switch (config?.voType) {
      case 'number':
        return (
          <InputNumber
            className="system-config-form-control-full"
            min={hasNumberConstraint(config.voMinNumberValue) ? config.voMinNumberValue : undefined}
            max={hasNumberConstraint(config.voMaxNumberValue) ? config.voMaxNumberValue : undefined}
            precision={config.voRequiresInteger ? 0 : undefined}
            step={config.voRequiresInteger ? 1 : undefined}
            placeholder={t('systemConfig.form.numberPlaceholder')}
          />
        );
      case 'boolean':
        return (
          <Select
            placeholder={t('systemConfig.form.booleanPlaceholder')}
            options={[
              { label: 'true', value: 'true' },
              { label: 'false', value: 'false' },
            ]}
          />
        );
      case 'json':
        return (
          <Input.TextArea
            placeholder={t('systemConfig.form.jsonPlaceholder')}
            rows={4}
            showCount
          />
        );
      default:
        return (
          <Input placeholder={t('systemConfig.form.stringPlaceholder')} />
        );
    }
  };

  const getDescriptionItems = () => {
    if (!config) {
      return [];
    }

    const items = [
      {
        key: 'category',
        label: t('systemConfig.form.category'),
        children: getSystemConfigCategoryLabel(config.voKey, config.voCategory, t),
      },
      { key: 'key', label: t('systemConfig.form.key'), children: <code>{config.voKey}</code> },
      { key: 'default', label: t('systemConfig.form.defaultValue'), children: config.voDefaultValue },
      {
        key: 'risk',
        label: t('systemConfig.form.risk'),
        children: (
          <Tag color={config.voRiskLevel === 'Low' ? 'success' : 'warning'}>
            {t(`systemConfig.risk.${config.voRiskLevel}`, { defaultValue: config.voRiskLevel })}
          </Tag>
        ),
      },
      {
        key: 'mode',
        label: t('systemConfig.form.effectiveMode'),
        children: t(`systemConfig.mode.${config.voEffectiveMode}`, { defaultValue: config.voEffectiveMode }),
      },
    ];

    if (config.voType === 'number') {
      items.push({
        key: 'validation',
        label: t('systemConfig.form.validation'),
        children: formatNumberConstraint(config, t),
      });
    }

    if (config.voImpactSummary) {
      items.push({
        key: 'impact',
        label: t('systemConfig.form.impact'),
        children: getSystemConfigImpact(config, t),
      });
    }

    return items;
  };

  return (
    <Modal
      title={t('systemConfig.form.title')}
      open={visible}
      onOk={handleSubmit}
      onCancel={handleCancel}
      confirmLoading={loading}
      okButtonProps={{
        disabled: initialLoading || !config?.voIsEditable,
      }}
      width={640}
      destroyOnHidden
      forceRender
    >
      {config ? (
        <Descriptions
          column={1}
          size="small"
          bordered
          items={getDescriptionItems()}
        />
      ) : null}

      {config && config.voRiskLevel !== 'Low' ? (
        <div className="system-config-risk-note">
          {t('systemConfig.form.riskNote')}
        </div>
      ) : null}

      {config ? (
        <div className="system-config-change-summary">
          <div><span>{t('systemConfig.form.oldValue')}</span><strong>{config.voEffectiveValue}</strong></div>
          <div><span>{t('systemConfig.form.newValue')}</span><strong>{watchedValue === undefined ? '-' : String(watchedValue)}</strong></div>
          <div><span>{t('systemConfig.form.impact')}</span><strong>{getSystemConfigImpact(config, t)}</strong></div>
          <div><span>{t('systemConfig.form.effectiveMode')}</span><strong>{t(`systemConfig.mode.${config.voEffectiveMode}`, { defaultValue: config.voEffectiveMode })}</strong></div>
        </div>
      ) : null}

      {versionConflict ? (
        <div className="system-config-risk-note" role="alert">
          <strong>{t('systemConfig.form.conflictTitle')}</strong>
          <p>{t('systemConfig.form.conflictDescription')}</p>
          <Button onClick={() => configId && void loadConfigDetail(configId)}>
            {t('systemConfig.form.reloadAuthority')}
          </Button>
        </div>
      ) : null}

      {loadError ? (
        <div className="system-config-risk-note" role="alert">
          <strong>{t('systemConfig.form.unavailableTitle')}</strong>
          <p>{loadError}</p>
          <Button onClick={() => configId && void loadConfigDetail(configId)}>
            {t('systemConfig.form.retry')}
          </Button>
        </div>
      ) : null}

      <Form
        form={form}
        layout="vertical"
        disabled={initialLoading || !config?.voIsEditable}
        className="system-config-edit-form"
        onValuesChange={() => {
          setDirty(true);
          setVersionConflict(false);
        }}
      >
        <Form.Item
          name="value"
          label={t('systemConfig.form.overrideValue')}
          rules={[
            { required: true, message: t('systemConfig.form.valueRequired') },
            {
              validator: async (_, value) => {
                if (!config) {
                  return;
                }
                if (config.voType === 'number') {
                  validateNumberValue(config, value, t);
                }
                if (config.voType === 'json') {
                  try {
                    JSON.parse(value);
                  } catch {
                    throw new Error(t('systemConfig.form.invalidJson'));
                  }
                }
              },
            },
          ]}
        >
          {renderValueInput()}
        </Form.Item>
        {config?.voRiskLevel !== 'Low' ? (
          <>
            <Form.Item
              name="confirmRiskLevel"
              label={t('systemConfig.form.confirmRiskLevel')}
              rules={[
                { required: true, message: t('systemConfig.form.confirmRiskLevelRequired') },
                {
                  validator: async (_, value) => {
                    if (String(value ?? '').trim().toLowerCase() !== config.voRiskLevel.toLowerCase()) {
                      throw new Error(t('systemConfig.form.confirmRiskLevelMismatch', { value: config.voRiskLevel }));
                    }
                  },
                },
              ]}
            >
              <Input placeholder={t('systemConfig.form.confirmRiskLevelPlaceholder', { value: config.voRiskLevel })} />
            </Form.Item>
            <Form.Item
              name="confirmKey"
              label={t('systemConfig.form.confirmKey')}
              rules={[
                { required: true, message: t('systemConfig.form.confirmKeyRequired') },
                {
                  validator: async (_, value) => {
                    if (String(value ?? '').trim() !== config.voKey) {
                      throw new Error(t('systemConfig.form.confirmKeyMismatch'));
                    }
                  },
                },
              ]}
            >
              <Input placeholder={config.voKey} />
            </Form.Item>
          </>
        ) : null}
        <Form.Item
          name="reason"
          label={t('systemConfig.form.reason')}
          rules={[
            { required: true, message: t('systemConfig.form.reasonRequired') },
            { max: 500, message: t('systemConfig.form.reasonMax') },
          ]}
        >
          <Input.TextArea
            placeholder={t('systemConfig.form.reasonPlaceholder')}
            rows={3}
            showCount
            maxLength={500}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
};

import { useCallback, useState, useEffect } from 'react';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  AntSelect as Select,
  Descriptions,
  InputNumber,
  Tag,
  message,
} from '@radish/ui';
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

  const loadConfigDetail = useCallback(async (id: number) => {
    try {
      setInitialLoading(true);
      const nextConfig = await getConfigById(id, t);
      setConfig(nextConfig);
      form.setFieldsValue({
        value: normalizeFormValue(nextConfig),
        reason: '',
      });
    } catch (error) {
      log.error('SystemConfigForm', '加载系统设置详情失败:', error);
      message.error(error instanceof Error ? error.message : t('systemConfig.feedback.loadDetailFailed'));
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
        confirmRiskLevel: config.voRiskLevel,
        confirmKey: config.voKey,
        expectedVersion: config.voVersion,
      }, t);
      message.success(t('systemConfig.feedback.updateSuccess'));
      onSuccess();
    } catch (error) {
      log.error('SystemConfigForm', '提交系统设置失败:', error);
      message.error(error instanceof Error ? error.message : t('systemConfig.feedback.updateFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    form.resetFields();
    setConfig(undefined);
    onCancel();
  };

  useEffect(() => {
    if (visible && configId) {
      void loadConfigDetail(configId);
    } else {
      form.resetFields();
      setConfig(undefined);
    }
  }, [visible, configId, form, loadConfigDetail]);

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

      <Form
        form={form}
        layout="vertical"
        disabled={initialLoading || !config?.voIsEditable}
        className="system-config-edit-form"
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

import { useState, useEffect } from 'react';
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

interface SystemConfigFormProps {
  visible: boolean;
  configId?: number;
  onCancel: () => void;
  onSuccess: () => void;
}

const hasNumberConstraint = (value?: number | null): value is number => (
  typeof value === 'number' && Number.isFinite(value)
);

const formatNumberConstraint = (config: SystemConfigVo) => {
  const parts: string[] = [];

  if (config.voRequiresInteger) {
    parts.push('整数');
  }

  if (hasNumberConstraint(config.voMinNumberValue) && hasNumberConstraint(config.voMaxNumberValue)) {
    parts.push(`范围 ${config.voMinNumberValue} - ${config.voMaxNumberValue}`);
  } else if (hasNumberConstraint(config.voMinNumberValue)) {
    parts.push(`不小于 ${config.voMinNumberValue}`);
  } else if (hasNumberConstraint(config.voMaxNumberValue)) {
    parts.push(`不大于 ${config.voMaxNumberValue}`);
  }

  return parts.length > 0 ? parts.join('，') : '未定义额外数字规则';
};

const normalizeFormValue = (config: SystemConfigVo) => {
  if (config.voType !== 'number') {
    return config.voEffectiveValue;
  }

  const numberValue = Number(config.voEffectiveValue);
  return Number.isFinite(numberValue) ? numberValue : config.voEffectiveValue;
};

const validateNumberValue = (config: SystemConfigVo, value: unknown) => {
  if (value === undefined || value === null || value === '') {
    return;
  }

  const numberValue = Number(value);
  if (!Number.isFinite(numberValue)) {
    throw new Error('请输入有效的数字');
  }

  if (config.voRequiresInteger && !Number.isInteger(numberValue)) {
    throw new Error('请输入整数');
  }

  if (hasNumberConstraint(config.voMinNumberValue) && numberValue < config.voMinNumberValue) {
    throw new Error(`设置值不能小于 ${config.voMinNumberValue}`);
  }

  if (hasNumberConstraint(config.voMaxNumberValue) && numberValue > config.voMaxNumberValue) {
    throw new Error(`设置值不能大于 ${config.voMaxNumberValue}`);
  }
};

export const SystemConfigForm = ({
  visible,
  configId,
  onCancel,
  onSuccess,
}: SystemConfigFormProps) => {
  const [form] = Form.useForm();
  const [config, setConfig] = useState<SystemConfigVo>();
  const [loading, setLoading] = useState(false);
  const [initialLoading, setInitialLoading] = useState(false);

  const loadConfigDetail = async (id: number) => {
    try {
      setInitialLoading(true);
      const nextConfig = await getConfigById(id);
      setConfig(nextConfig);
      form.setFieldsValue({
        value: normalizeFormValue(nextConfig),
        reason: '',
      });
    } catch (error) {
      log.error('SystemConfigForm', '加载系统设置详情失败:', error);
      message.error('加载系统设置详情失败');
    } finally {
      setInitialLoading(false);
    }
  };

  const handleSubmit = async () => {
    if (!configId || !config) {
      message.error('系统设置尚未加载完成');
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
      });
      message.success('系统设置已更新');
      onSuccess();
    } catch (error) {
      log.error('SystemConfigForm', '提交系统设置失败:', error);
      message.error(error instanceof Error ? error.message : '更新系统设置失败');
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
  }, [visible, configId, form]);

  const renderValueInput = () => {
    switch (config?.voType) {
      case 'number':
        return (
          <InputNumber
            min={hasNumberConstraint(config.voMinNumberValue) ? config.voMinNumberValue : undefined}
            max={hasNumberConstraint(config.voMaxNumberValue) ? config.voMaxNumberValue : undefined}
            precision={config.voRequiresInteger ? 0 : undefined}
            step={config.voRequiresInteger ? 1 : undefined}
            placeholder="请输入数字值"
            style={{ width: '100%' }}
          />
        );
      case 'boolean':
        return (
          <Select
            placeholder="请选择布尔值"
            options={[
              { label: 'true', value: 'true' },
              { label: 'false', value: 'false' },
            ]}
          />
        );
      case 'json':
        return (
          <Input.TextArea
            placeholder="请输入 JSON 格式的值"
            rows={4}
            showCount
          />
        );
      default:
        return (
          <Input placeholder="请输入设置值" />
        );
    }
  };

  const getDescriptionItems = () => {
    if (!config) {
      return [];
    }

    const items = [
      { key: 'category', label: '分类', children: config.voCategory },
      { key: 'key', label: '设置键', children: <code>{config.voKey}</code> },
      { key: 'default', label: '默认值', children: config.voDefaultValue },
      {
        key: 'risk',
        label: '风险等级',
        children: <Tag color={config.voRiskLevel === 'Low' ? 'success' : 'warning'}>{config.voRiskLevel}</Tag>,
      },
      { key: 'mode', label: '生效方式', children: config.voEffectiveMode === 'Immediate' ? '立即生效' : config.voEffectiveMode },
    ];

    if (config.voType === 'number') {
      items.push({
        key: 'validation',
        label: '校验规则',
        children: formatNumberConstraint(config),
      });
    }

    if (config.voImpactSummary) {
      items.push({
        key: 'impact',
        label: '影响范围',
        children: config.voImpactSummary,
      });
    }

    return items;
  };

  return (
    <Modal
      title="编辑系统设置"
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
          保存该设置会影响内容发布规则，需填写修改原因并写入变更审计。
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
          label="覆盖值"
          rules={[
            { required: true, message: '请输入设置值' },
            {
              validator: async (_, value) => {
                if (!config) {
                  return;
                }
                if (config.voType === 'number') {
                  validateNumberValue(config, value);
                }
                if (config.voType === 'json') {
                  try {
                    JSON.parse(value);
                  } catch {
                    throw new Error('请输入有效的 JSON 格式');
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
          label="修改原因"
          rules={[
            { required: true, message: '请填写修改原因' },
            { max: 500, message: '修改原因不能超过 500 个字符' },
          ]}
        >
          <Input.TextArea
            placeholder="说明本次修改的背景、影响范围或回滚依据"
            rows={3}
            showCount
            maxLength={500}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
};

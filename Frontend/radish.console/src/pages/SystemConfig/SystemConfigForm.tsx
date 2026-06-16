import { useState, useEffect } from 'react';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  AntSelect as Select,
  Descriptions,
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
        value: nextConfig.voEffectiveValue,
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
          <Input
            type="number"
            placeholder="请输入数字值"
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
          items={[
            { key: 'category', label: '分类', children: config.voCategory },
            { key: 'key', label: '设置键', children: <code>{config.voKey}</code> },
            { key: 'default', label: '默认值', children: config.voDefaultValue },
            {
              key: 'risk',
              label: '风险等级',
              children: <Tag color={config.voRiskLevel === 'Low' ? 'success' : 'warning'}>{config.voRiskLevel}</Tag>,
            },
            { key: 'mode', label: '生效方式', children: config.voEffectiveMode === 'Immediate' ? '立即生效' : config.voEffectiveMode },
          ]}
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
                if (config.voType === 'number' && Number.isNaN(Number(value))) {
                  throw new Error('请输入有效的数字');
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

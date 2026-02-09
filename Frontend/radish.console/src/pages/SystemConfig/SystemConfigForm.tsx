import { useState, useEffect } from 'react';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  AntSelect as Select,
  Switch,
  message,
} from '@radish/ui';
import {
  createConfig,
  updateConfig,
  getConfigById,
  type SystemConfig,
  type ConfigRequest,
} from '@/api/systemConfigApi';
import { log } from '@/utils/logger';

interface SystemConfigFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  configId?: number;
  categories: string[];
  onCancel: () => void;
  onSuccess: () => void;
}

export const SystemConfigForm = ({
  visible,
  mode,
  configId,
  categories,
  onCancel,
  onSuccess,
}: SystemConfigFormProps) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [initialLoading, setInitialLoading] = useState(false);

  // 配置类型选项
  const typeOptions = [
    { label: '字符串', value: 'string' },
    { label: '数字', value: 'number' },
    { label: '布尔值', value: 'boolean' },
    { label: 'JSON', value: 'json' },
  ];

  // 加载配置详情（编辑模式）
  const loadConfigDetail = async (id: number) => {
    try {
      setInitialLoading(true);
      const config = await getConfigById(id);
      form.setFieldsValue({
        category: config.category,
        key: config.key,
        name: config.name,
        value: config.value,
        description: config.description,
        type: config.type,
        isEnabled: config.isEnabled,
      });
    } catch (error) {
      log.error('SystemConfigForm', '加载配置详情失败:', error);
      message.error('加载配置详情失败');
    } finally {
      setInitialLoading(false);
    }
  };

  // 提交表单
  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      // 根据类型转换值
      let processedValue = values.value;
      if (values.type === 'number') {
        processedValue = String(Number(values.value));
      } else if (values.type === 'boolean') {
        processedValue = String(Boolean(values.value));
      }

      const configData: ConfigRequest = {
        value: processedValue,
        description: values.description,
        isEnabled: values.isEnabled,
      };

      if (mode === 'create') {
        await createConfig({
          ...configData,
          category: values.category,
          key: values.key,
          name: values.name,
          type: values.type,
        });
        message.success('创建配置成功');
      } else if (mode === 'edit' && configId) {
        await updateConfig(configId, configData);
        message.success('更新配置成功');
      }

      onSuccess();
    } catch (error) {
      log.error('SystemConfigForm', '提交表单失败:', error);
      message.error(mode === 'create' ? '创建配置失败' : '更新配置失败');
    } finally {
      setLoading(false);
    }
  };

  // 重置表单
  const handleCancel = () => {
    form.resetFields();
    onCancel();
  };

  // 监听 visible 和 configId 变化
  useEffect(() => {
    if (visible) {
      if (mode === 'edit' && configId) {
        loadConfigDetail(configId);
      } else {
        // 创建模式，设置默认值
        form.setFieldsValue({
          type: 'string',
          isEnabled: true,
        });
      }
    } else {
      form.resetFields();
    }
  }, [visible, mode, configId, form]);

  // 根据类型渲染值输入框
  const renderValueInput = () => {
    const type = form.getFieldValue('type');

    switch (type) {
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
              { label: 'false', value: 'false' }
            ]}
          />
        );
      case 'json':
        return (
          <Input.TextArea
            placeholder="请输入JSON格式的值"
            rows={4}
            showCount
          />
        );
      default:
        return (
          <Input placeholder="请输入配置值" />
        );
    }
  };

  return (
    <Modal
      title={mode === 'create' ? '新增配置' : '编辑配置'}
      open={visible}
      onOk={handleSubmit}
      onCancel={handleCancel}
      confirmLoading={loading}
      width={600}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        disabled={initialLoading}
      >
        <Form.Item
          name="category"
          label="配置分类"
          rules={[
            { required: true, message: '请选择配置分类' },
          ]}
        >
          <Select
            placeholder="请选择配置分类"
            showSearch
            allowClear
            disabled={mode === 'edit'}
            options={categories.map(category => ({ label: category, value: category }))}
          />
        </Form.Item>

        <Form.Item
          name="key"
          label="配置键"
          rules={[
            { required: true, message: '请输入配置键' },
            { pattern: /^[a-zA-Z][a-zA-Z0-9._]*$/, message: '配置键必须以字母开头，只能包含字母、数字、点和下划线' },
          ]}
        >
          <Input
            placeholder="请输入配置键，如：Shop.OrderTimeoutMinutes"
            disabled={mode === 'edit'}
          />
        </Form.Item>

        <Form.Item
          name="name"
          label="配置名称"
          rules={[
            { required: true, message: '请输入配置名称' },
            { max: 100, message: '配置名称不能超过100个字符' },
          ]}
        >
          <Input
            placeholder="请输入配置名称"
            disabled={mode === 'edit'}
          />
        </Form.Item>

        <Form.Item
          name="type"
          label="配置类型"
          rules={[
            { required: true, message: '请选择配置类型' },
          ]}
        >
          <Select
            placeholder="请选择配置类型"
            options={typeOptions}
            disabled={mode === 'edit'}
            onChange={() => {
              // 类型改变时清空值
              form.setFieldValue('value', '');
            }}
          />
        </Form.Item>

        <Form.Item
          name="value"
          label="配置值"
          rules={[
            { required: true, message: '请输入配置值' },
            {
              validator: async (_, value) => {
                const type = form.getFieldValue('type');
                if (type === 'number' && isNaN(Number(value))) {
                  throw new Error('请输入有效的数字');
                }
                if (type === 'json') {
                  try {
                    JSON.parse(value);
                  } catch {
                    throw new Error('请输入有效的JSON格式');
                  }
                }
              },
            },
          ]}
        >
          {renderValueInput()}
        </Form.Item>

        <Form.Item
          name="description"
          label="配置描述"
          rules={[
            { max: 500, message: '配置描述不能超过500个字符' },
          ]}
        >
          <Input.TextArea
            placeholder="请输入配置描述"
            rows={3}
            showCount
            maxLength={500}
          />
        </Form.Item>

        <Form.Item
          name="isEnabled"
          label="启用状态"
          valuePropName="checked"
        >
          <Switch checkedChildren="启用" unCheckedChildren="禁用" />
        </Form.Item>
      </Form>
    </Modal>
  );
};
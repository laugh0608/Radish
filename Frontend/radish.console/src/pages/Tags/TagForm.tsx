import { useEffect, useState } from 'react';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  InputNumber,
  Switch,
  message,
} from '@radish/ui';
import { createTag, updateTag, type TagVo, type TagUpsertRequest } from '@/api/tagApi';
import { log } from '@/utils/logger';

interface TagFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  tag?: TagVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const TagForm = ({ visible, mode, tag, onCancel, onSuccess }: TagFormProps) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const request: TagUpsertRequest = {
        name: values.name,
        slug: values.slug,
        description: values.description,
        color: values.color,
        sortOrder: values.sortOrder,
        isEnabled: values.isEnabled,
        isFixed: values.isFixed,
      };

      if (mode === 'create') {
        await createTag(request);
        message.success('创建标签成功');
      } else if (mode === 'edit' && tag) {
        await updateTag(tag.voId, request);
        message.success('更新标签成功');
      }

      onSuccess();
    } catch (error) {
      log.error('TagForm', '提交标签表单失败:', error);
      message.error(mode === 'create' ? '创建标签失败' : '更新标签失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!visible) {
      form.resetFields();
      return;
    }

    if (mode === 'edit' && tag) {
      form.setFieldsValue({
        name: tag.voName,
        slug: tag.voSlug,
        description: tag.voDescription,
        color: tag.voColor,
        sortOrder: tag.voSortOrder,
        isEnabled: tag.voIsEnabled,
        isFixed: tag.voIsFixed,
      });
      return;
    }

    form.setFieldsValue({
      name: '',
      slug: '',
      description: '',
      color: '',
      sortOrder: 0,
      isEnabled: true,
      isFixed: true,
    });
  }, [visible, mode, tag, form]);

  return (
    <Modal
      title={mode === 'create' ? '新增标签' : '编辑标签'}
      open={visible}
      onOk={handleSubmit}
      onCancel={onCancel}
      confirmLoading={loading}
      width={640}
      destroyOnHidden
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="name"
          label="标签名称"
          rules={[
            { required: true, message: '请输入标签名称' },
            { max: 50, message: '标签名称不能超过50个字符' },
          ]}
        >
          <Input placeholder="请输入标签名称" />
        </Form.Item>

        <Form.Item
          name="slug"
          label="Slug"
          rules={[{ max: 50, message: 'Slug 不能超过50个字符' }]}
        >
          <Input placeholder="可选，不填则按名称自动生成" />
        </Form.Item>

        <Form.Item
          name="description"
          label="描述"
          rules={[{ max: 500, message: '描述不能超过500个字符' }]}
        >
          <Input.TextArea placeholder="请输入标签描述" rows={3} maxLength={500} showCount />
        </Form.Item>

        <Form.Item
          name="color"
          label="颜色"
          rules={[{ max: 20, message: '颜色值不能超过20个字符' }]}
        >
          <Input placeholder="如 #1677FF" />
        </Form.Item>

        <Form.Item
          name="sortOrder"
          label="排序"
          rules={[
            { required: true, message: '请输入排序值' },
            { type: 'number', min: 0, message: '排序值不能为负数' },
          ]}
        >
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="isFixed" label="固定标签" valuePropName="checked">
          <Switch checkedChildren="固定" unCheckedChildren="普通" />
        </Form.Item>

        <Form.Item name="isEnabled" label="启用状态" valuePropName="checked">
          <Switch checkedChildren="启用" unCheckedChildren="禁用" />
        </Form.Item>
      </Form>
    </Modal>
  );
};

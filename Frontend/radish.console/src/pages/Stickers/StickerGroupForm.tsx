import { useEffect, useState } from 'react';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  InputNumber,
  Switch,
  AntSelect as Select,
  message,
} from '@radish/ui';
import {
  checkGroupCode,
  createStickerGroup,
  updateStickerGroup,
  type StickerGroupUpsertRequest,
  type StickerGroupVo,
} from '@/api/stickerApi';
import { log } from '@/utils/logger';

interface StickerGroupFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  group?: StickerGroupVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const StickerGroupForm = ({ visible, mode, group, onCancel, onSuccess }: StickerGroupFormProps) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [codeChecking, setCodeChecking] = useState(false);

  const handleCodeBlur = async () => {
    if (mode !== 'create') {
      return;
    }

    const code = String(form.getFieldValue('code') || '').trim().toLowerCase();
    if (!code) {
      return;
    }

    try {
      setCodeChecking(true);
      const result = await checkGroupCode(code);
      if (!result.voAvailable) {
        form.setFields([{ name: 'code', errors: ['分组标识符已存在'] }]);
      } else {
        form.setFields([{ name: 'code', errors: [] }]);
      }
    } catch (error) {
      log.error('StickerGroupForm', '校验分组编码失败:', error);
    } finally {
      setCodeChecking(false);
    }
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const request: StickerGroupUpsertRequest = {
        name: values.name.trim(),
        code: mode === 'create' ? values.code.trim().toLowerCase() : undefined,
        description: values.description?.trim() || undefined,
        coverImageUrl: values.coverImageUrl?.trim() || undefined,
        groupType: values.groupType,
        isEnabled: values.isEnabled,
        sort: values.sort,
      };

      if (mode === 'create') {
        const availability = await checkGroupCode(request.code || '');
        if (!availability.voAvailable) {
          form.setFields([{ name: 'code', errors: ['分组标识符已存在'] }]);
          return;
        }

        await createStickerGroup(request);
        message.success('创建表情包分组成功');
      } else if (mode === 'edit' && group) {
        await updateStickerGroup(group.voId, request);
        message.success('更新表情包分组成功');
      }

      onSuccess();
    } catch (error) {
      log.error('StickerGroupForm', '提交表情包分组失败:', error);
      message.error(mode === 'create' ? '创建表情包分组失败' : '更新表情包分组失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!visible) {
      form.resetFields();
      return;
    }

    if (mode === 'edit' && group) {
      form.setFieldsValue({
        name: group.voName,
        code: group.voCode,
        description: group.voDescription,
        coverImageUrl: group.voCoverImageUrl,
        groupType: group.voGroupType,
        isEnabled: group.voIsEnabled,
        sort: group.voSort,
      });
      return;
    }

    form.setFieldsValue({
      name: '',
      code: '',
      description: '',
      coverImageUrl: '',
      groupType: 1,
      isEnabled: true,
      sort: 0,
    });
  }, [visible, mode, group, form]);

  return (
    <Modal
      title={mode === 'create' ? '新增表情包分组' : '编辑表情包分组'}
      open={visible}
      onOk={() => {
        void handleSubmit();
      }}
      onCancel={onCancel}
      confirmLoading={loading}
      width={680}
      destroyOnHidden
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="name"
          label="分组名称"
          rules={[
            { required: true, message: '请输入分组名称' },
            { max: 100, message: '分组名称不能超过100个字符' },
          ]}
        >
          <Input placeholder="请输入分组名称" />
        </Form.Item>

        <Form.Item
          name="code"
          label="分组标识符"
          tooltip={mode === 'edit' ? '分组标识符创建后不可修改' : '仅允许小写字母、数字和下划线'}
          rules={[
            { required: true, message: '请输入分组标识符' },
            { max: 100, message: '分组标识符不能超过100个字符' },
            { pattern: /^[a-z0-9_]+$/, message: '仅允许小写字母、数字和下划线' },
          ]}
        >
          <Input
            placeholder="例如：radish_default"
            disabled={mode === 'edit'}
            onBlur={() => {
              void handleCodeBlur();
            }}
            suffix={codeChecking ? '校验中...' : undefined}
          />
        </Form.Item>

        <Form.Item
          name="description"
          label="描述"
          rules={[{ max: 500, message: '描述不能超过500个字符' }]}
        >
          <Input.TextArea rows={3} maxLength={500} showCount placeholder="请输入分组描述（可选）" />
        </Form.Item>

        <Form.Item
          name="coverImageUrl"
          label="封面图地址"
          rules={[{ max: 500, message: '封面图地址不能超过500个字符' }]}
        >
          <Input placeholder="请输入封面图 URL（可选）" />
        </Form.Item>

        <Form.Item
          name="groupType"
          label="分组类型"
          rules={[{ required: true, message: '请选择分组类型' }]}
        >
          <Select
            options={[
              { value: 1, label: '官方表情包' },
              { value: 2, label: '付费表情包' },
            ]}
          />
        </Form.Item>

        <Form.Item
          name="sort"
          label="排序"
          rules={[
            { required: true, message: '请输入排序值' },
            { type: 'number', min: 0, message: '排序值不能为负数' },
          ]}
        >
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="isEnabled" label="启用状态" valuePropName="checked">
          <Switch checkedChildren="启用" unCheckedChildren="禁用" />
        </Form.Item>
      </Form>
    </Modal>
  );
};

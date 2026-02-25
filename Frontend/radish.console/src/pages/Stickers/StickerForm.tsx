import { useEffect, useState } from 'react';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  InputNumber,
  Switch,
  Button,
  message,
} from '@radish/ui';
import {
  addSticker,
  checkStickerCode,
  updateSticker,
  type CreateStickerRequest,
  type StickerVo,
  type UpdateStickerRequest,
} from '@/api/stickerApi';
import { log } from '@/utils/logger';

interface StickerFormProps {
  visible: boolean;
  groupId: number;
  mode: 'create' | 'edit';
  sticker?: StickerVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const StickerForm = ({ visible, groupId, mode, sticker, onCancel, onSuccess }: StickerFormProps) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [codeChecking, setCodeChecking] = useState(false);

  const handleCodeBlur = async () => {
    if (mode !== 'create') {
      return;
    }

    const code = String(form.getFieldValue('code') || '').trim().toLowerCase();
    if (!code || groupId <= 0) {
      return;
    }

    try {
      setCodeChecking(true);
      const result = await checkStickerCode(groupId, code);
      if (!result.voAvailable) {
        form.setFields([{ name: 'code', errors: ['该分组内表情标识符已存在'] }]);
      } else {
        form.setFields([{ name: 'code', errors: [] }]);
      }
    } catch (error) {
      log.error('StickerForm', '校验表情编码失败:', error);
    } finally {
      setCodeChecking(false);
    }
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      if (mode === 'create') {
        const normalizedCode = values.code.trim().toLowerCase();
        const availability = await checkStickerCode(groupId, normalizedCode);
        if (!availability.voAvailable) {
          form.setFields([{ name: 'code', errors: ['该分组内表情标识符已存在'] }]);
          return;
        }

        const request: CreateStickerRequest = {
          groupId,
          code: normalizedCode,
          name: values.name.trim(),
          imageUrl: values.imageUrl?.trim() || undefined,
          thumbnailUrl: values.thumbnailUrl?.trim() || undefined,
          isAnimated: values.isAnimated,
          allowInline: values.allowInline,
          attachmentId: values.attachmentId || undefined,
          isEnabled: values.isEnabled,
          sort: values.sort,
        };

        await addSticker(request);
        message.success('新增表情成功');
      } else if (mode === 'edit' && sticker) {
        const request: UpdateStickerRequest = {
          name: values.name.trim(),
          imageUrl: values.imageUrl?.trim() || undefined,
          thumbnailUrl: values.thumbnailUrl?.trim() || undefined,
          isAnimated: values.isAnimated,
          allowInline: values.allowInline,
          attachmentId: values.attachmentId || undefined,
          isEnabled: values.isEnabled,
          sort: values.sort,
        };

        await updateSticker(sticker.voId, request);
        message.success('更新表情成功');
      }

      onSuccess();
    } catch (error) {
      log.error('StickerForm', '提交表情表单失败:', error);
      message.error(mode === 'create' ? '新增表情失败' : '更新表情失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!visible) {
      form.resetFields();
      return;
    }

    if (mode === 'edit' && sticker) {
      form.setFieldsValue({
        code: sticker.voCode,
        name: sticker.voName,
        imageUrl: sticker.voImageUrl,
        thumbnailUrl: sticker.voThumbnailUrl,
        isAnimated: sticker.voIsAnimated,
        allowInline: sticker.voAllowInline,
        attachmentId: sticker.voAttachmentId,
        isEnabled: sticker.voIsEnabled,
        sort: sticker.voSort,
      });
      return;
    }

    form.setFieldsValue({
      code: '',
      name: '',
      imageUrl: '',
      thumbnailUrl: '',
      isAnimated: false,
      allowInline: true,
      attachmentId: undefined,
      isEnabled: true,
      sort: 0,
    });
  }, [visible, mode, sticker, form]);

  return (
    <Modal
      title={mode === 'create' ? '新增表情' : '编辑表情'}
      open={visible}
      onOk={() => {
        void handleSubmit();
      }}
      onCancel={onCancel}
      confirmLoading={loading}
      width={720}
      destroyOnHidden
      footer={(_, { OkBtn, CancelBtn }) => (
        <>
          <CancelBtn />
          <Button onClick={() => form.resetFields()}>重置</Button>
          <OkBtn />
        </>
      )}
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="code"
          label="表情标识符"
          tooltip={mode === 'edit' ? '表情标识符创建后不可修改' : '仅允许小写字母、数字和下划线'}
          rules={[
            { required: true, message: '请输入表情标识符' },
            { max: 100, message: '表情标识符不能超过100个字符' },
            { pattern: /^[a-z0-9_]+$/, message: '仅允许小写字母、数字和下划线' },
          ]}
        >
          <Input
            placeholder="例如：happy"
            disabled={mode === 'edit'}
            onBlur={() => {
              void handleCodeBlur();
            }}
            suffix={codeChecking ? '校验中...' : undefined}
          />
        </Form.Item>

        <Form.Item
          name="name"
          label="显示名称"
          rules={[
            { required: true, message: '请输入显示名称' },
            { max: 200, message: '显示名称不能超过200个字符' },
          ]}
        >
          <Input placeholder="例如：开心" />
        </Form.Item>

        <Form.Item
          name="attachmentId"
          label="附件 ID"
          tooltip="优先填写附件ID；若不填可直接填图片 URL"
          rules={[{ type: 'number', min: 1, message: '附件ID必须大于0' }]}
        >
          <InputNumber min={1} style={{ width: '100%' }} placeholder="例如：9001" />
        </Form.Item>

        <Form.Item
          name="imageUrl"
          label="图片 URL"
          rules={[{ max: 500, message: '图片 URL 不能超过500个字符' }]}
        >
          <Input placeholder="https://..." />
        </Form.Item>

        <Form.Item
          name="thumbnailUrl"
          label="缩略图 URL"
          rules={[{ max: 500, message: '缩略图 URL 不能超过500个字符' }]}
        >
          <Input placeholder="https://...（可选）" />
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

        <Form.Item name="allowInline" label="允许内嵌正文" valuePropName="checked">
          <Switch checkedChildren="允许" unCheckedChildren="仅Reaction" />
        </Form.Item>

        <Form.Item name="isAnimated" label="是否动图" valuePropName="checked">
          <Switch checkedChildren="GIF" unCheckedChildren="静图" />
        </Form.Item>

        <Form.Item name="isEnabled" label="启用状态" valuePropName="checked">
          <Switch checkedChildren="启用" unCheckedChildren="禁用" />
        </Form.Item>
      </Form>
    </Modal>
  );
};

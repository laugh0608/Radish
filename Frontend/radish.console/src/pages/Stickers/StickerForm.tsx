import { useEffect, useState } from 'react';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  InputNumber,
  Switch,
  Button,
  PlusOutlined,
  Space,
  message,
} from '@radish/ui';
import { Upload } from 'antd';
import type { UploadProps } from 'antd';
import {
  addSticker,
  checkStickerCode,
  updateSticker,
  type CreateStickerRequest,
  type StickerVo,
  type UpdateStickerRequest,
} from '@/api/stickerApi';
import { uploadAttachmentImage } from '@/api/attachmentApi';
import { getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';

interface StickerFormProps {
  visible: boolean;
  groupId: string;
  mode: 'create' | 'edit';
  sticker?: StickerVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const StickerForm = ({ visible, groupId, mode, sticker, onCancel, onSuccess }: StickerFormProps) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [codeChecking, setCodeChecking] = useState(false);
  const [imageUploading, setImageUploading] = useState(false);
  const attachmentId = Form.useWatch('attachmentId', form);
  const [previewUrl, setPreviewUrl] = useState<string>('');

  const normalizeOptionalAttachmentId = (value: unknown): string | null => {
    if (typeof value === 'string' && /^[1-9]\d*$/.test(value.trim())) {
      return value.trim();
    }

    return null;
  };

  const handleImageUpload: UploadProps['customRequest'] = async (options) => {
    const file = options.file;
    if (!(file instanceof File)) {
      options.onError?.(new Error('无效文件'));
      return;
    }

    const isImage = file.type
      ? file.type.startsWith('image/')
      : /\.(jpe?g|png|gif|webp|bmp|svg)$/i.test(file.name);
    if (!isImage) {
      const error = new Error('仅支持上传图片文件');
      options.onError?.(error);
      message.error(error.message);
      return;
    }

    const isLt5M = file.size / 1024 / 1024 <= 5;
    if (!isLt5M) {
      const error = new Error('图片大小不能超过 5MB');
      options.onError?.(error);
      message.error(error.message);
      return;
    }

    try {
      setImageUploading(true);
      const result = await uploadAttachmentImage(file, { businessType: 'Sticker' }, (percent) => {
        options.onProgress?.({ percent });
      });

      form.setFieldsValue({
        attachmentId: result.attachmentId,
      });
      setPreviewUrl(getAvatarUrl(result.thumbnailUrl || result.url) || '');
      options.onSuccess?.(result);
      message.success('图片上传成功，已自动回填附件 ID');
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error('图片上传失败');
      options.onError?.(uploadError);
      log.error('StickerForm', '上传单个表情图片失败:', error);
      message.error(uploadError.message);
    } finally {
      setImageUploading(false);
    }
  };

  const handleCodeBlur = async () => {
    if (mode !== 'create') {
      return;
    }

    const code = String(form.getFieldValue('code') || '').trim().toLowerCase();
    if (!code || !/^[1-9]\d*$/.test(groupId)) {
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
    if (imageUploading) {
      message.warning('图片仍在上传中，请稍候提交');
      return;
    }

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
          groupId: groupId.trim(),
          code: normalizedCode,
          name: values.name.trim(),
          isAnimated: values.isAnimated,
          allowInline: values.allowInline,
          attachmentId: normalizeOptionalAttachmentId(values.attachmentId),
          isEnabled: values.isEnabled,
          sort: values.sort,
        };

        await addSticker(request);
        message.success('新增表情成功');
      } else if (mode === 'edit' && sticker) {
        const request: UpdateStickerRequest = {
          name: values.name.trim(),
          isAnimated: values.isAnimated,
          allowInline: values.allowInline,
          attachmentId: normalizeOptionalAttachmentId(values.attachmentId),
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
      setPreviewUrl('');
      return;
    }

    if (mode === 'edit' && sticker) {
      form.setFieldsValue({
        code: sticker.voCode,
        name: sticker.voName,
        isAnimated: sticker.voIsAnimated,
        allowInline: sticker.voAllowInline,
        attachmentId: sticker.voAttachmentId,
        isEnabled: sticker.voIsEnabled,
        sort: sticker.voSort,
      });
      setPreviewUrl(getAvatarUrl(sticker.voThumbnailUrl || sticker.voImageUrl) || '');
      return;
    }

    form.setFieldsValue({
      code: '',
      name: '',
      isAnimated: false,
      allowInline: true,
      attachmentId: undefined,
      isEnabled: true,
      sort: 0,
    });
    setPreviewUrl('');
  }, [visible, mode, sticker, form]);

  return (
    <Modal
      title={mode === 'create' ? '新增表情' : '编辑表情'}
      open={visible}
      onOk={() => {
        void handleSubmit();
      }}
      onCancel={onCancel}
      confirmLoading={loading || imageUploading}
      width={720}
      destroyOnHidden
      forceRender
      footer={(_, { OkBtn, CancelBtn }) => (
        <>
          <CancelBtn />
          <Button
            onClick={() => {
              form.resetFields();
              setPreviewUrl('');
            }}
          >
            重置
          </Button>
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

        <Form.Item label="图片资源">
          <Space direction="vertical" style={{ width: '100%' }} size={10}>
            <div
              style={{
                width: 120,
                height: 120,
                borderRadius: 10,
                border: '1px solid #f0f0f0',
                background: '#fafafa',
                overflow: 'hidden',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: '#999',
                fontSize: 12,
              }}
            >
              {previewUrl ? (
                <img
                  src={previewUrl}
                  alt="表情预览"
                  style={{ width: '100%', height: '100%', objectFit: 'contain', background: '#fff' }}
                />
              ) : (
                <span>暂无图片</span>
              )}
            </div>

            <Space>
              <Upload
                accept="image/*"
                showUploadList={false}
                customRequest={handleImageUpload}
                disabled={imageUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={imageUploading || loading}>
                  {imageUploading ? '上传中...' : '上传图片'}
                </Button>
              </Upload>
              <Button
                disabled={!previewUrl && !attachmentId}
                onClick={() => {
                  form.setFieldsValue({
                    attachmentId: undefined,
                  });
                  setPreviewUrl('');
                }}
              >
                清空图片
              </Button>
            </Space>
          </Space>
        </Form.Item>

        <Form.Item
          name="attachmentId"
          label="附件 ID"
          tooltip="上传后自动回填；也可手动填写"
          rules={[{ pattern: /^[1-9]\d*$/, message: '附件ID必须为正整数' }]}
        >
          <Input placeholder="例如：2028085755741470720" />
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

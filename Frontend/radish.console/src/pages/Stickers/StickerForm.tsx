import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  attachmentImageAccept,
  isSupportedAttachmentImageFile,
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
import '../adminForm.css';

interface StickerFormProps {
  visible: boolean;
  groupId: string;
  mode: 'create' | 'edit';
  sticker?: StickerVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const StickerForm = ({ visible, groupId, mode, sticker, onCancel, onSuccess }: StickerFormProps) => {
  const { t } = useTranslation();
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
      options.onError?.(new Error(t('stickers.common.invalidFile')));
      return;
    }

    const isImage = isSupportedAttachmentImageFile(file);
    if (!isImage) {
      const error = new Error(t('stickers.common.imageOnly'));
      options.onError?.(error);
      message.error(error.message);
      return;
    }

    const isLt5M = file.size / 1024 / 1024 <= 5;
    if (!isLt5M) {
      const error = new Error(t('stickers.common.imageTooLarge'));
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
      message.success(t('stickers.itemForm.imageUploaded'));
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error(t('stickers.common.uploadFailed'));
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
        form.setFields([{ name: 'code', errors: [t('stickers.itemForm.codeExists')] }]);
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
      message.warning(t('stickers.itemForm.uploading'));
      return;
    }

    try {
      const values = await form.validateFields();
      setLoading(true);

      if (mode === 'create') {
        const normalizedCode = values.code.trim().toLowerCase();
        const availability = await checkStickerCode(groupId, normalizedCode);
        if (!availability.voAvailable) {
          form.setFields([{ name: 'code', errors: [t('stickers.itemForm.codeExists')] }]);
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
        message.success(t('stickers.itemForm.feedback.created'));
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
        message.success(t('stickers.itemForm.feedback.updated'));
      }

      onSuccess();
    } catch (error) {
      log.error('StickerForm', '提交表情表单失败:', error);
      message.error(t(mode === 'create' ? 'stickers.itemForm.feedback.createFailed' : 'stickers.itemForm.feedback.updateFailed'));
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
      title={t(mode === 'create' ? 'stickers.itemForm.createTitle' : 'stickers.itemForm.editTitle')}
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
            {t('stickers.itemForm.reset')}
          </Button>
          <OkBtn />
        </>
      )}
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="code"
          label={t('stickers.itemForm.code')}
          tooltip={t(mode === 'edit' ? 'stickers.itemForm.codeImmutable' : 'stickers.common.codePattern')}
          rules={[
            { required: true, message: t('stickers.itemForm.codeRequired') },
            { max: 100, message: t('stickers.itemForm.codeMax') },
            { pattern: /^[a-z0-9_]+$/, message: t('stickers.common.codePattern') },
          ]}
        >
          <Input
            placeholder={t('stickers.itemForm.codePlaceholder')}
            disabled={mode === 'edit'}
            onBlur={() => {
              void handleCodeBlur();
            }}
            suffix={codeChecking ? t('stickers.common.codeChecking') : undefined}
          />
        </Form.Item>

        <Form.Item
          name="name"
          label={t('stickers.itemForm.name')}
          rules={[
            { required: true, message: t('stickers.itemForm.nameRequired') },
            { max: 200, message: t('stickers.itemForm.nameMax') },
          ]}
        >
          <Input placeholder={t('stickers.itemForm.namePlaceholder')} />
        </Form.Item>

        <Form.Item label={t('stickers.itemForm.image')}>
          <Space orientation="vertical" className="admin-form-field-stack" size={10}>
            <div className="admin-form-upload-preview admin-form-upload-preview--sticker">
              {previewUrl ? (
                <img
                  src={previewUrl}
                  alt={t('stickers.itemForm.imageAlt')}
                  className="admin-form-upload-preview__image admin-form-upload-preview__image--contain"
                />
              ) : (
                <span>{t('stickers.itemForm.noImage')}</span>
              )}
            </div>

            <Space>
              <Upload
                accept={attachmentImageAccept}
                showUploadList={false}
                customRequest={handleImageUpload}
                disabled={imageUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={imageUploading || loading}>
                  {t(imageUploading ? 'stickers.common.uploading' : 'stickers.itemForm.uploadImage')}
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
                {t('stickers.itemForm.clearImage')}
              </Button>
            </Space>
          </Space>
        </Form.Item>

        <Form.Item
          name="attachmentId"
          label={t('stickers.itemForm.attachmentId')}
          tooltip={t('stickers.itemForm.attachmentIdTooltip')}
          rules={[{ pattern: /^[1-9]\d*$/, message: t('stickers.common.attachmentIdInvalid') }]}
        >
          <Input placeholder={t('stickers.itemForm.attachmentIdPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="sort"
          label={t('stickers.common.sort')}
          rules={[
            { required: true, message: t('stickers.common.sortRequired') },
            { type: 'number', min: 0, message: t('stickers.common.sortMin') },
          ]}
        >
          <InputNumber min={0} className="admin-form-control-full" />
        </Form.Item>

        <Form.Item name="allowInline" label={t('stickers.itemForm.inline')} valuePropName="checked">
          <Switch checkedChildren={t('stickers.item.inline.allowed')} unCheckedChildren={t('stickers.item.inline.reactionOnly')} />
        </Form.Item>

        <Form.Item name="isAnimated" label={t('stickers.itemForm.animated')} valuePropName="checked">
          <Switch checkedChildren="GIF" unCheckedChildren={t('stickers.item.type.static')} />
        </Form.Item>

        <Form.Item name="isEnabled" label={t('stickers.itemForm.enabled')} valuePropName="checked">
          <Switch checkedChildren={t('stickers.common.enabled')} unCheckedChildren={t('stickers.common.disabled')} />
        </Form.Item>
      </Form>
    </Modal>
  );
};

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AntModal as Modal,
  BottomSheet,
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
import { Grid, Upload } from 'antd';
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
  canSubmit: boolean;
  onCancel: () => void;
  onSuccess: () => void;
}

export const StickerForm = ({ visible, groupId, mode, sticker, canSubmit, onCancel, onSuccess }: StickerFormProps) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [codeChecking, setCodeChecking] = useState(false);
  const [imageUploading, setImageUploading] = useState(false);
  const [isDirty, setIsDirty] = useState(false);
  const screens = Grid.useBreakpoint();
  const isMobile = !screens.md;
  const [previewUrl, setPreviewUrl] = useState<string>('');

  const handleImageUpload: UploadProps['customRequest'] = async (options) => {
    if (!canSubmit) {
      const error = new Error(t('stickers.common.permissionDenied'));
      message.error(error.message);
      options.onError?.(error);
      return;
    }

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
      setIsDirty(true);
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
    if (mode !== 'create' || !canSubmit) {
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
    if (loading) return;
    if (!canSubmit) {
      message.error(t('stickers.common.permissionDenied'));
      return;
    }
    if (imageUploading) {
      message.warning(t('stickers.itemForm.uploading'));
      return;
    }

    let values;
    try {
      values = await form.validateFields();
    } catch {
      return;
    }

    try {
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
          attachmentId: String(values.attachmentId).trim(),
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
          attachmentId: String(values.attachmentId).trim(),
          isEnabled: values.isEnabled,
          sort: values.sort,
        };

        await updateSticker(sticker.voId, request);
        message.success(t('stickers.itemForm.feedback.updated'));
      }

      setIsDirty(false);
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
      setIsDirty(false);
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
      setIsDirty(false);
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
    setIsDirty(false);
  }, [visible, mode, sticker, form]);

  useEffect(() => {
    if (!visible || !isDirty) return;
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [isDirty, visible]);

  const closeForm = () => {
    setIsDirty(false);
    form.resetFields();
    onCancel();
  };

  const handleRequestCancel = () => {
    if (loading || imageUploading) {
      message.warning(t('stickers.common.closeBusy'));
      return;
    }
    if (!isDirty) {
      closeForm();
      return;
    }
    Modal.confirm({
      title: t('stickers.common.discardTitle'),
      content: t('stickers.common.discardAttachmentDescription'),
      okText: t('stickers.common.discardConfirm'),
      cancelText: t('stickers.common.continueEditing'),
      okButtonProps: { danger: true },
      onOk: closeForm,
    });
  };

  const formContent = (
    <Form form={form} layout="vertical" onValuesChange={() => setIsDirty(true)}>

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
                disabled={!canSubmit || imageUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={!canSubmit || imageUploading || loading}>
                  {t(imageUploading ? 'stickers.common.uploading' : 'stickers.itemForm.uploadImage')}
                </Button>
              </Upload>
            </Space>
          </Space>
        </Form.Item>

        <Form.Item
          name="attachmentId"
          label={t('stickers.itemForm.attachmentId')}
          tooltip={t('stickers.itemForm.attachmentIdTooltip')}
          rules={[
            { required: true, message: t('stickers.itemForm.attachmentIdRequired') },
            { pattern: /^[1-9]\d*$/, message: t('stickers.common.attachmentIdInvalid') },
          ]}
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
  );

  const title = t(mode === 'create' ? 'stickers.itemForm.createTitle' : 'stickers.itemForm.editTitle');
  if (isMobile) {
    return (
      <BottomSheet
        isOpen={visible}
        onClose={handleRequestCancel}
        closeLabel={t('stickers.common.cancel')}
        title={title}
        height="92%"
        closeOnOverlayClick={false}
        closeOnEscape={!loading && !imageUploading}
        className="sticker-form-sheet"
        footer={(
          <div className="admin-form-mobile-actions">
            <Button disabled={loading || imageUploading} onClick={handleRequestCancel}>{t('stickers.common.cancel')}</Button>
            <Button variant="primary" disabled={!canSubmit || loading || imageUploading} onClick={() => void handleSubmit()}>{t('stickers.common.confirm')}</Button>
          </div>
        )}
      >
        {formContent}
      </BottomSheet>
    );
  }

  return (
    <Modal
      title={title}
      open={visible}
      onOk={() => void handleSubmit()}
      onCancel={handleRequestCancel}
      confirmLoading={loading || imageUploading}
      okButtonProps={{ disabled: !canSubmit }}
      width={720}
      maskClosable={false}
      keyboard={false}
      destroyOnHidden
      forceRender
    >
      {formContent}
    </Modal>
  );
};

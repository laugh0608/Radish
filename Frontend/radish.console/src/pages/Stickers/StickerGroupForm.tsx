import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  InputNumber,
  Button,
  Space,
  Switch,
  AntSelect as Select,
  message,
  attachmentImageAccept,
  isSupportedAttachmentImageFile,
} from '@radish/ui';
import { Upload } from 'antd';
import type { UploadProps } from 'antd';
import { PlusOutlined } from '@radish/ui';
import { uploadAttachmentImage } from '@/api/attachmentApi';
import {
  checkGroupCode,
  createStickerGroup,
  updateStickerGroup,
  type StickerGroupUpsertRequest,
  type StickerGroupVo,
} from '@/api/stickerApi';
import { getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';
import '../adminForm.css';

interface StickerGroupFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  group?: StickerGroupVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const StickerGroupForm = ({ visible, mode, group, onCancel, onSuccess }: StickerGroupFormProps) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [codeChecking, setCodeChecking] = useState(false);
  const [coverUploading, setCoverUploading] = useState(false);
  const coverAttachmentId = Form.useWatch('coverAttachmentId', form);
  const [coverPreviewUrl, setCoverPreviewUrl] = useState<string | undefined>(undefined);

  const normalizeOptionalAttachmentId = (value: unknown): string | null => {
    if (typeof value === 'string' && /^[1-9]\d*$/.test(value.trim())) {
      return value.trim();
    }

    return null;
  };

  const handleCoverUpload: UploadProps['customRequest'] = async (options) => {
    const file = options.file;
    if (!(file instanceof File)) {
      options.onError?.(new Error(t('stickers.common.invalidFile')));
      return;
    }

    const isImage = isSupportedAttachmentImageFile(file);
    if (!isImage) {
      const error = new Error(t('stickers.common.imageOnly'));
      message.error(error.message);
      options.onError?.(error);
      return;
    }

    const isLt5M = file.size / 1024 / 1024 <= 5;
    if (!isLt5M) {
      const error = new Error(t('stickers.groupForm.coverTooLarge'));
      message.error(error.message);
      options.onError?.(error);
      return;
    }

    try {
      setCoverUploading(true);
      const uploaded = await uploadAttachmentImage(file, { businessType: 'StickerCover' }, (percent) => {
        options.onProgress?.({ percent });
      });
      form.setFieldValue('coverAttachmentId', uploaded.attachmentId);
      setCoverPreviewUrl(getAvatarUrl(uploaded.thumbnailUrl || uploaded.url));
      options.onSuccess?.(uploaded);
      message.success(t('stickers.groupForm.coverUploaded'));
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error(t('stickers.groupForm.coverUploadFailed'));
      options.onError?.(uploadError);
      message.error(uploadError.message);
      log.error('StickerGroupForm', '上传封面图失败:', error);
    } finally {
      setCoverUploading(false);
    }
  };

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
        form.setFields([{ name: 'code', errors: [t('stickers.groupForm.codeExists')] }]);
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
    if (coverUploading) {
      message.warning(t('stickers.groupForm.coverUploading'));
      return;
    }

    try {
      const values = await form.validateFields();
      setLoading(true);

      const request: StickerGroupUpsertRequest = {
        name: values.name.trim(),
        code: mode === 'create' ? values.code.trim().toLowerCase() : undefined,
        description: values.description?.trim() || undefined,
        coverAttachmentId: normalizeOptionalAttachmentId(values.coverAttachmentId),
        groupType: values.groupType,
        isEnabled: values.isEnabled,
        sort: values.sort,
      };

      if (mode === 'create') {
        const availability = await checkGroupCode(request.code || '');
        if (!availability.voAvailable) {
          form.setFields([{ name: 'code', errors: [t('stickers.groupForm.codeExists')] }]);
          return;
        }

        await createStickerGroup(request);
        message.success(t('stickers.groupForm.feedback.created'));
      } else if (mode === 'edit' && group) {
        await updateStickerGroup(group.voId, request);
        message.success(t('stickers.groupForm.feedback.updated'));
      }

      onSuccess();
    } catch (error) {
      log.error('StickerGroupForm', '提交表情包分组失败:', error);
      message.error(t(mode === 'create' ? 'stickers.groupForm.feedback.createFailed' : 'stickers.groupForm.feedback.updateFailed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!visible) {
      form.resetFields();
      setCoverPreviewUrl(undefined);
      return;
    }

    if (mode === 'edit' && group) {
      form.setFieldsValue({
        name: group.voName,
        code: group.voCode,
        description: group.voDescription,
        coverAttachmentId: group.voCoverAttachmentId,
        groupType: group.voGroupType,
        isEnabled: group.voIsEnabled,
        sort: group.voSort,
      });
      setCoverPreviewUrl(getAvatarUrl(group.voCoverImageUrl));
      return;
    }

    form.setFieldsValue({
      name: '',
      code: '',
      description: '',
      coverAttachmentId: undefined,
      groupType: 1,
      isEnabled: true,
      sort: 0,
    });
    setCoverPreviewUrl(undefined);
  }, [visible, mode, group, form]);

  return (
    <Modal
      title={t(mode === 'create' ? 'stickers.groupForm.createTitle' : 'stickers.groupForm.editTitle')}
      open={visible}
      onOk={() => {
        void handleSubmit();
      }}
      onCancel={onCancel}
      confirmLoading={loading || coverUploading}
      width={680}
      destroyOnHidden
      forceRender
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="name"
          label={t('stickers.groupForm.name')}
          rules={[
            { required: true, message: t('stickers.groupForm.nameRequired') },
            { max: 100, message: t('stickers.groupForm.nameMax') },
          ]}
        >
          <Input placeholder={t('stickers.groupForm.namePlaceholder')} />
        </Form.Item>

        <Form.Item
          name="code"
          label={t('stickers.groupForm.code')}
          tooltip={t(mode === 'edit' ? 'stickers.groupForm.codeImmutable' : 'stickers.common.codePattern')}
          rules={[
            { required: true, message: t('stickers.groupForm.codeRequired') },
            { max: 100, message: t('stickers.groupForm.codeMax') },
            { pattern: /^[a-z0-9_]+$/, message: t('stickers.common.codePattern') },
          ]}
        >
          <Input
            placeholder={t('stickers.groupForm.codePlaceholder')}
            disabled={mode === 'edit'}
            onBlur={() => {
              void handleCodeBlur();
            }}
            suffix={codeChecking ? t('stickers.common.codeChecking') : undefined}
          />
        </Form.Item>

        <Form.Item
          name="description"
          label={t('stickers.groupForm.description')}
          rules={[{ max: 500, message: t('stickers.groupForm.descriptionMax') }]}
        >
          <Input.TextArea rows={3} maxLength={500} showCount placeholder={t('stickers.groupForm.descriptionPlaceholder')} />
        </Form.Item>

        <Form.Item label={t('stickers.groupForm.cover')}>
          <Form.Item
            name="coverAttachmentId"
            noStyle
            rules={[{ pattern: /^[1-9]\d*$/, message: t('stickers.common.attachmentIdInvalid') }]}
          >
            <Input className="admin-form-hidden-input" />
          </Form.Item>
          <Space orientation="vertical" className="admin-form-field-stack" size={10}>
            <div className="admin-form-upload-preview">
              {coverPreviewUrl ? (
                <img
                  src={coverPreviewUrl}
                  alt={t('stickers.groupForm.coverAlt')}
                  className="admin-form-upload-preview__image"
                />
              ) : (
                <span>{t('stickers.groupForm.noCover')}</span>
              )}
            </div>

            <Space>
              <Upload
                accept={attachmentImageAccept}
                showUploadList={false}
                customRequest={handleCoverUpload}
                disabled={coverUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={coverUploading || loading}>
                  {t(coverUploading ? 'stickers.common.uploading' : 'stickers.groupForm.uploadCover')}
                </Button>
              </Upload>
              <Button
                disabled={!coverAttachmentId || coverUploading || loading}
                onClick={() => {
                  form.setFieldValue('coverAttachmentId', undefined);
                  setCoverPreviewUrl(undefined);
                }}
              >
                {t('stickers.groupForm.clearCover')}
              </Button>
            </Space>

            <Input
              placeholder={t('stickers.common.attachmentIdPlaceholder')}
              value={coverAttachmentId ? String(coverAttachmentId) : ''}
              readOnly
            />
          </Space>
        </Form.Item>

        <Form.Item
          name="groupType"
          label={t('stickers.groupForm.type')}
          rules={[{ required: true, message: t('stickers.groupForm.typeRequired') }]}
        >
          <Select
            options={[
              { value: 1, label: t('stickers.group.type.officialFull') },
              { value: 2, label: t('stickers.group.type.paidFull') },
            ]}
          />
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

        <Form.Item name="isEnabled" label={t('stickers.groupForm.enabled')} valuePropName="checked">
          <Switch checkedChildren={t('stickers.common.enabled')} unCheckedChildren={t('stickers.common.disabled')} />
        </Form.Item>
      </Form>
    </Modal>
  );
};

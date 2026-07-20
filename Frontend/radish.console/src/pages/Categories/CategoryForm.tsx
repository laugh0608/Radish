import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AntInput as Input,
  AntSelect as Select,
  AntModal as Modal,
  Form,
  InputNumber,
  Switch,
  Button,
  Space,
  PlusOutlined,
  message,
  attachmentImageAccept,
  isSupportedAttachmentImageFile,
} from '@radish/ui';
import { Upload } from 'antd';
import type { UploadProps } from 'antd';
import {
  uploadAttachmentImage,
  type ConsoleAttachmentBusinessType,
} from '@/api/attachmentApi';
import {
  createCategory,
  getCategoryPage,
  updateCategory,
  type CategoryUpsertRequest,
  type CategoryVo,
} from '@/api/categoryApi';
import { getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';
import '../adminForm.css';

interface CategoryFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  category?: CategoryVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const CategoryForm = ({ visible, mode, category, onCancel, onSuccess }: CategoryFormProps) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [parentOptions, setParentOptions] = useState<CategoryVo[]>([]);
  const [iconUploading, setIconUploading] = useState(false);
  const [coverUploading, setCoverUploading] = useState(false);
  const [iconPreviewUrl, setIconPreviewUrl] = useState<string | undefined>(undefined);
  const [coverPreviewUrl, setCoverPreviewUrl] = useState<string | undefined>(undefined);
  const iconAttachmentId = Form.useWatch('iconAttachmentId', form);
  const coverAttachmentId = Form.useWatch('coverAttachmentId', form);

  const normalizeOptionalAttachmentId = (value: unknown): string | null | undefined => {
    if (typeof value === 'string' && /^[1-9]\d*$/.test(value.trim())) {
      return value.trim();
    }

    return null;
  };

  const createUploadHandler = (
    businessType: ConsoleAttachmentBusinessType,
    setUploading: (value: boolean) => void,
    setPreview: (value: string | undefined) => void,
    fieldName: 'iconAttachmentId' | 'coverAttachmentId'
  ): UploadProps['customRequest'] => async (options) => {
    const file = options.file;
    if (!(file instanceof File)) {
      options.onError?.(new Error(t('taxonomy.common.invalidFile')));
      return;
    }

    const isImage = isSupportedAttachmentImageFile(file);
    if (!isImage) {
      const error = new Error(t('taxonomy.common.imageOnly'));
      message.error(error.message);
      options.onError?.(error);
      return;
    }

    try {
      setUploading(true);
      const uploaded = await uploadAttachmentImage(file, { businessType }, (percent) => {
        options.onProgress?.({ percent });
      });

      form.setFieldValue(fieldName, uploaded.attachmentId);
      setPreview(getAvatarUrl(uploaded.thumbnailUrl || uploaded.url));
      options.onSuccess?.(uploaded);
      message.success(t('taxonomy.common.imageUploaded'));
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error(t('taxonomy.common.imageUploadFailed'));
      log.error('CategoryForm', '上传分类图片失败:', error);
      message.error(uploadError.message);
      options.onError?.(uploadError);
    } finally {
      setUploading(false);
    }
  };

  useEffect(() => {
    if (!visible) {
      return;
    }

    const loadParentOptions = async () => {
      try {
        const page = await getCategoryPage({ pageIndex: 1, pageSize: 200 });
        setParentOptions(page.data.filter((item) => !category || item.voId !== category.voId));
      } catch (error) {
        log.error('CategoryForm', '加载分类父级选项失败:', error);
      }
    };

    void loadParentOptions();
  }, [category, visible]);

  useEffect(() => {
    if (!visible) {
      form.resetFields();
      setIconPreviewUrl(undefined);
      setCoverPreviewUrl(undefined);
      return;
    }

    if (mode === 'edit' && category) {
      form.setFieldsValue({
        name: category.voName,
        slug: category.voSlug,
        description: category.voDescription,
        iconAttachmentId: category.voIconAttachmentId ?? undefined,
        coverAttachmentId: category.voCoverAttachmentId ?? undefined,
        parentId: category.voParentId ?? undefined,
        orderSort: category.voOrderSort,
        isEnabled: category.voIsEnabled,
      });
      setIconPreviewUrl(getAvatarUrl(category.voIcon));
      setCoverPreviewUrl(getAvatarUrl(category.voCoverImage));
      return;
    }

    form.setFieldsValue({
      name: '',
      slug: '',
      description: '',
      iconAttachmentId: undefined,
      coverAttachmentId: undefined,
      parentId: undefined,
      orderSort: 0,
      isEnabled: true,
    });
    setIconPreviewUrl(undefined);
    setCoverPreviewUrl(undefined);
  }, [visible, mode, category, form]);

  const handleSubmit = async () => {
    if (iconUploading || coverUploading) {
      message.warning(t('taxonomy.common.uploadInProgress'));
      return;
    }

    try {
      const values = await form.validateFields();
      setLoading(true);

      const request: CategoryUpsertRequest = {
        name: values.name,
        slug: values.slug,
        description: values.description,
        iconAttachmentId: normalizeOptionalAttachmentId(values.iconAttachmentId),
        coverAttachmentId: normalizeOptionalAttachmentId(values.coverAttachmentId),
        parentId: values.parentId ?? null,
        orderSort: values.orderSort,
        isEnabled: values.isEnabled,
      };

      if (mode === 'create') {
        await createCategory(request);
        message.success(t('categories.feedback.created'));
      } else if (mode === 'edit' && category) {
        await updateCategory(category.voId, request);
        message.success(t('categories.feedback.updated'));
      }

      onSuccess();
    } catch (error) {
      log.error('CategoryForm', '提交分类表单失败:', error);
      message.error(t(mode === 'create' ? 'categories.feedback.createFailed' : 'categories.feedback.updateFailed'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={t(mode === 'create' ? 'categories.form.createTitle' : 'categories.form.editTitle')}
      open={visible}
      onOk={handleSubmit}
      onCancel={onCancel}
      confirmLoading={loading || iconUploading || coverUploading}
      width={720}
      destroyOnHidden
      forceRender
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="name"
          label={t('categories.form.name')}
          rules={[
            { required: true, message: t('categories.form.nameRequired') },
            { max: 100, message: t('categories.form.nameMax') },
          ]}
        >
          <Input placeholder={t('categories.form.namePlaceholder')} />
        </Form.Item>

        <Form.Item
          name="slug"
          label="Slug"
          rules={[{ max: 100, message: t('categories.form.slugMax') }]}
        >
          <Input placeholder={t('categories.form.slugPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="description"
          label={t('categories.form.description')}
          rules={[{ max: 1000, message: t('categories.form.descriptionMax') }]}
        >
          <Input.TextArea placeholder={t('categories.form.descriptionPlaceholder')} rows={3} maxLength={1000} showCount />
        </Form.Item>

        <Form.Item label={t('categories.form.icon')}>
          <Form.Item
            name="iconAttachmentId"
            noStyle
            rules={[{ pattern: /^[1-9]\d*$/, message: t('taxonomy.common.attachmentIdInvalid') }]}
          >
            <Input className="admin-form-hidden-input" />
          </Form.Item>
          <Space orientation="vertical" className="admin-form-field-stack" size={10}>
            <div className="admin-form-upload-preview">
              {iconPreviewUrl ? (
                <img
                  src={iconPreviewUrl}
                  alt={t('categories.form.iconAlt')}
                  className="admin-form-upload-preview__image"
                />
              ) : (
                <span>{t('categories.form.noIcon')}</span>
              )}
            </div>

            <Space>
              <Upload
                accept={attachmentImageAccept}
                showUploadList={false}
                customRequest={createUploadHandler('CategoryIcon', setIconUploading, setIconPreviewUrl, 'iconAttachmentId')}
                disabled={iconUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={iconUploading || loading}>
                  {t(iconUploading ? 'taxonomy.common.uploading' : 'categories.form.uploadIcon')}
                </Button>
              </Upload>
              <Button
                disabled={!iconAttachmentId || iconUploading || loading}
                onClick={() => {
                  form.setFieldValue('iconAttachmentId', undefined);
                  setIconPreviewUrl(undefined);
                }}
              >
                {t('categories.form.clearIcon')}
              </Button>
            </Space>

            <Input
              placeholder={t('taxonomy.common.attachmentIdPlaceholder')}
              value={iconAttachmentId ? String(iconAttachmentId) : ''}
              readOnly
            />
          </Space>
        </Form.Item>

        <Form.Item label={t('categories.form.cover')}>
          <Form.Item
            name="coverAttachmentId"
            noStyle
            rules={[{ pattern: /^[1-9]\d*$/, message: t('taxonomy.common.attachmentIdInvalid') }]}
          >
            <Input className="admin-form-hidden-input" />
          </Form.Item>
          <Space orientation="vertical" className="admin-form-field-stack" size={10}>
            <div className="admin-form-upload-preview admin-form-upload-preview--wide">
              {coverPreviewUrl ? (
                <img
                  src={coverPreviewUrl}
                  alt={t('categories.form.coverAlt')}
                  className="admin-form-upload-preview__image"
                />
              ) : (
                <span>{t('categories.form.noCover')}</span>
              )}
            </div>

            <Space>
              <Upload
                accept={attachmentImageAccept}
                showUploadList={false}
                customRequest={createUploadHandler('CategoryCover', setCoverUploading, setCoverPreviewUrl, 'coverAttachmentId')}
                disabled={coverUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={coverUploading || loading}>
                  {t(coverUploading ? 'taxonomy.common.uploading' : 'categories.form.uploadCover')}
                </Button>
              </Upload>
              <Button
                disabled={!coverAttachmentId || coverUploading || loading}
                onClick={() => {
                  form.setFieldValue('coverAttachmentId', undefined);
                  setCoverPreviewUrl(undefined);
                }}
              >
                {t('categories.form.clearCover')}
              </Button>
            </Space>

            <Input
              placeholder={t('taxonomy.common.attachmentIdPlaceholder')}
              value={coverAttachmentId ? String(coverAttachmentId) : ''}
              readOnly
            />
          </Space>
        </Form.Item>

        <Form.Item name="parentId" label={t('categories.form.parent')}>
          <Select
            allowClear
            placeholder={t('categories.form.parentPlaceholder')}
            options={parentOptions.map((item) => ({
              label: `${'— '.repeat(item.voLevel)}${item.voName}`,
              value: item.voId,
            }))}
          />
        </Form.Item>

        <Form.Item
          name="orderSort"
          label={t('categories.form.sort')}
          rules={[
            { required: true, message: t('taxonomy.common.sortRequired') },
            { type: 'number', min: 0, message: t('taxonomy.common.sortMin') },
          ]}
        >
          <InputNumber min={0} className="admin-form-control-full" />
        </Form.Item>

        <Form.Item name="isEnabled" label={t('taxonomy.common.enabledField')} valuePropName="checked">
          <Switch checkedChildren={t('taxonomy.common.enabled')} unCheckedChildren={t('taxonomy.common.disabled')} />
        </Form.Item>
      </Form>
    </Modal>
  );
};

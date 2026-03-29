import { useEffect, useState } from 'react';
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
} from '@radish/ui';
import { Upload } from 'antd';
import type { UploadProps } from 'antd';
import { uploadAttachmentImage } from '@/api/attachmentApi';
import {
  createCategory,
  getCategoryPage,
  updateCategory,
  type CategoryUpsertRequest,
  type CategoryVo,
} from '@/api/categoryApi';
import { getAvatarUrl } from '@/config/env';
import { log } from '@/utils/logger';

interface CategoryFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  category?: CategoryVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const CategoryForm = ({ visible, mode, category, onCancel, onSuccess }: CategoryFormProps) => {
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
    businessType: string,
    setUploading: (value: boolean) => void,
    setPreview: (value: string | undefined) => void,
    fieldName: 'iconAttachmentId' | 'coverAttachmentId'
  ): UploadProps['customRequest'] => async (options) => {
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
      message.success('图片上传成功，已回填附件 ID');
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error('图片上传失败');
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
      message.warning('图片仍在上传中，请稍候提交');
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
        message.success('创建分类成功');
      } else if (mode === 'edit' && category) {
        await updateCategory(category.voId, request);
        message.success('更新分类成功');
      }

      onSuccess();
    } catch (error) {
      log.error('CategoryForm', '提交分类表单失败:', error);
      message.error(mode === 'create' ? '创建分类失败' : '更新分类失败');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={mode === 'create' ? '新增分类' : '编辑分类'}
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
          label="分类名称"
          rules={[
            { required: true, message: '请输入分类名称' },
            { max: 100, message: '分类名称不能超过100个字符' },
          ]}
        >
          <Input placeholder="请输入分类名称" />
        </Form.Item>

        <Form.Item
          name="slug"
          label="Slug"
          rules={[{ max: 100, message: 'Slug 不能超过100个字符' }]}
        >
          <Input placeholder="可选，不填则按分类名称自动生成" />
        </Form.Item>

        <Form.Item
          name="description"
          label="描述"
          rules={[{ max: 1000, message: '描述不能超过1000个字符' }]}
        >
          <Input.TextArea placeholder="请输入分类描述" rows={3} maxLength={1000} showCount />
        </Form.Item>

        <Form.Item label="分类图标">
          <Form.Item
            name="iconAttachmentId"
            noStyle
            rules={[{ pattern: /^[1-9]\d*$/, message: '附件 ID 必须为正整数' }]}
          >
            <Input style={{ display: 'none' }} />
          </Form.Item>
          <Space direction="vertical" style={{ width: '100%' }} size={10}>
            <div
              style={{
                width: 96,
                height: 96,
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
              {iconPreviewUrl ? (
                <img
                  src={iconPreviewUrl}
                  alt="分类图标预览"
                  style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                />
              ) : (
                <span>暂无图标</span>
              )}
            </div>

            <Space>
              <Upload
                accept="image/*"
                showUploadList={false}
                customRequest={createUploadHandler('CategoryIcon', setIconUploading, setIconPreviewUrl, 'iconAttachmentId')}
                disabled={iconUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={iconUploading || loading}>
                  {iconUploading ? '上传中...' : '上传图标'}
                </Button>
              </Upload>
              <Button
                disabled={!iconAttachmentId || iconUploading || loading}
                onClick={() => {
                  form.setFieldValue('iconAttachmentId', undefined);
                  setIconPreviewUrl(undefined);
                }}
              >
                清空图标
              </Button>
            </Space>

            <Input
              placeholder="上传后自动回填附件 ID"
              value={iconAttachmentId ? String(iconAttachmentId) : ''}
              readOnly
            />
          </Space>
        </Form.Item>

        <Form.Item label="封面图">
          <Form.Item
            name="coverAttachmentId"
            noStyle
            rules={[{ pattern: /^[1-9]\d*$/, message: '附件 ID 必须为正整数' }]}
          >
            <Input style={{ display: 'none' }} />
          </Form.Item>
          <Space direction="vertical" style={{ width: '100%' }} size={10}>
            <div
              style={{
                width: 160,
                height: 96,
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
              {coverPreviewUrl ? (
                <img
                  src={coverPreviewUrl}
                  alt="分类封面预览"
                  style={{ width: '100%', height: '100%', objectFit: 'cover' }}
                />
              ) : (
                <span>暂无封面</span>
              )}
            </div>

            <Space>
              <Upload
                accept="image/*"
                showUploadList={false}
                customRequest={createUploadHandler('CategoryCover', setCoverUploading, setCoverPreviewUrl, 'coverAttachmentId')}
                disabled={coverUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={coverUploading || loading}>
                  {coverUploading ? '上传中...' : '上传封面'}
                </Button>
              </Upload>
              <Button
                disabled={!coverAttachmentId || coverUploading || loading}
                onClick={() => {
                  form.setFieldValue('coverAttachmentId', undefined);
                  setCoverPreviewUrl(undefined);
                }}
              >
                清空封面
              </Button>
            </Space>

            <Input
              placeholder="上传后自动回填附件 ID"
              value={coverAttachmentId ? String(coverAttachmentId) : ''}
              readOnly
            />
          </Space>
        </Form.Item>

        <Form.Item name="parentId" label="父级分类">
          <Select
            allowClear
            placeholder="留空表示顶级分类"
            options={parentOptions.map((item) => ({
              label: `${'— '.repeat(item.voLevel)}${item.voName}`,
              value: item.voId,
            }))}
          />
        </Form.Item>

        <Form.Item
          name="orderSort"
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

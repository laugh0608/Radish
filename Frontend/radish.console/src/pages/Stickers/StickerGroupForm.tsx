import { useEffect, useState } from 'react';
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

    const isLt5M = file.size / 1024 / 1024 <= 5;
    if (!isLt5M) {
      const error = new Error('封面图大小不能超过 5MB');
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
      message.success('封面图上传成功，已回填附件 ID');
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error('封面图上传失败');
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
    if (coverUploading) {
      message.warning('封面图仍在上传中，请稍候提交');
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
      title={mode === 'create' ? '新增表情包分组' : '编辑表情包分组'}
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
              {coverPreviewUrl ? (
                <img
                  src={coverPreviewUrl}
                  alt="分组封面预览"
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
                customRequest={handleCoverUpload}
                disabled={coverUploading || loading}
              >
                <Button icon={<PlusOutlined />} disabled={coverUploading || loading}>
                  {coverUploading ? '上传中...' : '上传封面图'}
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

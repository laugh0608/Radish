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
import { getApiClientConfig } from '@radish/http';
import { PlusOutlined } from '@radish/ui';
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
  const coverImageUrl = Form.useWatch('coverImageUrl', form) || '';
  const coverPreviewUrl = getAvatarUrl(coverImageUrl);

  const uploadCoverImage = (file: File, onProgress: (percent: number) => void): Promise<string> => {
    return new Promise((resolve, reject) => {
      const config = getApiClientConfig();
      const normalizedBaseUrl = (config.baseUrl || '').trim().replace(/\/$/, '');
      if (!normalizedBaseUrl) {
        reject(new Error('API baseUrl 未配置'));
        return;
      }

      const uploadUrl = `${normalizedBaseUrl}/api/v1/Attachment/UploadImage`;
      const xhr = new XMLHttpRequest();
      xhr.open('POST', uploadUrl, true);

      const token = config.getToken?.();
      if (token) {
        xhr.setRequestHeader('Authorization', `Bearer ${token}`);
      }

      xhr.upload.onprogress = (event) => {
        if (!event.lengthComputable) {
          return;
        }

        const percent = Math.round((event.loaded / event.total) * 100);
        onProgress(Math.min(100, Math.max(0, percent)));
      };

      xhr.onerror = () => {
        reject(new Error('封面图上传失败，请检查网络'));
      };

      xhr.onload = () => {
        if (xhr.status < 200 || xhr.status >= 300) {
          reject(new Error(`封面图上传失败（HTTP ${xhr.status}）`));
          return;
        }

        let payload: Record<string, unknown> | null = null;
        try {
          payload = JSON.parse(xhr.responseText) as Record<string, unknown>;
        } catch {
          reject(new Error('封面图上传响应解析失败'));
          return;
        }

        const isSuccess = Boolean(payload.isSuccess ?? payload.IsSuccess);
        const messageText = String(payload.messageInfo ?? payload.MessageInfo ?? '封面图上传失败');
        if (!isSuccess) {
          reject(new Error(messageText));
          return;
        }

        const responseData = (payload.responseData ?? payload.ResponseData) as Record<string, unknown> | undefined;
        const uploadedUrl = String(responseData?.voUrl ?? responseData?.VoUrl ?? responseData?.url ?? '');
        if (!uploadedUrl) {
          reject(new Error('封面图上传成功但未返回 URL'));
          return;
        }

        resolve(uploadedUrl);
      };

      const formData = new FormData();
      formData.append('file', file);
      formData.append('businessType', 'StickerCover');
      formData.append('generateThumbnail', 'true');
      formData.append('removeExif', 'true');
      xhr.send(formData);
    });
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
      const uploadedUrl = await uploadCoverImage(file, (percent) => {
        options.onProgress?.({ percent });
      });

      form.setFieldValue('coverImageUrl', uploadedUrl);
      options.onSuccess?.({ url: uploadedUrl });
      message.success('封面图上传成功');
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
            name="coverImageUrl"
            noStyle
            rules={[{ max: 500, message: '封面图地址不能超过500个字符' }]}
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
                disabled={!coverImageUrl || coverUploading || loading}
                onClick={() => {
                  form.setFieldValue('coverImageUrl', '');
                }}
              >
                清空封面
              </Button>
            </Space>

            <Input
              placeholder="上传后自动回填封面 URL"
              value={coverImageUrl}
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

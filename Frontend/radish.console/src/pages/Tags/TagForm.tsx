import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AntModal as Modal,
  Form,
  AntInput as Input,
  InputNumber,
  Switch,
  message,
} from '@radish/ui';
import { createTag, updateTag, type TagVo, type TagUpsertRequest } from '@/api/tagApi';
import { log } from '@/utils/logger';

interface TagFormProps {
  visible: boolean;
  mode: 'create' | 'edit';
  tag?: TagVo;
  onCancel: () => void;
  onSuccess: () => void;
}

export const TagForm = ({ visible, mode, tag, onCancel, onSuccess }: TagFormProps) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const request: TagUpsertRequest = {
        name: values.name,
        slug: values.slug,
        description: values.description,
        color: values.color,
        sortOrder: values.sortOrder,
        isEnabled: values.isEnabled,
        isFixed: values.isFixed,
      };

      if (mode === 'create') {
        await createTag(request);
        message.success(t('tags.feedback.created'));
      } else if (mode === 'edit' && tag) {
        await updateTag(tag.voId, request);
        message.success(t('tags.feedback.updated'));
      }

      onSuccess();
    } catch (error) {
      log.error('TagForm', '提交标签表单失败:', error);
      message.error(t(mode === 'create' ? 'tags.feedback.createFailed' : 'tags.feedback.updateFailed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!visible) {
      form.resetFields();
      return;
    }

    if (mode === 'edit' && tag) {
      form.setFieldsValue({
        name: tag.voName,
        slug: tag.voSlug,
        description: tag.voDescription,
        color: tag.voColor,
        sortOrder: tag.voSortOrder,
        isEnabled: tag.voIsEnabled,
        isFixed: tag.voIsFixed,
      });
      return;
    }

    form.setFieldsValue({
      name: '',
      slug: '',
      description: '',
      color: '',
      sortOrder: 0,
      isEnabled: true,
      isFixed: true,
    });
  }, [visible, mode, tag, form]);

  return (
    <Modal
      title={t(mode === 'create' ? 'tags.form.createTitle' : 'tags.form.editTitle')}
      open={visible}
      onOk={handleSubmit}
      onCancel={onCancel}
      confirmLoading={loading}
      width={640}
      destroyOnHidden
      forceRender
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="name"
          label={t('tags.form.name')}
          rules={[
            { required: true, message: t('tags.form.nameRequired') },
            { max: 50, message: t('tags.form.nameMax') },
          ]}
        >
          <Input placeholder={t('tags.form.namePlaceholder')} />
        </Form.Item>

        <Form.Item
          name="slug"
          label="Slug"
          rules={[{ max: 50, message: t('tags.form.slugMax') }]}
        >
          <Input placeholder={t('taxonomy.common.slugOptional')} />
        </Form.Item>

        <Form.Item
          name="description"
          label={t('tags.form.description')}
          rules={[{ max: 500, message: t('tags.form.descriptionMax') }]}
        >
          <Input.TextArea placeholder={t('tags.form.descriptionPlaceholder')} rows={3} maxLength={500} showCount />
        </Form.Item>

        <Form.Item
          name="color"
          label={t('tags.form.color')}
          rules={[{ max: 20, message: t('tags.form.colorMax') }]}
        >
          <Input placeholder={t('tags.form.colorPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="sortOrder"
          label={t('tags.form.sort')}
          rules={[
            { required: true, message: t('taxonomy.common.sortRequired') },
            { type: 'number', min: 0, message: t('taxonomy.common.sortMin') },
          ]}
        >
          <InputNumber min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item name="isFixed" label={t('tags.form.fixed')} valuePropName="checked">
          <Switch checkedChildren={t('tags.type.fixed')} unCheckedChildren={t('tags.type.normal')} />
        </Form.Item>

        <Form.Item name="isEnabled" label={t('taxonomy.common.enabledField')} valuePropName="checked">
          <Switch checkedChildren={t('taxonomy.common.enabled')} unCheckedChildren={t('taxonomy.common.disabled')} />
        </Form.Item>
      </Form>
    </Modal>
  );
};

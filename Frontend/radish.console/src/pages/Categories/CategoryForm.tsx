import { useEffect, useState } from 'react';
import {
  AntInput as Input,
  AntSelect as Select,
  AntModal as Modal,
  Form,
  InputNumber,
  Switch,
  message,
} from '@radish/ui';
import {
  createCategory,
  getCategoryPage,
  updateCategory,
  type CategoryUpsertRequest,
  type CategoryVo,
} from '@/api/categoryApi';
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
      return;
    }

    if (mode === 'edit' && category) {
      form.setFieldsValue({
        name: category.voName,
        slug: category.voSlug,
        description: category.voDescription,
        icon: category.voIcon,
        coverImage: category.voCoverImage,
        parentId: category.voParentId ?? undefined,
        orderSort: category.voOrderSort,
        isEnabled: category.voIsEnabled,
      });
      return;
    }

    form.setFieldsValue({
      name: '',
      slug: '',
      description: '',
      icon: '',
      coverImage: '',
      parentId: undefined,
      orderSort: 0,
      isEnabled: true,
    });
  }, [visible, mode, category, form]);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const request: CategoryUpsertRequest = {
        name: values.name,
        slug: values.slug,
        description: values.description,
        icon: values.icon,
        coverImage: values.coverImage,
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
      confirmLoading={loading}
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

        <Form.Item name="icon" label="图标" rules={[{ max: 200, message: '图标不能超过200个字符' }]}>
          <Input placeholder="可填写 icon class 或图片地址" />
        </Form.Item>

        <Form.Item
          name="coverImage"
          label="封面图"
          rules={[{ max: 500, message: '封面图地址不能超过500个字符' }]}
        >
          <Input placeholder="可填写封面图 URL" />
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

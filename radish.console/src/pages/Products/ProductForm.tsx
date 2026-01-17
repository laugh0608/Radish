import { useEffect, useState } from 'react';
import {
  Modal,
  Form,
  AntInput as Input,
  InputNumber,
  AntSelect as Select,
  Radio,
  DatePicker,
  Switch,
  Button,
  message,
} from '@radish/ui';
import { getCategories, createProduct, updateProduct } from '../../api/shopApi';
import type {
  Product,
  ProductCategory,
  CreateProductDto,
  UpdateProductDto,
} from '../../api/types';
import { log } from '../../utils/logger';
import dayjs from 'dayjs';

interface ProductFormProps {
  visible: boolean;
  product?: Product;
  onClose: () => void;
  onSuccess: () => void;
}

export const ProductForm = ({ visible, product, onClose, onSuccess }: ProductFormProps) => {
  const [form] = Form.useForm<CreateProductDto | UpdateProductDto>();
  const [categories, setCategories] = useState<ProductCategory[]>([]);
  const [loading, setLoading] = useState(false);

  // 监听商品类型变化
  const productType = Form.useWatch('productType', form);
  const stockType = Form.useWatch('stockType', form);
  const durationType = Form.useWatch('durationType', form);

  // 加载分类列表
  useEffect(() => {
    if (visible) {
      loadCategories();
    }
  }, [visible]);

  // 初始化表单数据
  useEffect(() => {
    if (visible && product) {
      form.setFieldsValue({
        ...product,
        expiresAt: product.expiresAt ? dayjs(product.expiresAt) : undefined,
      } as any);
    } else if (visible) {
      form.resetFields();
      // 设置默认值
      form.setFieldsValue({
        productType: 1,
        stockType: 0,
        durationType: 0,
        stock: 0,
        limitPerUser: 0,
        sortOrder: 0,
        isOnSale: false,
      } as any);
    }
  }, [visible, product, form]);

  const loadCategories = async () => {
    try {
      const data = await getCategories();
      setCategories(data);
    } catch (error) {
      log.error('ProductForm', '加载分类列表失败:', error);
    }
  };

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      // 处理日期字段
      const dto = {
        ...values,
        expiresAt: values.expiresAt ? dayjs(values.expiresAt).format('YYYY-MM-DD HH:mm:ss') : undefined,
      };

      if (product) {
        // 更新
        await updateProduct({ ...dto, id: product.id } as UpdateProductDto);
        message.success('更新成功');
      } else {
        // 创建
        await createProduct(dto as CreateProductDto);
        message.success('创建成功');
      }

      onSuccess();
      onClose();
    } catch (error) {
      log.error('ProductForm', '提交失败:', error);
      if (error instanceof Error) {
        message.error(error.message);
      } else {
        message.error('操作失败');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={product ? '编辑商品' : '新建商品'}
      isOpen={visible}
      onClose={onClose}
      size="large"
      footer={
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
          <Button onClick={onClose}>取消</Button>
          <Button variant="primary" onClick={handleSubmit} disabled={loading}>
            {loading ? '保存中...' : '保存'}
          </Button>
        </div>
      }
    >
      <Form
        form={form}
        layout="vertical"
        autoComplete="off"
      >
        <Form.Item
          label="商品名称"
          name="name"
          rules={[{ required: true, message: '请输入商品名称' }]}
        >
          <Input placeholder="请输入商品名称" maxLength={50} />
        </Form.Item>

        <Form.Item
          label="商品描述"
          name="description"
        >
          <Input.TextArea
            placeholder="请输入商品描述"
            rows={3}
            maxLength={500}
          />
        </Form.Item>

        <Form.Item
          label="商品分类"
          name="categoryId"
          rules={[{ required: true, message: '请选择商品分类' }]}
        >
          <Select placeholder="请选择商品分类">
            {categories.map((cat) => (
              <Select.Option key={cat.id} value={cat.id}>
                {cat.name}
              </Select.Option>
            ))}
          </Select>
        </Form.Item>

        <Form.Item
          label="商品类型"
          name="productType"
          rules={[{ required: true, message: '请选择商品类型' }]}
        >
          <Radio.Group>
            <Radio value={1}>权益</Radio>
            <Radio value={2}>消耗品</Radio>
            <Radio value={3}>实物</Radio>
          </Radio.Group>
        </Form.Item>

        {productType === 1 && (
          <Form.Item
            label="权益类型"
            name="benefitType"
            rules={[{ required: true, message: '请选择权益类型' }]}
          >
            <Select placeholder="请选择权益类型">
              <Select.Option value={1}>徽章</Select.Option>
              <Select.Option value={2}>头像框</Select.Option>
              <Select.Option value={3}>称号</Select.Option>
              <Select.Option value={4}>主题</Select.Option>
              <Select.Option value={5}>签名档</Select.Option>
              <Select.Option value={6}>用户名颜色</Select.Option>
              <Select.Option value={7}>点赞特效</Select.Option>
            </Select>
          </Form.Item>
        )}

        {productType === 2 && (
          <Form.Item
            label="消耗品类型"
            name="consumableType"
            rules={[{ required: true, message: '请选择消耗品类型' }]}
          >
            <Select placeholder="请选择消耗品类型">
              <Select.Option value={1}>改名卡</Select.Option>
              <Select.Option value={2}>置顶卡</Select.Option>
              <Select.Option value={3}>高亮卡</Select.Option>
              <Select.Option value={4}>经验卡</Select.Option>
              <Select.Option value={5}>萝卜币红包</Select.Option>
              <Select.Option value={6}>双倍经验卡</Select.Option>
              <Select.Option value={7}>抽奖券</Select.Option>
            </Select>
          </Form.Item>
        )}

        {(productType === 1 || productType === 2) && (
          <Form.Item
            label="权益/消耗品值"
            name="benefitValue"
            tooltip="例如：徽章图标URL、经验卡数值等"
          >
            <Input placeholder="请输入权益/消耗品值" />
          </Form.Item>
        )}

        <Form.Item
          label="商品图标"
          name="icon"
          tooltip="图标URL，例如：mdi:star"
        >
          <Input placeholder="请输入图标URL" />
        </Form.Item>

        <Form.Item
          label="商品封面图"
          name="coverImage"
          tooltip="封面图URL"
        >
          <Input placeholder="请输入封面图URL" />
        </Form.Item>

        <Form.Item label="价格设置">
          <Input.Group compact>
            <Form.Item
              name="price"
              noStyle
              rules={[{ required: true, message: '请输入售价' }]}
            >
              <InputNumber
                placeholder="售价"
                min={0}
                style={{ width: '50%' }}
                addonAfter="胡萝卜"
              />
            </Form.Item>
            <Form.Item
              name="originalPrice"
              noStyle
            >
              <InputNumber
                placeholder="原价（可选）"
                min={0}
                style={{ width: '50%' }}
                addonAfter="胡萝卜"
              />
            </Form.Item>
          </Input.Group>
        </Form.Item>

        <Form.Item
          label="库存类型"
          name="stockType"
          rules={[{ required: true, message: '请选择库存类型' }]}
        >
          <Radio.Group>
            <Radio value={0}>无限库存</Radio>
            <Radio value={1}>限量库存</Radio>
          </Radio.Group>
        </Form.Item>

        {stockType === 1 && (
          <Form.Item
            label="库存数量"
            name="stock"
            rules={[{ required: true, message: '请输入库存数量' }]}
          >
            <InputNumber placeholder="请输入库存数量" min={0} style={{ width: '100%' }} />
          </Form.Item>
        )}

        <Form.Item
          label="每人限购数量"
          name="limitPerUser"
          tooltip="0表示不限购"
        >
          <InputNumber placeholder="每人限购数量" min={0} style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item
          label="有效期类型"
          name="durationType"
          rules={[{ required: true, message: '请选择有效期类型' }]}
        >
          <Radio.Group>
            <Radio value={0}>永久有效</Radio>
            <Radio value={1}>固定天数</Radio>
            <Radio value={2}>固定到期时间</Radio>
          </Radio.Group>
        </Form.Item>

        {durationType === 1 && (
          <Form.Item
            label="有效期天数"
            name="durationDays"
            rules={[{ required: true, message: '请输入有效期天数' }]}
          >
            <InputNumber placeholder="请输入有效期天数" min={1} style={{ width: '100%' }} />
          </Form.Item>
        )}

        {durationType === 2 && (
          <Form.Item
            label="到期时间"
            name="expiresAt"
            rules={[{ required: true, message: '请选择到期时间' }]}
          >
            <DatePicker
              showTime
              format="YYYY-MM-DD HH:mm:ss"
              style={{ width: '100%' }}
            />
          </Form.Item>
        )}

        <Form.Item
          label="排序权重"
          name="sortOrder"
          tooltip="数值越大越靠前"
        >
          <InputNumber placeholder="排序权重" style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item
          label="是否上架"
          name="isOnSale"
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>
      </Form>
    </Modal>
  );
};

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
  Space,
  PlusOutlined,
  message,
} from '@radish/ui';
import { Upload } from 'antd';
import type { UploadProps } from 'antd';
import { getCategories, createProduct, updateProduct } from '../../api/shopApi';
import type {
  Product,
  ProductCategory,
  CreateProductDto,
  UpdateProductDto,
} from '../../api/types';
import { uploadAttachmentImage } from '../../api/attachmentApi';
import { getAvatarUrl } from '../../config/env';
import { log } from '../../utils/logger';
import dayjs from 'dayjs';

interface ProductFormProps {
  visible: boolean;
  product?: Product;
  onClose: () => void;
  onSuccess: () => void;
}

function isUnsupportedSaleSelection(productType?: unknown, benefitType?: unknown, consumableType?: unknown): boolean {
  const normalizedProductType = Number(productType);

  if (normalizedProductType === 1) {
    return [1, 2, 3, 4, 5, 6, 7].includes(Number(benefitType));
  }

  if (normalizedProductType === 2) {
    return [2, 3, 6, 99].includes(Number(consumableType));
  }

  return false;
}

export const ProductForm = ({ visible, product, onClose, onSuccess }: ProductFormProps) => {
  const [form] = Form.useForm<CreateProductDto | UpdateProductDto>();
  const [categories, setCategories] = useState<ProductCategory[]>([]);
  const [loading, setLoading] = useState(false);
  const [iconUploading, setIconUploading] = useState(false);
  const [coverUploading, setCoverUploading] = useState(false);
  const [iconPreviewUrl, setIconPreviewUrl] = useState<string | undefined>(undefined);
  const [coverPreviewUrl, setCoverPreviewUrl] = useState<string | undefined>(undefined);
  const iconAttachmentId = Form.useWatch('iconAttachmentId', form);
  const coverAttachmentId = Form.useWatch('coverAttachmentId', form);

  // 监听商品类型变化
  const productType = Form.useWatch('productType', form);
  const benefitType = Form.useWatch('benefitType', form);
  const consumableType = Form.useWatch('consumableType', form);
  const stockType = Form.useWatch('stockType', form);
  const durationType = Form.useWatch('durationType', form);
  const unsupportedSaleSelection = isUnsupportedSaleSelection(productType, benefitType, consumableType);

  const normalizeEnumNumber = (value: unknown, mapping: Record<string, number>): number | undefined => {
    const normalized = String(value ?? '').trim();
    if (!normalized) {
      return undefined;
    }

    if (/^\d+$/.test(normalized)) {
      return Number.parseInt(normalized, 10);
    }

    return mapping[normalized];
  };

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
      options.onError?.(error);
      message.error(error.message);
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
      log.error('ProductForm', '上传商品图片失败:', error);
      options.onError?.(uploadError);
      message.error(uploadError.message);
    } finally {
      setUploading(false);
    }
  };

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
        name: product.voName,
        description: product.voDescription,
        iconAttachmentId: product.voIconAttachmentId ?? undefined,
        coverAttachmentId: product.voCoverAttachmentId ?? undefined,
        categoryId: product.voCategoryId,
        productType: normalizeEnumNumber(product.voProductType, {
          Benefit: 1,
          Consumable: 2,
          Physical: 99,
        }),
        benefitType: normalizeEnumNumber(product.voBenefitType, {
          Badge: 1,
          AvatarFrame: 2,
          Title: 3,
          Theme: 4,
          Signature: 5,
          NameColor: 6,
          LikeEffect: 7,
        }),
        consumableType: normalizeEnumNumber(product.voConsumableType, {
          RenameCard: 1,
          PostPinCard: 2,
          PostHighlightCard: 3,
          ExpCard: 4,
          CoinCard: 5,
          DoubleExpCard: 6,
          LotteryTicket: 99,
        }),
        price: product.voPrice,
        originalPrice: product.voOriginalPrice,
        stockType: normalizeEnumNumber(product.voStockType, {
          Unlimited: 0,
          Limited: 1,
        }),
        stock: product.voStock,
        limitPerUser: product.voLimitPerUser,
        durationType: normalizeEnumNumber(product.voDurationType, {
          Permanent: 0,
          Days: 1,
          FixedDate: 2,
        }),
        durationDays: product.voDurationDays,
        sortOrder: product.voSortOrder,
        isOnSale: product.voIsOnSale,
        expiresAt: product.voExpiresAt ? dayjs(product.voExpiresAt) : undefined,
      } as any);
      setIconPreviewUrl(getAvatarUrl(product.voIcon));
      setCoverPreviewUrl(getAvatarUrl(product.voCoverImage));
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
      setIconPreviewUrl(undefined);
      setCoverPreviewUrl(undefined);
    }
    if (!visible) {
      setIconPreviewUrl(undefined);
      setCoverPreviewUrl(undefined);
    }
  }, [visible, product, form]);

  useEffect(() => {
    if (!visible || !unsupportedSaleSelection) {
      return;
    }

    if (form.getFieldValue('isOnSale')) {
      form.setFieldValue('isOnSale', false);
    }
  }, [form, unsupportedSaleSelection, visible]);

  const loadCategories = async () => {
    try {
      const data = await getCategories();
      setCategories(data);
    } catch (error) {
      log.error('ProductForm', '加载分类列表失败:', error);
    }
  };

  const handleSubmit = async () => {
    if (iconUploading || coverUploading) {
      message.warning('图片仍在上传中，请稍候提交');
      return;
    }

    try {
      const values = await form.validateFields();
      setLoading(true);

      // 处理日期字段
      const dto = {
        ...values,
        iconAttachmentId: normalizeOptionalAttachmentId(values.iconAttachmentId),
        coverAttachmentId: normalizeOptionalAttachmentId(values.coverAttachmentId),
        expiresAt: values.expiresAt ? dayjs(values.expiresAt).format('YYYY-MM-DD HH:mm:ss') : undefined,
      };

      if (product) {
        // 更新
        await updateProduct({ ...dto, id: product.voId } as UpdateProductDto);
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
          <Button variant="primary" onClick={handleSubmit} disabled={loading || iconUploading || coverUploading}>
            {loading || iconUploading || coverUploading ? '保存中...' : '保存'}
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
              <Select.Option key={cat.voId} value={cat.voId}>
                {cat.voName}
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
            <Radio value={99}>实物</Radio>
          </Radio.Group>
        </Form.Item>

        {productType === 1 && (
          <>
            <Form.Item
              label="权益类型"
              name="benefitType"
              rules={[{ required: true, message: '请选择权益类型' }]}
            >
              <Select placeholder="请选择权益类型">
                <Select.Option value={1} disabled>徽章（暂未开放）</Select.Option>
                <Select.Option value={2} disabled>头像框（暂未开放）</Select.Option>
                <Select.Option value={3} disabled>称号（暂未开放）</Select.Option>
                <Select.Option value={4} disabled>主题（暂未开放）</Select.Option>
                <Select.Option value={5} disabled>签名档（暂未开放）</Select.Option>
                <Select.Option value={6} disabled>用户名颜色（暂未开放）</Select.Option>
                <Select.Option value={7} disabled>点赞特效（暂未开放）</Select.Option>
              </Select>
            </Form.Item>
            <div style={{ marginTop: -12, marginBottom: 16, color: 'var(--theme-text-placeholder)', fontSize: 12 }}>
              当前权益效果尚未接入真实消费链路，不能上架销售。
            </div>
          </>
        )}

        {productType === 2 && (
          <>
            <Form.Item
              label="消耗品类型"
              name="consumableType"
              rules={[{ required: true, message: '请选择消耗品类型' }]}
            >
              <Select placeholder="请选择消耗品类型">
                <Select.Option value={1}>改名卡</Select.Option>
                <Select.Option value={2} disabled>置顶卡（暂未开放）</Select.Option>
                <Select.Option value={3} disabled>高亮卡（暂未开放）</Select.Option>
                <Select.Option value={4}>经验卡</Select.Option>
                <Select.Option value={5}>萝卜币红包</Select.Option>
                <Select.Option value={6} disabled>双倍经验卡（暂未开放）</Select.Option>
                <Select.Option value={99} disabled>抽奖券（暂未开放）</Select.Option>
              </Select>
            </Form.Item>
            <div style={{ marginTop: -12, marginBottom: 16, color: 'var(--theme-text-placeholder)', fontSize: 12 }}>
              帖子置顶卡、帖子高亮卡、双倍经验卡、抽奖券当前未开放，不能上架销售。
            </div>
          </>
        )}

        {(productType === 1 || productType === 2) && (
          <Form.Item
            label="权益/消耗品值"
            name="benefitValue"
            tooltip="例如：徽章资源标识、经验卡数值等"
          >
            <Input placeholder="请输入权益/消耗品值" />
          </Form.Item>
        )}

        <Form.Item
          label="商品图标"
        >
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
                  alt="商品图标预览"
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
                customRequest={createUploadHandler('ProductIcon', setIconUploading, setIconPreviewUrl, 'iconAttachmentId')}
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

        <Form.Item
          label="商品封面图"
        >
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
                  alt="商品封面预览"
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
                customRequest={createUploadHandler('ProductCover', setCoverUploading, setCoverPreviewUrl, 'coverAttachmentId')}
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
          <Switch disabled={unsupportedSaleSelection} />
        </Form.Item>

        {unsupportedSaleSelection && (
          <div style={{ marginTop: -12, marginBottom: 16, color: 'var(--theme-text-placeholder)', fontSize: 12 }}>
            当前商品类型未开放，保存时将保持下架状态。
          </div>
        )}
      </Form>
    </Modal>
  );
};

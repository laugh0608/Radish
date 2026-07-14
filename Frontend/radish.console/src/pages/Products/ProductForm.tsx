import { useEffect, useState } from 'react';
import {
  Modal,
  AntModal,
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
import { getCategories, getProductCapabilities, createProduct, updateProduct } from '../../api/shopApi';
import type {
  Product,
  ProductCategory,
  ShopProductCapability,
  CreateProductDto,
  UpdateProductDto,
} from '../../api/types';
import { uploadAttachmentImage } from '../../api/attachmentApi';
import { getAvatarUrl } from '../../config/env';
import { log } from '../../utils/logger';
import dayjs, { type Dayjs } from 'dayjs';
import '../adminForm.css';

interface ProductFormProps {
  visible: boolean;
  product?: Product;
  onClose: () => void;
  onSuccess: () => void;
}

type ProductFormValues = Omit<CreateProductDto, 'expiresAt'> & {
  id?: string;
  expiresAt?: Dayjs;
};

function normalizeCapabilityEnum(value: unknown, mapping: Record<string, number>): number | undefined {
  const normalized = String(value ?? '').trim();
  if (!normalized) return undefined;
  if (/^\d+$/.test(normalized)) return Number.parseInt(normalized, 10);
  return mapping[normalized];
}

function findProductCapability(
  capabilities: ShopProductCapability[],
  productType?: unknown,
  benefitType?: unknown,
  consumableType?: unknown
): ShopProductCapability | undefined {
  const normalizedProductType = Number(productType);
  const normalizedBenefitType = Number(benefitType);
  const normalizedConsumableType = Number(consumableType);
  return capabilities.find((capability) => {
    const capabilityProductType = normalizeCapabilityEnum(capability.voProductType, {
      Benefit: 1,
      Consumable: 2,
      Physical: 99,
    });
    const capabilityBenefitType = normalizeCapabilityEnum(capability.voBenefitType, {
      Badge: 1,
      AvatarFrame: 2,
      Title: 3,
      Theme: 4,
      Signature: 5,
      NameColor: 6,
      LikeEffect: 7,
    });
    const capabilityConsumableType = normalizeCapabilityEnum(capability.voConsumableType, {
      RenameCard: 1,
      PostPinCard: 2,
      PostHighlightCard: 3,
      ExpCard: 4,
      CoinCard: 5,
      DoubleExpCard: 6,
      LotteryTicket: 99,
    });

    return capabilityProductType === normalizedProductType
      && (normalizedProductType !== 1 || capabilityBenefitType === normalizedBenefitType)
      && (normalizedProductType !== 2 || capabilityConsumableType === normalizedConsumableType);
  });
}

const benefitCategoryMap: Record<number, string> = {
  1: 'badge',
  2: 'frame',
  3: 'title',
  4: 'theme',
  5: 'signature',
  6: 'effect',
  7: 'effect',
};

const consumableCategoryMap: Record<number, string> = {
  1: 'effect',
  2: 'effect',
  3: 'effect',
  4: 'effect',
  5: 'effect',
  6: 'effect',
  99: 'effect',
};

const benefitCategoryIds = ['badge', 'frame', 'title', 'signature', 'effect', 'theme'];

interface BenefitValueFieldMeta {
  label: string;
  placeholder: string;
  tooltip?: string;
  help?: string;
  required?: boolean;
  positiveInteger?: boolean;
}

function getBenefitTypeDisplayName(benefitType?: unknown): string {
  switch (Number(benefitType)) {
    case 1:
      return '徽章';
    case 2:
      return '头像框';
    case 3:
      return '称号';
    case 4:
      return '主题';
    case 5:
      return '签名档';
    case 6:
      return '用户名颜色';
    case 7:
      return '点赞特效';
    default:
      return '权益';
  }
}

function resolveRecommendedCategoryId(productType?: unknown, benefitType?: unknown, consumableType?: unknown): string | undefined {
  const normalizedProductType = Number(productType);

  if (normalizedProductType === 1) {
    return benefitCategoryMap[Number(benefitType)];
  }

  if (normalizedProductType === 2) {
    return consumableCategoryMap[Number(consumableType)] ?? 'effect';
  }

  return undefined;
}

function getAllowedCategoryIds(productType?: unknown, benefitType?: unknown): string[] | undefined {
  const normalizedProductType = Number(productType);

  if (normalizedProductType === 1) {
    const mappedCategoryId = benefitCategoryMap[Number(benefitType)];
    return mappedCategoryId ? [mappedCategoryId] : benefitCategoryIds;
  }

  if (normalizedProductType === 2) {
    return ['effect'];
  }

  return undefined;
}

function getBenefitValueFieldMeta(productType?: unknown, benefitType?: unknown, consumableType?: unknown): BenefitValueFieldMeta | null {
  const normalizedProductType = Number(productType);

  if (normalizedProductType === 1) {
    const benefitTypeName = getBenefitTypeDisplayName(benefitType);
    return {
      label: `${benefitTypeName}资源值`,
      placeholder: `请输入${benefitTypeName}资源标识或展示值`,
      tooltip: '用于权益发放和展示，例如 badge-veteran、theme-sakura。',
      help: '权益类商品需要可被客户端或运行时识别的资源值。',
      required: true,
    };
  }

  if (normalizedProductType !== 2) {
    return null;
  }

  switch (Number(consumableType)) {
    case 4:
      return {
        label: '经验值',
        placeholder: '请输入经验值，例如 100',
        tooltip: '购买后发放到背包，使用时按这里的数值增加经验。',
        help: '仅支持正整数。',
        required: true,
        positiveInteger: true,
      };
    case 5:
      return {
        label: '红包面额',
        placeholder: '请输入胡萝卜数量，例如 50',
        tooltip: '购买后发放到背包，使用时按这里的数值发放胡萝卜。',
        help: '仅支持正整数。',
        required: true,
        positiveInteger: true,
      };
    case 1:
      return null;
    default:
      return {
        label: '道具配置值',
        placeholder: '如有需要请输入道具配置值',
        tooltip: '当前道具类型如需额外数值配置，可在这里填写。',
      };
  }
}

function normalizeBenefitValue(value: unknown): string | undefined {
  if (typeof value !== 'string') {
    return undefined;
  }

  const normalized = value.trim();
  return normalized ? normalized : undefined;
}

export const ProductForm = ({ visible, product, onClose, onSuccess }: ProductFormProps) => {
  const [form] = Form.useForm<ProductFormValues>();
  const [categories, setCategories] = useState<ProductCategory[]>([]);
  const [capabilities, setCapabilities] = useState<ShopProductCapability[]>([]);
  const [loading, setLoading] = useState(false);
  const [iconUploading, setIconUploading] = useState(false);
  const [coverUploading, setCoverUploading] = useState(false);
  const [iconPreviewUrl, setIconPreviewUrl] = useState<string | undefined>(undefined);
  const [coverPreviewUrl, setCoverPreviewUrl] = useState<string | undefined>(undefined);
  const [isDirty, setIsDirty] = useState(false);
  const iconAttachmentId = Form.useWatch('iconAttachmentId', form);
  const coverAttachmentId = Form.useWatch('coverAttachmentId', form);

  // 监听商品类型变化
  const categoryId = Form.useWatch('categoryId', form);
  const productType = Form.useWatch('productType', form);
  const benefitType = Form.useWatch('benefitType', form);
  const consumableType = Form.useWatch('consumableType', form);
  const stockType = Form.useWatch('stockType', form);
  const durationType = Form.useWatch('durationType', form);
  const selectedCapability = findProductCapability(capabilities, productType, benefitType, consumableType);
  const unsupportedSaleSelection = selectedCapability?.voCanSell !== true;
  const recommendedCategoryId = resolveRecommendedCategoryId(productType, benefitType, consumableType);
  const allowedCategoryIds = getAllowedCategoryIds(productType, benefitType);
  const filteredCategories = allowedCategoryIds
    ? categories.filter((cat) => allowedCategoryIds.includes(cat.voId))
    : categories;
  const categoryOptions = filteredCategories.length > 0 ? filteredCategories : categories;
  const isCategoryLocked = Boolean(recommendedCategoryId) && categoryOptions.length === 1;
  const recommendedCategoryName = categories.find((cat) => cat.voId === recommendedCategoryId)?.voName;
  const benefitValueFieldMeta = getBenefitValueFieldMeta(productType, benefitType, consumableType);
  const benefitValueRules = benefitValueFieldMeta
    ? [
        ...(benefitValueFieldMeta.required
          ? [{ required: true, whitespace: true, message: `请输入${benefitValueFieldMeta.label}` }]
          : []),
        ...(benefitValueFieldMeta.positiveInteger
          ? [{
              validator: (_: unknown, value: unknown) => {
                if (value === undefined || value === null || String(value).trim() === '') {
                  return Promise.resolve();
                }

                return /^[1-9]\d*$/.test(String(value).trim())
                  ? Promise.resolve()
                  : Promise.reject(new Error(`${benefitValueFieldMeta.label}必须为正整数`));
              }
            }]
          : [])
      ]
    : [];

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

  const handleRequestClose = () => {
    if (loading || iconUploading || coverUploading) {
      message.warning('当前仍有保存或上传任务，请稍候');
      return;
    }

    if (!isDirty) {
      onClose();
      return;
    }

    AntModal.confirm({
      title: '放弃未保存的商品资料？',
      content: '当前表单内容尚未保存，关闭后会丢失已填写的资料。',
      okText: '放弃并关闭',
      cancelText: '继续编辑',
      okButtonProps: { danger: true },
      onOk: () => {
        setIsDirty(false);
        onClose();
      },
    });
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
      setIsDirty(true);
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
      loadFormMetadata();
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
        benefitValue: product.voBenefitValue ?? undefined,
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
      });
      setIconPreviewUrl(getAvatarUrl(product.voIcon));
      setCoverPreviewUrl(getAvatarUrl(product.voCoverImage));
      setIsDirty(false);
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
      });
      setIconPreviewUrl(undefined);
      setCoverPreviewUrl(undefined);
      setIsDirty(false);
    }
    if (!visible) {
      setIconPreviewUrl(undefined);
      setCoverPreviewUrl(undefined);
      setIsDirty(false);
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

  useEffect(() => {
    if (!visible) {
      return;
    }

    const normalizedProductType = Number(productType);

    if (normalizedProductType === 1 && consumableType !== undefined) {
      form.setFieldValue('consumableType', undefined);
      return;
    }

    if (normalizedProductType === 2 && benefitType !== undefined) {
      form.setFieldValue('benefitType', undefined);
      return;
    }

    if (normalizedProductType === 99) {
      if (benefitType !== undefined) {
        form.setFieldValue('benefitType', undefined);
      }

      if (consumableType !== undefined) {
        form.setFieldValue('consumableType', undefined);
      }
    }
  }, [benefitType, consumableType, form, productType, visible]);

  useEffect(() => {
    if (!visible || !recommendedCategoryId || categoryId === recommendedCategoryId) {
      return;
    }

    form.setFieldValue('categoryId', recommendedCategoryId);
  }, [categoryId, form, recommendedCategoryId, visible]);

  const loadFormMetadata = async () => {
    try {
      const [categoryData, capabilityData] = await Promise.all([
        getCategories(),
        getProductCapabilities(),
      ]);
      setCategories(categoryData);
      setCapabilities(capabilityData);
    } catch (error) {
      log.error('ProductForm', '加载商品表单元数据失败:', error);
      setCapabilities([]);
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

      const normalizedProductType = Number(values.productType);
      const normalizedConsumableType = Number(values.consumableType);
      const normalizedDurationType = Number(values.durationType);
      const normalizedStockType = Number(values.stockType);
      const normalizedBenefitValue = normalizeBenefitValue(values.benefitValue);
      const normalizedCategoryId =
        resolveRecommendedCategoryId(values.productType, values.benefitType, values.consumableType)
        ?? values.categoryId;

      if ((normalizedConsumableType === 4 || normalizedConsumableType === 5) && !normalizedBenefitValue) {
        message.error(`${normalizedConsumableType === 4 ? '经验卡' : '萝卜币红包'}必须配置正整数数值`);
        return;
      }

      // 处理日期字段
      const dto = {
        ...values,
        categoryId: normalizedCategoryId,
        benefitType: normalizedProductType === 1 ? values.benefitType : undefined,
        consumableType: normalizedProductType === 2 ? values.consumableType : undefined,
        benefitValue: normalizedProductType === 2 && normalizedConsumableType === 1
          ? undefined
          : normalizedBenefitValue,
        iconAttachmentId: normalizeOptionalAttachmentId(values.iconAttachmentId),
        coverAttachmentId: normalizeOptionalAttachmentId(values.coverAttachmentId),
        stock: normalizedStockType === 1 ? values.stock : 0,
        durationDays: normalizedDurationType === 1 ? values.durationDays : undefined,
        expiresAt: normalizedDurationType === 2 && values.expiresAt
          ? dayjs(values.expiresAt).toDate().toISOString()
          : undefined,
      };

      if (product) {
        // 更新
        await updateProduct({ ...dto, id: product.voId, expectedVersion: product.voVersion } as UpdateProductDto);
        message.success('更新成功');
      } else {
        // 创建
        await createProduct(dto as CreateProductDto);
        message.success('创建成功');
      }

      setIsDirty(false);
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
      onClose={handleRequestClose}
      size="large"
      closeOnOverlayClick={false}
      closeOnEscape={false}
      footer={
        <div className="admin-form-modal-actions">
          <Button onClick={handleRequestClose}>取消</Button>
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
        onValuesChange={() => setIsDirty(true)}
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
          <Select
            placeholder={isCategoryLocked ? '分类将按当前商品类型自动匹配' : '请选择商品分类'}
            disabled={isCategoryLocked}
          >
            {categoryOptions.map((cat) => (
              <Select.Option key={cat.voId} value={cat.voId}>
                {cat.voName}
              </Select.Option>
            ))}
          </Select>
        </Form.Item>
        {recommendedCategoryName && (
          <div className="admin-form-help-text">
            当前商品会归入「{recommendedCategoryName}」分类，保存时将自动保持一致。
          </div>
        )}

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
                <Select.Option value={1} disabled={findProductCapability(capabilities, 1, 1)?.voCanSell !== true}>徽章</Select.Option>
                <Select.Option value={2} disabled={findProductCapability(capabilities, 1, 2)?.voCanSell !== true}>头像框</Select.Option>
                <Select.Option value={3} disabled={findProductCapability(capabilities, 1, 3)?.voCanSell !== true}>称号</Select.Option>
                <Select.Option value={4} disabled={findProductCapability(capabilities, 1, 4)?.voCanSell !== true}>主题</Select.Option>
                <Select.Option value={5} disabled={findProductCapability(capabilities, 1, 5)?.voCanSell !== true}>签名档</Select.Option>
                <Select.Option value={6} disabled={findProductCapability(capabilities, 1, 6)?.voCanSell !== true}>用户名颜色</Select.Option>
                <Select.Option value={7} disabled={findProductCapability(capabilities, 1, 7)?.voCanSell !== true}>点赞特效</Select.Option>
              </Select>
            </Form.Item>
            <div className="admin-form-help-text">
              {selectedCapability?.voUnavailableReason
                || selectedCapability?.voConfigurationRequirements.join('；')
                || '请选择服务端已开放的权益类型。'}
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
                <Select.Option value={1} disabled={findProductCapability(capabilities, 2, undefined, 1)?.voCanSell !== true}>改名卡</Select.Option>
                <Select.Option value={2} disabled={findProductCapability(capabilities, 2, undefined, 2)?.voCanSell !== true}>置顶卡</Select.Option>
                <Select.Option value={3} disabled={findProductCapability(capabilities, 2, undefined, 3)?.voCanSell !== true}>高亮卡</Select.Option>
                <Select.Option value={4} disabled={findProductCapability(capabilities, 2, undefined, 4)?.voCanSell !== true}>经验卡</Select.Option>
                <Select.Option value={5} disabled={findProductCapability(capabilities, 2, undefined, 5)?.voCanSell !== true}>萝卜币红包</Select.Option>
                <Select.Option value={6} disabled={findProductCapability(capabilities, 2, undefined, 6)?.voCanSell !== true}>双倍经验卡</Select.Option>
                <Select.Option value={99} disabled={findProductCapability(capabilities, 2, undefined, 99)?.voCanSell !== true}>抽奖券</Select.Option>
              </Select>
            </Form.Item>
            <div className="admin-form-help-text">
              {selectedCapability?.voUnavailableReason
                || selectedCapability?.voConfigurationRequirements.join('；')
                || '请选择服务端已开放的消耗品类型。'}
            </div>
          </>
        )}

        {benefitValueFieldMeta && (
          <Form.Item
            label={benefitValueFieldMeta.label}
            name="benefitValue"
            tooltip={benefitValueFieldMeta.tooltip}
            rules={benefitValueRules}
          >
            <Input placeholder={benefitValueFieldMeta.placeholder} />
          </Form.Item>
        )}
        {benefitValueFieldMeta?.help && (
          <div className="admin-form-help-text">
            {benefitValueFieldMeta.help}
          </div>
        )}

        <Form.Item
          label="商品图标"
        >
          <Form.Item
            name="iconAttachmentId"
            noStyle
            rules={[{ pattern: /^[1-9]\d*$/, message: '附件 ID 必须为正整数' }]}
          >
            <Input className="admin-form-hidden-input" />
          </Form.Item>
          <Space orientation="vertical" className="admin-form-field-stack" size={10}>
            <div className="admin-form-upload-preview">
              {iconPreviewUrl ? (
                <img
                  src={iconPreviewUrl}
                  alt="商品图标预览"
                  className="admin-form-upload-preview__image"
                />
              ) : (
                <span>暂无图标</span>
              )}
            </div>

            <Space wrap>
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
                  setIsDirty(true);
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
            <Input className="admin-form-hidden-input" />
          </Form.Item>
          <Space orientation="vertical" className="admin-form-field-stack" size={10}>
            <div className="admin-form-upload-preview admin-form-upload-preview--wide">
              {coverPreviewUrl ? (
                <img
                  src={coverPreviewUrl}
                  alt="商品封面预览"
                  className="admin-form-upload-preview__image"
                />
              ) : (
                <span>暂无封面</span>
              )}
            </div>

            <Space wrap>
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
                  setIsDirty(true);
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
                className="admin-form-control-half"
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
                className="admin-form-control-half"
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
            <InputNumber placeholder="请输入库存数量" min={0} className="admin-form-control-full" />
          </Form.Item>
        )}

        <Form.Item
          label="每人限购数量"
          name="limitPerUser"
          tooltip="0表示不限购"
        >
          <InputNumber placeholder="每人限购数量" min={0} className="admin-form-control-full" />
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
            <InputNumber placeholder="请输入有效期天数" min={1} className="admin-form-control-full" />
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
              className="admin-form-control-full"
            />
          </Form.Item>
        )}

        <Form.Item
          label="排序权重"
          name="sortOrder"
          tooltip="数值越大越靠前"
        >
          <InputNumber placeholder="排序权重" className="admin-form-control-full" />
        </Form.Item>

        <Form.Item
          label="是否上架"
          name="isOnSale"
          valuePropName="checked"
        >
          <Switch disabled={unsupportedSaleSelection} />
        </Form.Item>

        {unsupportedSaleSelection && (
          <div className="admin-form-help-text">
            当前商品类型未开放，保存时将保持下架状态。
          </div>
        )}
      </Form>
    </Modal>
  );
};

import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
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
import {
  findProductCapability,
  getBenefitTypeDisplay,
  getCapabilityDescription,
  normalizeEnumNumber,
} from './productDisplay';
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

function getBenefitValueFieldMeta(
  productType: unknown,
  benefitType: unknown,
  consumableType: unknown,
  t: TFunction,
): BenefitValueFieldMeta | null {
  const normalizedProductType = Number(productType);

  if (normalizedProductType === 1) {
    const normalizedBenefitType = Number(benefitType);
    const benefitTypeName = Number.isFinite(normalizedBenefitType) && normalizedBenefitType > 0
      ? getBenefitTypeDisplay(benefitType as string | number | null | undefined, t)
      : t('products.type.benefit');
    return {
      label: t('products.form.field.benefitValueLabel', { type: benefitTypeName }),
      placeholder: t('products.form.field.benefitValuePlaceholder', { type: benefitTypeName }),
      tooltip: t('products.form.field.benefitValueTooltip'),
      help: t('products.form.field.benefitValueHelp'),
      required: true,
    };
  }

  if (normalizedProductType !== 2) {
    return null;
  }

  switch (Number(consumableType)) {
    case 4:
      return {
        label: t('products.form.field.expValue'),
        placeholder: t('products.form.field.expValuePlaceholder'),
        tooltip: t('products.form.field.expValueTooltip'),
        help: t('products.form.field.positiveIntegerHelp'),
        required: true,
        positiveInteger: true,
      };
    case 5:
      return {
        label: t('products.form.field.coinValue'),
        placeholder: t('products.form.field.coinValuePlaceholder'),
        tooltip: t('products.form.field.coinValueTooltip'),
        help: t('products.form.field.positiveIntegerHelp'),
        required: true,
        positiveInteger: true,
      };
    case 1:
      return null;
    default:
      return {
        label: t('products.form.field.itemValue'),
        placeholder: t('products.form.field.itemValuePlaceholder'),
        tooltip: t('products.form.field.itemValueTooltip'),
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
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
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
  const benefitValueFieldMeta = getBenefitValueFieldMeta(productType, benefitType, consumableType, t);
  const benefitValueRules = benefitValueFieldMeta
    ? [
        ...(benefitValueFieldMeta.required
          ? [{ required: true, whitespace: true, message: t('products.form.validation.required', { field: benefitValueFieldMeta.label }) }]
          : []),
        ...(benefitValueFieldMeta.positiveInteger
          ? [{
              validator: (_: unknown, value: unknown) => {
                if (value === undefined || value === null || String(value).trim() === '') {
                  return Promise.resolve();
                }

                return /^[1-9]\d*$/.test(String(value).trim())
                  ? Promise.resolve()
                  : Promise.reject(new Error(t('products.form.validation.positiveInteger', { field: benefitValueFieldMeta.label })));
              }
            }]
          : [])
      ]
    : [];

  const normalizeOptionalAttachmentId = (value: unknown): string | null | undefined => {
    if (typeof value === 'string' && /^[1-9]\d*$/.test(value.trim())) {
      return value.trim();
    }

    return null;
  };

  const handleRequestClose = () => {
    if (loading || iconUploading || coverUploading) {
      message.warning(t('products.form.closeBusy'));
      return;
    }

    if (!isDirty) {
      onClose();
      return;
    }

    AntModal.confirm({
      title: t('products.form.discardTitle'),
      content: t('products.form.discardContent'),
      okText: t('products.form.discardConfirm'),
      cancelText: t('products.form.continueEditing'),
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
      options.onError?.(new Error(t('products.form.upload.invalidFile')));
      return;
    }

    const isImage = file.type
      ? file.type.startsWith('image/')
      : /\.(jpe?g|png|gif|webp|bmp|svg)$/i.test(file.name);
    if (!isImage) {
      const error = new Error(t('products.form.upload.imageOnly'));
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
      message.success(t('products.form.upload.success'));
    } catch (error) {
      const uploadError = error instanceof Error ? error : new Error(t('products.form.upload.failed'));
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
    // Metadata follows the current language so service fallbacks honor Accept-Language.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [language, visible]);

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
        getCategories(t),
        getProductCapabilities(t),
      ]);
      setCategories(categoryData);
      setCapabilities(capabilityData);
    } catch (error) {
      log.error('ProductForm', '加载商品表单元数据失败:', error);
      setCapabilities([]);
      message.error(error instanceof Error ? error.message : t('products.form.metadataLoadFailed'));
    }
  };

  const handleSubmit = async () => {
    if (iconUploading || coverUploading) {
      message.warning(t('products.form.uploadInProgress'));
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
        message.error(t('products.form.validation.consumableValue', {
          type: normalizedConsumableType === 4
            ? t('products.consumableType.expCard')
            : t('products.consumableType.coinCard'),
        }));
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
        await updateProduct(
          { ...dto, id: product.voId, expectedVersion: product.voVersion } as UpdateProductDto,
          t,
        );
        message.success(t('products.form.updateSuccess'));
      } else {
        // 创建
        await createProduct(dto as CreateProductDto, t);
        message.success(t('products.form.createSuccess'));
      }

      setIsDirty(false);
      onSuccess();
      onClose();
    } catch (error) {
      log.error('ProductForm', '提交失败:', error);
      if (error instanceof Error) {
        message.error(error.message);
      } else {
        message.error(t('products.form.submitFailed'));
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={product ? t('products.form.editTitle') : t('products.form.createTitle')}
      isOpen={visible}
      onClose={handleRequestClose}
      size="large"
      closeOnOverlayClick={false}
      closeOnEscape={false}
      footer={
        <div className="admin-form-modal-actions">
          <Button onClick={handleRequestClose}>{t('products.form.cancel')}</Button>
          <Button variant="primary" onClick={handleSubmit} disabled={loading || iconUploading || coverUploading}>
            {loading || iconUploading || coverUploading
              ? t('products.form.saving')
              : t('products.form.save')}
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
          label={t('products.form.field.name')}
          name="name"
          rules={[{ required: true, message: t('products.form.field.namePlaceholder') }]}
        >
          <Input placeholder={t('products.form.field.namePlaceholder')} maxLength={50} />
        </Form.Item>

        <Form.Item
          label={t('products.form.field.description')}
          name="description"
        >
          <Input.TextArea
            placeholder={t('products.form.field.descriptionPlaceholder')}
            rows={3}
            maxLength={500}
          />
        </Form.Item>

        <Form.Item
          label={t('products.form.field.category')}
          name="categoryId"
          rules={[{ required: true, message: t('products.form.field.categoryPlaceholder') }]}
        >
          <Select
            placeholder={isCategoryLocked
              ? t('products.form.field.categoryLocked')
              : t('products.form.field.categoryPlaceholder')}
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
            {t('products.form.field.categoryHelp', { name: recommendedCategoryName })}
          </div>
        )}

        <Form.Item
          label={t('products.form.field.productType')}
          name="productType"
          rules={[{ required: true, message: t('products.form.validation.selectRequired', { field: t('products.form.field.productType') }) }]}
        >
          <Radio.Group>
            <Radio value={1}>{t('products.type.benefit')}</Radio>
            <Radio value={2}>{t('products.type.consumable')}</Radio>
            <Radio value={99}>{t('products.type.physical')}</Radio>
          </Radio.Group>
        </Form.Item>

        {productType === 1 && (
          <>
            <Form.Item
              label={t('products.form.field.benefitType')}
              name="benefitType"
              rules={[{ required: true, message: t('products.form.field.benefitTypePlaceholder') }]}
            >
              <Select placeholder={t('products.form.field.benefitTypePlaceholder')}>
                <Select.Option value={1} disabled={findProductCapability(capabilities, 1, 1)?.voCanSell !== true}>{t('products.benefitType.badge')}</Select.Option>
                <Select.Option value={2} disabled={findProductCapability(capabilities, 1, 2)?.voCanSell !== true}>{t('products.benefitType.avatarFrame')}</Select.Option>
                <Select.Option value={3} disabled={findProductCapability(capabilities, 1, 3)?.voCanSell !== true}>{t('products.benefitType.title')}</Select.Option>
                <Select.Option value={4} disabled={findProductCapability(capabilities, 1, 4)?.voCanSell !== true}>{t('products.benefitType.theme')}</Select.Option>
                <Select.Option value={5} disabled={findProductCapability(capabilities, 1, 5)?.voCanSell !== true}>{t('products.benefitType.signature')}</Select.Option>
                <Select.Option value={6} disabled={findProductCapability(capabilities, 1, 6)?.voCanSell !== true}>{t('products.benefitType.nameColor')}</Select.Option>
                <Select.Option value={7} disabled={findProductCapability(capabilities, 1, 7)?.voCanSell !== true}>{t('products.benefitType.likeEffect')}</Select.Option>
              </Select>
            </Form.Item>
            <div className="admin-form-help-text">
              {selectedCapability
                ? getCapabilityDescription(selectedCapability, t)
                : t('products.form.field.benefitCapabilityHint')}
            </div>
          </>
        )}

        {productType === 2 && (
          <>
            <Form.Item
              label={t('products.form.field.consumableType')}
              name="consumableType"
              rules={[{ required: true, message: t('products.form.field.consumableTypePlaceholder') }]}
            >
              <Select placeholder={t('products.form.field.consumableTypePlaceholder')}>
                <Select.Option value={1} disabled={findProductCapability(capabilities, 2, undefined, 1)?.voCanSell !== true}>{t('products.consumableType.renameCard')}</Select.Option>
                <Select.Option value={2} disabled={findProductCapability(capabilities, 2, undefined, 2)?.voCanSell !== true}>{t('products.consumableType.postPinCard')}</Select.Option>
                <Select.Option value={3} disabled={findProductCapability(capabilities, 2, undefined, 3)?.voCanSell !== true}>{t('products.consumableType.postHighlightCard')}</Select.Option>
                <Select.Option value={4} disabled={findProductCapability(capabilities, 2, undefined, 4)?.voCanSell !== true}>{t('products.consumableType.expCard')}</Select.Option>
                <Select.Option value={5} disabled={findProductCapability(capabilities, 2, undefined, 5)?.voCanSell !== true}>{t('products.consumableType.coinCard')}</Select.Option>
                <Select.Option value={6} disabled={findProductCapability(capabilities, 2, undefined, 6)?.voCanSell !== true}>{t('products.consumableType.doubleExpCard')}</Select.Option>
                <Select.Option value={99} disabled={findProductCapability(capabilities, 2, undefined, 99)?.voCanSell !== true}>{t('products.consumableType.lotteryTicket')}</Select.Option>
              </Select>
            </Form.Item>
            <div className="admin-form-help-text">
              {selectedCapability
                ? getCapabilityDescription(selectedCapability, t)
                : t('products.form.field.consumableCapabilityHint')}
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
          label={t('products.form.field.icon')}
        >
          <Form.Item
            name="iconAttachmentId"
            noStyle
            rules={[{ pattern: /^[1-9]\d*$/, message: t('products.form.validation.attachmentId') }]}
          >
            <Input className="admin-form-hidden-input" />
          </Form.Item>
          <Space orientation="vertical" className="admin-form-field-stack" size={10}>
            <div className="admin-form-upload-preview">
              {iconPreviewUrl ? (
                <img
                  src={iconPreviewUrl}
                  alt={t('products.form.field.iconPreview')}
                  className="admin-form-upload-preview__image"
                />
              ) : (
                <span>{t('products.form.field.noIcon')}</span>
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
                  {iconUploading
                    ? t('products.form.field.uploading')
                    : t('products.form.field.uploadIcon')}
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
                {t('products.form.field.clearIcon')}
              </Button>
            </Space>

            <Input
              placeholder={t('products.form.field.attachmentPlaceholder')}
              value={iconAttachmentId ? String(iconAttachmentId) : ''}
              readOnly
            />
          </Space>
        </Form.Item>

        <Form.Item
          label={t('products.form.field.cover')}
        >
          <Form.Item
            name="coverAttachmentId"
            noStyle
            rules={[{ pattern: /^[1-9]\d*$/, message: t('products.form.validation.attachmentId') }]}
          >
            <Input className="admin-form-hidden-input" />
          </Form.Item>
          <Space orientation="vertical" className="admin-form-field-stack" size={10}>
            <div className="admin-form-upload-preview admin-form-upload-preview--wide">
              {coverPreviewUrl ? (
                <img
                  src={coverPreviewUrl}
                  alt={t('products.form.field.coverPreview')}
                  className="admin-form-upload-preview__image"
                />
              ) : (
                <span>{t('products.form.field.noCover')}</span>
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
                  {coverUploading
                    ? t('products.form.field.uploading')
                    : t('products.form.field.uploadCover')}
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
                {t('products.form.field.clearCover')}
              </Button>
            </Space>

            <Input
              placeholder={t('products.form.field.attachmentPlaceholder')}
              value={coverAttachmentId ? String(coverAttachmentId) : ''}
              readOnly
            />
          </Space>
        </Form.Item>

        <Form.Item label={t('products.form.field.price')}>
          <Input.Group compact>
            <Form.Item
              name="price"
              noStyle
              rules={[{ required: true, message: t('products.form.validation.required', { field: t('products.form.field.salePrice') }) }]}
            >
              <InputNumber
                placeholder={t('products.form.field.salePrice')}
                min={0}
                className="admin-form-control-half"
                addonAfter={t('console.unit.carrot')}
              />
            </Form.Item>
            <Form.Item
              name="originalPrice"
              noStyle
            >
              <InputNumber
                placeholder={t('products.form.field.originalPrice')}
                min={0}
                className="admin-form-control-half"
                addonAfter={t('console.unit.carrot')}
              />
            </Form.Item>
          </Input.Group>
        </Form.Item>

        <Form.Item
          label={t('products.form.field.stockType')}
          name="stockType"
          rules={[{ required: true, message: t('products.form.validation.selectRequired', { field: t('products.form.field.stockType') }) }]}
        >
          <Radio.Group>
            <Radio value={0}>{t('products.stock.unlimited')}</Radio>
            <Radio value={1}>{t('products.form.field.stockLimited')}</Radio>
          </Radio.Group>
        </Form.Item>

        {stockType === 1 && (
          <Form.Item
            label={t('products.form.field.stockQuantity')}
            name="stock"
            rules={[{ required: true, message: t('products.form.field.stockQuantityPlaceholder') }]}
          >
            <InputNumber placeholder={t('products.form.field.stockQuantityPlaceholder')} min={0} className="admin-form-control-full" />
          </Form.Item>
        )}

        <Form.Item
          label={t('products.form.field.limitPerUser')}
          name="limitPerUser"
          tooltip={t('products.form.field.limitPerUserTooltip')}
        >
          <InputNumber placeholder={t('products.form.field.limitPerUser')} min={0} className="admin-form-control-full" />
        </Form.Item>

        <Form.Item
          label={t('products.form.field.durationType')}
          name="durationType"
          rules={[{ required: true, message: t('products.form.validation.selectRequired', { field: t('products.form.field.durationType') }) }]}
        >
          <Radio.Group>
            <Radio value={0}>{t('products.form.field.durationPermanent')}</Radio>
            <Radio value={1}>{t('products.form.field.durationDays')}</Radio>
            <Radio value={2}>{t('products.form.field.durationFixed')}</Radio>
          </Radio.Group>
        </Form.Item>

        {durationType === 1 && (
          <Form.Item
            label={t('products.form.field.durationDaysLabel')}
            name="durationDays"
            rules={[{ required: true, message: t('products.form.field.durationDaysPlaceholder') }]}
          >
            <InputNumber placeholder={t('products.form.field.durationDaysPlaceholder')} min={1} className="admin-form-control-full" />
          </Form.Item>
        )}

        {durationType === 2 && (
          <Form.Item
            label={t('products.form.field.expiresAt')}
            name="expiresAt"
            rules={[{ required: true, message: t('products.form.validation.selectRequired', { field: t('products.form.field.expiresAt') }) }]}
          >
            <DatePicker
              showTime
              format="YYYY-MM-DD HH:mm:ss"
              className="admin-form-control-full"
            />
          </Form.Item>
        )}

        <Form.Item
          label={t('products.form.field.sortOrder')}
          name="sortOrder"
          tooltip={t('products.form.field.sortOrderTooltip')}
        >
          <InputNumber placeholder={t('products.form.field.sortOrder')} className="admin-form-control-full" />
        </Form.Item>

        <Form.Item
          label={t('products.form.field.onSale')}
          name="isOnSale"
          valuePropName="checked"
        >
          <Switch disabled={unsupportedSaleSelection} />
        </Form.Item>

        {unsupportedSaleSelection && (
          <div className="admin-form-help-text">
            {t('products.form.field.unavailableHelp')}
          </div>
        )}
      </Form>
    </Modal>
  );
};

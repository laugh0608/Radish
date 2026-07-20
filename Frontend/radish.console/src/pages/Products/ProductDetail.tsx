import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Modal,
  Descriptions,
  Tag,
  Button,
  Space,
  Image,
  message,
  formatLocalizedDateTime,
  formatLocalizedNumber,
} from '@radish/ui';
import { adminGetProduct } from '../../api/shopApi';
import type { Product, ShopProductCapability } from '../../api/types';
import {
  getBenefitTypeDisplay,
  getConsumableTypeDisplay,
  getProductConfigLabel,
  getProductDurationDisplay,
  getProductTypeDisplay,
  getUnsupportedSaleReason,
  getUnsupportedSaleStatusLabel,
  normalizeProductType,
} from './productDisplay';

interface ProductDetailProps {
  visible: boolean;
  productId?: string;
  fallbackProduct?: Product;
  capabilities: ShopProductCapability[];
  reloadToken?: number;
  onClose: () => void;
  onEdit?: (product: Product) => void;
  onViewOrders?: (product: Product) => void;
  onReturnToSource?: () => void;
}

function isUnlimitedStock(product: Product): boolean {
  const stockType = String(product.voStockType ?? '');
  return stockType === 'Unlimited' || stockType === '0';
}

export const ProductDetail = ({
  visible,
  productId,
  fallbackProduct,
  capabilities,
  reloadToken = 0,
  onClose,
  onEdit,
  onViewOrders,
  onReturnToSource,
}: ProductDetailProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const [product, setProduct] = useState<Product | undefined>(fallbackProduct);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) {
      setProduct(fallbackProduct);
      setLoadError(null);
      return;
    }

    setProduct(fallbackProduct);
  }, [fallbackProduct, visible]);

  useEffect(() => {
    if (!visible || !productId) {
      return;
    }

    let cancelled = false;

    const loadProduct = async () => {
      try {
        setLoading(true);
        setLoadError(null);
        const result = await adminGetProduct(productId, t);
        if (cancelled) {
          return;
        }

        setProduct(result);
      } catch (error) {
        if (cancelled) {
          return;
        }

        const errorMessage = error instanceof Error ? error.message : t('products.detail.loadFailed');
        setLoadError(errorMessage);
        setProduct((current) => current ?? fallbackProduct);
        message.error(errorMessage);
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadProduct();

    return () => {
      cancelled = true;
    };
  }, [fallbackProduct, productId, reloadToken, t, visible]);

  const currentProduct = product ?? fallbackProduct;
  const unsupportedSaleReason = currentProduct
    ? getUnsupportedSaleReason(currentProduct, capabilities, t)
    : null;
  const unsupportedSaleStatusLabel = currentProduct
    ? getUnsupportedSaleStatusLabel(currentProduct, capabilities, t)
    : null;
  const productConfigLabel = currentProduct
    ? getProductConfigLabel(currentProduct, t)
    : t('products.detail.field.configuration');
  const productConfigValue = currentProduct
    ? normalizeProductType(currentProduct.voProductType) === 'Benefit'
      ? getBenefitTypeDisplay(currentProduct.voBenefitType, t)
      : normalizeProductType(currentProduct.voProductType) === 'Consumable'
        ? getConsumableTypeDisplay(currentProduct.voConsumableType, t)
        : '-'
    : '-';

  return (
    <Modal
      title={t('products.detail.title')}
      isOpen={visible}
      onClose={onClose}
      closeLabel={t('products.action.close')}
      size="large"
      footer={
        <Space wrap>
          {currentProduct && onReturnToSource ? (
            <Button onClick={onReturnToSource}>
              {t('products.action.backToOrders')}
            </Button>
          ) : null}
          {currentProduct && onViewOrders ? (
            <Button onClick={() => onViewOrders(currentProduct)}>
              {t('products.action.orders')}
            </Button>
          ) : null}
          {currentProduct && onEdit ? (
            <Button variant="primary" onClick={() => onEdit(currentProduct)}>
              {t('products.action.edit')}
            </Button>
          ) : null}
          <Button onClick={onClose}>{t('products.action.close')}</Button>
        </Space>
      }
    >
      {!currentProduct ? (
        <div className="product-detail-empty">
          {loading ? t('products.detail.loading') : loadError ?? t('products.detail.notFound')}
        </div>
      ) : (
        <>
          <div className="product-detail-media">
            <div className="product-detail-media-block">
              <div className="product-detail-media-label">{t('products.detail.icon')}</div>
              <Image
                src={currentProduct.voIcon || '/placeholder.png'}
                alt={currentProduct.voName}
                width={96}
                height={96}
                className="product-detail-image"
                fallback="/placeholder.png"
              />
            </div>
            <div className="product-detail-media-block">
              <div className="product-detail-media-label">{t('products.detail.cover')}</div>
              <Image
                src={currentProduct.voCoverImage || currentProduct.voIcon || '/placeholder.png'}
                alt={t('products.detail.coverAlt', { name: currentProduct.voName })}
                width={180}
                height={108}
                className="product-detail-image"
                fallback="/placeholder.png"
              />
            </div>
          </div>

          <Descriptions bordered column={2}>
            <Descriptions.Item label={t('products.detail.field.id')}>
              {currentProduct.voId}
            </Descriptions.Item>
            <Descriptions.Item label={t('products.detail.field.category')}>
              {currentProduct.voCategoryName || currentProduct.voCategoryId}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.name')} span={2}>
              {currentProduct.voName}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.description')} span={2}>
              {currentProduct.voDescription || '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.productType')}>
              <Tag color="blue">{getProductTypeDisplay(currentProduct.voProductType, t)}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label={productConfigLabel}>
              {productConfigValue}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.configurationValue')} span={2}>
              {currentProduct.voBenefitValue || '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.price')}>
              <span className="product-detail-price">
                {formatLocalizedNumber(currentProduct.voPrice, language)} {t('console.unit.carrot')}
              </span>
            </Descriptions.Item>
            <Descriptions.Item label={t('products.detail.field.originalPrice')}>
              {currentProduct.voOriginalPrice
                ? `${formatLocalizedNumber(currentProduct.voOriginalPrice, language)} ${t('console.unit.carrot')}`
                : '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.stock')}>
              {isUnlimitedStock(currentProduct)
                ? t('products.stock.unlimited')
                : formatLocalizedNumber(currentProduct.voStock, language)}
            </Descriptions.Item>
            <Descriptions.Item label={t('products.detail.field.soldLimit')}>
              {formatLocalizedNumber(currentProduct.voSoldCount, language)} / {' '}
              {currentProduct.voLimitPerUser > 0
                ? formatLocalizedNumber(currentProduct.voLimitPerUser, language)
                : t('products.common.unlimited')}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.duration')}>
              {getProductDurationDisplay(
                currentProduct,
                t,
                (value) => formatLocalizedDateTime(value, language),
              )}
            </Descriptions.Item>
            <Descriptions.Item label={t('products.detail.field.fixedExpiry')}>
              {currentProduct.voExpiresAt
                ? formatLocalizedDateTime(currentProduct.voExpiresAt, language)
                : '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.saleStatus')}>
              <Space size="small" wrap>
                <Tag color={currentProduct.voIsOnSale ? 'success' : 'default'}>
                  {currentProduct.voIsOnSale
                    ? t('products.status.onSale')
                    : t('products.status.offSale')}
                </Tag>
                {unsupportedSaleStatusLabel ? (
                  <Tag color={currentProduct.voIsOnSale ? 'warning' : 'processing'}>
                    {unsupportedSaleStatusLabel}
                  </Tag>
                ) : null}
                <Tag color={currentProduct.voIsEnabled ? 'success' : 'error'}>
                  {currentProduct.voIsEnabled
                    ? t('products.status.enabled')
                    : t('products.status.disabled')}
                </Tag>
              </Space>
            </Descriptions.Item>
            <Descriptions.Item label={t('products.detail.field.saleTimes')}>
              {currentProduct.voOnSaleTime
                ? formatLocalizedDateTime(currentProduct.voOnSaleTime, language)
                : '-'} / {' '}
              {currentProduct.voOffSaleTime
                ? formatLocalizedDateTime(currentProduct.voOffSaleTime, language)
                : '-'}
            </Descriptions.Item>

            <Descriptions.Item label={t('products.detail.field.createdAt')} span={2}>
              {formatLocalizedDateTime(currentProduct.voCreateTime, language)}
            </Descriptions.Item>
          </Descriptions>

          {unsupportedSaleReason ? (
            <div className="product-detail-warning">
              {unsupportedSaleReason}
            </div>
          ) : null}
        </>
      )}
    </Modal>
  );
};

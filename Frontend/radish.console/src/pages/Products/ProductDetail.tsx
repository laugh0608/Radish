import { useEffect, useState } from 'react';
import { Modal, Descriptions, Tag, Button, Space, Image, message } from '@radish/ui';
import { adminGetProduct } from '../../api/shopApi';
import type { Product } from '../../api/types';
import {
  getBenefitTypeDisplay,
  getConsumableTypeDisplay,
  getProductConfigLabel,
  getProductTypeDisplay,
  getUnsupportedSaleReason,
  getUnsupportedSaleStatusLabel,
  normalizeProductType,
} from './productDisplay';

interface ProductDetailProps {
  visible: boolean;
  productId?: string;
  fallbackProduct?: Product;
  reloadToken?: number;
  onClose: () => void;
  onEdit?: (product: Product) => void;
  onViewOrders?: (product: Product) => void;
  onReturnToSource?: () => void;
}

function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString('zh-CN');
}

export const ProductDetail = ({
  visible,
  productId,
  fallbackProduct,
  reloadToken = 0,
  onClose,
  onEdit,
  onViewOrders,
  onReturnToSource,
}: ProductDetailProps) => {
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
        const result = await adminGetProduct(productId);
        if (cancelled) {
          return;
        }

        setProduct(result);
      } catch (error) {
        if (cancelled) {
          return;
        }

        const errorMessage = error instanceof Error ? error.message : '加载商品详情失败';
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
  }, [fallbackProduct, productId, reloadToken, visible]);

  const currentProduct = product ?? fallbackProduct;
  const unsupportedSaleReason = currentProduct ? getUnsupportedSaleReason(currentProduct) : null;
  const unsupportedSaleStatusLabel = currentProduct ? getUnsupportedSaleStatusLabel(currentProduct) : null;
  const productConfigLabel = currentProduct ? getProductConfigLabel(currentProduct) : '商品配置';
  const productConfigValue = currentProduct
    ? normalizeProductType(currentProduct.voProductType) === 'Benefit'
      ? getBenefitTypeDisplay(currentProduct.voBenefitType)
      : normalizeProductType(currentProduct.voProductType) === 'Consumable'
        ? getConsumableTypeDisplay(currentProduct.voConsumableType)
        : '-'
    : '-';

  return (
    <Modal
      title="商品详情"
      isOpen={visible}
      onClose={onClose}
      size="large"
      footer={
        <Space wrap>
          {currentProduct && onReturnToSource ? (
            <Button onClick={onReturnToSource}>
              返回订单
            </Button>
          ) : null}
          {currentProduct && onViewOrders ? (
            <Button onClick={() => onViewOrders(currentProduct)}>
              查看相关订单
            </Button>
          ) : null}
          {currentProduct && onEdit ? (
            <Button variant="primary" onClick={() => onEdit(currentProduct)}>
              编辑商品
            </Button>
          ) : null}
          <Button onClick={onClose}>关闭</Button>
        </Space>
      }
    >
      {!currentProduct ? (
        <div className="product-detail-empty">
          {loading ? '正在加载商品详情...' : loadError ?? '未找到商品详情'}
        </div>
      ) : (
        <>
          <div className="product-detail-media">
            <div className="product-detail-media-block">
              <div className="product-detail-media-label">商品图标</div>
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
              <div className="product-detail-media-label">商品封面</div>
              <Image
                src={currentProduct.voCoverImage || currentProduct.voIcon || '/placeholder.png'}
                alt={`${currentProduct.voName} 封面`}
                width={180}
                height={108}
                className="product-detail-image"
                fallback="/placeholder.png"
              />
            </div>
          </div>

          <Descriptions bordered column={2}>
            <Descriptions.Item label="商品 ID">
              {currentProduct.voId}
            </Descriptions.Item>
            <Descriptions.Item label="分类">
              {currentProduct.voCategoryName || currentProduct.voCategoryId}
            </Descriptions.Item>

            <Descriptions.Item label="商品名称" span={2}>
              {currentProduct.voName}
            </Descriptions.Item>

            <Descriptions.Item label="商品描述" span={2}>
              {currentProduct.voDescription || '-'}
            </Descriptions.Item>

            <Descriptions.Item label="商品类型">
              <Tag color="blue">{getProductTypeDisplay(currentProduct.voProductType)}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label={productConfigLabel}>
              {productConfigValue}
            </Descriptions.Item>

            <Descriptions.Item label="配置值" span={2}>
              {currentProduct.voBenefitValue || '-'}
            </Descriptions.Item>

            <Descriptions.Item label="售价">
              <span className="product-detail-price">
                {currentProduct.voPrice} 胡萝卜
              </span>
            </Descriptions.Item>
            <Descriptions.Item label="原价">
              {currentProduct.voOriginalPrice ? `${currentProduct.voOriginalPrice} 胡萝卜` : '-'}
            </Descriptions.Item>

            <Descriptions.Item label="库存">
              {currentProduct.voStockType === 'Unlimited' ? '无限库存' : currentProduct.voStock}
            </Descriptions.Item>
            <Descriptions.Item label="已售 / 限购">
              {currentProduct.voSoldCount} / {currentProduct.voLimitPerUser > 0 ? currentProduct.voLimitPerUser : '不限'}
            </Descriptions.Item>

            <Descriptions.Item label="有效期">
              {currentProduct.voDurationDisplay}
            </Descriptions.Item>
            <Descriptions.Item label="固定到期时间">
              {formatDateTime(currentProduct.voExpiresAt)}
            </Descriptions.Item>

            <Descriptions.Item label="上架状态">
              <Space size="small" wrap>
                <Tag color={currentProduct.voIsOnSale ? 'success' : 'default'}>
                  {currentProduct.voIsOnSale ? '已上架' : '已下架'}
                </Tag>
                {unsupportedSaleStatusLabel ? (
                  <Tag color={currentProduct.voIsOnSale ? 'warning' : 'processing'}>
                    {unsupportedSaleStatusLabel}
                  </Tag>
                ) : null}
                <Tag color={currentProduct.voIsEnabled ? 'success' : 'error'}>
                  {currentProduct.voIsEnabled ? '启用' : '禁用'}
                </Tag>
              </Space>
            </Descriptions.Item>
            <Descriptions.Item label="上 / 下架时间">
              {formatDateTime(currentProduct.voOnSaleTime)} / {formatDateTime(currentProduct.voOffSaleTime)}
            </Descriptions.Item>

            <Descriptions.Item label="创建时间" span={2}>
              {formatDateTime(currentProduct.voCreateTime)}
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

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  BottomSheet,
  Table,
  Button,
  AntInput as Input,
  AntSelect as Select,
  Space,
  Tag,
  Image,
  message,
  ConfirmDialog,
  formatLocalizedNumber,
  type TableColumnsType,
} from '@radish/ui';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  SearchOutlined,
  ReloadOutlined,
  EyeOutlined,
  ShoppingOutlined,
} from '@radish/ui';
import {
  adminGetProducts,
  getCategories,
  putOnSale,
  takeOffSale,
  deleteProduct,
  getProductCapabilities,
} from '../../api/shopApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleResourceList,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import type { Product, ProductCategory, ShopProductCapability } from '../../api/types';
import { ProductType } from '../../api/types';
import { ProductForm } from './ProductForm';
import { ProductDetail } from './ProductDetail';
import {
  getProductTypeDisplay,
  getUnsupportedSaleReason,
  getUnsupportedSaleStatusLabel,
} from './productDisplay';
import {
  buildProductDetailReturnTo,
  buildProductDetailSearchParams,
  DEFAULT_PRODUCT_LIST_QUERY,
  normalizeProductReturnTo,
  parseProductBooleanQuery,
  parseProductListQuery,
  parseProductLongIdQuery,
  serializeProductListQuery,
  type ProductListQuery,
} from './productListUrlState';
import { buildOrderSearchParams } from '../Orders/orderListUrlState';
import { getAvatarUrl } from '../../config/env';
import { log } from '../../utils/logger';
import '../adminFeature.css';
import './ProductList.css';

type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';
type MetadataReadState = 'loading' | 'ready' | 'unavailable';

function isUnlimitedStock(product: Product): boolean {
  const stockType = String(product.voStockType ?? '');
  return stockType === 'Unlimited' || stockType === '0';
}

function isStockWatchProduct(product: Product): boolean {
  return !isUnlimitedStock(product) && product.voStock <= 0;
}

function getProductStockSummary(
  product: Product,
  t: TFunction,
  language: string,
): string {
  return isUnlimitedStock(product)
    ? t('products.stock.unlimited')
    : t('products.stock.limited', {
        count: product.voStock,
        formattedCount: formatLocalizedNumber(product.voStock, language),
      });
}

export const ProductList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('console.route.products'));
  const navigate = useNavigate();
  const [urlSearchParams, setUrlSearchParams] = useSearchParams();
  const query = useMemo(() => parseProductListQuery(urlSearchParams), [urlSearchParams]);
  const queryProductId = parseProductLongIdQuery(urlSearchParams.get('productId'));
  const queryOpenDetail = parseProductBooleanQuery(urlSearchParams.get('openDetail'));
  const queryReturnTo = normalizeProductReturnTo(urlSearchParams.get('returnTo'));
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<ProductCategory[]>([]);
  const [capabilities, setCapabilities] = useState<ShopProductCapability[]>([]);
  const [loading, setLoading] = useState(false);
  const [readState, setReadState] = useState<ResourceReadState>('loading');
  const [metadataState, setMetadataState] = useState<MetadataReadState>('loading');
  const [total, setTotal] = useState(0);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [busyAction, setBusyAction] = useState<string>();
  const requestSequence = useRef(0);
  const snapshotQueryKey = useRef<string | undefined>(undefined);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);
  const canCreateProduct = usePermission(CONSOLE_PERMISSIONS.productsCreate);
  const canEditProduct = usePermission(CONSOLE_PERMISSIONS.productsEdit);
  const canDeleteProductPermission = usePermission(CONSOLE_PERMISSIONS.productsDelete);
  const canToggleProductSale = usePermission(CONSOLE_PERMISSIONS.productsToggleSale);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);
  const actionsAreAuthoritative = readState === 'ready';
  const metadataIsAuthoritative = metadataState === 'ready';

  const [formVisible, setFormVisible] = useState(false);
  const [editingProduct, setEditingProduct] = useState<Product | undefined>();
  const [detailVisible, setDetailVisible] = useState(false);
  const [selectedProductId, setSelectedProductId] = useState<string | undefined>();
  const [selectedProductSnapshot, setSelectedProductSnapshot] = useState<Product | undefined>();
  const [detailReloadToken, setDetailReloadToken] = useState(0);
  const [deletingProduct, setDeletingProduct] = useState<Product | undefined>();
  const [deleteConfirmVisible, setDeleteConfirmVisible] = useState(false);
  const [saleConfirmProduct, setSaleConfirmProduct] = useState<Product | undefined>();

  const [draftCategoryId, setDraftCategoryId] = useState<string | undefined>(query.categoryId);
  const [draftProductType, setDraftProductType] = useState<ProductType | undefined>(
    query.productType as ProductType | undefined,
  );
  const [draftIsOnSale, setDraftIsOnSale] = useState<boolean | undefined>(query.isOnSale);
  const [draftKeyword, setDraftKeyword] = useState(query.keyword);

  useEffect(() => {
    setDraftCategoryId(query.categoryId);
    setDraftProductType(query.productType as ProductType | undefined);
    setDraftIsOnSale(query.isOnSale);
    setDraftKeyword(query.keyword);
  }, [query.categoryId, query.isOnSale, query.keyword, query.productType]);

  const activeFilterCount = [
    query.categoryId,
    query.productType !== undefined ? 'productType' : undefined,
    query.isOnSale !== undefined ? 'saleStatus' : undefined,
    query.keyword ? 'keyword' : undefined,
  ].filter(Boolean).length;
  const currentCategoryName = categories.find((item) => String(item.voId) === query.categoryId)?.voName;
  const onSaleProducts = products.filter((product) => product.voIsOnSale).length;
  const enabledProducts = products.filter((product) => product.voIsEnabled).length;
  const stockWatchProducts = products.filter(isStockWatchProduct).length;
  const unsupportedSaleProducts = products.filter(
    (product) => getUnsupportedSaleReason(product, capabilities, t),
  ).length;
  const soldUnitsOnPage = products.reduce((sum, product) => sum + product.voSoldCount, 0);
  const primaryProduct = selectedProductSnapshot ?? products[0] ?? null;
  const primaryProductUnsupportedReason = primaryProduct
    ? getUnsupportedSaleReason(primaryProduct, capabilities, t)
    : null;
  const pageCount = Math.max(1, Math.ceil(total / query.pageSize));

  const updateQuery = useCallback((nextQuery: ProductListQuery) => {
    setUrlSearchParams(buildProductDetailSearchParams({
      listQuery: nextQuery,
      productId: queryProductId,
      openDetail: queryOpenDetail,
      returnTo: queryReturnTo,
    }), { replace: true });
  }, [queryOpenDetail, queryProductId, queryReturnTo, setUrlSearchParams]);

  const syncDetailSearchParams = useCallback((
    productId?: string,
    openDetail?: boolean,
    replace: boolean = false,
    returnTo?: string | null,
  ) => {
    setUrlSearchParams(buildProductDetailSearchParams({
      listQuery: query,
      productId,
      openDetail,
      returnTo,
    }), { replace });
  }, [query, setUrlSearchParams]);

  const loadProducts = useCallback(async () => {
    const requestId = requestSequence.current + 1;
    const queryKey = serializeProductListQuery(query).toString();
    const hasCurrentSnapshot = snapshotQueryKey.current === queryKey;
    requestSequence.current = requestId;

    try {
      setLoading(true);
      setReadState('loading');
      if (!hasCurrentSnapshot) {
        setProducts([]);
        setTotal(0);
      }

      const response = await adminGetProducts({
        categoryId: query.categoryId,
        productType: query.productType as ProductType | undefined,
        isOnSale: query.isOnSale,
        keyword: query.keyword || undefined,
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
      }, t);
      if (requestSequence.current !== requestId) return;

      const responsePageCount = Math.max(1, Math.ceil(response.dataCount / query.pageSize));
      if (response.data.length === 0 && response.dataCount > 0 && query.pageIndex > responsePageCount) {
        updateQuery({ ...query, pageIndex: responsePageCount });
        return;
      }

      setProducts(response.data);
      setTotal(response.dataCount);
      setSelectedProductSnapshot((current) => current
        ? response.data.find((item) => String(item.voId) === String(current.voId)) ?? current
        : current);
      snapshotQueryKey.current = queryKey;
      setReadState('ready');
    } catch (error) {
      if (requestSequence.current !== requestId) return;
      log.error('ProductList', '加载商品列表失败:', error);
      setReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(error instanceof Error ? error.message : t('products.list.loadFailed'));
    } finally {
      if (requestSequence.current === requestId) setLoading(false);
    }
  }, [query, t, updateQuery]);

  const loadMetadata = useCallback(async () => {
    try {
      setMetadataState('loading');
      const [categoryData, capabilityData] = await Promise.all([
        getCategories(t),
        getProductCapabilities(t),
      ]);
      setCategories(categoryData);
      setCapabilities(capabilityData);
      setMetadataState('ready');
    } catch (error) {
      log.error('ProductList', '加载商品元数据失败:', error);
      setMetadataState('unavailable');
      message.error(error instanceof Error ? error.message : t('products.list.capabilityLoadFailed'));
    }
  }, [t]);

  useEffect(() => {
    if (canViewProducts) void loadMetadata();
  }, [canViewProducts, language, loadMetadata]);

  useEffect(() => {
    if (canViewProducts) void loadProducts();
  }, [canViewProducts, loadProducts]);

  useEffect(() => {
    if (!queryOpenDetail || !queryProductId) return;
    setSelectedProductId(queryProductId);
    setSelectedProductSnapshot(products.find((item) => String(item.voId) === queryProductId));
    setDetailVisible(true);
  }, [products, queryOpenDetail, queryProductId]);

  const reportUnavailableAction = (hasPermission: boolean, needsMetadata = false) => {
    if (!hasPermission) {
      message.error(t('products.feedback.permissionDenied'));
    } else if (!actionsAreAuthoritative) {
      message.error(t('products.feedback.authorityUnavailable'));
    } else if (needsMetadata && !metadataIsAuthoritative) {
      message.error(t('products.feedback.metadataUnavailable'));
    }
  };

  const handleSearch = () => {
    updateQuery({
      ...query,
      pageIndex: 1,
      categoryId: draftCategoryId,
      productType: draftProductType as ProductListQuery['productType'],
      isOnSale: draftIsOnSale,
      keyword: draftKeyword.trim().slice(0, 100),
    });
    setFilterSheetOpen(false);
  };

  const handleReset = () => {
    setDraftCategoryId(undefined);
    setDraftProductType(undefined);
    setDraftIsOnSale(undefined);
    setDraftKeyword('');
    updateQuery({ ...DEFAULT_PRODUCT_LIST_QUERY, pageSize: query.pageSize });
    setFilterSheetOpen(false);
  };

  const handlePageChange = (pageIndex: number, pageSize = query.pageSize) => {
    updateQuery({ ...query, pageIndex, pageSize });
  };

  const handleOpenDetail = (productId: string, product?: Product, syncQuery = false) => {
    setSelectedProductId(productId);
    setSelectedProductSnapshot(product);
    setDetailVisible(true);
    if (syncQuery) syncDetailSearchParams(productId, true, false, queryReturnTo);
  };

  const handleCloseDetail = () => {
    setDetailVisible(false);
    setSelectedProductId(undefined);
    setSelectedProductSnapshot(undefined);
    if (queryOpenDetail || queryProductId || queryReturnTo) {
      syncDetailSearchParams(undefined, false, true);
    }
  };

  const handleCreateProduct = () => {
    if (!canCreateProduct || !actionsAreAuthoritative || !metadataIsAuthoritative) {
      reportUnavailableAction(canCreateProduct, true);
      return;
    }
    setEditingProduct(undefined);
    setFormVisible(true);
  };

  const handleEditProduct = (product: Product) => {
    if (!canEditProduct || !actionsAreAuthoritative || !metadataIsAuthoritative) {
      reportUnavailableAction(canEditProduct, true);
      return;
    }
    setEditingProduct(product);
    setFormVisible(true);
  };

  const handleToggleSale = (product: Product) => {
    const needsMetadata = !product.voIsOnSale;
    if (!canToggleProductSale || !actionsAreAuthoritative || (needsMetadata && !metadataIsAuthoritative)) {
      reportUnavailableAction(canToggleProductSale, needsMetadata);
      return;
    }
    if (!product.voIsOnSale && getUnsupportedSaleReason(product, capabilities, t)) {
      message.error(getUnsupportedSaleReason(product, capabilities, t) ?? t('products.list.saleChangeFailed'));
      return;
    }
    setSaleConfirmProduct(product);
  };

  const handleConfirmToggleSale = async () => {
    const product = saleConfirmProduct;
    if (!product || busyAction) return;
    const needsMetadata = !product.voIsOnSale;
    if (!canToggleProductSale || !actionsAreAuthoritative || (needsMetadata && !metadataIsAuthoritative)) {
      reportUnavailableAction(canToggleProductSale, needsMetadata);
      setSaleConfirmProduct(undefined);
      return;
    }

    const actionKey = `sale:${product.voId}`;
    try {
      setBusyAction(actionKey);
      if (product.voIsOnSale) {
        await takeOffSale(product.voId, product.voVersion, t);
        message.success(t('products.list.takeOffSaleSuccess'));
      } else {
        await putOnSale(product.voId, product.voVersion, t);
        message.success(t('products.list.putOnSaleSuccess'));
      }
      setSaleConfirmProduct(undefined);
      await loadProducts();
      if (String(selectedProductId) === String(product.voId)) {
        setDetailReloadToken((current) => current + 1);
      }
    } catch (error) {
      log.error('ProductList', '上架/下架失败:', error);
      message.error(error instanceof Error ? error.message : t('products.list.saleChangeFailed'));
    } finally {
      setBusyAction(undefined);
    }
  };

  const handleDelete = (product: Product) => {
    if (!canDeleteProductPermission || !actionsAreAuthoritative) {
      reportUnavailableAction(canDeleteProductPermission);
      return;
    }
    setDeletingProduct(product);
    setDeleteConfirmVisible(true);
  };

  const handleConfirmDelete = async () => {
    if (!deletingProduct || busyAction) return;
    if (!canDeleteProductPermission || !actionsAreAuthoritative) {
      reportUnavailableAction(canDeleteProductPermission);
      setDeleteConfirmVisible(false);
      setDeletingProduct(undefined);
      return;
    }

    const product = deletingProduct;
    try {
      setBusyAction(`delete:${product.voId}`);
      await deleteProduct(product.voId, t);
      message.success(t('products.list.deleteSuccess'));
      setDeleteConfirmVisible(false);
      setDeletingProduct(undefined);
      await loadProducts();
      if (String(selectedProductId) === String(product.voId)) handleCloseDetail();
    } catch (error) {
      log.error('ProductList', '删除商品失败:', error);
      message.error(error instanceof Error ? error.message : t('products.list.deleteFailed'));
    } finally {
      setBusyAction(undefined);
    }
  };

  const handleViewOrders = (product: Product) => {
    const productId = String(product.voId);
    const returnTo = buildProductDetailReturnTo({
      productId,
      returnTo: queryReturnTo,
      listQuery: query,
    });
    navigate(`/orders?${buildOrderSearchParams({ productId, returnTo }).toString()}`);
  };

  const handleReturnToSource = () => {
    if (queryReturnTo) navigate(queryReturnTo);
  };

  const renderProductIdentity = (product: Product) => (
    <div className="product-list-identity">
      <Image
        src={getAvatarUrl(product.voCoverImage || product.voIcon) || '/placeholder.png'}
        alt={product.voName}
        width={56}
        height={56}
        className="product-list-image"
        fallback="/placeholder.png"
      />
      <div className="product-list-name">
        <strong className="product-list-name__title">{product.voName}</strong>
        <span className="product-list-name__meta">{product.voCategoryName || product.voCategoryId}</span>
      </div>
    </div>
  );

  const renderProductActions = (product: Product) => {
    const unsupportedSaleReason = getUnsupportedSaleReason(product, capabilities, t);
    const saleBlockReason = !product.voIsOnSale ? unsupportedSaleReason : null;
    const toggleButtonTitle = product.voIsOnSale && unsupportedSaleReason
      ? t('products.action.historicalOnSaleHint')
      : saleBlockReason ?? undefined;
    const anyBusy = busyAction !== undefined;
    const saleDisabled = !actionsAreAuthoritative
      || anyBusy
      || (!product.voIsOnSale && (!metadataIsAuthoritative || !!saleBlockReason));

    return (
      <Space size="small" wrap>
        <Button
          variant="ghost"
          size="small"
          icon={<EyeOutlined />}
          onClick={() => handleOpenDetail(product.voId, product, true)}
        >
          {t('products.action.detail')}
        </Button>
        {canViewOrders ? (
          <Button variant="ghost" size="small" onClick={() => handleViewOrders(product)}>
            {t('products.action.orders')}
          </Button>
        ) : null}
        {canEditProduct ? (
          <Button
            variant="ghost"
            size="small"
            icon={<EditOutlined />}
            disabled={!actionsAreAuthoritative || !metadataIsAuthoritative || anyBusy}
            onClick={() => handleEditProduct(product)}
          >
            {t('products.action.edit')}
          </Button>
        ) : null}
        {canToggleProductSale ? (
          <Button
            variant="ghost"
            size="small"
            disabled={saleDisabled}
            title={toggleButtonTitle}
            onClick={() => handleToggleSale(product)}
          >
            {product.voIsOnSale ? t('products.action.takeOffSale') : t('products.action.putOnSale')}
          </Button>
        ) : null}
        {canDeleteProductPermission ? (
          <Button
            variant="danger"
            size="small"
            icon={<DeleteOutlined />}
            disabled={!actionsAreAuthoritative || anyBusy}
            onClick={() => handleDelete(product)}
          >
            {t('products.action.delete')}
          </Button>
        ) : null}
      </Space>
    );
  };

  const columns: TableColumnsType<Product> = [
    { title: 'ID', dataIndex: 'voId', key: 'voId', width: 90 },
    {
      title: t('products.column.name'),
      key: 'identity',
      width: 280,
      render: (_: unknown, product: Product) => renderProductIdentity(product),
    },
    {
      title: t('products.column.type'),
      dataIndex: 'voProductType',
      key: 'voProductType',
      width: 110,
      render: (type: ProductType) => <Tag color="blue">{getProductTypeDisplay(type, t)}</Tag>,
    },
    {
      title: t('products.column.price'),
      dataIndex: 'voPrice',
      key: 'voPrice',
      width: 130,
      render: (price: number, product: Product) => (
        <div className="product-list-price">
          <strong className="product-list-price__current">
            {formatLocalizedNumber(price, language)} {t('console.unit.carrot')}
          </strong>
          {product.voOriginalPrice && product.voOriginalPrice > price ? (
            <span className="product-list-price__original">
              {formatLocalizedNumber(product.voOriginalPrice, language)} {t('console.unit.carrot')}
            </span>
          ) : null}
        </div>
      ),
    },
    {
      title: t('products.column.stock'),
      dataIndex: 'voStock',
      key: 'voStock',
      width: 110,
      render: (stock: number, product: Product) => isUnlimitedStock(product)
        ? <Tag color="green">{t('products.common.unlimited')}</Tag>
        : <span className={`product-list-stock product-list-stock--${stock > 0 ? 'available' : 'empty'}`}>{formatLocalizedNumber(stock, language)}</span>,
    },
    {
      title: t('products.column.sold'),
      dataIndex: 'voSoldCount',
      key: 'voSoldCount',
      width: 90,
      render: (sold: number) => formatLocalizedNumber(sold, language),
    },
    {
      title: t('products.column.status'),
      key: 'status',
      width: 150,
      render: (_: unknown, product: Product) => {
        const unsupportedStatusLabel = getUnsupportedSaleStatusLabel(product, capabilities, t);
        return (
          <Space orientation="vertical" size="small">
            <Tag color={product.voIsOnSale ? 'success' : 'default'}>
              {product.voIsOnSale ? t('products.status.onSale') : t('products.status.offSale')}
            </Tag>
            {unsupportedStatusLabel ? (
              <Tag color={product.voIsOnSale ? 'warning' : 'processing'}>{unsupportedStatusLabel}</Tag>
            ) : null}
            <Tag color={product.voIsEnabled ? 'success' : 'error'}>
              {product.voIsEnabled ? t('products.status.enabled') : t('products.status.disabled')}
            </Tag>
          </Space>
        );
      },
    },
    {
      title: t('products.column.action'),
      key: 'action',
      width: 340,
      fixed: 'right',
      render: (_: unknown, product: Product) => renderProductActions(product),
    },
  ];

  const filterControls = (
    <div className="console-resource-filter-controls product-list-filter-controls">
      <Select
        className="product-list-filter-select"
        placeholder={t('products.list.filter.category')}
        allowClear
        value={draftCategoryId}
        onChange={setDraftCategoryId}
      >
        {categories.map((category) => (
          <Select.Option key={category.voId} value={category.voId}>{category.voName}</Select.Option>
        ))}
      </Select>
      <Select
        className="product-list-filter-select product-list-filter-select--type"
        placeholder={t('products.list.filter.productType')}
        allowClear
        value={draftProductType}
        onChange={setDraftProductType}
      >
        <Select.Option value={1}>{t('products.type.benefit')}</Select.Option>
        <Select.Option value={2}>{t('products.type.consumable')}</Select.Option>
        <Select.Option value={99}>{t('products.type.physical')}</Select.Option>
      </Select>
      <Select
        className="product-list-filter-select product-list-filter-select--sale"
        placeholder={t('products.list.filter.saleStatus')}
        allowClear
        value={draftIsOnSale}
        onChange={setDraftIsOnSale}
      >
        <Select.Option value={true}>{t('products.status.onSale')}</Select.Option>
        <Select.Option value={false}>{t('products.status.offSale')}</Select.Option>
      </Select>
      <Input
        className="product-list-filter-input"
        maxLength={100}
        placeholder={t('products.list.filter.keyword')}
        value={draftKeyword}
        onChange={(event) => setDraftKeyword(event.target.value)}
        onPressEnter={handleSearch}
        suffix={<SearchOutlined />}
      />
      <div className="console-resource-filter-controls__actions">
        <Button icon={<ReloadOutlined />} onClick={handleReset}>{t('products.list.reset')}</Button>
        <Button variant="primary" icon={<SearchOutlined />} onClick={handleSearch}>{t('products.list.search')}</Button>
      </div>
    </div>
  );

  const readNotice = readState === 'stale' || readState === 'unavailable' ? (
    <div className={`console-resource-list-notice console-resource-list-notice--${readState}`} role="alert">
      <div>
        <strong>{t(readState === 'stale' ? 'products.list.staleTitle' : 'products.list.unavailableTitle')}</strong>
        <span>{t(readState === 'stale' ? 'products.list.staleDescription' : 'products.list.unavailableDescription')}</span>
      </div>
      <Button size="small" onClick={() => void loadProducts()}>{t('products.action.retry')}</Button>
    </div>
  ) : null;

  const metadataNotice = metadataState === 'unavailable' ? (
    <div className="console-resource-list-notice console-resource-list-notice--stale" role="alert">
      <div>
        <strong>{t('products.list.metadataUnavailableTitle')}</strong>
        <span>{t('products.list.metadataUnavailableDescription')}</span>
      </div>
      <Button size="small" onClick={() => void loadMetadata()}>{t('products.action.retry')}</Button>
    </div>
  ) : null;

  return (
    <div className="admin-feature-page product-list-page">
      <ConsolePageHeader
        eyebrow={t('products.list.eyebrow')}
        title={t('products.list.title')}
        description={t('products.list.description')}
        icon={<ShoppingOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateProduct ? 'success' : 'neutral'}>
            {canCreateProduct ? t('products.common.canCreate') : t('products.common.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={(
          <>
            <Button icon={<ReloadOutlined />} disabled={!canViewProducts} onClick={() => void loadProducts()}>
              {t('products.action.retry')}
            </Button>
            {canCreateProduct ? (
              <Button
                variant="primary"
                icon={<PlusOutlined />}
                disabled={!actionsAreAuthoritative || !metadataIsAuthoritative || busyAction !== undefined}
                onClick={handleCreateProduct}
              >
                {t('products.action.create')}
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label={t('products.list.metrics.label')}>
        <ConsoleMetricCard label={t('products.list.metrics.results')} value={formatLocalizedNumber(total, language)} description={t('products.list.metrics.resultsDescription')} tone="info" />
        <ConsoleMetricCard label={t('products.list.metrics.page')} value={formatLocalizedNumber(products.length, language)} description={t('products.list.metrics.pageDescription')} />
        <ConsoleMetricCard label={t('products.list.metrics.onSale')} value={formatLocalizedNumber(onSaleProducts, language)} description={t('products.list.metrics.onSaleDescription')} tone="success" />
        <ConsoleMetricCard label={t('products.list.metrics.enabled')} value={formatLocalizedNumber(enabledProducts, language)} description={t('products.list.metrics.enabledDescription')} tone="success" />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label={t('products.list.flow.label')}>
        <div className="governance-task-flow__item">
          <span>1</span><strong>{t('products.list.flow.poolTitle')}</strong>
          <p>{t('products.list.flow.pool', { total: formatLocalizedNumber(total, language), visible: formatLocalizedNumber(products.length, language) })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span><strong>{t('products.list.flow.saleTitle')}</strong>
          <p>{t('products.list.flow.sale', { onSale: formatLocalizedNumber(onSaleProducts, language), unavailable: formatLocalizedNumber(unsupportedSaleProducts, language) })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span><strong>{t('products.list.flow.stockTitle')}</strong>
          <p>{t('products.list.flow.stock', { sold: formatLocalizedNumber(soldUnitsOnPage, language), watch: formatLocalizedNumber(stockWatchProducts, language) })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span><strong>{t('products.list.flow.orderTitle')}</strong>
          <p>{primaryProduct ? t('products.list.flow.orderSelected', { name: primaryProduct.voName }) : t('products.list.flow.orderEmpty')}</p>
        </div>
      </section>

      <ConsoleResourceList
        toolbar={(
          <ConsoleToolbar
            title={t('products.list.toolbar.title')}
            description={t('products.list.toolbar.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('products.list.filterCount', { count: activeFilterCount }) : t('products.list.noFilters')}
              </ConsoleStatusChip>
            )}
          >
            {filterControls}
          </ConsoleToolbar>
        )}
        mobileToolbar={(
          <div className="console-resource-mobile-summary">
            <div className="console-resource-mobile-summary__copy">
              <strong>{t('products.list.pagination', { count: total, formattedCount: formatLocalizedNumber(total, language) })}</strong>
              <span>{activeFilterCount > 0 ? t('products.list.filterCount', { count: activeFilterCount }) : t('products.list.noFilters')}</span>
            </div>
            <div className="console-resource-mobile-summary__actions">
              <Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>{t('products.list.mobileFilter')}</Button>
              <Button size="small" icon={<ReloadOutlined />} onClick={() => void loadProducts()}>{t('products.action.retry')}</Button>
            </div>
          </div>
        )}
        desktopList={(
          <section className="admin-table-panel">
            {readNotice}
            {metadataNotice}
            <Table<Product>
              columns={columns}
              dataSource={products}
              rowKey="voId"
              loading={loading}
              pagination={{
                current: query.pageIndex,
                pageSize: query.pageSize,
                total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (itemTotal) => t('products.list.pagination', { count: itemTotal, formattedCount: formatLocalizedNumber(itemTotal, language) }),
                onChange: handlePageChange,
              }}
              scroll={{ x: 1320 }}
            />
          </section>
        )}
        mobileList={(
          <>
            {readNotice}
            {metadataNotice}
            {loading && products.length === 0 ? <div className="console-resource-mobile-loading">{t('products.list.mobileLoading')}</div> : null}
            {readState === 'ready' && products.length === 0 ? (
              <div className="console-resource-mobile-empty">
                <strong>{t('products.list.emptyTitle')}</strong>
                <span>{t('products.list.emptyDescription')}</span>
              </div>
            ) : null}
            {products.map((product) => (
              <article className="console-resource-mobile-card product-mobile-card" key={product.voId}>
                <div className="console-resource-mobile-card__header">
                  {renderProductIdentity(product)}
                  <Tag color={product.voIsOnSale ? 'success' : 'default'}>
                    {product.voIsOnSale ? t('products.status.onSale') : t('products.status.offSale')}
                  </Tag>
                </div>
                <div className="console-resource-mobile-card__facts">
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('products.column.type')}</span><strong>{getProductTypeDisplay(product.voProductType, t)}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('products.column.price')}</span><strong>{formatLocalizedNumber(product.voPrice, language)} {t('console.unit.carrot')}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('products.column.stock')}</span><strong>{getProductStockSummary(product, t, language)}</strong>
                  </div>
                </div>
                <div className="console-resource-mobile-card__footer">
                  <Tag color={product.voIsEnabled ? 'success' : 'error'}>
                    {product.voIsEnabled ? t('products.status.enabled') : t('products.status.disabled')}
                  </Tag>
                  {renderProductActions(product)}
                </div>
              </article>
            ))}
            {products.length > 0 ? (
              <div className="console-resource-mobile-pagination">
                <Button size="small" disabled={query.pageIndex <= 1 || loading} onClick={() => handlePageChange(query.pageIndex - 1)}>{t('products.list.previous')}</Button>
                <span>{t('products.list.page', { current: query.pageIndex, total: pageCount })}</span>
                <Button size="small" disabled={query.pageIndex >= pageCount || loading} onClick={() => handlePageChange(query.pageIndex + 1)}>{t('products.list.next')}</Button>
              </div>
            ) : null}
          </>
        )}
        context={(
          <>
            <h3>{t('products.summary.title')}</h3>
            <p className="admin-feature-subtle">{t('products.summary.description')}</p>
            <div className="admin-table-summary">
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('products.summary.scope')}</span>
                <span className="admin-table-summary__value">{activeFilterCount > 0 ? t('products.summary.filterCount', { count: activeFilterCount }) : t('products.summary.allProducts')}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('products.summary.category')}</span>
                <span className="admin-table-summary__value">{currentCategoryName || t('products.summary.allCategories')}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('products.summary.writeAuthority')}</span>
                <span className="admin-table-summary__value">{t(actionsAreAuthoritative ? 'products.summary.authorityReady' : 'products.summary.authorityFrozen')}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('products.summary.stockWatch')}</span>
                <span className="admin-table-summary__value">{t('products.list.productCount', { count: stockWatchProducts })}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('products.summary.salePermission')}</span>
                <span className="admin-table-summary__value">{canToggleProductSale ? t('products.summary.canToggleSale') : t('products.summary.viewSaleOnly')}</span>
              </div>
            </div>

            <div className="admin-table-summary">
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('products.summary.current')}</span>
                <span className="admin-table-summary__value">{primaryProduct?.voName ?? t('products.summary.notSelected')}</span>
              </div>
              {primaryProduct ? (
                <>
                  <div className="admin-table-summary__item">
                    <span className="admin-table-summary__label">{t('products.summary.typeStock')}</span>
                    <span className="admin-table-summary__value">{getProductTypeDisplay(primaryProduct.voProductType, t)} · {getProductStockSummary(primaryProduct, t, language)}</span>
                  </div>
                  <div className="admin-table-summary__item">
                    <span className="admin-table-summary__label">{t('products.summary.priceSold')}</span>
                    <span className="admin-table-summary__value">{t('products.summary.priceSoldValue', { price: formatLocalizedNumber(primaryProduct.voPrice, language), sold: formatLocalizedNumber(primaryProduct.voSoldCount, language) })}</span>
                  </div>
                  {primaryProductUnsupportedReason ? (
                    <div className="admin-feature-inline-context"><strong>{t('products.summary.saleRestriction')}</strong><span>{primaryProductUnsupportedReason}</span></div>
                  ) : null}
                  <div className="admin-feature-rail__actions">
                    <Button size="small" onClick={() => handleOpenDetail(primaryProduct.voId, primaryProduct, true)}>{t('products.action.viewDetail')}</Button>
                    {canViewOrders ? <Button size="small" onClick={() => handleViewOrders(primaryProduct)}>{t('products.action.orders')}</Button> : null}
                    {canEditProduct ? <Button size="small" disabled={!actionsAreAuthoritative || !metadataIsAuthoritative || busyAction !== undefined} onClick={() => handleEditProduct(primaryProduct)}>{t('products.action.edit')}</Button> : null}
                    {canToggleProductSale ? (
                      <Button
                        size="small"
                        disabled={!actionsAreAuthoritative || busyAction !== undefined || (!primaryProduct.voIsOnSale && (!metadataIsAuthoritative || !!primaryProductUnsupportedReason))}
                        onClick={() => handleToggleSale(primaryProduct)}
                      >
                        {primaryProduct.voIsOnSale ? t('products.action.takeOffSale') : t('products.action.putOnSale')}
                      </Button>
                    ) : null}
                  </div>
                </>
              ) : <p className="admin-feature-rail__empty">{t('products.summary.empty')}</p>}
            </div>
          </>
        )}
      />

      <BottomSheet
        isOpen={filterSheetOpen}
        onClose={() => setFilterSheetOpen(false)}
        closeLabel={t('products.list.closeFilter')}
        title={t('products.list.filterTitle')}
        height="auto"
        className="console-resource-filter-sheet"
      >
        {filterControls}
      </BottomSheet>

      <ProductDetail
        visible={detailVisible}
        productId={selectedProductId}
        fallbackProduct={selectedProductSnapshot}
        capabilities={capabilities}
        reloadToken={detailReloadToken}
        onClose={handleCloseDetail}
        onEdit={canEditProduct && actionsAreAuthoritative && metadataIsAuthoritative ? handleEditProduct : undefined}
        onViewOrders={canViewOrders ? handleViewOrders : undefined}
        onReturnToSource={queryReturnTo ? handleReturnToSource : undefined}
      />

      <ProductForm
        visible={formVisible}
        product={editingProduct}
        canSubmit={(editingProduct ? canEditProduct : canCreateProduct) && actionsAreAuthoritative && metadataIsAuthoritative}
        onClose={() => {
          setFormVisible(false);
          setEditingProduct(undefined);
        }}
        onSuccess={() => {
          void loadProducts();
          if (editingProduct && String(editingProduct.voId) === String(selectedProductId)) {
            setDetailReloadToken((current) => current + 1);
          }
        }}
      />

      <ConfirmDialog
        isOpen={saleConfirmProduct !== undefined}
        title={t(saleConfirmProduct?.voIsOnSale ? 'products.saleConfirm.takeOffTitle' : 'products.saleConfirm.putOnTitle')}
        message={t(saleConfirmProduct?.voIsOnSale ? 'products.saleConfirm.takeOffMessage' : 'products.saleConfirm.putOnMessage', { name: saleConfirmProduct?.voName ?? '' })}
        confirmText={t(saleConfirmProduct?.voIsOnSale ? 'products.action.takeOffSale' : 'products.action.putOnSale')}
        cancelText={t('products.form.cancel')}
        onConfirm={handleConfirmToggleSale}
        onCancel={() => setSaleConfirmProduct(undefined)}
      />

      <ConfirmDialog
        isOpen={deleteConfirmVisible}
        title={t('products.delete.title')}
        message={t('products.delete.message', { name: deletingProduct?.voName ?? '' })}
        confirmText={t('products.action.delete')}
        cancelText={t('products.form.cancel')}
        onConfirm={handleConfirmDelete}
        onCancel={() => {
          setDeleteConfirmVisible(false);
          setDeletingProduct(undefined);
        }}
      />
    </div>
  );
};

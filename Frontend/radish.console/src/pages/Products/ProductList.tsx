import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  TableSkeleton,
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
  normalizeProductReturnTo,
  parseProductBooleanQuery,
  parseProductLongIdQuery,
} from './productListUrlState';
import { buildOrderSearchParams } from '../Orders/orderListUrlState';
import { getAvatarUrl } from '../../config/env';
import { log } from '../../utils/logger';
import '../adminFeature.css';
import './ProductList.css';

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
  const queryProductId = parseProductLongIdQuery(urlSearchParams.get('productId'));
  const queryOpenDetail = parseProductBooleanQuery(urlSearchParams.get('openDetail'));
  const queryReturnTo = normalizeProductReturnTo(urlSearchParams.get('returnTo'));
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<ProductCategory[]>([]);
  const [capabilities, setCapabilities] = useState<ShopProductCapability[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const canViewProducts = usePermission(CONSOLE_PERMISSIONS.productsView);
  const canCreateProduct = usePermission(CONSOLE_PERMISSIONS.productsCreate);
  const canEditProduct = usePermission(CONSOLE_PERMISSIONS.productsEdit);
  const canDeleteProductPermission = usePermission(CONSOLE_PERMISSIONS.productsDelete);
  const canToggleProductSale = usePermission(CONSOLE_PERMISSIONS.productsToggleSale);
  const canViewOrders = usePermission(CONSOLE_PERMISSIONS.ordersView);

  const [formVisible, setFormVisible] = useState(false);
  const [editingProduct, setEditingProduct] = useState<Product | undefined>();

  const [detailVisible, setDetailVisible] = useState(false);
  const [selectedProductId, setSelectedProductId] = useState<string | undefined>();
  const [selectedProductSnapshot, setSelectedProductSnapshot] = useState<Product | undefined>();
  const [detailReloadToken, setDetailReloadToken] = useState(0);
  const [deletingProduct, setDeletingProduct] = useState<Product | undefined>();
  const [deleteConfirmVisible, setDeleteConfirmVisible] = useState(false);

  const [draftCategoryId, setDraftCategoryId] = useState<string | undefined>();
  const [draftProductType, setDraftProductType] = useState<ProductType | undefined>();
  const [draftIsOnSale, setDraftIsOnSale] = useState<boolean | undefined>();
  const [draftKeyword, setDraftKeyword] = useState('');

  const [searchParams, setSearchParams] = useState<{
    categoryId?: string;
    productType?: ProductType;
    isOnSale?: boolean;
    keyword: string;
  }>({
    categoryId: undefined,
    productType: undefined,
    isOnSale: undefined,
    keyword: '',
  });
  const activeFilterCount = [
    searchParams.categoryId,
    searchParams.productType !== undefined ? 'productType' : undefined,
    searchParams.isOnSale !== undefined ? 'saleStatus' : undefined,
    searchParams.keyword.trim() ? 'keyword' : undefined,
  ].filter(Boolean).length;
  const currentCategoryName = categories.find((item) => String(item.voId) === String(searchParams.categoryId))?.voName;
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

  const syncDetailSearchParams = (
    productId?: string,
    openDetail?: boolean,
    replace: boolean = false,
    returnTo?: string | null,
  ) => {
    setUrlSearchParams(buildProductDetailSearchParams({ productId, openDetail, returnTo }), { replace });
  };

  const loadProducts = async () => {
    try {
      setLoading(true);
      const response = await adminGetProducts({
        categoryId: searchParams.categoryId,
        productType: searchParams.productType,
        isOnSale: searchParams.isOnSale,
        keyword: searchParams.keyword || undefined,
        pageIndex,
        pageSize,
      }, t);

      setProducts(response.data);
      setTotal(response.dataCount);
      setSelectedProductSnapshot((current) => current
        ? response.data.find((item) => String(item.voId) === String(current.voId)) ?? current
        : current);
    } catch (error) {
      log.error('ProductList', '加载商品列表失败:', error);
      message.error(error instanceof Error ? error.message : t('products.list.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  const loadMetadata = async () => {
    try {
      const [categoryData, capabilityData] = await Promise.all([
        getCategories(t),
        getProductCapabilities(t),
      ]);
      setCategories(categoryData);
      setCapabilities(capabilityData);
    } catch (error) {
      log.error('ProductList', '加载商品元数据失败:', error);
      setCapabilities([]);
      message.error(error instanceof Error ? error.message : t('products.list.capabilityLoadFailed'));
    }
  };

  useEffect(() => {
    if (!canViewProducts) {
      return;
    }

    void loadMetadata();
    // Metadata loading follows the current language so server fallbacks honor Accept-Language.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canViewProducts, language]);

  useEffect(() => {
    if (!canViewProducts) {
      return;
    }

    void loadProducts();
    // Product list loading is scoped to table state; memoizing loadProducts would pull transient detail state into the effect.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageIndex, pageSize, searchParams, canViewProducts]);

  useEffect(() => {
    if (!queryOpenDetail || !queryProductId) {
      return;
    }

    setSelectedProductId(queryProductId);
    setSelectedProductSnapshot(products.find((item) => String(item.voId) === queryProductId));
    setDetailVisible(true);
  }, [products, queryOpenDetail, queryProductId]);

  const handleSearch = () => {
    setPageIndex(1);
    setSearchParams({
      categoryId: draftCategoryId,
      productType: draftProductType,
      isOnSale: draftIsOnSale,
      keyword: draftKeyword.trim(),
    });
  };

  const handleReset = () => {
    setDraftCategoryId(undefined);
    setDraftProductType(undefined);
    setDraftIsOnSale(undefined);
    setDraftKeyword('');
    setPageIndex(1);
    setSearchParams({
      categoryId: undefined,
      productType: undefined,
      isOnSale: undefined,
      keyword: '',
    });
  };

  const handleOpenDetail = (productId: string, product?: Product, syncQuery: boolean = false) => {
    setSelectedProductId(productId);
    setSelectedProductSnapshot(product);
    setDetailVisible(true);

    if (syncQuery) {
      syncDetailSearchParams(productId, true, false, queryReturnTo);
    }
  };

  const handleCloseDetail = () => {
    setDetailVisible(false);
    setSelectedProductId(undefined);
    setSelectedProductSnapshot(undefined);

    if (queryOpenDetail || queryProductId || queryReturnTo) {
      syncDetailSearchParams(undefined, false, true);
    }
  };

  const handleToggleSale = async (product: Product) => {
    try {
      if (product.voIsOnSale) {
        await takeOffSale(product.voId, product.voVersion, t);
        message.success(t('products.list.takeOffSaleSuccess'));
      } else {
        await putOnSale(product.voId, product.voVersion, t);
        message.success(t('products.list.putOnSaleSuccess'));
      }

      await loadProducts();
      if (String(selectedProductId) === String(product.voId)) {
        setDetailReloadToken((current) => current + 1);
      }
    } catch (error) {
      log.error('ProductList', '上架/下架失败:', error);
      message.error(error instanceof Error ? error.message : t('products.list.saleChangeFailed'));
    }
  };

  const handleDelete = (product: Product) => {
    setDeletingProduct(product);
    setDeleteConfirmVisible(true);
  };

  const handleConfirmDelete = async () => {
    if (!deletingProduct) {
      return;
    }

    try {
      await deleteProduct(deletingProduct.voId, t);
      message.success(t('products.list.deleteSuccess'));
      await loadProducts();

      if (String(selectedProductId) === String(deletingProduct.voId)) {
        handleCloseDetail();
      }
    } catch (error) {
      log.error('ProductList', '删除商品失败:', error);
      message.error(error instanceof Error ? error.message : t('products.list.deleteFailed'));
    } finally {
      setDeleteConfirmVisible(false);
      setDeletingProduct(undefined);
    }
  };

  const handleViewOrders = (product: Product) => {
    const productId = String(product.voId);
    const returnTo = buildProductDetailReturnTo({
      productId,
      returnTo: queryReturnTo,
    });

    const searchParams = buildOrderSearchParams({
      productId,
      returnTo,
    });

    navigate(`/orders?${searchParams.toString()}`);
  };

  const handleReturnToSource = () => {
    if (queryReturnTo) {
      navigate(queryReturnTo);
    }
  };

  const handleEditProduct = (product: Product) => {
    setEditingProduct(product);
    setFormVisible(true);
  };

  const columns: TableColumnsType<Product> = [
    {
      title: 'ID',
      dataIndex: 'voId',
      key: 'voId',
      width: 80,
    },
    {
      title: t('products.column.image'),
      dataIndex: 'voCoverImage',
      key: 'voCoverImage',
      width: 100,
      render: (coverImage: string, record: Product) => (
        <Image
          src={getAvatarUrl(coverImage || record.voIcon) || '/placeholder.png'}
          alt={record.voName}
          width={60}
          height={60}
          className="product-list-image"
          fallback="/placeholder.png"
        />
      ),
    },
    {
      title: t('products.column.name'),
      dataIndex: 'voName',
      key: 'voName',
      width: 220,
      render: (name: string, record: Product) => (
        <div className="product-list-name">
          <div className="product-list-name__title">{name}</div>
          <div className="product-list-name__meta">
            {record.voCategoryName || record.voCategoryId}
          </div>
        </div>
      ),
    },
    {
      title: t('products.column.type'),
      dataIndex: 'voProductType',
      key: 'voProductType',
      width: 100,
      render: (type: ProductType) => (
        <Tag color="blue">{getProductTypeDisplay(type, t)}</Tag>
      ),
    },
    {
      title: t('products.column.price'),
      dataIndex: 'voPrice',
      key: 'voPrice',
      width: 120,
      render: (price: number, record: Product) => (
        <div className="product-list-price">
          <div className="product-list-price__current">
            {formatLocalizedNumber(price, language)} {t('console.unit.carrot')}
          </div>
          {record.voOriginalPrice && record.voOriginalPrice > price ? (
            <div className="product-list-price__original">
              {formatLocalizedNumber(record.voOriginalPrice, language)} {t('console.unit.carrot')}
            </div>
          ) : null}
        </div>
      ),
    },
    {
      title: t('products.column.stock'),
      dataIndex: 'voStock',
      key: 'voStock',
      width: 100,
      render: (stock: number, record: Product) => (
        isUnlimitedStock(record)
          ? <Tag color="green">{t('products.common.unlimited')}</Tag>
          : <span className={stock > 0 ? 'product-list-stock product-list-stock--available' : 'product-list-stock product-list-stock--empty'}>{formatLocalizedNumber(stock, language)}</span>
      ),
    },
    {
      title: t('products.column.sold'),
      dataIndex: 'voSoldCount',
      key: 'voSoldCount',
      width: 80,
      render: (sold: number) => formatLocalizedNumber(sold, language),
    },
    {
      title: t('products.column.status'),
      key: 'status',
      width: 120,
      render: (_: unknown, record: Product) => {
        const unsupportedStatusLabel = getUnsupportedSaleStatusLabel(record, capabilities, t);

        return (
          <Space orientation="vertical" size="small">
            <Tag color={record.voIsOnSale ? 'success' : 'default'}>
              {record.voIsOnSale ? t('products.status.onSale') : t('products.status.offSale')}
            </Tag>
            {unsupportedStatusLabel ? (
              <Tag color={record.voIsOnSale ? 'warning' : 'processing'}>
                {unsupportedStatusLabel}
              </Tag>
            ) : null}
            <Tag color={record.voIsEnabled ? 'success' : 'error'}>
              {record.voIsEnabled ? t('products.status.enabled') : t('products.status.disabled')}
            </Tag>
          </Space>
        );
      },
    },
    {
      title: t('products.column.action'),
      key: 'action',
      width: 320,
      fixed: 'right',
      render: (_: unknown, record: Product) => {
        const unsupportedSaleReason = getUnsupportedSaleReason(record, capabilities, t);
        const saleBlockReason = !record.voIsOnSale ? unsupportedSaleReason : null;
        const toggleButtonTitle = record.voIsOnSale && unsupportedSaleReason
          ? t('products.action.historicalOnSaleHint')
          : saleBlockReason ?? undefined;

        return (
          <Space size="small" wrap>
            <Button
              variant="ghost"
              size="small"
              icon={<EyeOutlined />}
              onClick={() => handleOpenDetail(record.voId, record, true)}
            >
              {t('products.action.detail')}
            </Button>
            {canViewOrders ? (
              <Button
                variant="ghost"
                size="small"
                onClick={() => handleViewOrders(record)}
              >
                {t('products.action.orders')}
              </Button>
            ) : null}
            {canEditProduct ? (
              <Button
                variant="ghost"
                size="small"
                icon={<EditOutlined />}
                onClick={() => handleEditProduct(record)}
              >
                {t('products.action.edit')}
              </Button>
            ) : null}
            {canToggleProductSale ? (
              <Button
                variant="ghost"
                size="small"
                onClick={() => handleToggleSale(record)}
                disabled={!!saleBlockReason}
                title={toggleButtonTitle}
              >
                {record.voIsOnSale ? t('products.action.takeOffSale') : t('products.action.putOnSale')}
              </Button>
            ) : null}
            {canDeleteProductPermission ? (
              <Button
                variant="danger"
                size="small"
                icon={<DeleteOutlined />}
                onClick={() => handleDelete(record)}
              >
                {t('products.action.delete')}
              </Button>
            ) : null}
          </Space>
        );
      },
    },
  ];

  if (loading && products.length === 0) {
    return <TableSkeleton rows={10} columns={6} showFilters={true} showActions={true} />;
  }

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
        actions={canCreateProduct ? (
          <Button
            variant="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              setEditingProduct(undefined);
              setFormVisible(true);
            }}
          >
            {t('products.action.create')}
          </Button>
        ) : undefined}
      />

      <ConsoleMetricGrid label={t('products.list.metrics.label')}>
        <ConsoleMetricCard label={t('products.list.metrics.results')} value={formatLocalizedNumber(total, language)} description={t('products.list.metrics.resultsDescription')} tone="info" />
        <ConsoleMetricCard label={t('products.list.metrics.page')} value={formatLocalizedNumber(products.length, language)} description={t('products.list.metrics.pageDescription')} />
        <ConsoleMetricCard label={t('products.list.metrics.onSale')} value={formatLocalizedNumber(onSaleProducts, language)} description={t('products.list.metrics.onSaleDescription')} tone="success" />
        <ConsoleMetricCard label={t('products.list.metrics.enabled')} value={formatLocalizedNumber(enabledProducts, language)} description={t('products.list.metrics.enabledDescription')} tone="success" />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label={t('products.list.flow.label')}>
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>{t('products.list.flow.poolTitle')}</strong>
          <p>{t('products.list.flow.pool', { total: formatLocalizedNumber(total, language), visible: formatLocalizedNumber(products.length, language) })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>{t('products.list.flow.saleTitle')}</strong>
          <p>{t('products.list.flow.sale', { onSale: formatLocalizedNumber(onSaleProducts, language), unavailable: formatLocalizedNumber(unsupportedSaleProducts, language) })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>{t('products.list.flow.stockTitle')}</strong>
          <p>{t('products.list.flow.stock', { sold: formatLocalizedNumber(soldUnitsOnPage, language), watch: formatLocalizedNumber(stockWatchProducts, language) })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>{t('products.list.flow.orderTitle')}</strong>
          <p>{primaryProduct ? t('products.list.flow.orderSelected', { name: primaryProduct.voName }) : t('products.list.flow.orderEmpty')}</p>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('products.list.toolbar.title')}
            description={t('products.list.toolbar.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0
                  ? t('products.list.filterCount', { count: activeFilterCount })
                  : t('products.list.noFilters')}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Select
                className="product-list-filter-select"
                placeholder={t('products.list.filter.category')}
                allowClear
                value={draftCategoryId}
                onChange={setDraftCategoryId}
              >
                {categories.map((cat) => (
                  <Select.Option key={cat.voId} value={cat.voId}>
                    {cat.voName}
                  </Select.Option>
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
                placeholder={t('products.list.filter.keyword')}
                value={draftKeyword}
                onChange={(e) => setDraftKeyword(e.target.value)}
                onPressEnter={handleSearch}
                suffix={<SearchOutlined />}
              />

              <Button variant="primary" icon={<SearchOutlined />} onClick={handleSearch}>
                {t('products.list.search')}
              </Button>

              <Button icon={<ReloadOutlined />} onClick={handleReset}>
                {t('products.list.reset')}
              </Button>
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table
              columns={columns}
              dataSource={products}
              rowKey="voId"
              loading={loading}
              pagination={{
                current: pageIndex,
                pageSize: pageSize,
                total: total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (itemTotal) => t('products.list.pagination', {
                  count: itemTotal,
                  formattedCount: formatLocalizedNumber(itemTotal, language),
                }),
                onChange: (page, size) => {
                  setPageIndex(page);
                  setPageSize(size);
                },
              }}
              scroll={{ x: 1520 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('products.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('products.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('products.summary.scope')}</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0
                  ? t('products.summary.filterCount', { count: activeFilterCount })
                  : t('products.summary.allProducts')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('products.summary.category')}</span>
              <span className="admin-table-summary__value">{currentCategoryName || t('products.summary.allCategories')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('products.summary.stockWatch')}</span>
              <span className="admin-table-summary__value">{t('products.list.productCount', { count: stockWatchProducts })}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('products.summary.salePermission')}</span>
              <span className="admin-table-summary__value">
                {canToggleProductSale
                  ? t('products.summary.canToggleSale')
                  : t('products.summary.viewSaleOnly')}
              </span>
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
                  <span className="admin-table-summary__value">
                    {getProductTypeDisplay(primaryProduct.voProductType, t)} · {getProductStockSummary(primaryProduct, t, language)}
                  </span>
                </div>
                <div className="admin-table-summary__item">
                  <span className="admin-table-summary__label">{t('products.summary.priceSold')}</span>
                  <span className="admin-table-summary__value">
                    {t('products.summary.priceSoldValue', {
                      price: formatLocalizedNumber(primaryProduct.voPrice, language),
                      sold: formatLocalizedNumber(primaryProduct.voSoldCount, language),
                    })}
                  </span>
                </div>
                {primaryProductUnsupportedReason ? (
                  <div className="admin-feature-inline-context">
                    <strong>{t('products.summary.saleRestriction')}</strong>
                    <span>{primaryProductUnsupportedReason}</span>
                  </div>
                ) : null}
                <div className="admin-feature-rail__actions">
                  <Button size="small" onClick={() => handleOpenDetail(primaryProduct.voId, primaryProduct, true)}>
                    {t('products.action.viewDetail')}
                  </Button>
                  {canViewOrders ? (
                    <Button size="small" onClick={() => handleViewOrders(primaryProduct)}>
                      {t('products.action.orders')}
                    </Button>
                  ) : null}
                  {canEditProduct ? (
                    <Button size="small" onClick={() => handleEditProduct(primaryProduct)}>
                      {t('products.action.edit')}
                    </Button>
                  ) : null}
                  {canToggleProductSale ? (
                    <Button
                      size="small"
                      disabled={!primaryProduct.voIsOnSale && !!primaryProductUnsupportedReason}
                      onClick={() => {
                        void handleToggleSale(primaryProduct);
                      }}
                    >
                      {primaryProduct.voIsOnSale
                        ? t('products.action.takeOffSale')
                        : t('products.action.putOnSale')}
                    </Button>
                  ) : null}
                </div>
              </>
            ) : (
              <p className="admin-feature-rail__empty">{t('products.summary.empty')}</p>
            )}
          </div>
        </aside>
      </div>

      <ProductDetail
        visible={detailVisible}
        productId={selectedProductId}
        fallbackProduct={selectedProductSnapshot}
        capabilities={capabilities}
        reloadToken={detailReloadToken}
        onClose={handleCloseDetail}
        onEdit={canEditProduct ? handleEditProduct : undefined}
        onViewOrders={canViewOrders ? handleViewOrders : undefined}
        onReturnToSource={queryReturnTo ? handleReturnToSource : undefined}
      />

      <ProductForm
        visible={formVisible}
        product={editingProduct}
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
        isOpen={deleteConfirmVisible}
        title={t('products.delete.title')}
        message={t('products.delete.message', { name: deletingProduct?.voName ?? '' })}
        onConfirm={handleConfirmDelete}
        onCancel={() => {
          setDeleteConfirmVisible(false);
          setDeletingProduct(undefined);
        }}
      />
    </div>
  );
};

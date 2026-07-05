import { useEffect, useState } from 'react';
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
import type { Product, ProductCategory } from '../../api/types';
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

function getProductStockSummary(product: Product): string {
  return isUnlimitedStock(product) ? '无限库存' : `${product.voStock} 件库存`;
}

export const ProductList = () => {
  useDocumentTitle('商品管理');
  const navigate = useNavigate();
  const [urlSearchParams, setUrlSearchParams] = useSearchParams();
  const queryProductId = parseProductLongIdQuery(urlSearchParams.get('productId'));
  const queryOpenDetail = parseProductBooleanQuery(urlSearchParams.get('openDetail'));
  const queryReturnTo = normalizeProductReturnTo(urlSearchParams.get('returnTo'));
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<ProductCategory[]>([]);
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
  const unsupportedSaleProducts = products.filter((product) => getUnsupportedSaleReason(product)).length;
  const soldUnitsOnPage = products.reduce((sum, product) => sum + product.voSoldCount, 0);
  const primaryProduct = selectedProductSnapshot ?? products[0] ?? null;
  const primaryProductUnsupportedReason = primaryProduct ? getUnsupportedSaleReason(primaryProduct) : null;

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
      });

      setProducts(response.data);
      setTotal(response.dataCount);
      setSelectedProductSnapshot((current) => current
        ? response.data.find((item) => String(item.voId) === String(current.voId)) ?? current
        : current);
    } catch (error) {
      log.error('ProductList', '加载商品列表失败:', error);
      message.error('加载商品列表失败');
    } finally {
      setLoading(false);
    }
  };

  const loadCategories = async () => {
    try {
      const data = await getCategories();
      setCategories(data);
    } catch (error) {
      log.error('ProductList', '加载分类列表失败:', error);
    }
  };

  useEffect(() => {
    if (!canViewProducts) {
      return;
    }

    void loadCategories();
  }, [canViewProducts]);

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
        await takeOffSale(product.voId, product.voVersion);
        message.success('下架成功');
      } else {
        await putOnSale(product.voId, product.voVersion);
        message.success('上架成功');
      }

      await loadProducts();
      if (String(selectedProductId) === String(product.voId)) {
        setDetailReloadToken((current) => current + 1);
      }
    } catch (error) {
      log.error('ProductList', '上架/下架失败:', error);
      message.error(error instanceof Error ? error.message : '操作失败');
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
      await deleteProduct(deletingProduct.voId);
      message.success('删除成功');
      await loadProducts();

      if (String(selectedProductId) === String(deletingProduct.voId)) {
        handleCloseDetail();
      }
    } catch (error) {
      log.error('ProductList', '删除商品失败:', error);
      message.error(error instanceof Error ? error.message : '删除失败');
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
      title: '商品图片',
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
      title: '商品名称',
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
      title: '类型',
      dataIndex: 'voProductType',
      key: 'voProductType',
      width: 100,
      render: (type: ProductType) => (
        <Tag color="blue">{getProductTypeDisplay(type)}</Tag>
      ),
    },
    {
      title: '价格',
      dataIndex: 'voPrice',
      key: 'voPrice',
      width: 120,
      render: (price: number, record: Product) => (
        <div className="product-list-price">
          <div className="product-list-price__current">
            {price} 胡萝卜
          </div>
          {record.voOriginalPrice && record.voOriginalPrice > price ? (
            <div className="product-list-price__original">
              {record.voOriginalPrice} 胡萝卜
            </div>
          ) : null}
        </div>
      ),
    },
    {
      title: '库存',
      dataIndex: 'voStock',
      key: 'voStock',
      width: 100,
      render: (stock: number, record: Product) => (
        record.voStockType === 'Unlimited'
          ? <Tag color="green">无限</Tag>
          : <span className={stock > 0 ? 'product-list-stock product-list-stock--available' : 'product-list-stock product-list-stock--empty'}>{stock}</span>
      ),
    },
    {
      title: '已售',
      dataIndex: 'voSoldCount',
      key: 'voSoldCount',
      width: 80,
    },
    {
      title: '状态',
      key: 'status',
      width: 120,
      render: (_: unknown, record: Product) => {
        const unsupportedStatusLabel = getUnsupportedSaleStatusLabel(record);

        return (
          <Space orientation="vertical" size="small">
            <Tag color={record.voIsOnSale ? 'success' : 'default'}>
              {record.voIsOnSale ? '已上架' : '已下架'}
            </Tag>
            {unsupportedStatusLabel ? (
              <Tag color={record.voIsOnSale ? 'warning' : 'processing'}>
                {unsupportedStatusLabel}
              </Tag>
            ) : null}
            <Tag color={record.voIsEnabled ? 'success' : 'error'}>
              {record.voIsEnabled ? '启用' : '禁用'}
            </Tag>
          </Space>
        );
      },
    },
    {
      title: '操作',
      key: 'action',
      width: 320,
      fixed: 'right',
      render: (_: unknown, record: Product) => {
        const unsupportedSaleReason = getUnsupportedSaleReason(record);
        const saleBlockReason = !record.voIsOnSale ? unsupportedSaleReason : null;
        const toggleButtonTitle = record.voIsOnSale && unsupportedSaleReason
          ? '当前商品属于未开放类型，建议先下架历史上架记录'
          : saleBlockReason ?? undefined;

        return (
          <Space size="small" wrap>
            <Button
              variant="ghost"
              size="small"
              icon={<EyeOutlined />}
              onClick={() => handleOpenDetail(record.voId, record, true)}
            >
              详情
            </Button>
            {canViewOrders ? (
              <Button
                variant="ghost"
                size="small"
                onClick={() => handleViewOrders(record)}
              >
                相关订单
              </Button>
            ) : null}
            {canEditProduct ? (
              <Button
                variant="ghost"
                size="small"
                icon={<EditOutlined />}
                onClick={() => handleEditProduct(record)}
              >
                编辑
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
                {record.voIsOnSale ? '下架' : '上架'}
              </Button>
            ) : null}
            {canDeleteProductPermission ? (
              <Button
                variant="danger"
                size="small"
                icon={<DeleteOutlined />}
                onClick={() => handleDelete(record)}
              >
                删除
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
        eyebrow="COMMERCE CATALOG"
        title="商品管理"
        description="维护商城商品、上架状态、库存与关联订单入口。"
        icon={<ShoppingOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateProduct ? 'success' : 'neutral'}>
            {canCreateProduct ? '可新建' : '只读'}
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
            新建商品
          </Button>
        ) : undefined}
      />

      <ConsoleMetricGrid label="商品列表指标">
        <ConsoleMetricCard label="当前结果" value={total} description="当前筛选后的商品数量" tone="info" />
        <ConsoleMetricCard label="本页商品" value={products.length} description="当前页可见商品" />
        <ConsoleMetricCard label="本页上架" value={onSaleProducts} description="当前页已上架商品" tone="success" />
        <ConsoleMetricCard label="本页启用" value={enabledProducts} description="当前页启用商品" tone="success" />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label="商品运营任务流">
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>商品池</strong>
          <p>{total} 个筛选结果，当前页 {products.length} 个商品。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>上架状态</strong>
          <p>{onSaleProducts} 个已上架，{unsupportedSaleProducts} 个存在未开放类型提示。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>库存售卖</strong>
          <p>本页已售 {soldUnitsOnPage} 件，{stockWatchProducts} 个商品需要库存关注。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>订单回流</strong>
          <p>{primaryProduct ? `当前可回看 ${primaryProduct.voName} 的相关订单。` : '选择商品后可进入相关订单。'}</p>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title="筛选商品"
            description="按分类、商品类型、上架状态或商品名称定位商城条目。"
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Select
                className="product-list-filter-select"
                placeholder="选择分类"
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
                placeholder="商品类型"
                allowClear
                value={draftProductType}
                onChange={setDraftProductType}
              >
                <Select.Option value={1}>权益</Select.Option>
                <Select.Option value={2}>消耗品</Select.Option>
                <Select.Option value={99}>实物</Select.Option>
              </Select>

              <Select
                className="product-list-filter-select product-list-filter-select--sale"
                placeholder="上架状态"
                allowClear
                value={draftIsOnSale}
                onChange={setDraftIsOnSale}
              >
                <Select.Option value={true}>已上架</Select.Option>
                <Select.Option value={false}>已下架</Select.Option>
              </Select>

              <Input
                className="product-list-filter-input"
                placeholder="搜索商品名称"
                value={draftKeyword}
                onChange={(e) => setDraftKeyword(e.target.value)}
                onPressEnter={handleSearch}
                suffix={<SearchOutlined />}
              />

              <Button variant="primary" icon={<SearchOutlined />} onClick={handleSearch}>
                搜索
              </Button>

              <Button icon={<ReloadOutlined />} onClick={handleReset}>
                重置
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
                showTotal: (itemTotal) => `共 ${itemTotal} 条`,
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
          <h3>商业运营摘要</h3>
          <p className="admin-feature-subtle">核对商品上架、库存、版本动作和订单回流，不扩展新的资产风控能力。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '全部商品'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">分类范围</span>
              <span className="admin-table-summary__value">{currentCategoryName || '全部分类'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">库存关注</span>
              <span className="admin-table-summary__value">{stockWatchProducts} 个商品</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">上架权限</span>
              <span className="admin-table-summary__value">
                {canToggleProductSale ? '可上架 / 下架' : '仅可查看上架状态'}
              </span>
            </div>
          </div>

          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">当前商品</span>
              <span className="admin-table-summary__value">{primaryProduct?.voName ?? '未选择商品'}</span>
            </div>
            {primaryProduct ? (
              <>
                <div className="admin-table-summary__item">
                  <span className="admin-table-summary__label">类型 / 库存</span>
                  <span className="admin-table-summary__value">
                    {getProductTypeDisplay(primaryProduct.voProductType)} · {getProductStockSummary(primaryProduct)}
                  </span>
                </div>
                <div className="admin-table-summary__item">
                  <span className="admin-table-summary__label">价格 / 已售</span>
                  <span className="admin-table-summary__value">
                    {primaryProduct.voPrice} 胡萝卜 · 已售 {primaryProduct.voSoldCount}
                  </span>
                </div>
                {primaryProductUnsupportedReason ? (
                  <div className="admin-feature-inline-context">
                    <strong>上架限制</strong>
                    <span>{primaryProductUnsupportedReason}</span>
                  </div>
                ) : null}
                <div className="admin-feature-rail__actions">
                  <Button size="small" onClick={() => handleOpenDetail(primaryProduct.voId, primaryProduct, true)}>
                    查看详情
                  </Button>
                  {canViewOrders ? (
                    <Button size="small" onClick={() => handleViewOrders(primaryProduct)}>
                      相关订单
                    </Button>
                  ) : null}
                  {canEditProduct ? (
                    <Button size="small" onClick={() => handleEditProduct(primaryProduct)}>
                      编辑
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
                      {primaryProduct.voIsOnSale ? '下架' : '上架'}
                    </Button>
                  ) : null}
                </div>
              </>
            ) : (
              <p className="admin-feature-rail__empty">当前页暂无商品，调整筛选条件后会形成运营摘要。</p>
            )}
          </div>
        </aside>
      </div>

      <ProductDetail
        visible={detailVisible}
        productId={selectedProductId}
        fallbackProduct={selectedProductSnapshot}
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
        title="确认删除"
        message={`确定要删除商品"${deletingProduct?.voName ?? ''}"吗？若商品已有订单或业务关联，系统会拦截删除。`}
        onConfirm={handleConfirmDelete}
        onCancel={() => {
          setDeleteConfirmVisible(false);
          setDeletingProduct(undefined);
        }}
      />
    </div>
  );
};

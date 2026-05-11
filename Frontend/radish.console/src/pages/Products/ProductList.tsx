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
  Modal,
  type TableColumnsType,
} from '@radish/ui';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  SearchOutlined,
  ReloadOutlined,
  EyeOutlined,
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
import type { Product, ProductCategory } from '../../api/types';
import { ProductType } from '../../api/types';
import { ProductForm } from './ProductForm';
import { ProductDetail } from './ProductDetail';
import {
  getProductTypeDisplay,
  getUnsupportedSaleReason,
  getUnsupportedSaleStatusLabel,
} from './productDisplay';
import { getAvatarUrl } from '../../config/env';
import { log } from '../../utils/logger';
import './ProductList.css';

function parsePositiveIntQuery(value: string | null): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function parseBooleanQuery(value: string | null): boolean {
  return value === '1' || value === 'true';
}

function buildProductDetailSearchParams(productId?: number, openDetail?: boolean): URLSearchParams {
  const searchParams = new URLSearchParams();

  if (productId !== undefined) {
    searchParams.set('productId', productId.toString());
  }

  if (openDetail) {
    searchParams.set('openDetail', '1');
  }

  return searchParams;
}

export const ProductList = () => {
  useDocumentTitle('商品管理');
  const navigate = useNavigate();
  const [urlSearchParams, setUrlSearchParams] = useSearchParams();
  const queryProductId = parsePositiveIntQuery(urlSearchParams.get('productId'));
  const queryOpenDetail = parseBooleanQuery(urlSearchParams.get('openDetail'));
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
  const [selectedProductId, setSelectedProductId] = useState<number | undefined>();
  const [selectedProductSnapshot, setSelectedProductSnapshot] = useState<Product | undefined>();
  const [detailReloadToken, setDetailReloadToken] = useState(0);

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

  const syncDetailSearchParams = (productId?: number, openDetail?: boolean, replace: boolean = false) => {
    setUrlSearchParams(buildProductDetailSearchParams(productId, openDetail), { replace });
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
        ? response.data.find((item) => item.voId === current.voId) ?? current
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
  }, [pageIndex, pageSize, searchParams, canViewProducts]);

  useEffect(() => {
    if (!queryOpenDetail || !queryProductId) {
      return;
    }

    setSelectedProductId(queryProductId);
    setSelectedProductSnapshot(products.find((item) => item.voId === queryProductId));
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

  const handleOpenDetail = (productId: number, product?: Product, syncQuery: boolean = false) => {
    setSelectedProductId(productId);
    setSelectedProductSnapshot(product);
    setDetailVisible(true);

    if (syncQuery) {
      syncDetailSearchParams(productId, true);
    }
  };

  const handleCloseDetail = () => {
    setDetailVisible(false);
    setSelectedProductId(undefined);
    setSelectedProductSnapshot(undefined);

    if (queryOpenDetail || queryProductId) {
      syncDetailSearchParams(undefined, false, true);
    }
  };

  const handleToggleSale = async (product: Product) => {
    try {
      if (product.voIsOnSale) {
        await takeOffSale(product.voId);
        message.success('下架成功');
      } else {
        await putOnSale(product.voId);
        message.success('上架成功');
      }

      await loadProducts();
      if (selectedProductId === product.voId) {
        setDetailReloadToken((current) => current + 1);
      }
    } catch (error) {
      log.error('ProductList', '上架/下架失败:', error);
      message.error(error instanceof Error ? error.message : '操作失败');
    }
  };

  const handleDelete = (product: Product) => {
    (Modal as any).confirm({
      title: '确认删除',
      content: `确定要删除商品"${product.voName}"吗？`,
      onOk: async () => {
        try {
          await deleteProduct(product.voId);
          message.success('删除成功');
          await loadProducts();

          if (selectedProductId === product.voId) {
            handleCloseDetail();
          }
        } catch (error) {
          log.error('ProductList', '删除商品失败:', error);
          message.error(error instanceof Error ? error.message : '删除失败');
        }
      },
    });
  };

  const handleViewOrders = (product: Product) => {
    navigate(`/orders?productId=${product.voId}`);
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
          style={{ objectFit: 'cover', borderRadius: '4px' }}
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
        <div>
          <div>{name}</div>
          <div style={{ fontSize: '12px', color: '#999' }}>
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
        <div>
          <div style={{ fontWeight: 'bold', color: '#ff4d4f' }}>
            {price} 胡萝卜
          </div>
          {record.voOriginalPrice && record.voOriginalPrice > price ? (
            <div style={{ fontSize: '12px', color: '#999', textDecoration: 'line-through' }}>
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
          : <span style={{ color: stock > 0 ? '#52c41a' : '#ff4d4f' }}>{stock}</span>
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
          <Space direction="vertical" size="small">
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
    <div className="product-list-page">
      <div className="page-header">
        <h2>商品管理</h2>
        {canCreateProduct ? (
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
        ) : null}
      </div>

      <div className="filter-bar">
        <Space wrap>
          <Select
            placeholder="选择分类"
            style={{ width: 150 }}
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
            placeholder="商品类型"
            style={{ width: 120 }}
            allowClear
            value={draftProductType}
            onChange={setDraftProductType}
          >
            <Select.Option value={1}>权益</Select.Option>
            <Select.Option value={2}>消耗品</Select.Option>
            <Select.Option value={99}>实物</Select.Option>
          </Select>

          <Select
            placeholder="上架状态"
            style={{ width: 120 }}
            allowClear
            value={draftIsOnSale}
            onChange={setDraftIsOnSale}
          >
            <Select.Option value={true}>已上架</Select.Option>
            <Select.Option value={false}>已下架</Select.Option>
          </Select>

          <Input
            placeholder="搜索商品名称"
            style={{ width: 200 }}
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
        </Space>
      </div>

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

      <ProductDetail
        visible={detailVisible}
        productId={selectedProductId}
        fallbackProduct={selectedProductSnapshot}
        reloadToken={detailReloadToken}
        onClose={handleCloseDetail}
        onEdit={handleEditProduct}
        onViewOrders={canViewOrders ? handleViewOrders : undefined}
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
          if (editingProduct && editingProduct.voId === selectedProductId) {
            setDetailReloadToken((current) => current + 1);
          }
        }}
      />
    </div>
  );
};

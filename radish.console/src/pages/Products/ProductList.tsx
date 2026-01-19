import { useState, useEffect } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { TableSkeleton } from '@/components/TableSkeleton';
import {
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
} from '@radish/ui';
import {
  adminGetProducts,
  getCategories,
  putOnSale,
  takeOffSale,
} from '../../api/shopApi';
import type { Product, ProductCategory } from '../../api/types';
import { ProductType } from '../../api/types';
import { ProductForm } from './ProductForm';
import { log } from '../../utils/logger';
import './ProductList.css';

// 本地工具函数
function getProductTypeDisplay(type: ProductType): string {
  switch (type) {
    case ProductType.Benefit:
      return '权益';
    case ProductType.Consumable:
      return '消耗品';
    case ProductType.Physical:
      return '实物';
    default:
      return '未知';
  }
}

export const ProductList = () => {
  useDocumentTitle('商品管理');
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<ProductCategory[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  // 表单状态
  const [formVisible, setFormVisible] = useState(false);
  const [editingProduct, setEditingProduct] = useState<Product | undefined>();

  // 筛选条件
  const [categoryId, setCategoryId] = useState<string | undefined>();
  const [productType, setProductType] = useState<ProductType | undefined>();
  const [isOnSale, setIsOnSale] = useState<boolean | undefined>();
  const [keyword, setKeyword] = useState<string>('');

  // 加载商品列表
  const loadProducts = async () => {
    try {
      setLoading(true);
      const response = await adminGetProducts({
        categoryId,
        productType,
        isOnSale,
        keyword: keyword || undefined,
        pageIndex,
        pageSize,
      });

      setProducts(response.data);
      setTotal(response.dataCount);
    } catch (error) {
      log.error('ProductList', '加载商品列表失败:', error);
      message.error('加载商品列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 加载分类列表
  const loadCategories = async () => {
    try {
      const data = await getCategories();
      setCategories(data);
    } catch (error) {
      log.error('ProductList', '加载分类列表失败:', error);
    }
  };

  // 初始化
  useEffect(() => {
    loadCategories();
  }, []);

  // 加载商品列表
  useEffect(() => {
    loadProducts();
  }, [pageIndex, pageSize, categoryId, productType, isOnSale]);

  // 搜索
  const handleSearch = () => {
    setPageIndex(1);
    loadProducts();
  };

  // 重置筛选
  const handleReset = () => {
    setCategoryId(undefined);
    setProductType(undefined);
    setIsOnSale(undefined);
    setKeyword('');
    setPageIndex(1);
  };

  // 上架/下架
  const handleToggleSale = async (product: Product) => {
    try {
      if (product.isOnSale) {
        await takeOffSale(product.id);
        message.success('下架成功');
      } else {
        await putOnSale(product.id);
        message.success('上架成功');
      }
      loadProducts();
    } catch (error) {
      log.error('ProductList', '上架/下架失败:', error);
      message.error('操作失败');
    }
  };

  // 删除商品
  const handleDelete = (product: Product) => {
    (Modal as any).confirm({
      title: '确认删除',
      content: `确定要删除商品"${product.name}"吗？`,
      onOk: async () => {
        try {
          // TODO: 实现删除 API
          message.success('删除成功');
          loadProducts();
        } catch (error) {
          log.error('ProductList', '删除商品失败:', error);
          message.error('删除失败');
        }
      },
    });
  };

  // 表格列定义
  const columns: TableColumnsType<Product> = [
    {
      title: 'ID',
      dataIndex: 'id',
      key: 'id',
      width: 80,
    },
    {
      title: '商品图片',
      dataIndex: 'coverImage',
      key: 'coverImage',
      width: 100,
      render: (coverImage: string, record: Product) => (
        <Image
          src={coverImage || record.icon || '/placeholder.png'}
          alt={record.name}
          width={60}
          height={60}
          style={{ objectFit: 'cover', borderRadius: '4px' }}
          fallback="/placeholder.png"
        />
      ),
    },
    {
      title: '商品名称',
      dataIndex: 'name',
      key: 'name',
      width: 200,
    },
    {
      title: '分类',
      dataIndex: 'categoryName',
      key: 'categoryName',
      width: 100,
    },
    {
      title: '类型',
      dataIndex: 'productType',
      key: 'productType',
      width: 100,
      render: (type: ProductType) => (
        <Tag color="blue">{getProductTypeDisplay(type)}</Tag>
      ),
    },
    {
      title: '价格',
      dataIndex: 'price',
      key: 'price',
      width: 120,
      render: (price: number, record: Product) => (
        <div>
          <div style={{ fontWeight: 'bold', color: '#ff4d4f' }}>
            {price} 胡萝卜
          </div>
          {record.originalPrice && record.originalPrice > price && (
            <div style={{ fontSize: '12px', color: '#999', textDecoration: 'line-through' }}>
              {record.originalPrice} 胡萝卜
            </div>
          )}
        </div>
      ),
    },
    {
      title: '库存',
      dataIndex: 'stock',
      key: 'stock',
      width: 100,
      render: (stock: number, record: Product) => (
        <div>
          {record.stockType === 'Unlimited' ? (
            <Tag color="green">无限</Tag>
          ) : (
            <span style={{ color: stock > 0 ? '#52c41a' : '#ff4d4f' }}>
              {stock}
            </span>
          )}
        </div>
      ),
    },
    {
      title: '已售',
      dataIndex: 'soldCount',
      key: 'soldCount',
      width: 80,
    },
    {
      title: '状态',
      key: 'status',
      width: 100,
      render: (_: unknown, record: Product) => (
        <Space direction="vertical" size="small">
          <Tag color={record.isOnSale ? 'success' : 'default'}>
            {record.isOnSale ? '已上架' : '已下架'}
          </Tag>
          <Tag color={record.isEnabled ? 'success' : 'error'}>
            {record.isEnabled ? '启用' : '禁用'}
          </Tag>
        </Space>
      ),
    },
    {
      title: '操作',
      key: 'action',
      width: 200,
      fixed: 'right',
      render: (_: unknown, record: Product) => (
        <Space size="small">
          <Button
            variant="ghost"
            size="small"
            icon={<EditOutlined />}
            onClick={() => {
              setEditingProduct(record);
              setFormVisible(true);
            }}
          >
            编辑
          </Button>
          <Button
            variant="ghost"
            size="small"
            onClick={() => handleToggleSale(record)}
          >
            {record.isOnSale ? '下架' : '上架'}
          </Button>
          <Button
            variant="danger"
            size="small"
            icon={<DeleteOutlined />}
            onClick={() => handleDelete(record)}
          >
            删除
          </Button>
        </Space>
      ),
    },
  ];

  // 如果正在加载且没有数据，显示骨架屏
  if (loading && products.length === 0) {
    return <TableSkeleton rows={10} columns={6} showFilters={true} showActions={true} />;
  }

  return (
    <div className="product-list-page">
      <div className="page-header">
        <h2>商品管理</h2>
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
      </div>

      <div className="filter-bar">
        <Space wrap>
          <Select
            placeholder="选择分类"
            style={{ width: 150 }}
            allowClear
            value={categoryId}
            onChange={setCategoryId}
          >
            {categories.map((cat) => (
              <Select.Option key={cat.id} value={cat.id}>
                {cat.name}
              </Select.Option>
            ))}
          </Select>

          <Select
            placeholder="商品类型"
            style={{ width: 120 }}
            allowClear
            value={productType}
            onChange={setProductType}
          >
            <Select.Option value={1}>权益</Select.Option>
            <Select.Option value={2}>消耗品</Select.Option>
            <Select.Option value={3}>实物</Select.Option>
          </Select>

          <Select
            placeholder="上架状态"
            style={{ width: 120 }}
            allowClear
            value={isOnSale}
            onChange={setIsOnSale}
          >
            <Select.Option value={true}>已上架</Select.Option>
            <Select.Option value={false}>已下架</Select.Option>
          </Select>

          <Input
            placeholder="搜索商品名称"
            style={{ width: 200 }}
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
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
        rowKey="id"
        loading={loading}
        pagination={{
          current: pageIndex,
          pageSize: pageSize,
          total: total,
          showSizeChanger: true,
          showQuickJumper: true,
          showTotal: (total) => `共 ${total} 条`,
          onChange: (page, size) => {
            setPageIndex(page);
            setPageSize(size);
          },
        }}
        scroll={{ x: 1400 }}
      />

      <ProductForm
        visible={formVisible}
        product={editingProduct}
        onClose={() => {
          setFormVisible(false);
          setEditingProduct(undefined);
        }}
        onSuccess={() => {
          loadProducts();
        }}
      />
    </div>
  );
};

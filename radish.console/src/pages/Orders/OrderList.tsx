import { useState, useEffect } from 'react';
import {
  Table,
  Button,
  AntInput as Input,
  AntSelect as Select,
  Space,
  Tag,
  message,
  ConfirmDialog,
  type TableColumnsType,
} from '@radish/ui';
import {
  EyeOutlined,
  ReloadOutlined,
  SearchOutlined,
  SyncOutlined,
} from '@radish/ui';
import {
  adminGetOrders,
  retryGrantBenefit,
  getOrderStatusDisplay,
  getOrderStatusColor,
  getProductTypeDisplay,
} from '../../api/shopApi';
import type { Order, OrderStatus } from '../../api/types';
import { OrderDetail } from './OrderDetail';
import { log } from '../../utils/logger';
import './OrderList.css';

export const OrderList = () => {
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  // 详情弹窗状态
  const [detailVisible, setDetailVisible] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState<Order | undefined>();

  // 确认对话框状态
  const [confirmVisible, setConfirmVisible] = useState(false);
  const [retryOrder, setRetryOrder] = useState<Order | undefined>();

  // 筛选条件
  const [userId, setUserId] = useState<number | undefined>();
  const [status, setStatus] = useState<OrderStatus | undefined>();
  const [productId, setProductId] = useState<number | undefined>();
  const [orderNo, setOrderNo] = useState<string>('');

  // 加载订单列表
  const loadOrders = async () => {
    try {
      setLoading(true);
      const response = await adminGetOrders({
        userId,
        status,
        productId,
        orderNo: orderNo || undefined,
        pageIndex,
        pageSize,
      });

      setOrders(response.data);
      setTotal(response.dataCount);
    } catch (error) {
      log.error('OrderList', '加载订单列表失败:', error);
      message.error('加载订单列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 初始化和筛选条件变化时加载
  useEffect(() => {
    loadOrders();
  }, [pageIndex, pageSize, userId, status, productId]);

  // 搜索
  const handleSearch = () => {
    setPageIndex(1);
    loadOrders();
  };

  // 重置筛选
  const handleReset = () => {
    setUserId(undefined);
    setStatus(undefined);
    setProductId(undefined);
    setOrderNo('');
    setPageIndex(1);
  };

  // 查看详情
  const handleViewDetail = (order: Order) => {
    setSelectedOrder(order);
    setDetailVisible(true);
  };

  // 重试失败订单
  const handleRetry = (order: Order) => {
    setRetryOrder(order);
    setConfirmVisible(true);
  };

  // 确认重试
  const handleConfirmRetry = async () => {
    if (!retryOrder) return;

    try {
      await retryGrantBenefit(retryOrder.id);
      message.success('重试成功');
      loadOrders();
    } catch (error) {
      log.error('OrderList', '重试失败:', error);
      message.error('重试失败');
    } finally {
      setConfirmVisible(false);
      setRetryOrder(undefined);
    }
  };

  // 表格列定义
  const columns: TableColumnsType<Order> = [
    {
      title: '订单号',
      dataIndex: 'orderNo',
      key: 'orderNo',
      width: 180,
      fixed: 'left',
    },
    {
      title: '用户',
      key: 'user',
      width: 150,
      render: (_: unknown, record: Order) => (
        <div>
          <div>{record.userName || '未知'}</div>
          <div style={{ fontSize: '12px', color: '#999' }}>ID: {record.userId}</div>
        </div>
      ),
    },
    {
      title: '商品',
      key: 'product',
      width: 200,
      render: (_: unknown, record: Order) => (
        <div>
          <div>{record.productName}</div>
          <div style={{ fontSize: '12px', color: '#999' }}>
            {getProductTypeDisplay(record.productType)} | ID: {record.productId}
          </div>
        </div>
      ),
    },
    {
      title: '数量',
      dataIndex: 'quantity',
      key: 'quantity',
      width: 80,
      align: 'center',
    },
    {
      title: '单价',
      dataIndex: 'unitPrice',
      key: 'unitPrice',
      width: 120,
      render: (price: number) => (
        <span style={{ color: '#ff4d4f' }}>{price} 胡萝卜</span>
      ),
    },
    {
      title: '总价',
      dataIndex: 'totalPrice',
      key: 'totalPrice',
      width: 120,
      render: (price: number) => (
        <span style={{ fontWeight: 'bold', color: '#ff4d4f' }}>
          {price} 胡萝卜
        </span>
      ),
    },
    {
      title: '状态',
      key: 'status',
      width: 120,
      render: (_: unknown, record: Order) => (
        <Tag color={getOrderStatusColor(record.status)}>
          {record.statusDisplay}
        </Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'createTime',
      key: 'createTime',
      width: 180,
      render: (time: string) => new Date(time).toLocaleString('zh-CN'),
    },
    {
      title: '操作',
      key: 'action',
      width: 150,
      fixed: 'right',
      render: (_: unknown, record: Order) => (
        <Space size="small">
          <Button
            variant="ghost"
            size="small"
            icon={<EyeOutlined />}
            onClick={() => handleViewDetail(record)}
          >
            详情
          </Button>
          {record.status === 'Failed' && ( // Failed
            <Button
              variant="ghost"
              size="small"
              icon={<SyncOutlined />}
              onClick={() => handleRetry(record)}
            >
              重试
            </Button>
          )}
        </Space>
      ),
    },
  ];

  return (
    <div className="order-list-page">
      <div className="page-header">
        <h2>订单管理</h2>
      </div>

      <div className="filter-bar">
        <Space wrap>
          <Input
            placeholder="用户 ID"
            style={{ width: 120 }}
            type="number"
            value={userId}
            onChange={(e) => setUserId(e.target.value ? Number(e.target.value) : undefined)}
          />

          <Select
            placeholder="订单状态"
            style={{ width: 120 }}
            allowClear
            value={status}
            onChange={setStatus}
          >
            <Select.Option value={0}>待支付</Select.Option>
            <Select.Option value={1}>已支付</Select.Option>
            <Select.Option value={2}>已完成</Select.Option>
            <Select.Option value={3}>已取消</Select.Option>
            <Select.Option value={4}>已退款</Select.Option>
            <Select.Option value={5}>发放失败</Select.Option>
          </Select>

          <Input
            placeholder="商品 ID"
            style={{ width: 120 }}
            type="number"
            value={productId}
            onChange={(e) => setProductId(e.target.value ? Number(e.target.value) : undefined)}
          />

          <Input
            placeholder="订单号"
            style={{ width: 200 }}
            value={orderNo}
            onChange={(e) => setOrderNo(e.target.value)}
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
        dataSource={orders}
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

      <OrderDetail
        visible={detailVisible}
        order={selectedOrder}
        onClose={() => {
          setDetailVisible(false);
          setSelectedOrder(undefined);
        }}
        onRetry={() => {
          if (selectedOrder) {
            handleRetry(selectedOrder);
          }
        }}
      />

      <ConfirmDialog
        isOpen={confirmVisible}
        title="确认重试"
        message={`确定要重新发放订单"${retryOrder?.orderNo}"的权益吗？`}
        onConfirm={handleConfirmRetry}
        onCancel={() => {
          setConfirmVisible(false);
          setRetryOrder(undefined);
        }}
      />
    </div>
  );
};

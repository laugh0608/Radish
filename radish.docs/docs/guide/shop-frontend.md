# 7. 前端设计

> 入口页：[商城系统设计方案](/guide/shop-system)

## 7.1 整体架构

### 7.1.1 WebOS 集成方式

商城系统作为 WebOS 的内置应用，以窗口形式展示：

```typescript
// radish.client/src/apps/shop/index.tsx
export const ShopApp: AppConfig = {
  id: 'shop',
  name: '商城',
  icon: 'mdi:shopping',
  type: 'window',
  component: ShopWindow,
  defaultSize: { width: 1000, height: 700 },
  minSize: { width: 800, height: 600 },
  resizable: true,
  category: 'system'
};
```

### 7.1.2 路由结构

```
/shop
├── /                          # 商城首页
├── /products                  # 商品列表
├── /products/:id              # 商品详情
├── /orders                    # 我的订单
├── /orders/:id                # 订单详情
└── /inventory                 # 我的背包
    ├── /benefits              # 权益列表
    └── /consumables           # 消耗品列表
```

**实现方式**：
```typescript
// ShopWindow.tsx
import { BrowserRouter, Routes, Route } from 'react-router-dom';

export const ShopWindow = () => {
  return (
    <BrowserRouter basename="/shop">
      <Routes>
        <Route path="/" element={<ShopHome />} />
        <Route path="/products" element={<ProductList />} />
        <Route path="/products/:id" element={<ProductDetail />} />
        <Route path="/orders" element={<OrderList />} />
        <Route path="/orders/:id" element={<OrderDetail />} />
        <Route path="/inventory" element={<Inventory />} />
      </Routes>
    </BrowserRouter>
  );
};
```

---

## 7.2 页面设计

### 7.2.1 商城首页

**功能**：
- 推荐商品展示
- 商品分类导航
- 热销商品
- 新品上架

**布局**：
```
┌─────────────────────────────────────────────────────┐
│  [我的背包]  [我的订单]                      [余额: 1000 币] │
├─────────────────────────────────────────────────────┤
│  [会员服务] [徽章装饰] [功能卡片] [加成道具] [限定商品]    │
├─────────────────────────────────────────────────────┤
│  推荐商品                                            │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐   │
│  │ VIP月卡 │ │元老徽章 │ │ 改名卡  │ │ 经验卡  │   │
│  │  300币  │ │ 1000币  │ │  100币  │ │  50币   │   │
│  │  [购买] │ │  [购买] │ │  [购买] │ │  [购买] │   │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘   │
├─────────────────────────────────────────────────────┤
│  热销商品                                   [查看更多] │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐               │
│  │ 商品A   │ │ 商品B   │ │ 商品C   │               │
│  └─────────┘ └─────────┘ └─────────┘               │
└─────────────────────────────────────────────────────┘
```

**组件实现**：
```typescript
// ShopHome.tsx
export const ShopHome = () => {
  const { data: categories } = useCategories();
  const { data: recommendedProducts } = useRecommendedProducts();
  const { data: balance } = useCoinBalance();

  return (
    <div className="shop-home">
      <ShopHeader balance={balance} />
      <CategoryNav categories={categories} />
      <ProductSection title="推荐商品" products={recommendedProducts} />
      <ProductSection title="热销商品" products={hotProducts} />
    </div>
  );
};
```

### 7.2.2 商品列表页

**功能**：
- 分类筛选
- 搜索功能
- 排序（价格、销量、新品）
- 分页加载

**布局**：
```
┌─────────────────────────────────────────────────────┐
│  [返回]  商品列表 - 会员服务                         │
├─────────────────────────────────────────────────────┤
│  [搜索框]                      [排序: 综合▼] [筛选]  │
├─────────────────────────────────────────────────────┤
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   │
│  │ [图片]      │ │ [图片]      │ │ [图片]      │   │
│  │ 300 币      │ │ 800 币      │ │ 2500 币     │   │
│  │ 已售 123    │ │ 已售 89     │ │ 已售 45     │   │
│  │ [立即购买]  │ │ [立即购买]  │ │ [立即购买]  │   │
│  └─────────────┘ └─────────────┘ └─────────────┘   │
│                                                     │
│  [上一页] 1 2 3 4 5 [下一页]                        │
└─────────────────────────────────────────────────────┘
```

**组件实现**：
```typescript
// ProductList.tsx
export const ProductList = () => {
  const { categoryId } = useParams();
  const [filters, setFilters] = useState<ProductFilters>({
    categoryId,
    keyword: '',
    sortBy: 'default',
    pageIndex: 1,
    pageSize: 12
  });

  const { data: products, isLoading } = useProducts(filters);

  return (
    <div className="product-list">
      <ProductListHeader categoryName={getCategoryName(categoryId)} />
      <ProductFilters filters={filters} onChange={setFilters} />
      <ProductGrid products={products?.items} loading={isLoading} />
      <Pagination
        current={filters.pageIndex}
        total={products?.total}
        pageSize={filters.pageSize}
        onChange={(page) => setFilters({ ...filters, pageIndex: page })}
      />
    </div>
  );
};
```

### 7.2.3 商品详情页

**功能**：
- 商品信息展示
- 购买数量选择
- 限购提示
- 购买按钮

**布局**：
```
┌─────────────────────────────────────────────────────┐
├──────────────────────┬──────────────────────────────┤
│                      │  尊享多项特权，经验萝卜币双加成│
│      [商品图片]       │                              │
│                      │  ¥ 300 币  ¥500              │
│                      │                              │
│                      │  有效期：30 天                │
│                      │  库存：无限                   │
│                      │  已售：123 件                 │
│                      │                              │
│                      │  数量：[- 1 +]               │
│                      │                              │
│                      │  [立即购买 - 300 币]         │
├──────────────────────┴──────────────────────────────┤
│  商品详情                                            │
│                                                     │
│  【特权说明】                                        │
│  • 专属 VIP 标识                                     │
│  • 经验值获取 +20%                                   │
│  • 萝卜币获取 +10%                                   │
│  • 每日签到奖励 ×2                                   │
│  • 专属 VIP 头像框                                   │
│  • 评论高亮显示                                      │
│                                                     │
└─────────────────────────────────────────────────────┘
```

**组件实现**：
```typescript
// ProductDetail.tsx
export const ProductDetail = () => {
  const { id } = useParams();
  const { data: product, isLoading } = useProduct(id);
  const [quantity, setQuantity] = useState(1);
  const { mutate: purchase, isPending } = usePurchase();

  const handlePurchase = () => {
    Modal.confirm({
      title: '确认购买',
      content: `确认购买「${product.name}」×${quantity}，共需 ${product.price * quantity} 萝卜币？`,
      onOk: () => {
        purchase({ productId: product.id, quantity });
      }
    });
  };

  if (isLoading) return <Skeleton />;

  return (
    <div className="product-detail">
      <ProductImage src={product.imageUrl} />
      <ProductInfo product={product}>
        <QuantitySelector value={quantity} onChange={setQuantity} max={10} />
        <PurchaseButton
          onClick={handlePurchase}
          loading={isPending}
          price={product.price * quantity}
        />
      </ProductInfo>
      <ProductDescription content={product.description} />
    </div>
  );
};
```

### 7.2.4 我的订单页

**功能**：
- 订单列表
- 状态筛选（全部、待支付、已完成、已取消）
- 订单详情跳转

**布局**：
```
┌─────────────────────────────────────────────────────┐
│  [返回]  我的订单                                     │
├─────────────────────────────────────────────────────┤
│  [全部] [待支付] [已完成] [已取消]                    │
├─────────────────────────────────────────────────────┤
│  ┌───────────────────────────────────────────────┐  │
│  │ 订单号: 20260112123456789012          已完成   │  │
│  │ ─────────────────────────────────────────────│  │
│  │ 购买时间: 2026-01-12 12:34:56                 │  │
│  │                          [查看详情] [再次购买] │  │
│  └───────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────┐  │
│  │ 订单号: 20260111098765432109          已完成   │  │
│  │ ─────────────────────────────────────────────│  │
│  │ [图] 改名卡 × 1                     100 币    │  │
│  │ 购买时间: 2026-01-11 09:12:34                 │  │
│  │                          [查看详情] [再次购买] │  │
│  └───────────────────────────────────────────────┘  │
│                                                     │
│  [上一页] 1 2 3 [下一页]                            │
└─────────────────────────────────────────────────────┘
```

**组件实现**：
```typescript
// OrderList.tsx
export const OrderList = () => {
  const [status, setStatus] = useState<OrderStatus | undefined>();
  const [page, setPage] = useState(1);
  const { data: orders, isLoading } = useOrders({ status, pageIndex: page });

  return (
    <div className="order-list">
      <OrderHeader />
      <OrderTabs activeTab={status} onChange={setStatus} />
      <OrderItems orders={orders?.items} loading={isLoading} />
      <Pagination
        current={page}
        total={orders?.total}
        onChange={setPage}
      />
    </div>
  );
};

// OrderItem.tsx
export const OrderItem = ({ order }: { order: Order }) => {
  const navigate = useNavigate();

  return (
    <div className="order-item">
      <OrderItemHeader
        orderNo={order.orderNo}
        status={order.status}
        time={order.createTime}
      />
      <OrderItemProduct
        image={order.productImage}
        name={order.productName}
        quantity={order.quantity}
        price={order.paidAmount}
      />
      <OrderItemActions>
        <Button onClick={() => navigate(`/orders/${order.id}`)}>
          查看详情
        </Button>
        {order.status === OrderStatus.Completed && (
          <Button type="primary" onClick={() => repurchase(order.productId)}>
            再次购买
          </Button>
        )}
      </OrderItemActions>
    </div>
  );
};
```

### 7.2.5 订单详情页

**功能**：
- 订单完整信息
- 订单状态追踪
- 权益信息（如已发放）

**布局**：
```
┌─────────────────────────────────────────────────────┐
│  [返回]  订单详情                                     │
├─────────────────────────────────────────────────────┤
│  订单号: 20260112123456789012                        │
│  订单状态: 已完成                                     │
│                                                     │
│  【商品信息】                                        │
│       单价: 300 币                                   │
│       数量: 1                                        │
│       小计: 300 币                                   │
│                                                     │
│  【支付信息】                                        │
│  支付方式: 萝卜币                                     │
│  实付金额: 300 币                                    │
│  支付时间: 2026-01-12 12:34:56                       │
│                                                     │
│  【权益信息】                                        │
│  生效时间: 2026-01-12 12:34:56                       │
│  到期时间: 2026-02-11 12:34:56                       │
│  权益状态: 生效中                                     │
│                                                     │
│  【时间轴】                                          │
│  • 2026-01-12 12:34:56  订单创建                     │
│  • 2026-01-12 12:34:56  支付成功                     │
│  • 2026-01-12 12:34:57  权益发放成功                 │
│  • 2026-01-12 12:34:57  订单完成                     │
└─────────────────────────────────────────────────────┘
```

**组件实现**：
```typescript
// OrderDetail.tsx
export const OrderDetail = () => {
  const { id } = useParams();
  const { data: order, isLoading } = useOrderDetail(id);

  if (isLoading) return <Skeleton />;

  return (
    <div className="order-detail">
      <OrderDetailHeader order={order} />
      <OrderProductInfo order={order} />
      <OrderPaymentInfo order={order} />
      {order.benefitStartTime && (
        <OrderBenefitInfo order={order} />
      )}
      <OrderTimeline order={order} />
    </div>
  );
};
```

### 7.2.6 我的背包页

**功能**：
- 权益列表（VIP、徽章、头像框等）
- 消耗品列表
- 装备/使用按钮
- 到期时间提示

**布局**：
```
┌─────────────────────────────────────────────────────┐
│  [返回]  我的背包                                     │
├─────────────────────────────────────────────────────┤
│  [权益] [消耗品]                                      │
├─────────────────────────────────────────────────────┤
│  权益列表                                            │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   │
│  │ [图标]      │ │ [图标]      │ │ [图标]      │   │
│  │ 生效中      │ │ 永久        │ │ 已装备      │   │
│  │ 30天后到期  │ │ [装备]      │ │ [卸下]      │   │
│  └─────────────┘ └─────────────┘ └─────────────┘   │
│                                                     │
│  消耗品列表                                          │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐   │
│  │ 改名卡      │ │ 置顶卡      │ │ 经验卡      │   │
│  │ [图标]      │ │ [图标]      │ │ [图标]      │   │
│  │ × 2         │ │ × 5         │ │ × 10        │   │
│  │ [使用]      │ │ [使用]      │ │ [使用]      │   │
│  └─────────────┘ └─────────────┘ └─────────────┘   │
└─────────────────────────────────────────────────────┘
```

**组件实现**：
```typescript
// Inventory.tsx
export const Inventory = () => {
  const [tab, setTab] = useState<'benefits' | 'consumables'>('benefits');
  const { data: inventory, isLoading } = useInventory();

  return (
    <div className="inventory">
      <InventoryHeader />
      <InventoryTabs activeTab={tab} onChange={setTab} />
      {tab === 'benefits' ? (
        <BenefitList benefits={inventory?.benefits} />
      ) : (
        <ConsumableList consumables={inventory?.consumables} />
      )}
    </div>
  );
};

// BenefitItem.tsx
export const BenefitItem = ({ benefit }: { benefit: BenefitItem }) => {
  const { mutate: equip } = useEquipItem();
  const { mutate: unequip } = useUnequipItem();

  return (
    <div className="benefit-item">
      <BenefitIcon type={benefit.benefitType} />
      <BenefitInfo
        name={benefit.itemName}
        status={getBenefitStatus(benefit)}
        expiresAt={benefit.expiresAt}
      />
      <BenefitActions>
        {benefit.isEquippable && (
          benefit.isEquipped ? (
            <Button onClick={() => unequip(benefit.id)}>卸下</Button>
          ) : (
            <Button type="primary" onClick={() => equip(benefit.id)}>装备</Button>
          )
        )}
      </BenefitActions>
    </div>
  );
};

// ConsumableItem.tsx
export const ConsumableItem = ({ consumable }: { consumable: ConsumableItem }) => {
  const [useModalVisible, setUseModalVisible] = useState(false);
  const { mutate: useItem } = useConsumableItem();

  return (
    <div className="consumable-item">
      <ConsumableIcon type={consumable.consumableType} />
      <ConsumableInfo
        name={consumable.itemName}
        quantity={consumable.quantity}
        deadline={consumable.useDeadline}
      />
      <ConsumableActions>
        <Button
          type="primary"
          onClick={() => setUseModalVisible(true)}
          disabled={consumable.quantity <= 0}
        >
          使用
        </Button>
      </ConsumableActions>
      <UseConsumableModal
        visible={useModalVisible}
        consumable={consumable}
        onUse={(input) => {
          useItem({ inventoryId: consumable.id, ...input });
          setUseModalVisible(false);
        }}
        onCancel={() => setUseModalVisible(false)}
      />
    </div>
  );
};
```

---

## 7.3 组件设计

### 7.3.1 共享组件

**ProductCard - 商品卡片**：
```typescript
interface ProductCardProps {
  product: Product;
  onClick?: () => void;
  showActions?: boolean;
}

export const ProductCard = ({ product, onClick, showActions = true }: ProductCardProps) => {
  return (
    <div className="product-card" onClick={onClick}>
      <ProductImage src={product.imageUrl} />
      {product.isHot && <Badge text="热销" color="red" />}
      {product.isNew && <Badge text="新品" color="blue" />}
      <ProductName>{product.name}</ProductName>
      {product.subtitle && <ProductSubtitle>{product.subtitle}</ProductSubtitle>}
      <ProductPrice>
        <CurrentPrice>{product.price}</CurrentPrice>
        {product.showOriginalPrice && (
          <OriginalPrice>{product.originalPrice}</OriginalPrice>
        )}
      </ProductPrice>
      {showActions && (
        <Button type="primary" block>
          立即购买
        </Button>
      )}
    </div>
  );
};
```

**PurchaseModal - 购买确认弹窗**：
```typescript
interface PurchaseModalProps {
  visible: boolean;
  product: Product;
  quantity: number;
  onConfirm: () => void;
  onCancel: () => void;
}

export const PurchaseModal = ({
  visible,
  product,
  quantity,
  onConfirm,
  onCancel
}: PurchaseModalProps) => {
  const totalPrice = product.price * quantity;
  const { data: balance } = useCoinBalance();

  return (
    <Modal
      title="确认购买"
      visible={visible}
      onOk={onConfirm}
      onCancel={onCancel}
      okText="确认购买"
      cancelText="取消"
    >
      <div className="purchase-modal">
        <ProductSummary product={product} quantity={quantity} />
        <PriceSummary totalPrice={totalPrice} balance={balance} />
        {balance < totalPrice && (
          <Alert type="warning" message="萝卜币余额不足，请先充值" />
        )}
      </div>
    </Modal>
  );
};
```

**CoinBalance - 萝卜币余额显示**：
```typescript
export const CoinBalance = () => {
  const { data: balance, isLoading } = useCoinBalance();

  if (isLoading) return <Skeleton.Button size="small" />;

  return (
    <div className="coin-balance">
      <Icon name="mdi:currency-sign" />
      <span>{balance} 币</span>
    </div>
  );
};
```

### 7.3.2 业务组件

**CategoryNav - 分类导航**：
```typescript
export const CategoryNav = ({ categories }: { categories: Category[] }) => {
  const navigate = useNavigate();

  return (
    <div className="category-nav">
      {categories.map((category) => (
        <CategoryItem
          key={category.id}
          icon={category.icon}
          name={category.name}
          onClick={() => navigate(`/products?category=${category.id}`)}
        />
      ))}
    </div>
  );
};
```

**OrderStatusBadge - 订单状态徽章**：
```typescript
export const OrderStatusBadge = ({ status }: { status: OrderStatus }) => {
  const statusConfig = {
    [OrderStatus.Pending]: { text: '待支付', color: 'orange' },
    [OrderStatus.Paid]: { text: '已支付', color: 'blue' },
    [OrderStatus.Completed]: { text: '已完成', color: 'green' },
    [OrderStatus.Cancelled]: { text: '已取消', color: 'gray' },
    [OrderStatus.Failed]: { text: '异常', color: 'red' }
  };

  const config = statusConfig[status];

  return <Badge text={config.text} color={config.color} />;
};
```

**UseConsumableModal - 使用消耗品弹窗**：
```typescript
interface UseConsumableModalProps {
  visible: boolean;
  consumable: ConsumableItem;
  onUse: (input: UseConsumableInput) => void;
  onCancel: () => void;
}

export const UseConsumableModal = ({
  visible,
  consumable,
  onUse,
  onCancel
}: UseConsumableModalProps) => {
  const [formData, setFormData] = useState<UseConsumableInput>({});

  // 根据消耗品类型显示不同的输入
  const renderInputs = () => {
    switch (consumable.consumableType) {
      case ConsumableType.RenameCard:
        return (
          <Input
            placeholder="请输入新昵称"
            value={formData.newName}
            onChange={(e) => setFormData({ ...formData, newName: e.target.value })}
          />
        );

      case ConsumableType.PostPinCard:
      case ConsumableType.PostHighlightCard:
        return (
          <Select
            placeholder="选择要操作的帖子"
            value={formData.targetId}
            onChange={(value) => setFormData({ ...formData, targetId: value })}
          >
            {/* 用户的帖子列表 */}
          </Select>
        );

      default:
        return null;
    }
  };

  return (
    <Modal
      title={`使用${consumable.itemName}`}
      visible={visible}
      onOk={() => onUse(formData)}
      onCancel={onCancel}
    >
      {renderInputs()}
    </Modal>
  );
};
```

---

## 7.4 状态管理

### 7.4.1 API 请求层

使用 `TanStack Query` (React Query) 管理服务端状态：

```typescript
// api/shop.ts
export const shopApi = {
  // 商品相关
  getProducts: (params: ProductQueryParams) =>
    request.get<PageModel<Product>>('/api/shop/products', { params }),

  getProductDetail: (id: number) =>
    request.get<ProductDetail>(`/api/shop/products/${id}`),

  // 订单相关
  createOrder: (data: OrderCreateInput) =>
    request.post<OrderResult>('/api/shop/purchase', data),

  getOrders: (params: OrderQueryParams) =>
    request.get<PageModel<Order>>('/api/orders', { params }),

  getOrderDetail: (id: number) =>
    request.get<OrderDetail>(`/api/orders/${id}`),

  cancelOrder: (id: number) =>
    request.post<boolean>(`/api/orders/${id}/cancel`),

  // 背包相关
  getInventory: () =>
    request.get<UserInventory>('/api/inventory'),

  useConsumable: (id: number, data: UseConsumableInput) =>
    request.post<UseConsumableResult>(`/api/inventory/${id}/use`, data),

  equipItem: (id: number) =>
    request.post<boolean>(`/api/inventory/${id}/equip`),

  unequipItem: (id: number) =>
    request.post<boolean>(`/api/inventory/${id}/unequip`),

  // 萝卜币余额
  getCoinBalance: () =>
    request.get<number>('/api/coins/balance')
};
```

### 7.4.2 React Query Hooks

```typescript
// hooks/useShop.ts
export const useProducts = (params: ProductQueryParams) => {
  return useQuery({
    queryKey: ['products', params],
    queryFn: () => shopApi.getProducts(params)
  });
};

export const useProduct = (id: number) => {
  return useQuery({
    queryKey: ['product', id],
    queryFn: () => shopApi.getProductDetail(id)
  });
};

export const usePurchase = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: shopApi.createOrder,
    onSuccess: () => {
      // 刷新订单列表和余额
      queryClient.invalidateQueries({ queryKey: ['orders'] });
      queryClient.invalidateQueries({ queryKey: ['coinBalance'] });
      queryClient.invalidateQueries({ queryKey: ['inventory'] });

      notification.success({
        message: '购买成功',
        description: '商品已成功购买，权益已发放到背包'
      });
    },
    onError: (error: any) => {
      notification.error({
        message: '购买失败',
        description: error.response?.data?.msg || '购买失败，请重试'
      });
    }
  });
};

export const useOrders = (params: OrderQueryParams) => {
  return useQuery({
    queryKey: ['orders', params],
    queryFn: () => shopApi.getOrders(params)
  });
};

export const useInventory = () => {
  return useQuery({
    queryKey: ['inventory'],
    queryFn: shopApi.getInventory
  });
};

export const useConsumableItem = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ inventoryId, ...data }: UseConsumableParams) =>
      shopApi.useConsumable(inventoryId, data),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['inventory'] });

      notification.success({
        message: '使用成功',
        description: result.message
      });
    }
  });
};

export const useCoinBalance = () => {
  return useQuery({
    queryKey: ['coinBalance'],
    queryFn: shopApi.getCoinBalance,
    staleTime: 30000 // 30 秒内不重新请求
  });
};
```

### 7.4.3 本地状态管理

使用 `useState` 和 `useContext` 管理局部状态：

```typescript
// context/ShopContext.tsx
interface ShopContextValue {
  selectedCategory: string | null;
  setSelectedCategory: (id: string | null) => void;
}

const ShopContext = createContext<ShopContextValue | null>(null);

export const ShopProvider = ({ children }: { children: ReactNode }) => {
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);

  return (
    <ShopContext.Provider value={{ selectedCategory, setSelectedCategory }}>
      {children}
    </ShopContext.Provider>
  );
};

export const useShopContext = () => {
  const context = useContext(ShopContext);
  if (!context) throw new Error('useShopContext must be used within ShopProvider');
  return context;
};
```

---

## 7.5 样式设计

### 7.5.1 主题色彩

```scss
// shop-theme.scss
$shop-primary: #FF6B35;      // 橙色 - 主色调
$shop-secondary: #FFA500;    // 橘黄色 - 辅助色
$shop-success: #52C41A;      // 绿色 - 成功状态
$shop-warning: #FAAD14;      // 黄色 - 警告
$shop-error: #F5222D;        // 红色 - 错误
$shop-info: #1890FF;         // 蓝色 - 信息

// VIP 专属色
$vip-gold: linear-gradient(135deg, #FFD700, #FFA500);

// 背景色
$bg-primary: #FFFFFF;
$bg-secondary: #F5F5F5;
$bg-tertiary: #FAFAFA;

// 边框色
$border-color: #E8E8E8;
$border-radius: 8px;

// 阴影
$shadow-small: 0 2px 8px rgba(0, 0, 0, 0.08);
$shadow-medium: 0 4px 16px rgba(0, 0, 0, 0.12);
```

### 7.5.2 商品卡片样式

```scss
// ProductCard.scss
.product-card {
  position: relative;
  border: 1px solid $border-color;
  border-radius: $border-radius;
  padding: 16px;
  cursor: pointer;
  transition: all 0.3s;
  background: $bg-primary;

  &:hover {
    transform: translateY(-4px);
    box-shadow: $shadow-medium;
  }

  .product-image {
    width: 100%;
    height: 200px;
    object-fit: cover;
    border-radius: $border-radius;
  }

  .product-badge {
    position: absolute;
    top: 8px;
    right: 8px;
    padding: 4px 8px;
    border-radius: 4px;
    font-size: 12px;
    color: white;

    &.hot {
      background: $shop-error;
    }

    &.new {
      background: $shop-info;
    }
  }

  .product-name {
    margin-top: 12px;
    font-size: 16px;
    font-weight: 500;
    color: #333;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .product-subtitle {
    font-size: 12px;
    color: #999;
    margin-top: 4px;
  }

  .product-price {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-top: 12px;

    .current-price {
      font-size: 20px;
      font-weight: bold;
      color: $shop-primary;

      &::before {
        content: '¥';
        font-size: 14px;
      }

      &::after {
        content: ' 币';
        font-size: 12px;
      }
    }

    .original-price {
      font-size: 14px;
      color: #999;
      text-decoration: line-through;
    }
  }

  .purchase-button {
    margin-top: 12px;
    width: 100%;
    height: 36px;
    background: $shop-primary;
    border: none;
    border-radius: $border-radius;
    color: white;
    font-size: 14px;
    cursor: pointer;
    transition: background 0.3s;

    &:hover {
      background: darken($shop-primary, 10%);
    }
  }
}
```

### 7.5.3 VIP 样式

```scss
// VIP.scss
.vip-card {
  background: $vip-gold;
  border: 2px solid #FFD700;
  box-shadow: 0 4px 16px rgba(255, 215, 0, 0.3);

  .vip-badge {
    position: absolute;
    top: -8px;
    left: 50%;
    transform: translateX(-50%);
    background: $vip-gold;
    padding: 4px 16px;
    border-radius: 12px;
    font-size: 12px;
    font-weight: bold;
    color: white;
    box-shadow: 0 2px 8px rgba(255, 215, 0, 0.4);

    &::before {
      content: '👑 ';
    }
  }
}

.vip-icon {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  background: $vip-gold;
  border-radius: 4px;
  font-size: 12px;
  font-weight: bold;
  color: white;

  &::before {
    content: '👑';
  }
}
```

---

## 7.6 交互设计

### 7.6.1 购买流程

1. 点击商品进入详情页
2. 选择购买数量
3. 点击"立即购买"按钮
4. 弹出确认弹窗，显示：
   - 商品信息
   - 总价
   - 当前余额
   - 余额是否充足
5. 点击"确认购买"
6. 显示加载状态
7. 购买成功：
   - 显示成功通知
   - 跳转到订单详情或背包页
8. 购买失败：
   - 显示错误通知
   - 提示失败原因

### 7.6.2 使用消耗品流程

1. 进入"我的背包"
2. 切换到"消耗品"标签
3. 点击"使用"按钮
4. 弹出使用弹窗：
   - 改名卡：输入新昵称
   - 置顶卡/高亮卡：选择目标帖子
   - 经验卡/萝卜币卡：无需输入，直接确认
   - 双倍经验卡：无需输入，直接确认
5. 点击"确认使用"
6. 显示加载状态
7. 使用成功：
   - 显示成功通知
   - 刷新背包数据
   - 跳转到相关页面（如改名成功跳转到个人设置）

### 7.6.3 装备权益流程

1. 进入"我的背包"
2. 切换到"权益"标签
3. 找到可装备的物品（徽章、头像框、称号）
4. 点击"装备"按钮
5. 显示加载状态
6. 装备成功：
   - 显示成功通知
   - 按钮变为"卸下"
   - 同类型的其他装备自动卸下

---

## 7.7 响应式设计

### 7.7.1 断点设计

```scss
// breakpoints.scss
$breakpoints: (
  xs: 0,      // < 576px  手机竖屏
  sm: 576px,  // 手机横屏
  md: 768px,  // 平板竖屏
  lg: 992px,  // 平板横屏 / 小屏笔记本
  xl: 1200px, // 桌面
  xxl: 1600px // 大屏桌面
);

@mixin respond-to($breakpoint) {
  @media (min-width: map-get($breakpoints, $breakpoint)) {
    @content;
  }
}
```

### 7.7.2 商品列表响应式

```scss
.product-grid {
  display: grid;
  gap: 16px;

  // 手机竖屏：1 列
  grid-template-columns: 1fr;

  // 手机横屏/平板竖屏：2 列
  @include respond-to(sm) {
    grid-template-columns: repeat(2, 1fr);
  }

  // 平板横屏：3 列
  @include respond-to(md) {
    grid-template-columns: repeat(3, 1fr);
  }

  // 桌面：4 列
  @include respond-to(lg) {
    grid-template-columns: repeat(4, 1fr);
  }
}
```

### 7.7.3 WebOS 窗口适配

```typescript
// ShopWindow.tsx
export const ShopWindow = () => {
  const { width, height } = useWindowSize();

  // 根据窗口大小调整布局
  const isMobile = width < 768;
  const columns = isMobile ? 1 : width < 992 ? 2 : 4;

  return (
    <div className="shop-window" style={{ width, height }}>
      <ProductGrid columns={columns} />
    </div>
  );
};
```

---

## 7.8 性能优化

### 7.8.1 图片懒加载

```typescript
import { LazyLoadImage } from 'react-lazy-load-image-component';

export const ProductImage = ({ src, alt }: { src: string; alt: string }) => {
  return (
    <LazyLoadImage
      src={src}
      alt={alt}
      effect="blur"
      placeholderSrc="/placeholder.png"
    />
  );
};
```

### 7.8.2 虚拟列表

对于长列表（如订单列表），使用虚拟滚动：

```typescript
import { VirtualList } from '@radish/ui';

export const OrderList = ({ orders }: { orders: Order[] }) => {
  return (
    <VirtualList
      data={orders}
      height={600}
      itemHeight={120}
      renderItem={(order) => <OrderItem order={order} />}
    />
  );
};
```

### 7.8.3 代码分割

```typescript
import { lazy, Suspense } from 'react';

const ProductDetail = lazy(() => import('./pages/ProductDetail'));
const OrderList = lazy(() => import('./pages/OrderList'));
const Inventory = lazy(() => import('./pages/Inventory'));

export const ShopRoutes = () => {
  return (
    <Suspense fallback={<Loading />}>
      <Routes>
        <Route path="/products/:id" element={<ProductDetail />} />
        <Route path="/orders" element={<OrderList />} />
        <Route path="/inventory" element={<Inventory />} />
      </Routes>
    </Suspense>
  );
};
```

---

## 7.9 错误处理

### 7.9.1 错误边界

```typescript
export class ShopErrorBoundary extends React.Component<
  { children: ReactNode },
  { hasError: boolean; error: Error | null }
> {
  constructor(props: { children: ReactNode }) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error) {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Shop Error:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <ErrorPage
          message="商城加载失败"
          description={this.state.error?.message}
          onRetry={() => this.setState({ hasError: false, error: null })}
        />
      );
    }

    return this.props.children;
  }
}
```

### 7.9.2 API 错误处理

```typescript
// request.ts
const request = axios.create({
  baseURL: '/api',
  timeout: 10000
});

request.interceptors.response.use(
  (response) => response.data,
  (error) => {
    const message = error.response?.data?.msg || '请求失败';

    notification.error({
      message: '错误',
      description: message
    });

    return Promise.reject(error);
  }
);
```

---

## 7.10 可访问性

### 7.10.1 语义化 HTML

```tsx
<article className="product-card">
  <figure>
    <img src={product.imageUrl} alt={product.name} />
  </figure>
  <h3>{product.name}</h3>
  <p>{product.subtitle}</p>
  <button aria-label={`购买${product.name}`}>立即购买</button>
</article>
```

### 7.10.2 键盘导航

```typescript
export const ProductCard = ({ product }: ProductCardProps) => {
  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      navigate(`/products/${product.id}`);
    }
  };

  return (
    <div
      className="product-card"
      tabIndex={0}
      role="button"
      onKeyPress={handleKeyPress}
    >
      {/* ... */}
    </div>
  );
};
```

---

> 下一篇：[8. 实施计划](/guide/shop-roadmap)

# @radish/http - HTTP 客户端库

## 概述

`@radish/http` 是 Radish 项目的统一 HTTP 客户端库，提供类型安全的 API 请求封装。该库从 `@radish/ui` 中独立出来，专注于 HTTP 通信功能，支持认证、拦截器、超时控制等特性。

## 包信息

- **包名**: `@radish/http`
- **版本**: 26.1.1
- **类型**: ESM 模块
- **位置**: `radish.http/`

## 核心特性

- ✅ **统一配置管理**：全局配置 baseUrl、timeout、token 获取方式
- ✅ **类型安全**：完整的 TypeScript 类型定义
- ✅ **认证支持**：自动添加 Bearer Token
- ✅ **拦截器**：请求/响应/错误拦截器
- ✅ **超时控制**：可配置的请求超时时间
- ✅ **统一错误处理**：标准化的错误响应格式
- ✅ **分页支持**：内置分页数据类型
- ✅ **国际化支持**：支持 i18n 消息键

## 安装和配置

### 1. 安装依赖

```bash
# 在前端项目根目录
npm install
```

`@radish/http` 通过 npm workspaces 自动链接，无需单独安装。

### 2. 配置 API 客户端

在应用入口或 API 文件顶部配置客户端：

```typescript
import { configureApiClient } from '@radish/http';

// 配置 API 基础 URL
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000';

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''), // 移除末尾斜杠
  timeout: 30000, // 30 秒超时
  getToken: () => {
    // 自定义 token 获取逻辑
    return localStorage.getItem('access_token');
  },
});
```

### 3. 环境变量配置

在 `.env.development` 或 `.env.production` 中配置：

```env
# API 基础 URL
VITE_API_BASE_URL=https://localhost:5000
```

## API 使用示例

### 基础请求方法

#### GET 请求

```typescript
import { apiGet } from '@radish/http';

// 不需要认证的请求
const response = await apiGet<Product[]>('/api/v1/Shop/GetProducts');

if (response.ok && response.data) {
  console.log('商品列表:', response.data);
} else {
  console.error('请求失败:', response.message);
}

// 需要认证的请求
const response = await apiGet<UserProfile>('/api/v1/User/GetProfile', {
  withAuth: true,
});
```

#### POST 请求

```typescript
import { apiPost } from '@radish/http';

// 创建商品
const response = await apiPost<Product>(
  '/api/v1/Shop/CreateProduct',
  {
    name: '新商品',
    price: 99.99,
    stock: 100,
  },
  { withAuth: true }
);

if (response.ok && response.data) {
  console.log('创建成功:', response.data);
}
```

#### PUT 请求

```typescript
import { apiPut } from '@radish/http';

// 更新商品
const response = await apiPut<Product>(
  '/api/v1/Shop/UpdateProduct',
  {
    id: 1,
    name: '更新后的商品',
    price: 89.99,
  },
  { withAuth: true }
);
```

#### DELETE 请求

```typescript
import { apiDelete } from '@radish/http';

// 删除商品
const response = await apiDelete<boolean>(
  '/api/v1/Shop/DeleteProduct?id=1',
  { withAuth: true }
);

if (response.ok) {
  console.log('删除成功');
}
```

### 分页请求

```typescript
import { apiGet, type PagedResponse } from '@radish/http';

// 分页查询商品
const response = await apiGet<PagedResponse<Product>>(
  '/api/v1/Shop/GetProducts?pageIndex=1&pageSize=20',
  { withAuth: true }
);

if (response.ok && response.data) {
  const { data, dataCount, page, pageCount } = response.data;
  console.log(`第 ${page} 页，共 ${pageCount} 页，总计 ${dataCount} 条`);
  console.log('商品列表:', data);
}
```

### 错误处理

```typescript
import { apiGet } from '@radish/http';

try {
  const response = await apiGet<Product[]>('/api/v1/Shop/GetProducts');

  if (response.ok && response.data) {
    // 请求成功
    console.log('数据:', response.data);
  } else {
    // 业务错误
    console.error('业务错误:', response.message);
    console.error('错误码:', response.code);
    console.error('HTTP 状态码:', response.statusCode);
  }
} catch (error) {
  // 网络错误或超时
  console.error('网络错误:', error);
}
```

## 高级配置

### 自定义拦截器

```typescript
import { configureApiClient } from '@radish/http';

configureApiClient({
  baseUrl: 'https://localhost:5000',

  // 请求拦截器
  onRequest: (url, options) => {
    console.log('发送请求:', url);
    console.log('请求配置:', options);
  },

  // 响应拦截器
  onResponse: (response) => {
    console.log('收到响应:', response.status);
  },

  // 错误拦截器
  onError: (error) => {
    console.error('请求错误:', error.message);

    // 可以在这里统一处理错误，如显示通知
    if (error.message.includes('timeout')) {
      showNotification('请求超时，请稍后重试');
    }
  },
});
```

### 自定义 Token 获取

```typescript
import { configureApiClient } from '@radish/http';

configureApiClient({
  baseUrl: 'https://localhost:5000',

  // 自定义 token 获取逻辑
  getToken: () => {
    // 从 localStorage 获取
    const token = localStorage.getItem('access_token');

    // 检查 token 是否过期
    if (token && isTokenExpired(token)) {
      // 清除过期 token
      localStorage.removeItem('access_token');
      return null;
    }

    return token;
  },
});
```

### 获取当前配置

```typescript
import { getApiClientConfig } from '@radish/http';

// 获取当前配置（只读）
const config = getApiClientConfig();
console.log('当前 baseUrl:', config.baseUrl);
console.log('当前 timeout:', config.timeout);
```

## 类型定义

### ApiResponse

后端 `MessageModel` 对应的响应类型：

```typescript
interface ApiResponse<T = any> {
  /** 操作是否成功 */
  isSuccess: boolean;
  /** HTTP 状态码 */
  statusCode: number;
  /** 消息内容 */
  messageInfo: string;
  /** 开发环境消息（可选） */
  messageInfoDev?: string;
  /** 响应数据 */
  responseData?: T;
  /** 错误码（可选，用于客户端特殊处理） */
  code?: string;
  /** 国际化消息键（可选，用于 i18n） */
  messageKey?: string;
}
```

### ParsedApiResponse

解析后的响应类型：

```typescript
interface ParsedApiResponse<T> {
  /** 请求是否成功 */
  ok: boolean;
  /** 响应数据 */
  data?: T;
  /** 错误消息 */
  message?: string;
  /** 错误码 */
  code?: string;
  /** HTTP 状态码 */
  statusCode?: number;
}
```

### PagedResponse

分页数据类型：

```typescript
interface PagedResponse<T> {
  /** 当前页码 */
  page: number;
  /** 每页数量 */
  pageSize: number;
  /** 总数据量 */
  dataCount: number;
  /** 总页数 */
  pageCount: number;
  /** 数据列表 */
  data: T[];
}
```

### ApiRequestOptions

请求配置类型：

```typescript
interface ApiRequestOptions extends RequestInit {
  /** 是否携带认证信息 */
  withAuth?: boolean;
  /** 基础 URL（可选，默认使用全局配置） */
  baseUrl?: string;
  /** 请求超时时间（毫秒） */
  timeout?: number;
}
```

## 实战示例

### 创建 API 模块

```typescript
// src/api/product.ts
import { apiGet, apiPost, apiPut, apiDelete, configureApiClient, type PagedResponse } from '@radish/http';

// 配置 API 客户端
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000';
configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
});

// 商品数据类型
export interface Product {
  voId: number;
  voName: string;
  voDescription: string;
  voPrice: number;
  voStock: number;
  voIcon: string;
}

// 创建商品 DTO
export interface CreateProductDto {
  name: string;
  description: string;
  price: number;
  stock: number;
  icon: string;
}

// 商品 API
export const productApi = {
  /**
   * 获取商品列表
   */
  async getProducts(pageIndex: number = 1, pageSize: number = 20) {
    const response = await apiGet<PagedResponse<Product>>(
      `/api/v1/Shop/GetProducts?pageIndex=${pageIndex}&pageSize=${pageSize}`,
      { withAuth: true }
    );

    if (!response.ok || !response.data) {
      throw new Error(response.message || '获取商品列表失败');
    }

    return response.data;
  },

  /**
   * 获取商品详情
   */
  async getProduct(id: number) {
    const response = await apiGet<Product>(
      `/api/v1/Shop/GetProduct?id=${id}`,
      { withAuth: true }
    );

    if (!response.ok || !response.data) {
      throw new Error(response.message || '获取商品详情失败');
    }

    return response.data;
  },

  /**
   * 创建商品
   */
  async createProduct(data: CreateProductDto) {
    const response = await apiPost<Product>(
      '/api/v1/Shop/CreateProduct',
      data,
      { withAuth: true }
    );

    if (!response.ok || !response.data) {
      throw new Error(response.message || '创建商品失败');
    }

    return response.data;
  },

  /**
   * 更新商品
   */
  async updateProduct(id: number, data: Partial<CreateProductDto>) {
    const response = await apiPut<Product>(
      '/api/v1/Shop/UpdateProduct',
      { id, ...data },
      { withAuth: true }
    );

    if (!response.ok || !response.data) {
      throw new Error(response.message || '更新商品失败');
    }

    return response.data;
  },

  /**
   * 删除商品
   */
  async deleteProduct(id: number) {
    const response = await apiDelete<boolean>(
      `/api/v1/Shop/DeleteProduct?id=${id}`,
      { withAuth: true }
    );

    if (!response.ok) {
      throw new Error(response.message || '删除商品失败');
    }

    return true;
  },
};
```

### 在组件中使用

```typescript
// src/components/ProductList.tsx
import { useState, useEffect } from 'react';
import { productApi, type Product } from '@/api/product';

export const ProductList: React.FC = () => {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadProducts();
  }, []);

  const loadProducts = async () => {
    setLoading(true);
    setError(null);

    try {
      const data = await productApi.getProducts(1, 20);
      setProducts(data.data);
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载失败');
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div>加载中...</div>;
  if (error) return <div>错误: {error}</div>;

  return (
    <div>
      {products.map(product => (
        <div key={product.voId}>
          <h3>{product.voName}</h3>
          <p>{product.voDescription}</p>
          <p>价格: ¥{product.voPrice}</p>
        </div>
      ))}
    </div>
  );
};
```

## 特殊场景处理

### 上传进度回调

对于需要监听上传进度的场景（如分片上传），可以使用 XMLHttpRequest：

```typescript
import { getApiClientConfig } from '@radish/http';

/**
 * 上传分片
 * 注意：此方法使用 XMLHttpRequest 而非统一 API 客户端，
 * 因为需要支持上传进度回调功能
 */
export async function uploadChunk(
  sessionId: string,
  chunkBlob: Blob,
  onProgress?: (progress: number) => void
): Promise<UploadSession> {
  const config = getApiClientConfig();
  const url = `${config.baseUrl}/api/v1/ChunkedUpload/UploadChunk`;

  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();

    // 监听上传进度
    if (onProgress) {
      xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable) {
          onProgress(Math.round((e.loaded / e.total) * 100));
        }
      });
    }

    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        const json = JSON.parse(xhr.responseText);
        resolve(json.responseData);
      } else {
        reject(new Error(`上传失败: HTTP ${xhr.status}`));
      }
    });

    xhr.addEventListener('error', () => {
      reject(new Error('网络错误'));
    });

    xhr.open('POST', url);

    // 从统一配置获取 token
    const token = config.getToken?.();
    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    const formData = new FormData();
    formData.append('sessionId', sessionId);
    formData.append('chunkData', chunkBlob);
    xhr.send(formData);
  });
}
```

## 从 @radish/ui 迁移

如果你的代码之前从 `@radish/ui` 导入 API 客户端，需要进行以下迁移：

### 迁移步骤

1. **更新导入语句**

```typescript
// ❌ 旧代码
import { apiGet, apiPost, configureApiClient } from '@radish/ui';

// ✅ 新代码
import { apiGet, apiPost, configureApiClient } from '@radish/http';
```

2. **更新类型导入**

```typescript
// ❌ 旧代码
import type { PagedResponse } from '@radish/ui';

// ✅ 新代码
import type { PagedResponse } from '@radish/http';
```

3. **配置方式不变**

```typescript
// 配置方式保持不变
import { configureApiClient } from '@radish/http';

configureApiClient({
  baseUrl: 'https://localhost:5000',
});
```

### 迁移检查清单

- [ ] 更新所有 API 文件的导入语句
- [ ] 更新所有组件中的导入语句
- [ ] 更新类型导入
- [ ] 测试所有 API 调用是否正常工作
- [ ] 检查认证是否正常工作

## 最佳实践

### 1. 统一配置管理

在应用入口统一配置，避免在多处重复配置：

```typescript
// src/config/api.ts
import { configureApiClient } from '@radish/http';
import { env } from '@/config/env';

export function initApiClient() {
  configureApiClient({
    baseUrl: env.apiBaseUrl,
    timeout: 30000,
    getToken: () => localStorage.getItem('access_token'),
    onError: (error) => {
      // 统一错误处理
      console.error('API 错误:', error);
    },
  });
}

// src/main.tsx
import { initApiClient } from '@/config/api';

initApiClient();
```

### 2. 创建 API 模块

为每个业务领域创建独立的 API 模块：

```
src/api/
  ├── user.ts       # 用户相关 API
  ├── product.ts    # 商品相关 API
  ├── order.ts      # 订单相关 API
  └── leaderboard.ts # 排行榜相关 API
```

### 3. 统一错误处理

在 API 模块中统一处理错误：

```typescript
async function handleApiCall<T>(
  apiCall: () => Promise<ParsedApiResponse<T>>
): Promise<T> {
  const response = await apiCall();

  if (!response.ok || !response.data) {
    throw new Error(response.message || '请求失败');
  }

  return response.data;
}

// 使用
export const productApi = {
  async getProducts() {
    return handleApiCall(() =>
      apiGet<PagedResponse<Product>>('/api/v1/Shop/GetProducts')
    );
  },
};
```

### 4. 类型安全

始终为 API 响应定义类型：

```typescript
// ✅ 正确：定义类型
interface Product {
  voId: number;
  voName: string;
  voPrice: number;
}

const response = await apiGet<Product>('/api/v1/Shop/GetProduct?id=1');

// ❌ 错误：不定义类型
const response = await apiGet('/api/v1/Shop/GetProduct?id=1');
```

## 常见问题

### Q: 为什么要从 @radish/ui 中独立出来？

A: 职责分离。`@radish/ui` 专注于 UI 组件，`@radish/http` 专注于 HTTP 通信。这样可以：
- 减少 UI 库的体积
- 提高代码可维护性
- 方便独立测试和优化

### Q: 如何处理 401 未授权错误？

A: 在错误拦截器中统一处理：

```typescript
configureApiClient({
  onError: (error) => {
    if (error.message.includes('401')) {
      // 清除 token
      localStorage.removeItem('access_token');
      // 跳转到登录页
      window.location.href = '/login';
    }
  },
});
```

### Q: 如何取消请求？

A: 使用 AbortController：

```typescript
const controller = new AbortController();

const response = await apiGet('/api/v1/Shop/GetProducts', {
  signal: controller.signal,
});

// 取消请求
controller.abort();
```

### Q: 如何处理文件上传？

A: 使用 FormData：

```typescript
const formData = new FormData();
formData.append('file', file);

const response = await apiPost<UploadResult>(
  '/api/v1/Upload/UploadFile',
  formData,
  {
    withAuth: true,
    headers: {
      // 不要设置 Content-Type，让浏览器自动设置
    },
  }
);
```

## 相关文档

- [前端设计](./design.md)
- [认证服务统一指南](../guide/authentication-service.md)
- [环境配置](../guide/configuration.md)

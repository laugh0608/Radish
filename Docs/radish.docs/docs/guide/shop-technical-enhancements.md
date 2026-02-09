# 9. 技术方案完善

> 入口页：[商城系统设计方案](/guide/shop-system)

本文档补充商城系统实现过程中需要完善的技术细节，包括图片管理、商品搜索、幂等性保证、实时通知等关键技术方案。

---

## 9.1 图片管理系统

### 9.1.1 需求分析

商城系统需要处理以下类型的图片：
- 商品主图（必需，用于列表展示）
- 商品详情图（可选，多张，用于详情页轮播）
- 商品分类图标（可选，用于分类导航）

### 9.1.2 存储方案

**推荐方案**：对象存储（OSS/S3）+ CDN

```
用户上传 → API 服务器验证 → 上传到 OSS → 返回 CDN URL → 保存到数据库
```

**优势**：
- 成本低：按实际使用量付费
- 性能好：CDN 加速，全球分发
- 可扩展：无存储容量限制
- 安全性：支持权限控制、防盗链

### 9.1.3 上传接口设计

```csharp
/// <summary>
/// 图片上传控制器
/// </summary>
[Route("api/upload")]
[Authorize(Policy = "Client")]
public class UploadController : ControllerBase
{
    private readonly IFileStorage _fileStorage;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 上传商品图片
    /// </summary>
    [HttpPost("product")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5MB 限制
    public async Task<MessageModel<ImageUploadResultVo>> UploadProductImage(
        IFormFile file)
    {
        // 1. 验证文件
        var validationResult = ValidateImageFile(file);
        if (!validationResult.Success)
            return Failed<ImageUploadResultVo>(validationResult.Message);

        // 2. 生成文件名
        var fileName = GenerateFileName(file);
        var filePath = $"shop/products/{DateTime.Now:yyyy/MM}/{fileName}";

        // 3. 上传到 OSS
        var url = await _fileStorage.UploadAsync(file.OpenReadStream(), filePath);

        // 4. 返回 CDN URL
        var cdnUrl = GetCdnUrl(url);

        return Success(new ImageUploadResultVo
        {
            Url = cdnUrl,
            FileName = fileName,
            FileSize = file.Length
        });
    }

    /// <summary>
    /// 验证图片文件
    /// </summary>
    private ValidationResult ValidateImageFile(IFormFile file)
    {
        // 检查文件是否为空
        if (file == null || file.Length == 0)
            return ValidationResult.Fail("文件不能为空");

        // 检查文件大小（最大 5MB）
        var maxSize = _configuration.GetValue<long>("Upload:MaxImageSize", 5 * 1024 * 1024);
        if (file.Length > maxSize)
            return ValidationResult.Fail($"文件大小不能超过 {maxSize / 1024 / 1024}MB");

        // 检查文件类型
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return ValidationResult.Fail("仅支持 JPG、PNG、GIF、WebP 格式");

        // 检查 MIME 类型
        var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            return ValidationResult.Fail("无效的文件类型");

        return ValidationResult.Ok();
    }

    /// <summary>
    /// 生成唯一文件名
    /// </summary>
    private string GenerateFileName(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var random = Guid.NewGuid().ToString("N").Substring(0, 8);
        return $"{timestamp}_{random}{extension}";
    }

    /// <summary>
    /// 获取 CDN URL
    /// </summary>
    private string GetCdnUrl(string ossUrl)
    {
        var cdnDomain = _configuration["Upload:CdnDomain"];
        if (string.IsNullOrEmpty(cdnDomain))
            return ossUrl;

        // 替换 OSS 域名为 CDN 域名
        var uri = new Uri(ossUrl);
        return $"https://{cdnDomain}{uri.AbsolutePath}";
    }
}
```

### 9.1.4 对象存储接口

```csharp
/// <summary>
/// 文件存储接口
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// 上传文件
    /// </summary>
    Task<string> UploadAsync(Stream stream, string filePath);

    /// <summary>
    /// 删除文件
    /// </summary>
    Task<bool> DeleteAsync(string filePath);

    /// <summary>
    /// 获取文件 URL
    /// </summary>
    string GetUrl(string filePath);
}

/// <summary>
/// 阿里云 OSS 实现
/// </summary>
public class AliyunOssStorage : IFileStorage
{
    private readonly OssClient _client;
    private readonly string _bucketName;

    public async Task<string> UploadAsync(Stream stream, string filePath)
    {
        try
        {
            _client.PutObject(_bucketName, filePath, stream);
            return GetUrl(filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OSS 上传失败：{FilePath}", filePath);
            throw new BusinessException("图片上传失败");
        }
    }

    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            _client.DeleteObject(_bucketName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OSS 删除失败：{FilePath}", filePath);
            return false;
        }
    }

    public string GetUrl(string filePath)
    {
        return $"https://{_bucketName}.oss-cn-hangzhou.aliyuncs.com/{filePath}";
    }
}
```

### 9.1.5 配置文件

```json
{
  "Upload": {
    "Provider": "AliyunOss",
    "MaxImageSize": 5242880,
    "CdnDomain": "cdn.example.com",
    "AliyunOss": {
      "AccessKeyId": "your-access-key-id",
      "AccessKeySecret": "your-access-key-secret",
      "Endpoint": "oss-cn-hangzhou.aliyuncs.com",
      "BucketName": "radish-shop"
    }
  }
}
```

### 9.1.6 前端上传组件

```typescript
// ImageUploader.tsx
export const ImageUploader = ({ value, onChange }: ImageUploaderProps) => {
  const [uploading, setUploading] = useState(false);

  const handleUpload = async (file: File) => {
    setUploading(true);

    try {
      const formData = new FormData();
      formData.append('file', file);

      const result = await shopApi.uploadProductImage(formData);

      onChange(result.url);
      notification.success({ message: '上传成功' });
    } catch (error) {
      notification.error({ message: '上传失败' });
    } finally {
      setUploading(false);
    }
  };

  return (
    <Upload
      accept="image/*"
      showUploadList={false}
      beforeUpload={handleUpload}
      disabled={uploading}
    >
      {value ? (
        <Image src={value} alt="商品图片" style={{ width: 200 }} />
      ) : (
        <Button loading={uploading}>
          {uploading ? '上传中...' : '上传图片'}
        </Button>
      )}
    </Upload>
  );
};
```

---

## 9.2 商品搜索优化

### 9.2.1 当前方案（SQL LIKE）

**缺点**：
- 性能差：无法使用索引，全表扫描
- 功能弱：不支持分词、模糊匹配、拼音搜索
- 排序差：无法按相关度排序

### 9.2.2 优化方案一：PostgreSQL 全文搜索

**适用场景**：中小规模商城（< 10万商品）

```sql
-- 创建全文搜索索引
CREATE INDEX idx_product_search ON ShopProduct
USING GIN(to_tsvector('chinese', name || ' ' || COALESCE(subtitle, '')));

-- 搜索查询
SELECT * FROM ShopProduct
WHERE to_tsvector('chinese', name || ' ' || COALESCE(subtitle, '')) @@ to_tsquery('chinese', '搜索关键词')
ORDER BY ts_rank(to_tsvector('chinese', name || ' ' || subtitle), to_tsquery('chinese', '搜索关键词')) DESC;
```

### 9.2.3 优化方案二：ElasticSearch

**适用场景**：大规模商城（> 10万商品）

**架构**：
```
API → ElasticSearch（搜索） → PostgreSQL（详情）
     ↑
   同步
     ↑
  PostgreSQL（主数据）
```

**索引结构**：
```json
{
  "mappings": {
    "properties": {
      "id": { "type": "long" },
      "name": {
        "type": "text",
        "analyzer": "ik_max_word",
        "search_analyzer": "ik_smart"
      },
      "subtitle": {
        "type": "text",
        "analyzer": "ik_max_word"
      },
      "category_id": { "type": "keyword" },
      "price": { "type": "integer" },
      "sold_count": { "type": "integer" },
      "is_on_sale": { "type": "boolean" },
      "create_time": { "type": "date" }
    }
  }
}
```

**搜索实现**：
```csharp
public async Task<PageModel<ProductListVo>> SearchProductsAsync(ProductSearchVo query)
{
    var searchRequest = new SearchRequest<ProductDocument>
    {
        Query = new BoolQuery
        {
            Must = new List<QueryContainer>
            {
                new MultiMatchQuery
                {
                    Fields = new[] { "name^2", "subtitle" },
                    Query = query.Keyword,
                    Type = TextQueryType.BestFields
                }
            },
            Filter = new List<QueryContainer>
            {
                new TermQuery { Field = "is_on_sale", Value = true }
            }
        },
        Sort = GetSortOptions(query.SortBy),
        From = (query.PageIndex - 1) * query.PageSize,
        Size = query.PageSize
    };

    var response = await _elasticClient.SearchAsync<ProductDocument>(searchRequest);

    return new PageModel<ProductListVo>
    {
        Items = _mapper.Map<List<ProductListVo>>(response.Documents),
        Total = response.Total,
        PageIndex = query.PageIndex,
        PageSize = query.PageSize
    };
}
```

### 9.2.4 搜索关键词高亮

```csharp
var searchRequest = new SearchRequest<ProductDocument>
{
    Query = ...,
    Highlight = new Highlight
    {
        PreTags = new[] { "<em>" },
        PostTags = new[] { "</em>" },
        Fields = new Dictionary<Field, IHighlightField>
        {
            { "name", new HighlightField() },
            { "subtitle", new HighlightField() }
        }
    }
};
```

### 9.2.5 搜索历史记录

```csharp
/// <summary>
/// 保存搜索历史
/// </summary>
public async Task SaveSearchHistoryAsync(long userId, string keyword)
{
    var cacheKey = $"search:history:{userId}";
    var history = await _cache.GetAsync<List<string>>(cacheKey) ?? new List<string>();

    // 去重并移到最前面
    history.Remove(keyword);
    history.Insert(0, keyword);

    // 最多保存 10 条
    if (history.Count > 10)
        history = history.Take(10).ToList();

    await _cache.SetAsync(cacheKey, history, TimeSpan.FromDays(30));
}

/// <summary>
/// 获取搜索历史
/// </summary>
public async Task<List<string>> GetSearchHistoryAsync(long userId)
{
    var cacheKey = $"search:history:{userId}";
    return await _cache.GetAsync<List<string>>(cacheKey) ?? new List<string>();
}
```

---

## 9.3 幂等性保证

### 9.3.1 幂等键设计

**目标**：防止用户重复下单（网络抖动、重复点击等）

**方案**：基于用户ID + 商品ID + 时间窗口生成幂等键

```csharp
/// <summary>
/// 幂等键生成器
/// </summary>
public class IdempotencyKeyGenerator
{
    /// <summary>
    /// 生成幂等键
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="productId">商品ID</param>
    /// <param name="windowMinutes">时间窗口（分钟），默认10分钟</param>
    public static string Generate(long userId, long productId, int windowMinutes = 10)
    {
        // 当前时间按窗口对齐
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (windowMinutes * 60);
        return $"{userId}:{productId}:{timestamp}";
    }
}
```

### 9.3.2 订单创建接口改造

```csharp
/// <summary>
/// 创建订单（支持幂等）
/// </summary>
[HttpPost("purchase")]
public async Task<MessageModel<OrderResultVo>> Purchase([FromBody] OrderCreateVo input)
{
    var userId = GetCurrentUserId();

    // 生成幂等键
    var idempotencyKey = IdempotencyKeyGenerator.Generate(userId, input.ProductId);

    // 调用幂等服务
    var result = await _orderService.CreateOrderWithIdempotencyAsync(
        userId,
        input,
        idempotencyKey
    );

    return Success(result);
}
```

### 9.3.3 幂等实现

```csharp
public async Task<OrderResultVo> CreateOrderWithIdempotencyAsync(
    long userId,
    OrderCreateVo input,
    string idempotencyKey)
{
    var cacheKey = $"order:idempotency:{idempotencyKey}";

    // 1. 检查幂等键是否已使用
    var existingOrderId = await _cache.GetAsync<long?>(cacheKey);
    if (existingOrderId.HasValue)
    {
        Log.Information("订单幂等：用户 {UserId} 重复下单，返回已有订单 {OrderId}",
            userId, existingOrderId.Value);

        var existingOrder = await _orderRepository.QueryFirstAsync(
            o => o.Id == existingOrderId.Value
        );

        return new OrderResultVo
        {
            OrderId = existingOrder.Id,
            OrderNo = existingOrder.OrderNo,
            Status = existingOrder.Status,
            Message = "订单已存在（幂等检查）"
        };
    }

    // 2. 使用分布式锁防止并发
    var lockKey = $"order:lock:{userId}:{input.ProductId}";
    if (!await _distributedLock.TryLockAsync(lockKey, TimeSpan.FromSeconds(10)))
    {
        throw new BusinessException("操作过于频繁，请稍后重试");
    }

    try
    {
        // 3. 创建订单
        var result = await CreateOrderAsync(userId, input);

        // 4. 缓存幂等键（30 分钟）
        await _cache.SetAsync(cacheKey, result.OrderId, TimeSpan.FromMinutes(30));

        return result;
    }
    finally
    {
        await _distributedLock.UnlockAsync(lockKey);
    }
}
```

### 9.3.4 分布式锁实现

```csharp
/// <summary>
/// 分布式锁接口
/// </summary>
public interface IDistributedLock
{
    Task<bool> TryLockAsync(string key, TimeSpan expiry);
    Task UnlockAsync(string key);
}

/// <summary>
/// Redis 分布式锁实现
/// </summary>
public class RedisDistributedLock : IDistributedLock
{
    private readonly IDatabase _redis;
    private readonly Dictionary<string, string> _tokens = new();

    public async Task<bool> TryLockAsync(string key, TimeSpan expiry)
    {
        var token = Guid.NewGuid().ToString("N");
        var acquired = await _redis.StringSetAsync(
            key,
            token,
            expiry,
            When.NotExists
        );

        if (acquired)
        {
            _tokens[key] = token;
        }

        return acquired;
    }

    public async Task UnlockAsync(string key)
    {
        if (!_tokens.TryGetValue(key, out var token))
            return;

        // Lua 脚本确保原子性
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end
        ";

        await _redis.ScriptEvaluateAsync(script, new RedisKey[] { key }, new RedisValue[] { token });

        _tokens.Remove(key);
    }
}
```

---

## 9.4 实时通知推送

### 9.4.1 WebSocket 连接管理

```csharp
/// <summary>
/// WebSocket 连接管理器
/// </summary>
public class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<long, List<WebSocket>> _connections = new();

    public void AddConnection(long userId, WebSocket socket)
    {
        _connections.AddOrUpdate(
            userId,
            _ => new List<WebSocket> { socket },
            (_, list) =>
            {
                list.Add(socket);
                return list;
            }
        );
    }

    public void RemoveConnection(long userId, WebSocket socket)
    {
        if (_connections.TryGetValue(userId, out var list))
        {
            list.Remove(socket);
            if (list.Count == 0)
            {
                _connections.TryRemove(userId, out _);
            }
        }
    }

    public async Task SendToUserAsync(long userId, string message)
    {
        if (_connections.TryGetValue(userId, out var list))
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            foreach (var socket in list.ToList())
            {
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        await socket.SendAsync(
                            new ArraySegment<byte>(buffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "WebSocket 发送失败，移除连接");
                        RemoveConnection(userId, socket);
                    }
                }
            }
        }
    }
}
```

### 9.4.2 通知推送服务

```csharp
/// <summary>
/// 通知推送服务
/// </summary>
public class NotificationPushService
{
    private readonly WebSocketConnectionManager _connectionManager;

    /// <summary>
    /// 推送购买成功通知
    /// </summary>
    public async Task PushOrderCompletedAsync(Order order)
    {
        var message = new
        {
            type = "order.completed",
            data = new
            {
                orderId = order.Id,
                orderNo = order.OrderNo,
                productName = order.ProductName,
                paidAmount = order.PaidAmount
            }
        };

        await _connectionManager.SendToUserAsync(
            order.UserId,
            JsonSerializer.Serialize(message)
        );
    }

    /// <summary>
    /// 推送权益到期提醒
    /// </summary>
    public async Task PushBenefitExpiringAsync(long userId, UserInventory inventory)
    {
        var message = new
        {
            type = "benefit.expiring",
            data = new
            {
                itemName = inventory.ItemName,
                expiresAt = inventory.ExpiresAt
            }
        };

        await _connectionManager.SendToUserAsync(
            userId,
            JsonSerializer.Serialize(message)
        );
    }
}
```

### 9.4.3 前端 WebSocket 连接

```typescript
// useWebSocket.ts
export const useWebSocket = () => {
  const [socket, setSocket] = useState<WebSocket | null>(null);

  useEffect(() => {
    const token = getAuthToken();
    const ws = new WebSocket(`wss://api.example.com/ws?token=${token}`);

    ws.onopen = () => {
      console.log('WebSocket 连接成功');
    };

    ws.onmessage = (event) => {
      const message = JSON.parse(event.data);
      handleMessage(message);
    };

    ws.onerror = (error) => {
      console.error('WebSocket 错误:', error);
    };

    ws.onclose = () => {
      console.log('WebSocket 连接关闭');
      // 5 秒后重连
      setTimeout(() => {
        setSocket(null);
      }, 5000);
    };

    setSocket(ws);

    return () => {
      ws.close();
    };
  }, []);

  const handleMessage = (message: any) => {
    switch (message.type) {
      case 'order.completed':
        notification.success({
          message: '购买成功',
          description: `您已成功购买「${message.data.productName}」`
        });
        break;

      case 'benefit.expiring':
        notification.warning({
          message: '权益即将到期',
          description: `您的「${message.data.itemName}」将于 ${formatDate(message.data.expiresAt)} 到期`
        });
        break;
    }
  };

  return socket;
};
```

---

## 9.5 数据备份与恢复

### 9.5.1 自动备份策略

```bash
#!/bin/bash
# backup-shop.sh

# 配置
DB_NAME="radish"
BACKUP_DIR="/data/backups/shop"
DATE=$(date +%Y%m%d_%H%M%S)
RETENTION_DAYS=30

# 创建备份目录
mkdir -p ${BACKUP_DIR}

# 备份数据库
pg_dump ${DB_NAME} \
  --table="Shop*" \
  --file="${BACKUP_DIR}/Shop_${DATE}.sql" \
  --verbose

# 压缩备份文件
gzip "${BACKUP_DIR}/Shop_${DATE}.sql"

# 删除旧备份
find ${BACKUP_DIR} -name "Shop_*.sql.gz" -mtime +${RETENTION_DAYS} -delete

echo "备份完成：${BACKUP_DIR}/Shop_${DATE}.sql.gz"
```

### 9.5.2 定时任务配置

```bash
# crontab -e
# 每天凌晨 2 点备份
0 2 * * * /scripts/backup-shop.sh >> /var/log/backup-shop.log 2>&1
```

### 9.5.3 数据恢复

```bash
#!/bin/bash
# restore-shop.sh

BACKUP_FILE=$1

if [ -z "$BACKUP_FILE" ]; then
  echo "用法: $0 <备份文件>"
  exit 1
fi

# 解压备份
gunzip -c ${BACKUP_FILE} > /tmp/restore.sql

# 恢复数据库
psql radish < /tmp/restore.sql

# 清理临时文件
rm /tmp/restore.sql

echo "恢复完成"
```

---

## 9.6 审计日志

### 9.6.1 审计日志表设计

```sql
CREATE TABLE ShopAuditLog (
    id BIGINT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    action VARCHAR(50) NOT NULL,
    entity_type VARCHAR(50) NOT NULL,
    entity_id VARCHAR(100),
    old_value TEXT,
    new_value TEXT,
    ip_address VARCHAR(50),
    user_agent VARCHAR(500),
    create_time TIMESTAMP NOT NULL,
    INDEX idx_user_action (user_id, action),
    INDEX idx_entity (entity_type, entity_id),
    INDEX idx_create_time (create_time)
);
```

### 9.6.2 审计日志记录

```csharp
/// <summary>
/// 审计日志服务
/// </summary>
public class AuditLogService
{
    public async Task LogAsync(AuditLogVo log)
    {
        var entity = new AuditLog
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            UserId = log.UserId,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            OldValue = log.OldValue != null ? JsonSerializer.Serialize(log.OldValue) : null,
            NewValue = log.NewValue != null ? JsonSerializer.Serialize(log.NewValue) : null,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            CreateTime = DateTime.Now
        };

        await _auditLogRepository.AddAsync(entity);
    }
}

// 使用示例
await _auditLogService.LogAsync(new AuditLogVo
{
    UserId = userId,
    Action = "CreateOrder",
    EntityType = "Order",
    EntityId = order.Id.ToString(),
    NewValue = order,
    IpAddress = GetClientIp(),
    UserAgent = GetUserAgent()
});
```

---

## 9.7 性能监控

### 9.7.1 APM 集成

**推荐方案**：SkyWalking / Application Insights

```csharp
// Program.cs
builder.Services.AddSkyApmExtensions();

// appsettings.json
{
  "SkyWalking": {
    "ServiceName": "radish-shop-api",
    "Namespace": "production",
    "HeaderVersions": [ "sw8" ],
    "Sampling": {
      "SamplePer3Secs": -1
    },
    "Logging": {
      "Level": "Information"
    },
    "Transport": {
      "gRPC": {
        "Servers": "skywalking-server:11800"
      }
    }
  }
}
```

### 9.7.2 慢查询监控

```csharp
/// <summary>
/// SQL 性能监控过滤器
/// </summary>
public class SqlPerformanceMonitor : IDataFilter
{
    public void OnExecuting(SqlSugarClient context, string sql, SugarParameter[] pars)
    {
        context.Ado.SqlExecutionTime = Stopwatch.StartNew();
    }

    public void OnExecuted(SqlSugarClient context, string sql, SugarParameter[] pars)
    {
        var stopwatch = context.Ado.SqlExecutionTime as Stopwatch;
        if (stopwatch != null)
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;

            // 超过 1 秒的查询记录为慢查询
            if (elapsed > 1000)
            {
                Log.Warning("慢查询检测：耗时 {Elapsed}ms，SQL: {Sql}",
                    elapsed, sql);
            }
        }
    }
}
```

---

## 9.8 接口文档规范

### 9.8.1 Swagger 配置完善

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Radish Shop API",
        Version = "v1",
        Description = "Radish 商城系统 API 文档",
        Contact = new OpenApiContact
        {
            Name = "Radish Team",
            Email = "team@radish.com"
        }
    });

    // 添加 JWT 认证
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    // 添加 XML 注释
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);

    // 添加示例
    options.SchemaFilter<ExampleSchemaFilter>();
});
```

### 9.8.2 API 注释示例

```csharp
/// <summary>
/// 购买商品
/// </summary>
/// <param name="input">购买参数</param>
/// <returns>订单结果</returns>
/// <response code="200">购买成功</response>
/// <response code="400">余额不足、库存不足等业务错误</response>
/// <response code="401">未登录</response>
/// <response code="429">操作过于频繁</response>
[HttpPost("purchase")]
[ProducesResponseType(typeof(MessageModel<OrderResultVo>), 200)]
[ProducesResponseType(typeof(MessageModel), 400)]
public async Task<MessageModel<OrderResultVo>> Purchase(
    [FromBody] OrderCreateVo input)
{
    // ...
}

/// <summary>
/// 购买参数
/// </summary>
public class OrderCreateVo
{
    /// <summary>
    /// 商品 ID
    /// </summary>
    /// <example>1234567890123456789</example>
    [Required(ErrorMessage = "商品 ID 不能为空")]
    public long ProductId { get; set; }

    /// <summary>
    /// 购买数量
    /// </summary>
    /// <example>1</example>
    [Range(1, 100, ErrorMessage = "购买数量必须在 1-100 之间")]
    public int Quantity { get; set; } = 1;
}
```

---

## 9.9 总结

本文档补充了商城系统实现过程中的关键技术方案：

1. **图片管理系统**：对象存储 + CDN 方案，支持图片上传、验证、压缩
2. **商品搜索优化**：PostgreSQL 全文搜索 / ElasticSearch 方案
3. **幂等性保证**：幂等键 + 分布式锁，防止重复下单
4. **实时通知推送**：WebSocket 连接管理，实时推送订单、权益通知
5. **数据备份恢复**：自动备份策略，灾难恢复流程
6. **审计日志**：完整的操作审计，可追溯
7. **性能监控**：APM 集成，慢查询监控
8. **接口文档**：Swagger 规范，示例完善

这些技术方案应在实施过程中根据实际情况选择性实现，优先级建议：
- **P0（必须）**：图片管理、幂等性保证、数据备份
- **P1（重要）**：实时通知、接口文档、审计日志
- **P2（可选）**：商品搜索优化、性能监控

---

> 相关文档：
> - [6. 后端设计](/guide/shop-backend)
> - [7. 前端设计](/guide/shop-frontend)
> - [8. 实施计划](/guide/shop-roadmap)

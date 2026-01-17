# 日志工具使用指南

## 概述

Client 项目现在使用统一的日志工具 `logger.ts`，替代直接使用 `console.*` 调用。

## 为什么需要日志工具？

1. **生产环境控制**：自动根据 `env.debug` 控制调试日志输出
2. **统一格式**：所有日志包含时间戳、级别、标签
3. **便于调试**：支持分组、表格、性能计时等高级功能
4. **减少噪音**：生产环境只输出警告和错误

## 使用方式

### 基础用法

```typescript
import { log } from '@/utils/logger';

// 调试日志（仅 debug 模式）
log.debug('用户登录成功');
log.debug('API', '请求成功', response);

// 信息日志（仅 debug 模式）
log.info('数据加载完成');

// 警告日志（总是输出）
log.warn('Token 即将过期');

// 错误日志（总是输出）
log.error('请求失败:', error);
```

### 带标签的日志

```typescript
// 推荐：使用标签分类日志
log.debug('NotificationHub', '连接成功');
log.warn('AuthService', 'Token 未找到');
log.error('API', '请求失败:', error);
```

### 高级功能

```typescript
// 分组日志
log.group('用户信息');
log.debug('用户名:', user.name);
log.debug('邮箱:', user.email);
log.groupEnd();

// 表格日志
log.table(users);

// 性能计时
log.time('数据加载');
await loadData();
log.timeEnd('数据加载');
```

## 日志级别说明

| 级别 | 方法 | 何时输出 | 用途 |
|------|------|----------|------|
| DEBUG | `log.debug()` | 仅 debug 模式 | 开发调试信息 |
| INFO | `log.info()` | 仅 debug 模式 | 一般信息记录 |
| WARN | `log.warn()` | 总是输出 | 警告信息 |
| ERROR | `log.error()` | 总是输出 | 错误信息 |

## 迁移指南

### 替换规则

```typescript
// ❌ 旧方式
console.log('连接成功');
console.info('数据加载');
console.warn('警告');
console.error('错误');

// ✅ 新方式
log.debug('连接成功');
log.info('数据加载');
log.warn('警告');
log.error('错误');
```

### 批量替换

项目提供了批量替换脚本 `scripts/replace-console-logs.sh`：

```bash
# 查看帮助
bash scripts/replace-console-logs.sh --help

# 批量替换所有文件
bash scripts/replace-console-logs.sh

# 替换指定文件
bash scripts/replace-console-logs.sh src/services/notificationHub.ts
```

**注意**：批量替换后请检查：
1. import 语句是否正确
2. 是否有重复的 import
3. 运行 `npm run lint` 检查语法

## 已迁移的文件

- ✅ `services/notificationHub.ts` - SignalR 连接日志

## 待迁移的文件

以下文件仍在使用 `console.*`，建议逐步迁移：

- `apps/leaderboard/LeaderboardApp.tsx`
- `hooks/useLevelUpListener.ts`
- `apps/forum/components/PublishPostForm.tsx`
- `apps/shop/hooks/useShopActions.ts`
- `apps/experience-detail/ExperienceDetailApp.tsx`
- `apps/shop/hooks/useShopData.ts`
- `api/attachment.ts`
- `apps/forum/components/CommentNode.tsx`
- `desktop/components/ExperienceDisplay.tsx`
- `apps/shop/pages/OrderDetail.tsx`
- `apps/notification/NotificationApp.tsx`
- `apps/forum/components/PublishPostModal.tsx`
- `apps/profile/ProfileApp.tsx`
- `apps/forum/components/CreateCommentForm.tsx`
- `apps/profile/components/UserPostList.tsx`
- `apps/profile/components/UserCommentList.tsx`
- `apps/profile/components/UserInfoCard.tsx`
- `apps/forum/hooks/useForumData.ts`

## 最佳实践

1. **使用标签**：为不同模块使用不同的标签
   ```typescript
   log.debug('Forum', '帖子加载完成');
   log.debug('Shop', '商品列表更新');
   ```

2. **错误处理**：总是记录错误
   ```typescript
   try {
     await api.call();
   } catch (error) {
     log.error('API', '请求失败:', error);
   }
   ```

3. **性能监控**：使用计时功能
   ```typescript
   log.time('数据加载');
   const data = await loadData();
   log.timeEnd('数据加载');
   ```

4. **避免敏感信息**：不要记录密码、Token 等敏感信息
   ```typescript
   // ❌ 错误
   log.debug('Token:', token);

   // ✅ 正确
   log.debug('Token 已获取');
   ```

## 环境配置

在 `.env.development` 中设置：

```bash
# 启用调试日志
VITE_DEBUG=true
```

在 `.env.production` 中设置：

```bash
# 禁用调试日志
VITE_DEBUG=false
```

## 输出示例

```
[14:30:25] [DEBUG][NotificationHub] 连接成功
[14:30:26] [DEBUG][NotificationHub] 未读数更新: 3
[14:30:27] [WARN][AuthService] Token 即将过期
[14:30:28] [ERROR][API] 请求失败: Network Error
```

## 常见问题

### Q: 为什么我的 debug 日志没有输出？

A: 检查 `.env.development` 中的 `VITE_DEBUG` 是否设置为 `true`。

### Q: 生产环境会输出日志吗？

A: 只会输出 `warn` 和 `error` 级别的日志，`debug` 和 `info` 会被自动禁用。

### Q: 如何临时启用生产环境的调试日志？

A: 在浏览器控制台执行：
```javascript
localStorage.setItem('force_debug', 'true');
location.reload();
```

## 参考资料

- [logger.ts 源码](../radish.client/src/utils/logger.ts)
- [环境配置文档](../radish.client/README.md#环境配置)

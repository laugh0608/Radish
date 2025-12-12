# 论坛 API 测试指南

## 📋 测试前准备

### 1. 启动服务

```bash
# 方式1: 直接启动 API
dotnet run --project Radish.Api

# 方式2: 使用启动脚本（同时启动多个服务）
pwsh ./start.ps1  # Windows/PowerShell
./start.sh        # Linux/macOS
```

API 默认运行在：`http://localhost:5100`

### 2. 初始化数据库

确保数据库已经初始化并包含论坛种子数据：

```bash
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- init
```

这会创建：
- 3 个论坛分类（技术交流、生活随笔、问答讨论）
- 5 个标签（C#, .NET, Web开发, 数据库, 综合）

### 3. 配置环境变量（已配置）

`http-client.env.json` 已经配置好：
- **Api_HostAddress**: `http://localhost:5100`
- **JwtToken**: 测试用的 JWT Token

**当前种子用户**：
- `system` (ID: 20000, 角色: System) - 系统管理员
- `admin` (ID: 20001, 角色: Admin) - 管理员
- `test` (ID: 20002, 角色: Test) - 测试用户

**注意**：JWT Token 可能已过期，如果遇到 401 错误，需要通过 Auth 服务重新获取 Token。

## 🧪 测试方式

### 使用 VS Code + REST Client 插件

1. 安装插件：`REST Client` (humao.rest-client)
2. 打开文件：`Radish.Api/Radish.Api.http`
3. 点击请求上方的 `Send Request` 按钮

### 使用 JetBrains Rider / IntelliJ IDEA

1. 打开文件：`Radish.Api/Radish.Api.http`
2. 点击请求左侧的绿色运行按钮

### 使用 Visual Studio 2022

1. 打开文件：`Radish.Api/Radish.Api.http`
2. 点击请求旁边的 `Send` 按钮

## 📝 测试清单

### 基础功能测试（18个请求）

#### ✅ 分类管理（4个）
- [ ] 1. 获取所有顶级分类
- [ ] 2. 获取子分类
- [ ] 3. 根据ID获取分类详情
- [ ] 4. 创建新分类（需要认证）

#### ✅ 标签管理（4个）
- [ ] 5. 获取所有标签
- [ ] 6. 获取热门标签（默认）
- [ ] 7. 获取热门标签（指定数量）
- [ ] 8. 根据ID获取标签详情

#### ✅ 帖子管理（6个）
- [ ] 9. 获取所有帖子列表
- [ ] 10. 获取指定分类的帖子
- [ ] 11. 根据ID获取帖子详情
- [ ] 12. 发布新帖子（完整数据）
- [ ] 13. 发布帖子（最小化数据）
- [ ] 14. 更新帖子

#### ✅ 评论管理（4个）
- [ ] 15. 获取评论树
- [ ] 16. 发表根评论
- [ ] 17. 发表回复评论
- [ ] 18. 发表多级嵌套回复

### 综合测试场景

#### 场景1: 完整业务流程
```
创建分类 → 发布帖子 → 查看详情 → 发表评论 → 查看评论树
```

#### 场景2-4: 边界测试
- [ ] 查询不存在的分类（应返回 404）
- [ ] 查询不存在的帖子（应返回 404）
- [ ] 查询不存在的标签（应返回 404）

#### 场景5-6: 错误测试
- [ ] 发布空内容的帖子（应返回 400）
- [ ] 发表空内容的评论（应返回 400）

## 🎯 推荐测试顺序

### 第一阶段：数据查询（无需认证）
1. 测试请求 1-3：分类查询
2. 测试请求 5-8：标签查询
3. 验证种子数据是否正确

### 第二阶段：数据创建（需要认证）
1. 测试请求 4：创建新分类
2. 测试请求 12-13：发布帖子
3. 检查数据库中的数据

### 第三阶段：关联功能
1. 测试请求 9-11：查询帖子
2. 测试请求 15-18：评论功能
3. 验证评论树结构

### 第四阶段：边界和错误处理
1. 测试场景 2-4：边界测试
2. 测试场景 5-6：错误测试
3. 验证错误响应格式

## 📊 预期响应格式

### 成功响应
```json7
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "获取成功",
  "responseData": { /* 数据对象 */ }
}
```

### 失败响应
```json
{
  "isSuccess": false,
  "statusCode": 404,
  "messageInfo": "资源不存在"
}
```

## 🔍 常见问题

### 1. 请求返回 401 Unauthorized

**原因**：JWT Token 过期

**解决方法**：需要重新获取 Token

#### 方式1：通过 Auth 服务获取 Token（推荐）

```bash
# 1. 启动 Auth 服务
dotnet run --project Radish.Auth

# 2. 使用浏览器访问或使用 HTTP 请求获取 Token
# POST http://localhost:5200/connect/token
# 参数：
#   grant_type=password
#   client_id=radish-web
#   client_secret=radish-secret
#   username=system (或 admin, test)
#   password=radish123 (默认密码)
#   scope=openid profile radish-api
```

#### 方式2：临时使用（测试环境）

如果只是临时测试，可以继续使用当前的 Token，但某些需要特定用户权限的操作可能会失败。

#### 方式3：更新 http-client.env.json

获取新 Token 后，更新 `Radish.Api/http-client.env.json` 文件中的 `JwtToken` 值：

```json
{
  "Radish": {
    "Api_HostAddress": "http://localhost:5100",
    "JwtToken": "新的Token字符串"
  }
}
```

### 2. 请求返回 404 Not Found

**原因**：
- 路由错误（检查 URL 是否正确）
- 资源不存在（检查 ID 是否有效）
- API 版本不匹配（确认使用 v1.0）

### 3. 创建帖子时找不到分类

**原因**：CategoryId 不存在

**解决**：
1. 先执行"获取所有顶级分类"请求
2. 使用返回的分类 ID
3. 或者先创建一个新分类

### 4. 评论树为空

**原因**：帖子还没有评论

**解决**：
1. 先发表根评论（请求 16）
2. 再获取评论树（请求 15）

## 📈 测试报告

测试完成后，可以记录以下信息：

| 功能模块 | 测试用例数 | 通过 | 失败 | 备注 |
|---------|-----------|------|------|------|
| 分类管理 | 4 | | | |
| 标签管理 | 4 | | | |
| 帖子管理 | 6 | | | |
| 评论管理 | 4 | | | |
| 边界测试 | 3 | | | |
| 错误测试 | 2 | | | |
| **总计** | **23** | | | |

## 🚀 下一步

测试通过后，可以：
1. 编写单元测试（`Radish.Api.Tests/Controllers/`）
2. 开发前端论坛页面
3. 实现更多高级功能（搜索、点赞、收藏等）

## 📚 相关文档

- [开发规范](../radish.docs/docs/DevelopmentSpecifications.md)
- [BaseService 使用指南](../radish.docs/docs/DevelopmentSpecifications.md#baseservice-与-baserepository-使用指南)
- [论坛模型设计](../radish.docs/docs/ForumDesign.md)

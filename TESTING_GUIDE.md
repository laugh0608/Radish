# 统一Gateway架构测试指引

## 问题分析总结

### 已修复的问题
1. ✅ **Client logout报错** - 在数据库中添加了带trailing slash的post_logout_redirect_uri
2. ✅ **YARP路由配置** - 修复了PathPattern错误
3. ✅ **OpenIddict配置** - 简化为仅使用Gateway URL

### 当前问题根源
所有报告的问题都源于**浏览器缓存了旧的Auth Server session（cookie）**：

1. **Client点击登录直接成功** = Auth Server的cookie还在，跳过了登录页面
2. **Console点击登录原地刷新** = Auth Server快速完成了重定向（因为有cookie），整个流程1-2秒完成，看起来像是刷新

## 完整测试步骤

### 步骤1: 停止所有服务

```bash
# 在所有终端中按 Ctrl+C 停止服务
# 或者使用 pkill
pkill -f "dotnet run"
pkill -f "vite"
```

### 步骤2: 清除浏览器状态（重要！）

**方法A: 使用浏览器无痕/隐私模式（推荐）**
- Chrome/Edge: Ctrl+Shift+N
- Firefox: Ctrl+Shift+P

**方法B: 手动清除（如果不想用无痕模式）**
1. 打开浏览器开发者工具 (F12)
2. Application/存储 标签页
3. 清除以下内容：
   - **Cookies**: 删除 localhost 的所有 cookies（特别是 `.AspNetCore.Cookies` 和 OpenIddict相关cookie）
   - **Local Storage**: 清除 localhost 的所有条目
   - **Session Storage**: 清除所有条目
4. 关闭所有 localhost 标签页

### 步骤3: 按顺序启动所有服务

**终端1: Gateway**
```bash
dotnet run --project Radish.Gateway/Radish.Gateway.csproj
```
等待提示: `Now listening on: https://localhost:5000`

**终端2: Auth**
```bash
dotnet run --project Radish.Auth/Radish.Auth.csproj
```
等待提示: `Now listening on: http://localhost:5200`

**终端3: API**
```bash
dotnet run --project Radish.Api/Radish.Api.csproj
```
等待提示: `Now listening on: http://localhost:5100`

**终端4: Client**
```bash
npm run dev --workspace=radish.client
```
等待提示: `VITE ready in xxx ms` 和 `Local: http://localhost:3000/`

**终端5: Console**
```bash
npm run dev --workspace=radish.console
```
等待提示: `VITE ready in xxx ms` 和 `Local: http://localhost:3002/`

### 步骤4: 测试Client登录/登出流程

1. **访问Client**
   - 打开浏览器（无痕模式或已清除状态）
   - 访问: `https://localhost:5000/`
   - 证书警告: 点击"高级" → "继续访问"

2. **测试登录**
   - 点击右上角"登录"按钮
   - **预期**: 应该跳转到Auth Server登录页面（`https://localhost:5000/Account/Login`）
   - **而不是**: 直接跳回Client（那说明还有缓存的session）
   - 输入账号密码: `admin` / `admin123456`
   - 点击登录
   - **预期**: 重定向回Client，右上角显示"退出"按钮

3. **测试登出**
   - 点击右上角"退出"按钮
   - **预期**: 应该跳转到Auth Server登出确认页面，然后自动重定向回Client首页
   - **而不是**: 显示 `invalid_request` 错误（已修复）
   - 确认回到Client首页，右上角显示"登录"按钮

### 步骤5: 测试Console登录流程

1. **访问Console**
   - 在同一个浏览器中（不要新开无痕窗口）
   - 访问: `https://localhost:5000/console/`
   - **预期**: 显示Console登录页面

2. **测试SSO（单点登录）**
   - **如果刚才已经在Client登录过**，Auth Server还有session
   - 点击"OIDC登录（推荐）"按钮
   - **预期**: 应该快速重定向（1-2秒），直接进入Console管理界面，无需再次输入密码
   - 这是**正常的SSO行为**，不是bug！

3. **测试独立登录（如果刚才已登出）**
   - 如果刚才在Client点击了登出，Auth Server的session已清除
   - 点击"OIDC登录（推荐）"按钮
   - **预期**: 跳转到Auth Server登录页面，输入账号密码后进入Console

### 步骤6: 验证SSO（在两个应用间切换）

1. 在Client登录
2. 打开新标签页访问Console: `https://localhost:5000/console/`
3. 点击"OIDC登录"
4. **预期**: 无需输入密码，直接进入Console（因为Client已经登录了）
5. 在Console点击右上角头像 → "退出"
6. 刷新Client页面
7. **预期**: Client也被登出了（SSO的反向效果）

## 预期行为说明

### "原地刷新"不是Bug
用户报告的"Console点击登录后原地刷新"其实是**正常的快速SSO流程**：

1. Console跳转到 `/connect/authorize`
2. Auth Server检测到有效session（cookie）
3. 立即重定向到 `/console/callback?code=xxx`
4. Console用code换取token（1个HTTP请求）
5. 保存token后重定向到 `/console/`

整个流程只需要1-2秒，在用户看来就像是"刷新"了一下。这是**预期的行为**，说明SSO正常工作。

### 如何区分Bug和正常行为

**✅ 正常**:
- Client登录后，Console快速完成登录（SSO）
- 点击登录后看到短暂的"正在完成登录..."提示
- 1-2秒后进入管理界面

**❌ 异常**:
- 点击登录后完全没反应（页面不跳转）
- 点击登录后显示错误消息
- 登录后页面无限刷新（循环重定向）
- 退出登录后显示 `invalid_request` 错误

## 数据库配置验证

当前OpenIddict数据库配置（已修复）:

```bash
# 验证命令
sqlite3 DataBases/RadishAuth.OpenIddict.db \
  "SELECT ClientId, RedirectUris, PostLogoutRedirectUris FROM OpenIddictApplications"
```

**预期输出**:
```
radish-client|["https://localhost:5000/oidc/callback"]|["https://localhost:5000","https://localhost:5000/"]
radish-console|["https://localhost:5000/console/callback"]|["https://localhost:5000/console","https://localhost:5000/console/"]
```

注意 `PostLogoutRedirectUris` 现在包含了带和不带trailing slash的两个版本。

## 故障排查

### 如果Client登出还是报错

1. 检查浏览器控制台的Network标签页
2. 找到 `/connect/endsession` 请求
3. 查看请求的 `post_logout_redirect_uri` 参数
4. 查看响应状态码和错误消息

### 如果Console登录无反应

1. 打开浏览器开发者工具 (F12)
2. 切换到Console标签页，查看JavaScript错误
3. 切换到Network标签页，查看是否有网络请求失败
4. 确认所有服务（Gateway/Auth/API）都在运行

### 如果HMR不工作

当前配置下，HMR直接连接到Vite开发服务器（3000/3002端口），不通过Gateway。如果HMR不工作：

1. 确认Vite开发服务器正在运行（终端4和5）
2. 检查浏览器控制台是否有WebSocket连接错误
3. 如果看到 `ws://localhost:3000` 连接失败，重启Vite服务器

## 成功标志

如果所有功能正常，你应该能够：

1. ✅ 在Client登录时看到Auth Server登录页面（首次登录）
2. ✅ Client登出后不报错，正常返回首页
3. ✅ Console登录时快速完成（有SSO session时）
4. ✅ 在Client和Console之间实现单点登录
5. ✅ 修改前端代码后自动热更新（HMR）

## 架构总结

### 开发环境架构
```
浏览器
  ↓ https://localhost:5000/
Gateway (5000) ← 用户访问的唯一入口
  ↓ 路由转发
  ├─→ Auth (5200)    ← /connect/* 请求
  ├─→ API (5100)     ← /api/* 请求
  ├─→ Client (3000)  ← / 请求（根路径和前端资源）
  └─→ Console (3002) ← /console/* 请求

HMR WebSocket 连接 (不通过Gateway):
  - Client: ws://localhost:3000
  - Console: ws://localhost:3002
```

### Cookie域
- **所有Cookie域**: `localhost`
- **所有请求Origin**: `https://localhost:5000`
- **无跨域问题**: Client、Console、API、Auth在浏览器看来都是同一个域

这就是为什么统一Gateway架构能解决之前的所有问题！

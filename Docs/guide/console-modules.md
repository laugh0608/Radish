# Console 功能模块

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 3 章

## 3. 功能模块

本章只记录 **当前真实模块状态、权限边界和治理结果**，不再保留已明显过期的“理想功能全景图”。

## 3.1 模块总览

| 模块 | 页面状态 | 核心权限 | 资源种子状态 | 备注 |
| --- | --- | --- | --- | --- |
| Dashboard | ✅ 已接入 | `console.dashboard.view` | ✅ 已补齐 | 最近订单受 `console.orders.view` 约束 |
| Applications | ✅ 已接入 | `console.applications.*` | ✅ 已补齐 | 列表/新增/编辑/删除/重置密钥已闭环 |
| Users | ✅ 已接入 | `console.users.view` | ✅ 已补齐 | 未落地操作已下线 |
| Roles | ✅ 已接入 | `console.roles.*` | ✅ 已补齐 | 角色详情与编辑链路已闭环 |
| Products | ✅ 已接入 | `console.products.*` | ✅ 已补齐 | `GetCategories` 辅助接口已纳入 |
| Orders | ✅ 已接入 | `console.orders.*` | ✅ 已补齐 | 当前主动作仅保留查看/重试 |
| Tags | ✅ 已接入 | `console.tags.*` | ✅ 已补齐 | 页面与资源映射已对齐 |
| Stickers | ✅ 已接入 | `console.stickers.*` | ✅ 已补齐 | 编码校验与批量上传辅助接口已纳入 |
| SystemConfig | ✅ 已接入 | `console.system-config.*` | ✅ 已补齐 | 编辑详情与站点图标链路已闭环 |
| Hangfire | ✅ 已接入 | `console.hangfire.view` | ✅ 已补齐 | 通过特殊入口授权过滤器校验 |

## 3.2 Dashboard

### 当前边界

- 页面访问依赖 `console.dashboard.view`
- “最近订单”仅在拥有 `console.orders.view` 时加载
- 快捷入口按具体模块权限分别控制

### 当前状态

- ✅ 页面权限闭环完成
- ✅ 资源种子闭环完成
- ⏸️ 趋势图与扩展统计接口未纳入本阶段

## 3.3 Applications

### 当前边界

- 列表：`console.applications.view`
- 新增：`console.applications.create`
- 编辑：`console.applications.edit`
- 删除：`console.applications.delete`
- 重置密钥：`console.applications.reset-secret`

### 当前状态

- ✅ 页面访问与按钮级权限均已接入
- ✅ 详情加载资源映射已对齐
- ⏸️ 独立详情页、使用统计暂不作为当前主线

## 3.4 Users

### 当前边界

当前只保留：

- 列表访问
- 用户详情入口（按 `console.users.view`）

### 已明确下线

以下能力已从页面与权限常量中移除：

- 创建用户
- 更新状态
- 重置密码
- 强制下线
- 删除用户

### 当前状态

- ✅ 页面访问闭环完成
- ✅ 误暴露入口收口完成
- ⏸️ 用户详情仍以占位/模拟数据为主，未进入下一轮功能扩张

## 3.5 Roles

### 当前边界

- 查看：`console.roles.view`
- 创建：`console.roles.create`
- 编辑：`console.roles.edit`
- 启用/禁用：`console.roles.toggle`
- 删除：`console.roles.delete`

### 当前状态

- ✅ 首批权限治理闭环模块
- ✅ 详情加载与编辑链路已对齐到资源映射

## 3.6 Products

### 当前边界

- 查看：`console.products.view`
- 创建：`console.products.create`
- 编辑：`console.products.edit`
- 删除：`console.products.delete`
- 上下架：`console.products.toggle-sale`

### 辅助接口

已纳入本轮治理：

- `Shop/GetCategories -> console.products.view`

### 当前状态

- ✅ 页面与按钮权限已接入
- ✅ 辅助接口资源种子已补齐
- ⏸️ `AdminGetProduct` 未被当前页面实际使用，暂不纳入额外补齐范围

## 3.7 Orders

### 当前边界

- 查看：`console.orders.view`
- 重试发放：`console.orders.retry`

### 当前状态

- ✅ 页面访问已接入
- ✅ 重试按钮按权限控制
- ⏸️ 更完整的订单处理后台不在本阶段展开

## 3.8 Tags

### 当前边界

- 查看 / 创建 / 编辑 / 删除 / 恢复 / 启停 / 排序均已独立授权

### 当前状态

- ✅ 页面访问与操作权限已接入
- ✅ 资源映射与种子已闭环

## 3.9 Stickers

### 当前边界

- 查看：`console.stickers.view`
- 创建：`console.stickers.create`
- 编辑：`console.stickers.edit`
- 删除：`console.stickers.delete`
- 启停：`console.stickers.toggle`
- 排序：`console.stickers.sort`
- 批量上传：`console.stickers.batch-upload`

### 本轮已补齐的辅助接口

- `Sticker/CheckGroupCode`
- `Sticker/CheckStickerCode`
- `Sticker/NormalizeCode`

### 当前状态

- ✅ 页面权限闭环完成
- ✅ 批量上传辅助接口种子已补齐
- ✅ `Attachment/UploadImage` 已按方案 B 完成最小收口：仅 `Sticker` / `StickerCover` 业务类型复用既有 Sticker 权限

## 3.10 SystemConfig

### 当前边界

- 查看：`console.system-config.view`
- 创建：`console.system-config.create`
- 编辑：`console.system-config.edit`
- 删除：`console.system-config.delete`
- 公开站点设置：`GET /api/v1/SystemConfig/GetPublicSiteSettings`（匿名读取，不纳入 Console 权限）

### 当前状态

- ✅ 页面访问闭环完成
- ✅ 编辑详情资源映射已对齐
- ✅ 系统配置当前已改为本地 JSON 持久化，默认落盘到 `DataBases/SystemConfigs/system-configs.json`
- ✅ `system-configs.json` 当前按“UTF-8 + 中文直写、仅保留必要 JSON 转义”的策略落盘，便于本地排障、人工审阅与差异比对
- ✅ `DataBases/SystemConfigs/system-configs.json` 属于运行时本地状态文件，不作为源码资产提交，继续遵循 `DataBases/` 目录忽略规则
- ✅ SystemConfig 页面已支持站点 favicon `.ico` 上传、预览与恢复默认
- ✅ 默认站点图标已固定为 `/uploads/DefaultIco/bailuobo.ico`，默认种子文件位于 `DataBases/Uploads/DefaultIco/bailuobo.ico`
- ✅ `radish.client / radish.console` 当前都通过公开站点设置接口读取 favicon，标签页图标不再写死在前端静态资源中

## 3.11 Hangfire

### 当前边界

- 查看：`console.hangfire.view`

### 当前状态

- ✅ 已不再依赖 `System/Admin` 角色硬编码放行
- ✅ 资源 URL 与种子已对齐到 `/hangfire(/.*)?`

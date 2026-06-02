# Console 功能模块

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 3 章

## 3. 功能模块

本章只记录 **当前真实模块状态、权限边界和治理结果**，不再保留已明显过期的“理想功能全景图”。

## 3.1 模块总览

| 模块 | 页面状态 | 页面类型 | 核心权限 | 资源种子状态 | 备注 |
| --- | --- | --- | --- | --- | --- |
| Dashboard | ✅ 已接入 | 调度总览 | `console.dashboard.view` | ✅ 已补齐 | 最近订单受 `console.orders.view` 约束 |
| Applications | ✅ 已接入 | 表格 CRUD | `console.applications.*` | ✅ 已补齐 | 列表/新增/编辑/删除/重置密钥已闭环 |
| Users | ✅ 已接入 | 表格 CRUD + 详情 | `console.users.view` | ✅ 已补齐 | 用户详情、资产、经验、订单摘要已接入 |
| Roles | ✅ 已接入 | 表格 CRUD + 权限配置 | `console.roles.*` | ✅ 已补齐 | 角色详情、编辑和权限配置链路已闭环 |
| Products | ✅ 已接入 | 表格 CRUD | `console.products.*` | ✅ 已补齐 | `GetCategories` 辅助接口已纳入 |
| Orders | ✅ 已接入 | 表格 CRUD + 详情弹层 | `console.orders.*` | ✅ 已补齐 | 查看、重试、管理员备注和扣款流水定位已接入 |
| Tags | ✅ 已接入 | 表格 CRUD | `console.tags.*` | ✅ 已补齐 | 页面与资源映射已对齐 |
| Stickers | ✅ 已接入 | 表格 CRUD | `console.stickers.*` | ✅ 已补齐 | 编码校验与批量上传辅助接口已纳入 |
| SystemConfig | ✅ 已接入 | 表格 CRUD + 配置面板 | `console.system-config.*` | ✅ 已补齐 | 编辑详情与站点图标链路已闭环 |
| Coins | ✅ 已接入 | 工具型页面 | `console.coins.*` | ✅ 已补齐 | 用户余额查询、业务流水筛选与管理员调账已接入 |
| Experience | ✅ 已接入 | 治理工作台 | `console.experience.*` | ✅ 已补齐 | 经验观察、流水、冻结、调整和等级配置已接入 |
| Moderation | ✅ 已接入 | 治理工作台 | `console.moderation.*` | ✅ 已补齐 | 举报审核、手动治理和治理日志已接入 |
| Settings / Profile | ✅ 已接入 | 设置 / 个人资料 | 登录态 | 不适用 | 个人偏好、密码修改、头像上传和资料保存不走 Console 专属权限树 |
| Hangfire | ✅ 已接入 | 特殊入口 | `console.hangfire.view` | ✅ 已补齐 | 通过特殊入口授权过滤器校验 |

页面类型只描述 UI 承载方式，不改变权限键、API 契约或业务动作语义。新增页面时先确认权限模型，再按页面类型选择布局基座。

## 3.2 Dashboard

### 当前边界

- 页面访问依赖 `console.dashboard.view`
- “最近订单”仅在拥有 `console.orders.view` 时加载
- 快捷入口按具体模块权限分别控制
- 页面视觉使用调度总览型结构：指标、快捷操作、最近事项和右侧调度入口

### 当前状态

- ✅ 页面权限闭环完成
- ✅ 资源种子闭环完成
- ✅ 已按调度总览型页面基座对齐
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
- ✅ 已按表格 CRUD 页面基座对齐
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
- ✅ 用户详情已接入基础信息、资产、经验和订单摘要，按详情型页面基座承载
- ⏸️ 创建用户、强制下线、重置密码等管理动作仍未重新开放

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
- ✅ 角色列表已按表格 CRUD 页面基座对齐
- ✅ 角色权限配置已按权限配置型页面基座承载

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
- ✅ 已按表格 CRUD 页面基座对齐
- ⏸️ `AdminGetProduct` 未被当前页面实际使用，暂不纳入额外补齐范围

## 3.7 Orders

### 当前边界

- 查看：`console.orders.view`
- 重试发放：`console.orders.retry`
- 备注订单：`console.orders.remark`

### 当前状态

- ✅ 页面访问已接入
- ✅ 重试按钮按权限控制
- ✅ 管理员备注已接入独立权限，适用于失败 / 异常订单留痕
- ✅ 仪表盘最近订单现已支持携带 `orderNo` 深链进入订单页，并自动展开目标订单详情
- ✅ 订单详情已展示扣款流水 ID；拥有 `console.coins.view` 时可跳转到对应胡萝卜流水筛选结果
- ✅ 已按表格 CRUD 页面基座对齐
- ⏸️ 更完整的订单处理后台不在本阶段展开

## 3.8 Tags

### 当前边界

- 查看 / 创建 / 编辑 / 删除 / 恢复 / 启停 / 排序均已独立授权

### 当前状态

- ✅ 页面访问与操作权限已接入
- ✅ 资源映射与种子已闭环
- ✅ 已按表格 CRUD 页面基座对齐

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
- ✅ 分组和表情列表已按表格 CRUD 页面基座对齐

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
- ✅ 已按表格 CRUD + 配置面板页面基座对齐

## 3.11 Coins

### 当前边界

- 用户余额查询、业务流水筛选、管理员调账、交易摘要和表单字段仍沿用既有接口与权限判断。
- 业务流水筛选支持 `businessType / businessId`，当前用于从订单详情定位购买扣款流水。
- 调账属于高风险后台动作，页面必须保留明确原因、金额方向和提交反馈。

### 当前状态

- ✅ 已按工具型页面基座承载
- ✅ 查询工具条、余额摘要、调账主区和右侧说明区已对齐

## 3.12 Experience

### 当前边界

- 经验观察、经验流水、复核结论、冻结 / 解冻、管理员调整和等级配置均保留既有 API 与业务语义。
- 当前不新增自动处罚、自动扣经验或经验发放主流程改造。

### 当前状态

- ✅ 已按治理工作台结构承载
- ✅ 页面拆分和视觉承载不改变经验规则、冻结语义或数据契约

## 3.13 Moderation

### 当前边界

- 举报审核、目标回看、手动禁言 / 封禁和治理动作日志保留既有 API 与权限语义。
- 当前不新增批量治理、敏感词策略或自动化处罚平台。

### 当前状态

- ✅ 已按治理工作台结构承载
- ✅ 审核队列、手动动作区和治理日志保留同页人工复核工作流

## 3.14 Settings / Profile

### 当前边界

- `Settings` 和 `UserProfile` 属于登录态个人设置 / 资料页面，不因视觉归入设置型布局而自动进入 Console 专属权限树。
- 头像上传、资料保存、个人时区偏好、重置默认和密码修改保留既有接口与表单校验。

### 当前状态

- ✅ 已按设置型页面基座承载
- ✅ 分组导航、设置列、个人资料摘要和影响范围说明已对齐

## 3.15 Hangfire

### 当前边界

- 查看：`console.hangfire.view`

### 当前状态

- ✅ 已不再依赖 `System/Admin` 角色硬编码放行
- ✅ 资源 URL 与种子已对齐到 `/hangfire(/.*)?`

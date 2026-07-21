# Console 功能模块

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 3 章

## 3. 功能模块

本章只记录 **当前真实模块状态、权限边界和治理结果**，不再保留已明显过期的“理想功能全景图”。

## 3.1 模块总览

| 模块 | 页面状态 | 页面类型 | 核心权限 | 资源种子状态 | 备注 |
| --- | --- | --- | --- | --- | --- |
| Dashboard | ✅ 已接入 | 调度总览 | `console.dashboard.view` | ✅ 已补齐 | 最近订单受 `console.orders.view` 约束 |
| Applications | ✅ 已接入 | 表格 CRUD | `console.applications.*` | ✅ 已补齐 | 列表/新增/编辑/删除/重置密钥已闭环 |
| Users | ✅ 已接入 | 表格 CRUD + 详情 | `console.users.view` | ✅ 已补齐 | 用户详情、资产、经验、订单摘要和内容治理入口已接入 |
| Roles | ✅ 已接入 | 表格 CRUD + 权限配置 | `console.roles.*` | ✅ 已补齐 | 角色详情、编辑和权限配置链路已闭环 |
| Products | ✅ 已接入 | 表格 CRUD | `console.products.*` | ✅ 已补齐 | 商品详情与相关订单排障回流已接入，`GetCategories` 辅助接口已纳入 |
| Orders | ✅ 已接入 | 表格 CRUD + 详情弹层 | `console.orders.*` | ✅ 已补齐 | 查看、重试、管理员备注、商品来源返回和扣款流水定位已接入 |
| Categories | ✅ 已接入 | 表格 CRUD | `console.categories.*` | ✅ 已补齐 | 分类层级、排序、启停、删除 / 恢复已接入 |
| Tags | ✅ 已接入 | 表格 CRUD | `console.tags.*` | ✅ 已补齐 | 页面与资源映射已对齐 |
| Stickers | ✅ 已接入 | 表格 CRUD | `console.stickers.*` | ✅ 已补齐 | 编码校验、批量上传、分组详情和排序闭环已纳入 |
| SystemConfig | ✅ 已接入 | 表格 CRUD + 配置面板 | `console.system-config.*` | ✅ 已补齐 | 编辑详情与站点图标链路已闭环 |
| Coins | ✅ 已接入 | 工具型页面 | `console.coins.*` | ✅ 已补齐 | 用户余额查询、业务流水筛选与管理员调账已接入 |
| Experience | ✅ 已接入 | 治理工作台 | `console.experience.*` | ✅ 已补齐 | 经验观察、流水、冻结、调整和等级配置已接入 |
| Moderation | ✅ 服务端案件契约；页面迁移中 | 治理工作台 | `console.moderation.view/review/action` | ✅ 已补齐 | F4-I-B Case API 与权限已落地；F4-I-C 迁移案件、证据、决定、动作和事件页面 |
| Settings / Profile | ✅ 已接入 | 设置 / 个人资料 | 登录态 | 不适用 | 个人偏好、密码修改、头像上传和资料保存不走 Console 专属权限树 |
| Documents | ✅ 已接入 | 审核证据 + 治理工作台 | `console.docs.*` | ✅ 已补齐 | 待审队列、审核应用、独立发布、访问策略、版本和导入导出已接入 |
| Hangfire | ✅ 已接入 | 特殊入口 / 运维外壳 | `console.hangfire.view` | ✅ 已补齐 | React 外层页承载受保护 iframe，特殊入口授权过滤器校验 |

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
- ✅ 用户详情可进入内容治理并带入目标用户过滤，继续排查该用户相关举报、手动治理和治理日志
- ✅ 列表、详情、资产、经验、订单与权益摘要已使用 Console 中英文资源、locale 格式化和稳定系统词元；人工资料与备注保持原文
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
- ✅ 商品详情进入相关订单时保留当前商品详情 `returnTo`，并继续保持商品 / 订单 ID 字符串查询参数
- ✅ 商品编辑、上架和下架使用 `VoVersion` / `ExpectedVersion` 乐观并发语义，旧详情快照提交会被服务端拒绝
- ✅ 商品类型与能力展示消费稳定枚举和 `voConfigurationRequirementKeys / voUnavailableReasonKey`；兼容说明文本不参与可售与上架控制
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
- ✅ 从商品详情或胡萝卜流水进入订单页时，订单详情关闭、分页、筛选和重置会保留合法 `returnTo`
- ✅ 已按表格 CRUD 页面基座对齐
- ✅ 订单状态、失败阶段、商品类型与权益摘要按稳定字段本地化，API 失败保留结构化错误字段，不匹配展示消息控制流程
- ⏸️ 更完整的订单处理后台不在本阶段展开

## 3.8 Tags

### 当前边界

- 查看 / 创建 / 编辑 / 删除 / 恢复 / 启停 / 排序均已独立授权
- 标签类型、固定标签、软删除范围、批量删除和排序保持既有接口与业务语义

### 当前状态

- ✅ 页面访问与操作权限已接入
- ✅ 资源映射与种子已闭环
- ✅ 已按 D14 Console 语义组件迁移页头、指标和筛选工具条
- ✅ Gateway PC `1920x1080` 与移动 `390x844` CSS 视图真实联调已覆盖页面渲染与关键词筛选

## 3.8.1 Categories

### 当前边界

- 查看 / 创建 / 编辑 / 删除 / 恢复 / 启停 / 排序均已独立授权
- 分类层级、父级分类、软删除范围、批量删除和排序保持既有接口与业务语义

### 当前状态

- ✅ 页面访问与操作权限已接入
- ✅ 资源映射与种子已闭环
- ✅ 已按 D14 Console 语义组件迁移页头、指标和筛选工具条
- ✅ Gateway PC `1920x1080` 与移动 `390x844` CSS 视图真实联调已覆盖页面渲染与关键词筛选

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
- ✅ 分组和表情列表保持表格 CRUD 功能闭环
- ✅ 贴纸分组 / 分组表情列表已按普通 CRUD 外层迁入 Console 语义组件
- ✅ 图片预览、分组详情跳转、单个新增、批量上传、批量排序和删除确认保持既有业务契约
- ✅ 批量上传弹窗内表格已补局部滚动和提示色 token 收口
- ⏸️ 媒体资产工作台、素材详情页和完整资产审核流不在当前阶段展开

## 3.10 SystemConfig

### 当前边界

- 查看：`console.system-config.view`
- 编辑：`console.system-config.edit`
- 创建：`console.system-config.create`（兼容旧路由，后端拒绝 Console 新增未知设置）
- 删除：`console.system-config.delete`（兼容旧路由，语义收敛为恢复默认）
- 公开站点设置：`GET /api/v1/SystemConfig/GetPublicSiteSettings`（匿名读取，不纳入 Console 权限）

### 当前状态

- ✅ 页面访问闭环完成
- ✅ 编辑详情资源映射已对齐
- ✅ 系统设置已收敛为代码级设置定义 + 默认值 + JSON 覆盖值，Console 默认只展示已注册设置
- ✅ 覆盖值默认落盘到 `DataBases/SystemConfigs/system-configs.json`
- ✅ `system-configs.json` 当前按“UTF-8 + 中文直写、仅保留必要 JSON 转义”的策略落盘，便于本地排障、人工审阅与差异比对
- ✅ `DataBases/SystemConfigs/system-configs.json` 属于运行时本地状态文件，不作为源码资产提交，继续遵循 `DataBases/` 目录忽略规则
- ✅ 系统设置变更历史落盘到 `DataBases/SystemConfigs/system-config-change-logs.json`，用于记录旧值、新值、默认值、原因、风险等级、生效方式、操作者、IP、User-Agent 和时间
- ✅ 当前开放 `Site.Branding.FaviconUrl`、账号身份长度、帖子标题 / 正文 / 摘要长度、评论内容长度、论坛轻回应内容 / 返回条数 / 冷却 / 去重窗口，以及神评 / 沙发稳定窗口和替换阈值设置
- ✅ Medium 设置必须填写修改原因并确认风险等级 / 设置键，High / Critical 设置不开放编辑
- ✅ 数字设置已展示数值范围、整数约束和影响范围摘要，前端控件按规则约束输入，后端仍是最终校验权威
- ✅ SystemConfig 页面已支持站点 favicon `.ico` 上传、预览与恢复默认
- ✅ 默认站点图标已固定为 `/uploads/DefaultIco/bailuobo.ico`，默认种子文件位于 `DataBases/Uploads/DefaultIco/bailuobo.ico`
- ✅ `radish.client / radish.console` 当前都通过公开站点设置接口读取 favicon，标签页图标不再写死在前端静态资源中
- ✅ 帖子标题 / 正文 / 摘要长度、评论内容长度、论坛轻回应内容 / 返回条数 / 冷却 / 去重窗口，以及神评 / 沙发稳定窗口和替换阈值已通过 `ISystemSettingProvider` 接入业务发布 / 编辑 / 列表 / 实时重算路径
- ✅ 已按表格 CRUD + 配置面板页面基座对齐
- ✅ 数字设置控件宽度、变更历史表格滚动和移动端分页换行已按 Console 表格交互口径收口
- ✅ 设置定义使用稳定 `voKey` 解析本地名称、说明和影响摘要；未知定义与设置值保留服务端原文，语言偏好不写入 SystemConfig

## 3.11 Coins

### 当前边界

- 用户余额查询、业务流水筛选、管理员调账、交易摘要和表单字段仍沿用既有接口与权限判断。
- 业务流水筛选支持 `businessType / businessId`，当前用于从订单详情定位购买扣款流水。
- 从订单详情进入流水筛选时，`businessId` 保持订单 ID 字符串；若流水页回看订单，应继续使用同源相对 `returnTo`，不接受外部 URL。
- 调账属于高风险后台动作，页面必须保留明确原因、金额方向和提交反馈。

### 当前状态

- ✅ 已按工具型页面基座承载
- ✅ 查询工具条、余额摘要、调账主区和右侧说明区已对齐
- ✅ 流水表格金额正负色、外边距和操作密度已迁入 Console token / CSS

## 3.12 Experience

### 当前边界

- 经验观察、经验流水、复核结论、冻结 / 解冻、管理员调整和等级配置均保留既有 API 与业务语义。
- 当前不新增自动处罚、自动扣经验或经验发放主流程改造。

### 当前状态

- ✅ 已按治理工作台结构承载
- ✅ 页面拆分和视觉承载不改变经验规则、冻结语义或数据契约
- ✅ 经验观察、经验流水、复核动作、治理表单和等级配置的内部样式已迁入页面 CSS 与 Console token
- ✅ 经验流水表格保持治理工作台内的局部滚动、分页换行和弱文本 CSS class 口径

## 3.13 Moderation

### 当前边界

- 服务端已建立 Case / Evidence / Event / UserModerationState 权威契约，五类目标统一进入案件、证据、决定、动作和留痕链路。
- 权限拆分为 `console.moderation.view / review / action`；无 Action 权限可以登记无动作决定，不能提交用户处置载荷或纠正动作。
- 旧举报审核、手动禁言 / 封禁与动作日志 API 只作为 F4-I-C 页面迁移前的兼容消费者，不再作为长期契约。
- 当前不新增批量治理、敏感词策略或自动化处罚平台。

### 当前状态

- ✅ 已按治理工作台结构承载
- ✅ F4-I-B 已完成案件聚合、追加式证据、Case / State 版本保护、五类目标处置、可靠通知和新 Case API
- ⏳ F4-I-C 将现有举报单页面迁移为案件队列、详情、证据、决定、动作与事件工作台，并在消费者清零后退役旧写 API
- ✅ 案件决定和用户当前状态分别使用版本保护；同一操作键支持幂等回放，同键异参返回冲突
- ✅ 支持从用户详情或 URL 状态带入目标用户过滤，方便用户排障与内容治理串联
- ✅ 内部提示、筛选区、表格列弱文本和手动治理动作区已迁入 CSS 与 Console token
- ✅ 举报状态、目标类型和治理动作按稳定字段解析中英文词元，高频失败按结构化 status / Code 分支

## 3.14 Settings / Profile

### 当前边界

- `Settings` 和 `UserProfile` 属于登录态个人设置 / 资料页面，不因视觉归入设置型布局而自动进入 Console 专属权限树。
- 头像上传、资料保存、个人时区偏好、重置默认和密码修改保留既有接口与表单校验。

### 当前状态

- ✅ 已按设置型页面基座承载
- ✅ 分组导航、设置列、个人资料摘要和影响范围说明已对齐
- ✅ 商品、分类、贴纸和贴纸分组等深层表单的上传预览、隐藏输入、控件宽度、弱提示和弹窗 footer 动作区已统一到 `adminForm.css`

## 3.15 Documents

### 当前边界

- 查看：`console.docs.view`
- 审核：`console.docs.review`，只允许 RequestChanges / Reject / Apply
- 发布 / 下架：`console.docs.publish`
- 归档：`console.docs.archive`
- 删除 / 恢复：`console.docs.delete` / `console.docs.restore`
- 访问策略、版本回滚、导入和导出分别使用 `console.docs.permissions / rollback / import / export`

### 当前状态

- ✅ 待审队列、正式正文 / 草稿证据、协作者与审核时间线已经接入；Apply 只更新权威正文并生成 Revision，不自动 Publish
- ✅ 文档治理列表、详情、状态与访问策略、版本回看 / 回滚、Markdown 导入 / 导出均已接入
- ✅ 状态、可见性和来源类型按稳定字段解析中英文词元，标题、正文、Slug、角色 / 权限键和修订说明保留原文
- ✅ 日期、数量和英文复数按当前 locale 展示；Wiki API 失败统一保留 HTTP status、`Wiki.*` Code 和 `error.wiki.*` MessageKey
- ✅ 正式 Web Author 入口、Console 审核 / 治理和公开阅读保持职责分层，不把审核或发布动作混入作者态

## 3.16 Hangfire

### 当前边界

- 查看：`console.hangfire.view`

### 当前状态

- ✅ 已不再依赖 `System/Admin` 角色硬编码放行
- ✅ 资源 URL 与种子已对齐到 `/hangfire(/.*)?`
- ✅ React 侧已通过 `SystemTools/HangfirePage` 承载外层页头、指标和 iframe 容器
- ⏸️ 当前只承载受保护的外部 Hangfire Dashboard，不扩展项目内任务队列、失败重试或运行审计平台

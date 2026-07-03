# P3-12-D38 UI 边界裁决与阶段验收清单

> 日期：2026-07-01（Asia/Shanghai）
>
> 类型：范围裁决 / 阶段验收清单
>
> 状态：已完成 D37 命中边界的发布前归属裁决；真实 Gateway PC / mobile 阶段验收仍待用户明确说明前后端已启动后执行

## 背景

`P3-12-D37` 已把 public / private / foundation / console 四类设计源对齐到真实路由、代码状态、验证证据和剩余动作。D37 命中的主要分歧不是代码 bug，而是设计源中若干画板是否应进入 P3-12 发布前范围。

本批只做边界裁决和阶段验收清单整理，不修改业务代码，不改 API / 权限 / 路由 / 保存动作 / 载荷，不启动 Gateway 页面联调。

## 裁决原则

1. 已有正式 Web 主路径优先稳定，不为单个设计画板新增未确认产品能力。
2. 需要后端数据来源、实时协议、治理策略或权限动作的能力，先补专题设计，不用静态 UI 代替产品能力。
3. 移动 Console 当前作为响应式管理后台验收目标，不拆成独立移动 Console 应用。
4. D38 后若无发布前代码治理项，下一步进入成组 Gateway PC / mobile 阶段验收。

## 边界裁决

| 项目 | D37 问题 | D38 裁决 | 后续处理 |
| --- | --- | --- | --- |
| 公开首页 `P01` | `/` 浏览器入口当前解析到 `/discover`，没有独立 `PublicHomeApp` | 发布前不新增独立公开首页；`P01` 作为公开 App 首页概念层，由 `/discover` 承接 | 更新公开设计说明；阶段验收检查 `/` 到 `/discover` 的默认入口语义 |
| 公开聊天室 `P15 / P16` | 设计源存在 `/chat` / `/chat/:room`，但正式公开路由未接入 | 后置评审，不作为 P3-12-D 退出条件和 P3-12-E 前置项 | 保留设计稿作后续参考；若要推进，先补公开聊天室产品方案、实时协议、登录 / 匿名边界和治理策略 |
| 公开低频入口与 `P13` 移动工作台 | 商城、榜单、聊天室无法都放入移动一级底栏 | 保持 `/workbench` 承接低频入口；`P13` 继续保留在 public 设计源说明中，但真实功能地图归 private / foundation 交叉约束 | 阶段验收观察 `/workbench` 在 PC / mobile 的入口密度和导航可达性 |
| Console 调度总览 `P04` | 设计源目标包含跨模块治理负载和今日分派，当前 Dashboard 不是完整调度中心 | 发布前不启动内部调度平台；Dashboard 继续作为 Console 总览与代表性状态入口 | 若后续要做调度中心，先补数据来源、任务模型、权限动作和 API 契约 |
| Console 运维工具 `P13 / P18` | 设计源目标包含任务队列、失败重试和运行审计；当前只有系统设置和 Hangfire iframe | 发布前不扩展内部任务平台；`/hangfire` 继续只承载受保护外部 Dashboard | 系统设置和 Hangfire 外壳进入阶段验收；内部 Jobs 平台后置专题 |
| 移动 Console `P07 / P08 / P14-P17` | 设计源包含移动治理、商业、文档、权限和运维任务流 | 作为响应式 Console 验收参考，不拆独立移动应用、不新增移动专属路由 | Gateway mobile CSS 视图覆盖代表页面和弹窗 / 抽屉，不要求完整原生移动 Console |
| 共享基座 `F01 / F02` | 需要确认 public / private / console 是否产生共享结构漂移 | 当前不新增共享变体；继续沿用 `WebShellHeader`、`WebStateSlot`、Console 语义组件和既有 token | 阶段验收抽查 header、状态槽、移动底栏和 Console 语义组件一致性 |

## 阶段验收清单

真实页面验收只在用户明确说明前后端已启动后执行。默认访问 Gateway：

```text
https://localhost:5000
https://localhost:5000/console/
```

视口：

- PC：`1920x1080`
- Mobile CSS 视口：`390x844`
- DPR 物理高分屏不写成完整结论，除非另行指定工具和设备

### Public Web

| 页面族 | 代表路径 | 验收重点 |
| --- | --- | --- |
| 默认入口 / 发现 | `/`、`/discover` | `/` 进入 `/discover` 语义清晰；信息流、状态槽和公开入口密度正常 |
| 论坛列表 / 搜索 / 类型流 | `/forum`、`/forum/search`、`/forum/question`、`/forum/poll`、`/forum/lottery` | 列表密度、筛选、分页、真实链接和移动单列不撑破 |
| 论坛详情 | `/forum/post/:id` | 正文、轻回应、评论树、神评 / 沙发 badge、登录参与和作者 intent 入口不重叠 |
| 文档列表 / 详情 | `/docs`、`/docs/:slug` | 目录、搜索、正文阅读、锚点和相关文档节奏正常 |
| 商城公开浏览 | `/shop`、`/shop/products`、`/shop/product/:id` | 商品列表 / 详情、登录购买回流和公开 / 私域边界清楚 |
| 榜单 | `/leaderboard`、`/leaderboard/:type` | 类型切换、分页、公开主页跳转和空态正常 |
| 公开主页 | `/u/:id` | 公开内容 tab、关注登录回流、来源返回和身份展示正常 |
| 工作台 | `/workbench` | 低频入口承接清楚，不回退 WebOS 默认路径 |

### Private / Author Web

| 页面族 | 代表路径 | 验收重点 |
| --- | --- | --- |
| 工作台 / 我的状态 | `/workbench`、`/me` | 功能地图、身份摘要、最近复访和移动底栏一致 |
| 内容 / 历史 / 附件 / 经验 | `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` | tab、分页、筛选、空态和公开详情回流正常 |
| 资产 / 订单 / 背包 | `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:id`、`/shop/inventory` | 数据卡片、流水、订单详情、背包权益和移动单列正常 |
| 通知 / 消息 | `/notifications`、`/messages` | 未读、目标跳转、会话定位、移动输入区和登录恢复正常 |
| 圈子 / 宠物 | `/circle`、`/pet` | 关注动态、关系链、宠物状态、照护反馈和流水正常 |
| 论坛作者态 | `/forum/compose`、`/forum/post/:id?intent=answer|edit|history` | 发布器、问答回答、编辑历史和提交反馈正常 |
| Docs 作者态 | `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` | 作者库、编辑器、版本栈和保存反馈正常 |

### Console

| 页面族 | 代表路径 | 验收重点 |
| --- | --- | --- |
| 壳层 / 总览 | `/console/` | 侧栏分组、顶栏、权限状态、Dashboard 卡片和最近订单表格不溢出 |
| 商业与资产 | `/console/orders`、`/console/products`、`/console/coins` | 表格滚动、操作列换行、详情抽屉和金额状态正常 |
| 用户 / 权限 | `/console/users`、`/console/users/:userId`、`/console/roles`、`/console/roles/:roleId/permissions` | 详情内嵌表格、权限树、保存反馈和移动窄屏缩进正常 |
| 内容与文档 | `/console/documents`、`/console/moderation` | 文档抽屉 / 版本弹窗、治理队列、证据区、动作区和留痕区正常 |
| 经验治理 | `/console/experience` | 观察摘要、流水、复核表单、等级配置和移动单列堆叠正常 |
| 分类 / 标签 / 贴纸 | `/console/categories`、`/console/tags`、`/console/stickers`、`/console/stickers/:groupId/items` | 普通 CRUD 密度、图片预览、批量上传弹窗和分页正常 |
| 系统工具 | `/console/system-config`、`/console/hangfire` | 设置历史弹窗、数字控件、Hangfire iframe 外壳和权限守卫正常 |

## 验收记录要求

- 记录访问日期、视口、入口 URL、登录态、测试数据来源和浏览器 console error 概况。
- 对缺少真实数据的详情页，不伪造通过结论；可记录为“待安全测试数据补验”。
- 若运行态发现问题，按页面族成组回拉，不把单页局部问题扩成全站重构。
- 验收完成后再判断 `P3-12-D` 是否满足退出条件；未满足时继续回到对应设计说明或代码页面族。

## 后续建议

1. 下一步进入 `P3-12-D39` UI 专题 Gateway PC / mobile 阶段验收准备。
2. 若用户明确前后端已启动，即可按本清单执行真实页面复核。
3. 若暂不执行联调，则先整理阶段验收记录模板和需要测试数据的详情页清单。

## 本批不做

- 不新增 `/chat`、`PublicHomeApp`、内部调度中心或内部 Jobs 平台。
- 不修改业务代码、API、权限、路由、保存动作或载荷。
- 不修改 Pencil 设计源。
- 不启动 Gateway 页面联调。
- 不进入 `P3-12-E`，不创建发布 tag，不恢复 PR / 发布流程。

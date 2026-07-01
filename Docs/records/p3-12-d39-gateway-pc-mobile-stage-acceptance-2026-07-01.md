# P3-12-D39 Gateway PC / Mobile 阶段验收记录

> 日期：2026-07-01（Asia/Shanghai）
>
> 类型：阶段验收 / 真实页面复核
>
> 状态：已完成 Gateway 运行态健康检查、public / private / author / console 代表页面 PC 与移动 CSS 视口扫描，以及 Console 只读交互补验；未发现阻断级页面问题

## 背景

`P3-12-D38` 已完成 UI 边界裁决，明确公开聊天室、内部调度中心、内部 Jobs 平台和独立移动 Console 应用不进入当前发布前范围。本批按 D38 清单执行真实 Gateway PC / mobile 阶段验收，用于判断 UI 专题是否可以进入退出判断和发布候选前准备。

本批不修改业务代码、API、权限、路由、保存动作或载荷；Console 交互补验只打开详情 / 历史 / 权限页，不提交表单。

## 环境与账号

- 入口：`https://localhost:5000`、`https://localhost:5000/console/`
- 健康检查：`npm run check:host-runtime -- --details`
- 登录态：本地开发种子管理员账号 `admin@radishx.com`
- PC 视口：`1920x1080`
- 移动视口：`390x844` CSS 视口
- 移动 DPR：本批未声明高 DPR 物理屏完整结论
- 浏览器：Codex in-app Browser

## 运行态健康检查

`npm run check:host-runtime -- --details` 通过：

- Gateway `https://localhost:5000/health`：`200`
- Api `http://localhost:5100/health`：`200`
- Auth `http://localhost:5200/health`：`200`

## 页面扫描范围

批量扫描按页面真实挂载、标题 / 正文存在、登录误跳、页面错误文案、横向溢出和浏览器 console error 做基础判断。

| 批次 | PC 点位 | Mobile CSS 点位 | 结论 |
| --- | ---: | ---: | --- |
| Public Web | 17 | 17 | 通过 |
| Private / Author Web | 22 | 22 | 通过 |
| Console | 14 | 14 | 通过 |
| 合计 | 53 | 53 | 通过 |

### Public Web

覆盖：

- `/`、`/discover`
- `/forum`、`/forum/search`、`/forum/question`、`/forum/poll`、`/forum/lottery`
- 首个真实帖子详情：`/forum/post/pst_019f0907b1c37ef5a7a8fbbafd65afb4`
- `/docs`、`/docs/index`
- `/shop`、`/shop/products`、`/shop/product/100061`
- `/leaderboard`、`/leaderboard/balance`
- `/u/usr_019ef99117377efb85389bfe6d9d55a5`
- `/workbench`

结论：

- `/` 正常进入 `/discover`。
- 公开信息流、论坛、文档、商城、榜单、公开主页和工作台均完成挂载。
- PC 与移动 CSS 视口未检出横向溢出、应用未挂载、页面错误文案或浏览器 console error。

### Private / Author Web

覆盖：

- `/workbench`、`/me`
- `/me/content`、`/me/history`、`/me/attachments`、`/me/experience`
- `/me/assets`、`/me/assets/transactions`
- `/shop/orders`、`/shop/order/931200000001`、`/shop/inventory`
- `/notifications`、`/messages`
- `/circle`、`/pet`
- `/forum/compose`
- `/forum/post/pst_019f0907b1c37ef5a7a8fbbafd65afb4?intent=answer`
- `/forum/post/pst_019f0907b1c37ef5a7a8fbbafd65afb4?intent=edit`
- `/forum/post/pst_019f0907b1c37ef5a7a8fbbafd65afb4?intent=history`
- `/docs/mine`、`/docs/compose`、`/docs/revisions/2072303610636533760`

结论：

- 登录态私域、资产、订单、通知、消息、圈子、宠物、论坛作者态和 Docs 作者态均完成挂载。
- PC 与移动 CSS 视口未检出登录误跳、横向溢出、应用未挂载、页面错误文案或浏览器 console error。
- `/docs/mine` 未暴露普通 `/docs/edit/:id` 链接，本批未把 Docs 编辑详情写成已验证。

### Console

覆盖：

- `/console/`
- `/console/orders`、`/console/products`、`/console/coins`
- `/console/users`、`/console/roles`
- `/console/documents`、`/console/moderation`
- `/console/experience`
- `/console/categories`、`/console/tags`、`/console/stickers`
- `/console/system-config`、`/console/hangfire`

结论：

- Console 登录和授权回流成功，Dashboard、业务资产、用户权限、内容文档、经验治理、分类标签、贴纸、系统设置和 Hangfire 外壳均完成挂载。
- PC 与移动 CSS 视口未检出登录误跳、授权页停留、横向溢出、应用未挂载、页面错误文案或浏览器 console error。
- 移动 Console 作为响应式后台验收参考通过，不扩展为独立移动 Console 应用结论。

## Console 只读交互补验

| 交互 | 路径 / 对象 | 结论 |
| --- | --- | --- |
| 订单详情 | `/console/orders?orderId=931200000001&openDetail=1` | 详情区、订单字段、管理员备注区、关闭动作可见；PC 无横向溢出 |
| 用户详情 | `/console/users/20001` | 用户资料、经验、胡萝卜余额、最近流水 / 订单表格可见；PC 无横向溢出 |
| 角色权限 | `/console/roles/10001/permissions` | 权限树、已选资源汇总和接口映射表格可见；PC 无横向溢出 |
| 系统设置历史 | `/console/system-config` 第一行历史 | 历史弹窗打开成功，变更历史表格可见；PC 无横向溢出 |

观察项：

- 系统设置历史补验前，页面 DOM 中存在一个 0 尺寸隐藏编辑弹窗节点；未造成可见遮挡、横向溢出或保存动作。若后续出现焦点、快捷键或浮层遮挡问题，再作为系统设置弹窗状态治理回拉。

## 工具限制

- Playwright CLI wrapper 本轮 `open` 初始化未稳定返回，改用 Codex in-app Browser 执行真实页面复核。
- 移动端只覆盖 `390x844` CSS 视口；未覆盖 DPR 3 物理高分屏。
- 本批以页面挂载、布局宽度、代表详情和只读浮层为主；未执行保存、删除、重试、上传、购买、发布等会改变数据的动作。
- Docs 编辑详情没有从当前作者库暴露普通链接，本批不写成已验证。

## 阶段结论

`P3-12-D39` 未发现阻断 `P3-12-D` 退出判断的真实页面问题。当前可以进入 `P3-12-D40`：整理 UI 专题退出判断、剩余风险和 `P3-12-E` 发布候选前置清单。

正式进入 `P3-12-E` 前仍应保留：

- 准备合并到 `master` 前的完整 baseline / identity / host runtime 验证。
- 发布候选级 PC / mobile 页面复核记录。
- 若需要完整移动高分屏结论，再补 DPR 3 设备或工具验证。

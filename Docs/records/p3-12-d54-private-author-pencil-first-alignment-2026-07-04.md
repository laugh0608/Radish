# P3-12-D54 Private / Author Pencil 首轮页面对齐复核

## 基本信息

- 日期：`2026-07-04`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：Private / Author 设计源到真实路由实现的首轮对齐复核、代码修正与 Gateway 真实页面补验
- 设计依据：[私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)、`Docs/frontend/design-sources/private-web-workflows.pen`

## 背景

`P3-12-D51` 已完成私域 / 作者态移动任务流 UI 代码侧首批，但不等同于 `P01-P30` 已完成 Pencil 到真实页面逐页验收。`P3-12-D53` 已完成 Public Web 设计源与真实 mobile 页面首轮对齐，本批继续回到 Private / Author 页面族，优先核对 `/workbench`、`/me` 系列、资产 / 订单 / 背包、通知、消息、圈子、宠物、论坛作者态和 Docs 作者态。

## 复核结论

- Pencil MCP 已连接当前编辑文件 `private-web-workflows.pen`，当前设计源包含 `P01-P30`，未发现 `.pen` 布局结构问题。
- `P22 - Mobile Me` 与 `P23 - Mobile Content History` 截图复核显示设计源移动页使用紧凑身份摘要、双列状态指标和紧凑任务 tab / 列表节奏。
- 源码静态复核命中 `/me` 系列移动端差距：个人状态页二级入口在窄屏全部堆为单列按钮，状态摘要卡在移动端退为全单列；`/me/content` 的帖子 / 评论 / 轻回应 tab 在移动端也退为单列，容易把真实列表内容压到首屏过低位置。
- 资产、订单 / 背包、通知、消息、圈子、宠物和 Docs 作者态已具备 D51 后的双列摘要、状态槽或任务区收紧，未在启动前静态复核中命中同类阻断级缺口。
- 用户确认前后端已启动后，已使用 Browser 插件经 Gateway `https://localhost:5000` 补齐 PC `1920x1080` 与移动 `390x844` CSS 视口真实页面复核；移动视口受插件能力限制无法设置 DPR，未写作 `@ DPR 3` 完整设备仿真结论。
- 本批真实页面复核未发现登录态回跳、首屏空白、横向溢出、底部导航缺失或关键标题 / 状态槽缺失；订单详情、Docs 编辑页和 Docs 修订页在可用种子数据下完成补充覆盖。
- Smoke 过程中命中 `/me/experience` Recharts 初始 `-1/-1` 宽高 warning；已在共享图表容器内通过 `initialDimension` 和 `min-width: 0` 治理，PC / mobile 成长页复核不再产生新 warning。

## 实现范围

- `/me` 移动个人状态入口改为双列触控网格，保留公开主页主动作独立展示。
- `/me` 移动摘要区改为主卡横跨 + 两列辅助卡，避免经验、资产、最近复访和宠物状态在手机首屏全部纵向堆叠。
- `/me/content` 移动 tab 改为三段式紧凑网格，帖子、评论和轻回应入口保持同屏可见。
- 共享图表组件为 Recharts `ResponsiveContainer` 提供正值 `initialDimension`，并补图表 wrapper / canvas 的收缩约束，避免初次挂载阶段输出无效尺寸 warning。

## 保持不变

- 不新增业务 API、权限键、数据库结构、路由语义、登录回流或保存 / 提交载荷。
- 不修改个人中心数据加载、公开来源返回、资产 / 订单 / 背包、通知、消息、圈子、宠物、论坛作者态或 Docs 作者态业务契约。
- 不进入 `P3-12-E`，不创建 tag，不进入 M15 测试或生产部署流程。

## 验证记录

- Pencil MCP：已连接并读取 `private-web-workflows.pen` 当前编辑状态；`snapshot_layout(problemsOnly=true)` 未发现布局问题；抽查 `P22 / P23` 移动画板截图。
- Browser 插件：使用本地开发种子账号 `admin@radishx.com` 登录 Gateway；PC `1920x1080` 覆盖 `/workbench`、`/me`、`/me/content`、`/me/history`、`/me/attachments`、`/me/experience`、`/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/inventory`、`/notifications`、`/messages`、`/circle`、`/pet`、`/forum/compose`、`/docs/mine`、`/docs/compose`、`/shop/order/931200000001`、`/docs/revisions/2072303610636533760`、`/docs/edit/2072303610636533760`。
- Browser 插件：移动 `390x844` CSS 视口覆盖同一组私域 / 作者态路径；全路径未出现登录表单回跳、横向溢出或移动底部导航缺失；`/me` 与 `/me/content` 移动首屏截图目检通过。
- Browser 插件：`/me/experience` 图表 warning 治理后，PC 与移动复核均确认图表渲染、无横向溢出且无新 `warn/error`。
- `npm run type-check --workspace=@radish/ui`：通过。
- `npm run build --workspace=radish.client`：通过。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:changed`：通过。

## 下一步

D54 已闭合 Private / Author 这一组页面的 Pencil / 源码 / 构建 / Gateway PC / mobile 真实页面复核。下一步仍应留在 `P3-12-D` UI 实现内，继续按设计源差距矩阵核对尚未完成或证据不足的页面族；不能把 D54 写成整个 UI 专题的退出判断，也不能直接进入 `P3-12-E`。

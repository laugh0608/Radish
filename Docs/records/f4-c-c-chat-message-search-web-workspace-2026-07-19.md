# F4-C-C 聊天历史搜索正式 Web 工作区完成记录

> 日期：2026-07-19
>
> 结论：F4-C-C 已完成，下一顺位进入 F4-C-D 双账号成组验收。

## 本批边界

本批先更新 Pencil PC / mobile 设计源，再实现正式 `/messages` 搜索工作区和 WebOS 同组件兼容；没有修改服务端搜索协议、数据库迁移、Flutter 或 Console，也没有启动 Radish 服务或执行浏览器 smoke。

## 设计源

`Docs/frontend/design-sources/private-web-workflows.pen` 已新增并完成布局检查：

- `P13C - Messages Search / Desktop`：当前 / 全部会话、日期筛选、结果、继续加载与权威消息窗口；
- `P13D - Messages Search States / Desktop`：加载、空态、离线、错误、cursor 失效、目标失效、长文本和键盘焦点；
- `P27C - Mobile Messages Search`：`390 × 844` 单列检索表单与结果；
- `P27D - Mobile Messages Search States`：移动离线、目标失效和长文本状态。

四个画板均通过 Pencil layout problem 检查，不压缩为窄屏三栏。

## 正式 Web 与复用边界

- 新增独立 `ChatMessageSearchPanel`、`useChatMessageSearch` 和搜索样式模块；当前 / 全部会话、日期边界、首批与 cursor 继续加载均使用服务端 POST 契约，不扫描 `chatStore.messageMap`。
- 关键词、结果和 cursor 只保存在组件内存，不进入 URL、浏览器存储、跨标签消息或诊断日志；账号变化立即重置完整搜索状态。
- PC 搜索面板与成员面板互斥；mobile 使用单列搜索视图，打开结果时保留已挂载搜索面板，因此 Back 可恢复条件、结果和滚动位置。
- `MessagesApp` 以 `pushState / popstate` 响应频道和消息目标；Back 恢复检索，Forward 再次进入消息目标，不整页刷新且不把关键词写入路由。
- 已从 `ChatApp` 抽出 `useChatMessageNavigation`，所有搜索结果重新调用 `GetMessageWindow`；目标失败展示明确不可用状态，不回退到频道最新消息，也不预先清空现有消息历史。
- WebOS 复用同一 Chat App 内存搜索与消息导航，不新增 WebOS API、Store 或搜索协议。

## 状态、可访问性与本地化

- 中英文覆盖入口、范围、日期、加载、空态、离线、普通错误、cursor 失效、目标失效、重试和继续加载。
- 日期输入按浏览器本地自然日转换为 UTC 边界；结果时间和数量使用当前 locale formatter。
- 搜索表单支持 Enter，面板支持 Esc；控件保持 Tab 可达，状态使用 `aria-live`，结果使用有序列表语义。
- 摘要以文本节点和 `<mark>` 分段渲染，不使用 HTML 注入；三行截断和 `overflow-wrap: anywhere` 覆盖长英文、连续数字、emoji 与长摘要。
- 搜索样式只消费现有 `--theme-*` 语义 token；新增控制样式拆到独立模块，`ChatApp.module.css` 保持在 1500 行硬上限内。

## 验证结果

- `radish.client`：430 项测试通过，type-check、lint 和 production build 通过。
- `@radish/http`：type-check 与 lint 通过。
- `npm run validate:baseline:quick`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

本批未启动服务，因此未执行真实 API、SignalR、PC / mobile 浏览器或双账号权限矩阵；这些按既定批次留给 F4-C-D。

## 下一顺位

进入 F4-C-D：在明确启动前后端后，使用至少两个普通账号完成 `zh / en × 1920 × 1080 PC / 390 × 844 mobile` 的当前 / 全部会话搜索、分页、定位、Back / Forward、撤回、权限变化、离线恢复与 WebOS 阻断级兼容验收，并清理临时数据和运行环境。

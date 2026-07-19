# F4-D-C 聊天消息 Reaction 正式 Web 完成记录

> 日期：2026-07-19
>
> 结论：F4-D-C 已完成，下一顺位进入 F4-D-D 双账号成组验收。

## 本批边界

本批在 F4-D-B 服务端权威契约上完成 PC / mobile Pencil 与正式 `/messages` 页面实现；WebOS `/desktop` 复用同一 Chat App。没有启动 Radish 服务、执行 Gateway / 浏览器 smoke、创建通知事件或扩展 Flutter / Tauri。

## 完成内容

### Pencil 与交互

- 在权威设计源新增 PC `P13E - Message Reactions / Desktop` 与 mobile `P27E - Message Reactions / Mobile`。
- PC 在气泡下方展示聚合、本人已选态和显式“回应”入口，选择器覆盖 Unicode 与 sticker；mobile 使用可触达的显式入口和底部选择面板，不依赖 hover 或长按。
- 两个画板均完成布局问题检查和截图复核，无裁切、溢出或折叠。

### 权限、状态与实时

- `ChannelVo` 新增独立 `VoCanReact`，由服务端统一 `ChatChannelAccessResult.CanReact` 派生；公告频道不因 `CanSend=false` 失去回应能力，Direct pending / declined / blocked 不开放写入。
- Chat Store 按消息 ID 保存内存 Reaction 状态；批量读取按 100 条分批覆盖历史、消息定位窗口、新消息与重连后的当前加载消息，账号 reset 清空。
- Hub 只接受更高 revision；广播聚合保留本账号已知选择，避免把广播发起者的 `VoIsReacted` 误写到其他账号。本人 HTTP 回包允许在相同 revision 下校正选择态，覆盖 Hub / HTTP 到达顺序竞态。
- 撤回事件同时移除本地 Reaction；本地临时、发送失败、已撤回和引用快照不渲染可操作入口。

### 组件、主题与文案

- 复用共享 `ReactionBar` 与 `StickerPicker`，增加只读聚合和显式文字入口；只读切换会关闭选择器，不能从已打开面板绕过权限。
- 共享 Reaction 颜色改用主题语义 token，覆盖四套正式主题；长聚合继续使用既有展开 / 收起能力。
- sticker 目录 Hook 提升为 client 共用 Hook，Forum 保留兼容导出，避免 Chat 复制同一资源映射。
- 补齐中文 / 英文回应、上限、选择器、加载、错误与重试文案；加载失败不清空消息历史和草稿。

## 验证结果

- Pencil PC / mobile 两个新增画板 `snapshot_layout` 均无布局问题，并完成截图复核。
- client `436` 项测试通过，新增 LongId revision、Hub 乱序 / 相同 revision、广播账号视角与 operation ID 回归。
- `radish.client`、`@radish/http`、`@radish/ui` type-check 通过；client 与共享 UI lint 通过。
- 后端全量 `907` 项通过、`20` 项环境用例按配置跳过；Chat 定向测试 `53` 项通过、`5` 项环境用例按配置跳过，`VoCanReact` 公告频道能力传播测试另行通过。
- client production build 与 `npm run validate:baseline:quick` 通过；最终门禁包含 `@radish/http 16`、`@radish/ui 23`、client `436`、Console `56` 项测试及版本、权限、敏感字面量、时间语义、Repo Quality 与身份门禁。
- 本批未启动服务或执行浏览器 smoke；双账号、真实 picker / sticker、PC / mobile、WebOS、并发和重连矩阵按停止线留给 F4-D-D。

## 下一顺位

进入 F4-D-D：完成双普通账号 `zh / en × PC / mobile`、公开 / 公告 / 私有 / Direct 状态、撤回、并发目标状态、多标签、断线重连、WebOS 和键盘矩阵；修复共同根因后清理临时 Reaction、operation、账号、消息、通知、凭据与备份，并复核数据库完整性。

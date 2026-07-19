# F4-E-C 聊天消息置顶正式 Web 完成记录

> 日期：2026-07-19
>
> 结论：F4-E-C 已完成，下一顺位进入 F4-E-D 三普通账号成组验收。

## 本批边界

本批在 F4-E-B 服务端权威契约上完成 PC / mobile Pencil 与正式 `/messages` 页面实现；WebOS `/desktop` 复用同一 Chat App。没有启动 Radish 服务、执行 Gateway / 浏览器 smoke、创建置顶通知或扩展 Flutter / Tauri。

## 完成内容

### Pencil 与交互

- 在权威设计源新增 PC `P13F - Message Pins / Desktop` 与 mobile `P27F - Message Pins / Mobile`。
- PC 使用会话顶部紧凑置顶条与完整浮层列表；mobile 使用同一紧凑入口和 Bottom Sheet。无置顶时完全收起，不占消息阅读空间。
- 列表展示消息摘要、发送者、置顶人、置顶时间、容量与 revision，可定位原消息；受权用户可从消息动作或列表取消置顶。
- 两个画板均完成布局问题检查和截图复核，无裁切、溢出或折叠。

### 权限、状态与实时

- 页面只依据服务端 `VoCanPinMessages` 展示管理入口；临时、发送失败和已撤回消息不提供置顶动作。
- Chat Store 按频道保存权威完整快照；Hub 只接受更高 revision，HTTP 初始读取与重连追平允许相同或更高 revision 覆盖，LongId 全程使用字符串比较。
- 当前频道、连接状态、定位目标不可用和显式重试共同驱动 HTTP 追平；账号 `reset` 清空全部置顶状态，不持久化正文、附件授权 URL 或关键词。
- 撤回事件立即从本地快照移除目标，后续由服务端更高 revision 的完整 Hub 快照收敛；置顶不改变消息、未读、最后消息、搜索、通知或频道排序。

### 组件、主题与文案

- 新增独立 `ChatPinnedMessages` 与 `useChatMessagePins`，消息列表负责工作区编排，继续复用 `useChatMessageNavigation` 的 `GetMessageWindow` 权威定位链路。
- 置顶列表支持 Escape 关闭、打开 / 关闭焦点恢复、频道切换自动关闭、加载 / 错误 / 重试和写入 pending 状态。
- 样式只使用现有主题语义 token，覆盖四套正式主题；长内容进入受控摘要，不复制正文或附件授权状态。
- 补齐中文 / 英文置顶、取消、容量、定位、revision、加载和错误文案，并覆盖英文数量复数。

## 验证结果

- Pencil PC / mobile 两个新增画板 `snapshot_layout` 均无布局问题，并完成截图复核。
- client `442` 项测试通过，新增 LongId revision、Hub 高 revision 合并、HTTP 同 revision 校正、API / Hub / Store / reset / 定位源码契约与英文数量回归。
- `radish.client` 与 `@radish/http` type-check、lint 通过；client production build 通过，只有仓库现有的大分块体积提示。
- `npm run validate:baseline:quick` 通过，包含 `@radish/http 16`、`@radish/ui 23`、client `442`、Console `56` 项测试及版本、权限、敏感字面量、时间语义、Repo Quality 与身份门禁。
- 本批未启动服务或执行浏览器 smoke；三普通账号、真实 PC / mobile、WebOS、多标签、离线重连、并发和 20 条容量矩阵按停止线留给 F4-E-D。

## 下一顺位

进入 F4-E-D：使用至少三个普通账号覆盖 Public、Announcement、普通 Private、Accepted / Pending / Declined / Blocked Direct、撤回与权限变化；完成 `zh / en × PC / mobile`、多标签、真实离线重连、并发目标状态、20 条上限、定位、键盘和 WebOS 矩阵。共同根因修复并补定向测试后，清理临时 pin、账号、频道、消息、通知、凭据与备份，复核六库完整性和 migration verify。

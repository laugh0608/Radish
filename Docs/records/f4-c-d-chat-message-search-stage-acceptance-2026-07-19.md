# F4-C-D 聊天历史搜索成组验收记录

> 日期：2026-07-19
>
> 结论：F4-C-D 已通过，F4-C 聊天历史搜索与消息定位专题正式关闭。

## 验收边界

本批在用户明确授权后启动本地 SQLite 后端、Gateway 与正式 Web 前端，执行服务端迁移校验、双账号真实交互、PC / mobile 与 WebOS 复用面成组验收。验收只修复搜索专题内暴露的共同根因，没有扩入 Reaction、消息置顶、逐条已读、Flutter、PWA 或移动系统通知。

## 运行态矩阵

| 场景 | 结果 |
| --- | --- |
| `zh / en × PC / mobile` | Gateway 正式路径通过；PC 使用 `1920 × 1080`，mobile 使用 `390 × 844` CSS 视口 |
| 当前 / 全部会话与日期筛选 | 请求范围和日期边界正确，Enter 提交与空结果状态通过 |
| Cursor 分页 | 21 条匹配消息首批加载 20 条，继续加载后稳定到 21 条，无重复 |
| 消息定位与历史 | 结果进入权威消息窗口；PC Back / Forward 保留搜索侧栏，mobile Back 恢复条件与结果、Forward 返回消息目标 |
| 公开与互关私聊 | 两个普通账号均可检索公开消息；互关后建立正常私聊，双方均能检索同一私聊消息 |
| 陌生请求状态 | 待处理双方可检索；拒绝和阻断后仍按既有历史读取策略可检索，不把关系状态误作正文权限 |
| 撤回与失效 | 撤回后当前消息与已加载搜索摘要同步脱敏；跨租户 / 无权频道显示明确不可用状态 |
| 主题 | `default / guofeng` 运行态通过；普通账号未持有 `dark / sakura` 权益，后两者按现有 token 与静态契约复核 |
| WebOS | `/desktop?app=chat&channelId=...` 的 PC / mobile 复用同一搜索组件、API 与消息定位链路，无阻断 |

内置浏览器本轮只能设置 CSS 视口，不能覆盖 DPR，也没有离线网络切换能力。因此 `390 × 844 @ DPR 3` 中的 CSS 布局已实跑，DPR 3 与真实断网恢复没有伪写成运行态通过；离线提示、重试和状态保持继续由 F4-C-C 的组件契约与静态回归覆盖。实体手机软键盘、系统分享和原生通知也不属于本专题。

## 验收修复

1. 搜索关键词输入框原先只具备表单语义，实际 Enter 未触发查询；现改为显式处理 Enter，并在 IME 组合输入期间保持不提交。
2. 消息目标历史修订原先会在 PC Forward 时错误关闭搜索侧栏；现只在 compact mobile 视图消费隐藏修订，PC 保持搜索与消息上下文并存。
3. 已加载搜索快照原先可能在消息撤回后保留旧摘要；Store 现在无论目标消息窗口是否已加载都记录撤回 ID，搜索面板即时过滤对应摘要，账号重置时同步清空。

以上三项均补入 `chatMessageSearch.test.ts` 的静态契约，并在真实浏览器中复核 Enter、PC / mobile 历史与撤回摘要。

## 验证结果

- Chat search / migration 定向后端测试：20 项通过，4 项 PostgreSQL 环境用例按配置跳过；本专题 PostgreSQL migration 与字面查询已在 F4-C-B 隔离环境实跑通过。
- `radish.client`：430 项测试通过，type-check、lint 与 production build 通过。
- 本地 SQLite 已完成 `20260718_003_chat_message_search` apply / verify；清理后再次执行 `Radish.DbMigrate verify`，Main / Log / Message / Chat ledger 均已应用，OpenIddict pending 为 0。
- Gateway、API、Auth、正式 Web 启动和宿主运行探针通过；验收结束后进程已全部停止，端口已释放。
- Baseline Quick、仓库卫生与 `git diff --check` 在最终文档差异完成后再次执行。

## 数据清理

- 两批临时普通账号及其公共定位消息、私聊、陌生请求、互关、通知、授权 token、余额流水、可靠 Outbox 与审计日志均按精确 ID 清理。
- 公开频道 `LastMessageId / LastMessageTime` 已恢复到最新保留消息。
- Main / Chat / Message / OpenIddict / Log 的临时对象精确残留均为 0；六个 SQLite 数据库 `integrity_check` 全部为 `ok`。
- 浏览器凭据、验收标签页和临时数据库备份均已清除。

## 关闭结论与下一顺位

F4-C 的服务端权威检索、SQLite / PostgreSQL 迁移、正式 Web / WebOS 工作区、PC / mobile、中英文、分页、定位、权限变化、撤回和清理条件均已满足，专题关闭。

按路线图中已后置能力的长期价值与现有 Chat 扩展顺序，下一顺位进入 F4-D-A 聊天消息 Reaction 的现状审计与专题设计；消息置顶、逐条已读和移动系统通知继续分别后置，主动生产证据采集保持最终收尾冻结。

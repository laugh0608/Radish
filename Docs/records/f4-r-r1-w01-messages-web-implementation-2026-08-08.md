# F4-R R1-W01 Private 消息工作区成组实现记录

> 日期：2026-08-08（Asia/Shanghai）
>
> 状态：正式代表设计、代码实现、Gateway PC / mobile 运行态验收与临时数据清理均已完成；`R1-W01` 已关闭

## 1. 本批结论

- 唯一活动设计源 `radish-web-family-ui-v1.pen` 已完成并确认 `R1-W01 / Private 消息列表—详情` 的 PC `1440`、Mobile `390` 与必要关键状态代表设计；没有修改历史 `.pen`，RadishX `family-ui` 只作为上游参考读取。
- 正式锚点保持 `/messages`，继续复用现有 ChatApp、API、Store 与 SignalR Hub。代表身份固定为“登录普通 User + 与可用普通用户的 Accepted 互关 Direct + 双方未屏蔽且已有历史”，当前会话覆盖文本、图片、引用、Reaction、一条 Pin 和 Direct 已读边界。
- PC 固定为“会话列表 + 消息主轴 + 按需右侧上下文”的连续分栏工作区；搜索与在线成员面板互斥，Pin 在消息区紧凑表达。外层取消大卡片间距、圆角和阴影，避免工作区被切成彼此疏离的看板卡片。
- Mobile 固定为单任务流：会话列表与详情不并排，Pin 和关系确认进入共享 Bottom Sheet；页面结构统一使用 `720px` 紧凑断点。
- 现有 API、权限、路由、SignalR、LongId、幂等和写入边界保持不变；本批没有新增聊天能力，也没有放宽 Recall、频道角色或治理权限。Gateway 验收发现并修正 Chat 私密附件已绑定消息后的 ACL 查询翻译错误，权限判定仍复用原频道策略。

## 2. 正式代表设计

- PC 正式画板：`ugikO`，`R1-W01 / Private 消息列表—详情 / PC 1440`。
- Mobile 正式画板：`YQaBy`，`R1-W01 / Private 消息详情 / Mobile 390`。
- 必要关键状态区：`NhD2r`，`R1-W01 / Private 消息必要关键状态`。
- PC 使用紧凑连续分栏，右侧上下文仅在用户打开在线成员时占位；搜索打开时关闭成员面板，反向亦然。
- 侧栏保留未读 / `@`、陌生请求、既有群与公共频道的代表入口，不虚构完整成员名册、离线 / 在线状态、五分钟消息分组、会话折叠、`99+` 未读、快捷日期范围或显示修订。
- Pending 接收方、Declined / Blocked、Archived、搜索失败、离线 / 发送失败和 Pin 私密读者只进入必要关键状态，不混入默认代表整页。

## 3. 正式 Web 实现

### 3.1 工作区结构

- `MessagesApp` 外层改为最大 `1440px` 的连续工作区；PC 仅保留 `12px` 页面边距，Mobile 进入全宽单任务布局。
- `ChatApp` PC 使用 `300px` 会话栏、弹性消息主轴和按需 `280px` 在线成员栏；成员栏关闭时不保留空轨道。
- `ChatApp.tsx` 收敛至 `1415` 行；输入、附件、提及和 typing throttle 抽取到独立 `ChatComposer`，没有增加无业务边界的转发层。
- 会话头以头像、显示名、权威会话类型和已有描述构成身份区，不推导或展示未落地的离线 / 在线状态。

### 3.2 搜索、成员与 Pin

- 搜索和在线成员入口显式互斥；关闭搜索时恢复之前的焦点，重新打开时焦点进入搜索输入框。
- 在线成员只在面板打开时加载；加载中、失败、空态分别表达，失败提供显式重试，不再把错误伪装成“无人在线”。
- Pin 继续使用现有权限和读取契约；PC 为消息区紧凑展开层，Mobile 使用共享 Bottom Sheet。界面不显示或暗示未落地的修订能力。

### 3.3 响应式与账户边界

- ChatApp、会话工作区、搜索、Pin、阅读回执和输入区统一使用 `CHAT_COMPACT_BREAKPOINT_PX = 720`，避免相邻宽度下出现结构分叉。
- Mobile 关系屏蔽 / 解除屏蔽确认复用共享 Bottom Sheet 的 Escape、焦点和安全区治理；桌面仍使用现有确认弹层。
- 登录账户变更或退出时清空 Chat store 的频道、消息、搜索和选中态，避免同一浏览器会话内跨账户残留。

## 4. 能力与停止线

- Direct 请求接受 / 拒绝、屏蔽 / 解除屏蔽继续由既有 `VoCan*` 与服务端关系状态决定；本批只调整呈现结构。
- Reaction、Pin、阅读回执、搜索、引用、图片和发送失败重试继续调用现有 HTTP / SignalR 链路，未新增客户端自定义 fetch、数字型 LongId 或第二套幂等键。
- Recall 边界仍未关闭；没有新增或暗示版主、群主或其他频道角色可以撤回他人消息。
- 在线成员面板只表达现有 Hub 返回的在线成员，不扩展为完整成员名册，也不持久化推断状态。
- 不恢复旧消息摘要看板、WebOS 窗口或 Dock；WebOS 历史入口继续复用同一 ChatApp，不形成第二套产品实现。

## 5. 静态验证

- `npm run test --workspace=radish.client`：`509 passed / 0 failed`。
- `dotnet test Radish.Api.Tests -c Debug --no-restore`：`1194 passed / 39 skipped / 0 failed`；PostgreSQL 条件用例因本机未配置继续显式跳过。
- `npm run lint --workspace=radish.client`：通过，ESLint `0` warning。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run type-check --workspace=@radish/ui`：通过。
- `npm run type-check --workspace=@radish/http`：通过。
- `npm run build --workspace=radish.client`：production build 通过；仅保留仓库既有的大 chunk 提示。
- 新增代表布局静态契约测试，覆盖连续三栏、按需成员栏、搜索互斥、共享 Bottom Sheet、统一断点、Pin 不显示修订和账户切换 reset；附件 ACL 回归测试覆盖绑定 messageId 的精确匹配与不匹配失败关闭。

## 6. Gateway 运行态验收

- 经当前任务明确授权，使用 `./start.sh` 选项 `8` 启动 Gateway `5000`、API `5100`、Auth `5200`，使用 `npm run dev --workspace=radish.client` 启动 Client `3000`；所有真实页面均从 `https://localhost:5000/messages` 进入。
- 运行态身份为普通用户 `TestUser` 与本轮临时普通用户的 Accepted 互关 Direct，双方未屏蔽并已有历史；真实会话写入文本、引用、图片、`👍` Reaction、一条 Pin 和 Direct 已读边界。临时数据没有扩展为 Pending / Declined / Blocked / Archived 等默认整页状态。
- PC 使用 `1440 × 900`：页面与工作区均无横向溢出；工作区 `1416 × 788`，会话栏 `300px`，在线成员上下文打开后为 `280px`，消息主轴保持弹性宽度。成员面板只在打开时加载两位在线成员；搜索打开时成员面板关闭，关键词“连续分栏”返回 `1` 条结果，关闭搜索后焦点恢复到搜索入口。
- PC Pin 紧凑层显示 `1 / 20`、置顶者、时间、定位与取消置顶；没有显示内部 revision，也没有暗示额外 Recall 权限。默认整页、在线成员、搜索和 Pin 截图分别保存在 `output/playwright/r1-w01-gateway-pc-1440x900.png`、`r1-w01-gateway-pc-members-1440x900.png`、`r1-w01-gateway-pc-search-1440x900.png`、`r1-w01-gateway-pc-pin-1440x900.png`。
- Mobile 使用 `390 × 844 @ DPR 3`：工作区 `390 × 780`，无横向溢出；详情态不并排显示会话栏，返回 `/messages` 后只显示会话列表。Pin 进入共享 Bottom Sheet；顶部搜索与主页按钮具有可访问名称。详情、会话列表和 Pin 截图分别保存在 `output/playwright/r1-w01-gateway-mobile-390x844.png`、`r1-w01-gateway-mobile-list-390x844.png`、`r1-w01-gateway-mobile-pin-390x844.png`。
- 干净 Mobile 会话控制台为 `0 errors / 0 warnings`；图片消息完成受保护资源加载，`naturalWidth=150`、`naturalHeight=87`。PC / Mobile 均确认文本、引用、Reaction、Pin、图片、已读边界和输入区在同一连续消息主轴内。

## 7. 运行态发现与修正

- Mobile smoke 发现会话头在隐藏按钮文字后缺少可访问名称；搜索与查看主页入口补充现有 i18n 文案对应的 `aria-label`，不改变视觉或交互边界。
- 图片上传完成、尚未绑定消息时受保护缩略图返回 `200`，绑定 `Attachment.BusinessId=MessageId` 后转为 `404`。根因是 `CanAccessChatAttachmentAsync` 在同一 SQLSugar 表达式中组合 nullable `messageId.HasValue` 与 OR，被 SQLite 翻译为错误谓词。
- 服务端改为“有 messageId 时按附件 + 精确消息查询；无 messageId 时仅按附件查询”两个明确分支，随后仍调用原 `GetAccessAsync` 频道 ACL。修复后 Gateway 缩略图恢复 `200`，没有增加公开 fallback、上传者旁路或新的附件读取端点。

## 8. 清理与停止

- 精确清理：临时用户 `1`、余额与注册奖励各 `1`、关注关系 `2`、关注通知及收件组各 `2`、Direct `1`、频道成员 `3`、消息 `3`、Reaction / Reaction operation / Pin 各 `1`、附件 `1`，以及对应 Reliable Outbox、Audit、Balance log 和本轮 OIDC 增量。
- 五个受影响 SQLite 数据库均通过 `PRAGMA integrity_check`；所有临时 ID 反查为 `0`。OpenIddict 恢复到验收前基线：Authorization `113` 条、最大 `rowid=124`；Token `2461` 条、最大 `rowid=2579`。
- 本轮上传原图、缩略图、含令牌的临时 Playwright storage state 和短期回滚备份均已删除；正式截图保留为验收证据。
- Gateway、API、Auth、Client 与浏览器会话均已停止；`5000 / 5001 / 5100 / 5200 / 3000` 无监听。

## 9. 结论与下一顺位

- `R1-W01` 正式代表设计、成组实现、静态回归和 Gateway PC / mobile 验收均已关闭。
- Recall 角色边界仍未关闭；本批没有新增或暗示版主、群主撤回他人消息。
- 后续可按既定顺位进入 `R1-C01`，但本轮不启动新专题、不修改新的代表画板。

关联记录：[R1-W01 设计前代码事实与能力覆盖门禁](/records/f4-r-r1-w01-messages-readiness-audit-2026-08-08)、[R1-W01 能力门禁修复](/records/f4-r-r1-w01-messages-capability-gate-implementation-2026-08-08)。

# 2026-02 第四周 (02-24 至 03-02)

## 2026-02-24 (周二)

### 表情包系统规划文档落地

- **系统设计完成**：新增 `emoji-sticker-system.md`，明确 `StickerGroup` / `Sticker` / `Reaction` 数据模型、`sticker://` 语法、API 设计与缓存策略。
- **Console 管理方案完成**：新增 `emoji-sticker-console.md`，定义表情包管理后台与批量上传四步流程、上传后确认表格、文件名清洗规则。
- **前端 UI 规范完成**：新增 `emoji-sticker-ui-spec.md`，补充 Emoji/Sticker 尺寸规范、`StickerPicker` 交互、`ReactionBar` 动效与 GIF/虚拟滚动性能策略。

### 聊天室 App 规划文档落地

- **后端系统设计完成**：新增 `chat-system.md`，确定 Discord 风格频道制模型（分类/频道/消息/成员）、`ChatHub` 事件、REST API、在线状态与限流方案。
- **前端方案设计完成**：新增 `chat-frontend.md`，明确三栏布局、`chatHub.ts` 单例、虚拟滚动、`contenteditable` 输入框（@mention/表情包）与 `chatStore` 状态管理方案。
- **阶段路线图补齐**：聊天室 App 的实现路径、与现有 WebOS/Dock/组件复用关系形成可执行规划，便于后续拆分任务推进。

## 2026-02-25 (周三)

### 表情包系统后端实现推进（Phase 1 首版）

- **后端基础能力提交**：完成 `StickerGroup/Sticker` 实体、Service、Controller 基础链路与缓存失效。
- **批量能力提交**：完成 `BatchAddStickers`、`BatchUpdateSort`、缩略图生成链路与管理端错误码返回。
- **管理端分组接口补齐**：新增 `GetAdminGroups`，用于 Console 查询含禁用分组的数据。
- **联调与测试资产补齐**：
  - 新增 `Radish.Api.Tests/HttpTest/Radish.Api.Sticker.http`（表情包接口联调样例）
  - 新增并扩展 `StickerController` 单元测试（含批量接口与管理端分组查询）
  - `Radish.Api.Tests` 全量通过（96 个测试）

### Console 表情包管理首版落地

- **路由与导航接入**：新增 `/stickers`、`/stickers/:groupId/items`，侧边栏新增“表情包管理”入口。
- **分组管理页完成**：列表、创建/编辑弹窗、删除流程落地。
- **分组内表情管理页完成**：列表、单个新增/编辑弹窗、删除、批量排序保存落地。
- **API 封装完成**：新增 `stickerApi.ts`，覆盖当前已实现的管理端接口。

### 文档与代码对齐

- 同步更新表情包系统与 Console 文档，修正早期 `/Admin/` 路由示例为当前实际路由。
- 补充“已实现 / 未实现 / 次日衔接”状态，避免文档与代码偏差。

### 已知状态与阻塞说明

- **后端验证状态**：`Radish.Api.Tests` 全量通过（96/96）。
- **Console 构建状态**：表情包新增页面代码无新增 TS 报错，但仓库仍存在既有 TS 报错（`Dashboard`、`SystemConfig`、`Tags` 页面），当前 `npm run build --workspace=radish.console` 不能全绿。
- **明日建议处理方式**：表情包任务可继续推进（不依赖这些页面）；若需要全量构建通过，可先单独清理 Console 存量类型问题。

### 次日任务衔接（2026-02-26）

- **T1（优先）**：实现 Console 批量上传四步流（上传进度、确认表格、冲突高亮重提）。
- **T2**：接入分组封面图上传组件（替换当前 URL 手填）。
- **T3**：开始 `StickerPicker`（insert 模式）与 Forum 编辑器 `sticker://` 渲染链路。

## 2026-02-26 (周四)

### Console 批量上传四步流（T1）完成

- **批量流程闭环**：分组表情页新增“批量上传”入口，完成四步流：文件选择 → 上传进度 → 确认表格 → 冲突修复重提。
- **上传进度能力**：使用 `XMLHttpRequest` 接入附件上传并展示逐条进度，支持失败项重传；上传配置从 `getApiClientConfig()` 读取。
- **冲突修复能力**：批量提交后可保留服务端冲突/失败明细，按行高亮并支持编辑后重提，减少重复选择文件成本。
- **API 对接增强**：`stickerApi.ts` 新增保留失败响应体的批量提交方法（用于读取 `BatchAddStickers` 的冲突明细）。

### 当前状态（2026-02-26）

- **已完成**：T1。
- **待继续**：T2（分组封面图上传组件）、T3（`StickerPicker` insert 模式 + Forum `sticker://` 渲染链路）。

## 2026-02-27 (周五)

### T2 收口确认：分组封面图上传组件

- **分组封面图上传链路确认可用**：`StickerGroupForm` 已采用上传 + 预览 + 清空模式，替换 `CoverImageUrl` 手填路径。
- **上传约束完整**：图片类型校验、5MB 大小限制、上传进度回传与失败提示已具备。

### T3 首段落地：StickerPicker（insert）+ `sticker://` 渲染链路

- **`@radish/ui` 新增 `StickerPicker` 组件（insert 模式）**：
  - 支持分组 Tab、关键词过滤、Unicode/Sticker 选择与 `allowInline=false` 禁选态。
- **`MarkdownEditor` 接入贴图插入能力**：
  - 新增 `stickerGroups` / `stickerMap` / `onStickerSelect` 扩展 props。
  - 选择 Sticker 后插入 `![name](sticker://group/code#radish:image=...&thumbnail=...)`，保留历史内容 fallback 渲染信息。
- **`MarkdownRenderer` 扩展 `sticker://` 渲染**：
  - 支持 `sticker://group/code` 解析；
  - 优先命中 `stickerMap`，未命中时回退 `#radish:image/thumbnail`；
  - 正文按 inline sticker 样式渲染，不触发图片灯箱。
- **Forum 侧接入第一段链路**：
  - 新增 `api/sticker.ts` 与 `useStickerCatalog`（分组加载、渲染映射、`RecordUse` 上报）。
  - 发布/编辑入口已接入 `StickerPicker`：`PublishPostModal`、`EditPostModal`、`PublishPostForm`。
  - 帖子详情 `PostDetail` 已接入 `MarkdownRenderer` 的 `stickerMap`，形成“插入 -> 渲染”闭环。

### 验证状态（2026-02-27）

- ✅ `npm run type-check --workspace=@radish/ui` 通过。
- ✅ `npm exec --workspace=radish.client -- tsc -b` 通过。
- ⚠️ `npm run build --workspace=radish.client` 受环境依赖阻塞（`@rolldown/binding-linux-x64-gnu` 缺失），与本次业务改动无直接耦合。

### 评论框接入补充（2026-02-27）

- **CreateCommentForm 接入 `StickerPicker`（insert 模式）**：
  - 评论输入框工具栏支持插入 Unicode 与 Sticker，生成 `sticker://` 语法。
  - 与 Forum 侧 `RecordUse` 上报链路打通。
- **评论渲染支持 `sticker://`**：
  - `CommentNode` 新增 `sticker://group/code` 解析与内联渲染（`stickerMap` 优先，hash fallback 次之）。
  - 继续保留原有图片网格 + 灯箱链路，非 sticker 图片渲染行为不变。
- **数据下发收口**：
  - `PostDetailContentView` 统一加载 `stickerGroups/stickerMap`，传递给 `CommentTree` 与 `CreateCommentForm`，避免重复请求。

### 设计文档同步对齐（2026-02-27）

- 已同步更新 `emoji-sticker-system.md`，将实现进度与接口/渲染说明对齐到 2026-02-27 代码状态。
- 已同步更新 `emoji-sticker-ui-spec.md`，补充 insert 模式落地范围、`sticker://` fallback 规范与当前代码结构。
- 已同步更新 `development-plan.md`，将“次日任务”替换为“已完成 + 下一阶段任务”，避免文档与执行顺序偏差。

### 论坛表情交互与 Console 登录补丁（2026-02-27 夜间）

- **Console 自动登录链路补齐**：
  - 未登录访问 `/console/` 时改为自动跳转 `login?auto=1` 并自动发起 OIDC 登录，减少手动点击一次“登录”的步骤。
- **评论区 StickerPicker 交互修复**：
  - 评论输入框固定展示 StickerPicker 入口；
  - 贴纸面板支持 `left/right` 对齐，评论场景改为左对齐，修复 BottomSheet 中面板被裁切成窄竖条的问题。
- **发帖编辑器入口收口**：
  - `MarkdownEditor` 统一使用 `StickerPicker`，移除旧的独立 Emoji 面板分支，确保发帖/评论一致展示“Emoji + 分组 Tab”。
- **StickerPicker 体验增强**：
  - Tab 增加文字分组标识（如 Emoji、radish 等）；
  - 外部点击收起事件改为 `pointerdown`（覆盖鼠标与触屏）。
- **Console 顶栏用户名兜底**：
  - 当 `/api/v1/User/GetUserByHttpContext` 返回空用户名或请求失败时，前端从 access token claim（`name` / `preferred_username`）兜底展示，避免出现 `Unknown`。

### 当前遗留（2026-02-27 收工前）

- 本轮修复后仍有部分体验细节待继续打磨，已记录为次日继续项（2026-02-28）。
- 本次先完成“可用性修复 + 文档对齐 + 提交收口”，避免夜间继续改动引入新回归。

## 2026-03-01 (周日)

### Forum 表情面板与 Console 用户信息修复

- **StickerPicker Emoji 对齐修复**：修复 Emoji 按钮 hover 背景与字符偏移问题，统一居中与零内边距样式。
- **Console 用户态刷新链路修复**：OIDC 回调后主动触发用户上下文刷新，修复个人信息页空白与右上角 `Unknown` 的问题。
- **用户名展示兜底增强**：用户接口异常时优先从 token claim 解析用户名，减少导航栏空值。

### 表情包 Phase 2 文档冻结与入口收口

- **新增专项文档**：新增 `emoji-sticker-reaction-phase2.md`，冻结 Reaction Phase 2 的 API 合同、并发状态机、前端交互状态机、测试与发布清单。
- **文档互链补齐**：`emoji-sticker-system.md`、`emoji-sticker-console.md`、`emoji-sticker-ui-spec.md` 已统一补充 Phase 2 链接，避免实现入口分散。
- **规划文档同步**：`development-plan.md` 已补充“Phase 2 设计冻结完成”节点，作为后续编码基线。

### Client/Console 认证隔离与回归修复

- **Token 本地存储隔离**：`radish.client` 与 `radish.console` 分别改用命名空间键（`radish_client_*` / `radish_console_*`），避免同源下互相覆盖 `refresh_token`。
- **认证读取统一收口**：两端业务代码统一通过 `tokenService` 读写 token，减少直接 `localStorage` 访问导致的散点风险。
- **Console 401 跳转修正**：401 后统一跳转 `/console/login`，避免误跳到站点根路由导致落入 client。
- **兼容策略调整**：移除 Console 对旧通用 token 键的自动迁移，避免误读 client 旧会话。

### Forum 神评预览与回应面板体验修复

- **神评首屏稳定性增强**：帖子列表批量神评接口增加前端重试与退避，缓解 dev 重启后首次打开论坛偶发不显示神评的问题。
- **神评失败可观测性补齐**：热门神评与帖子神评预览批量请求失败由静默吞错改为 `warn` 日志记录，便于排查环境与请求时序问题。
- **回应快捷面板改为浮层**：`ReactionBar` 快捷面板改为绝对定位浮层，不再撑开评论卡片高度。
- **回应面板视觉一致性修复**：Forum 中 `StickerPicker` 反应模式显式使用浅色主题，解决小面板与大面板颜色不一致。

### 阶段决策：M11 收官并启动 M12 社区功能冲刺

- **里程碑调整生效**：宣布 M11（查漏补缺阶段）于 2026-03-01 收官，进入 M12（社区功能冲刺阶段）。
- **后续顺延**：原 M12（可观测性与测试）顺延为 M13，原 M13（部署与运维）顺延为 M14，原 M14（Gateway & BFF）顺延为 M15（暂缓）。
- **M12 主线范围**：
  - 社区主线：聊天室 MVP、关系链、内容治理、分发能力
  - 文档体系：Markdown 导入导出、`radish.docs` 迁移到 Wiki/文档 App
  - 体验规范：主题切换、中国风 UI 与配色规范、i18n 规范加强
  - 权限治理：权限管理 + 菜单/按钮级权限控制
  - 新功能孵化：文字修仙 App、抽奖/投票/问答、邮件通知、开源软件清单声明组件

### 验证状态

- `npm run type-check --workspace=radish.client` 通过。
- `npm run type-check --workspace=radish.console` 通过。

## 本周总结

- **表情包系统进入实现阶段**：完成设计文档后，已落地后端 Phase 1 首版与 Console 首版管理能力。
- **聊天室 App 方案成型**：完成后端事件与前端交互的整体规划，明确技术路线与性能策略。
- **文档持续对齐代码**：补足实现进度、联调入口与次日任务衔接，降低断点续开发成本。

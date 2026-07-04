# P3-12-D61 Public Web `/discover` Pencil 首批实现记录

## 基本信息

- 日期：`2026-07-04`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：Public Web Pencil 逐页 UI 与功能缺口实现首批
- 范围：`/discover` 承接 `P01-P02` 公开 App Home / 发现流的首屏结构、真实内容密度、公开参与信息和正式文案收口

## 本批结论

本批开始进入 `P3-12-D61 Public Web Pencil 逐页 UI 与功能缺口实现`，优先修正 `/discover` 与 Pencil `P01` 的核心偏差：页面首屏不再以大型“社区发现 / 公开只读”验收型 hero、入口矩阵和说明卡作为主信息，而是回到 Public App Home 的产品级品牌、紧凑公开脉搏、真实帖子讨论流和右侧公共内容亮点。

本批不代表 D61 完成。后续仍需继续覆盖 Public Web 其余页面族：论坛列表 / 详情、文档列表 / 详情、商城 / 商品、榜单、公开主页和移动公开任务流。`P15-P16` 公开聊天室仍按 D60 作为 Public 小专题内的后置产品 / API 缺口记录，不在本批实现。

## 代码范围

- `Frontend/radish.client/src/public/discover/PublicDiscoverApp.tsx`
  - 将 `/discover` 壳层品牌从页面级“社区发现 / 公开社区分发页”调整为 Pencil 公开首页口径的 `Radish / 公开入口`。
  - 用 `pulseHome` 替换首屏大型 hero：左侧保留公开介绍、forum / docs / shop / 分享动作；右侧改为紧凑指标卡。
  - 在首屏第二层新增真实公开讨论区：复用现有 `forumPosts` 数据与 `PostCard publicCompact`，展示 3 条真实帖子、互动数字、标签、作者和最近互动信息。
  - 新增右侧公共亮点：从现有 docs / shop / leaderboard 数据生成可点击的文档、商品和榜单入口。
  - 保留既有数据请求、导航回调、分享逻辑、来源记忆、forum/docs/shop/leaderboard 路由语义不变。
- `Frontend/radish.client/src/public/discover/PublicDiscoverApp.module.css`
  - 新增公开首页首屏两栏、讨论区两栏、右侧 rail 和 mobile 堆叠样式。
  - 收紧卡片半径、间距和指标密度，移除旧 hero 的装饰性浮层。
  - 保证 `390px` 窄屏下首屏按介绍、指标、真实讨论、亮点顺序堆叠，不新增横向滚动来源。
- `Frontend/radish.client/src/i18n.ts`
  - 补齐公开首页品牌、脉搏区、讨论区、右侧亮点和登录后参与提示的中英文文案。

## 追加收口

- 已将 `/discover` 与公共页头中的“社区发现”统一为“发现”。
- 已将首屏 `公开 Web`、`公开社区脉搏`、`公开只读`、`Web-first`、`forum / docs` 等临时或实现说明式文案替换为正式产品文案。
- 已补 Public 右侧登录 / 登录状态入口：未登录态显示 `登录 + 工作台`，登录态显示 `我的状态 + 我的圈子 + 工作台`。
- 已将 Public 右侧动作从单个胶囊组回拉为独立按钮：`登录 / 我的状态 / 我的圈子` 使用浅色描边按钮，`工作台` 使用主色实心按钮，对齐 `P01` 设计稿。
- 已同步 `public-web-unified-experience.pen` 的 `P01 - Public App Home` 编辑器内容：品牌副标题、hero 徽标、hero 说明、示例帖子说明和登录后参与说明与代码口径一致。
- Pencil `P01` 布局检查结果：`No layout problems`。

## P03-P04 论坛列表 / 详情追加实现

- `Frontend/radish.client/src/public/forum/PublicForumList.tsx`
  - 将公开论坛列表从单栏卡片页调整为主列表 + 右侧 rail：主栏继续承载分类、标签、作者、赞评阅、最近互动和神评摘要；右栏承载列表语义、公开路由入口和登录后发帖入口。
  - 发帖入口复用既有 `/forum/compose` 路由与已有公开发帖页，不新增路由语义、API、权限或提交载荷。
- `Frontend/radish.client/src/public/forum/PublicForumDetail.tsx`
  - 将公开帖子详情调整为主详情 + 右侧 rail：主栏承载正文、只读 reaction 汇总、轻回应和评论树；右栏承载来源返回、阅读提示、神评 / 沙发 / 轻回应语义和登录后参与入口。
  - reaction 仅通过现有 `getReactionSummary('Post', postId)` 读取汇总并展示，不接入新增切换或提交能力；列表页不展开 reaction。
  - 来源返回继续使用传入的 `backHref / backLabel / onBack`，保留列表、搜索、标签和结构化 feed 的返回上下文。
- `Frontend/radish.client/src/apps/forum/components/PostDetail.tsx`
  - 新增可选 `density="compact"`，仅由公开帖子详情使用，用于收紧标题、正文、meta 和标签密度；默认 `normal` 不影响 WebOS 内部论坛。
- `Frontend/radish.client/src/apps/forum/components/PostQuickReplyWall.tsx`
  - 新增可选 `density="compact"`，公开详情页下收紧轻回应墙、输入区和 pill 密度；提交载荷保持既有 `createPostQuickReply`。
- `Frontend/radish.client/src/apps/forum/components/CommentTree.tsx`、`CommentNode.tsx`
  - 新增可选 `density="compact"`，公开详情页下收紧评论树间距、头像、内容行高和子回复缩进。
  - 神评、沙发、二级评论结构、子回复展开和排序继续复用既有后端标记与组件行为。
- `Frontend/radish.client/src/apps/forum/components/PostCard.tsx`
  - 公开紧凑卡片的互动标签改为“最近互动”，继续复用已有 `voLatestInteractors`、评论数和神评摘要数据。
- `Frontend/radish.client/src/public/forum/PublicForumApp.module.css`
  - 将公开论坛 P03/P04 主体宽度扩展为 PC 主栏 + 右栏布局，`1120px` 以下回落单栏 / 双列 rail，`720px` 以下适配 390px mobile 纵向堆叠。
- `Frontend/radish.client/src/i18n.ts`
  - 补齐 P03/P04 列表 rail、详情 rail、reaction 汇总、来源返回和“最近互动”中英文文案。

## P05-P06 文档列表 / 详情追加实现

- `Frontend/radish.client/src/public/docs/PublicDocsApp.tsx`
  - 将公开文档列表从单卡片内两列调整为 `P05` 对应的目录 / 文档索引 / 右侧 rail 三栏结构。
  - 搜索页保留既有关键词检索、分页和详情跳转语义，补右侧搜索上下文、目录返回和阅读提示 rail。
  - 详情页保留顶部来源返回、复制链接和登录后编辑入口；正文区域调整为主阅读栏 + 右侧来源返回 / 阅读元信息 / 作者入口 / 相关文档 rail。
  - 相关文档仅从父级已加载的公开文档列表派生，不新增 API 请求、业务路由或保存载荷。
- `Frontend/radish.client/src/public/docs/PublicDocsRails.tsx`
  - 新增公开文档列表、搜索和详情复用 rail 组件，承载阅读提示、搜索入口、来源返回、作者入口和相关文档推荐。
- `Frontend/radish.client/src/public/docs/publicDocsFormat.ts`
  - 将公开文档可见性、状态和来源显示格式化抽出复用，避免主页面继续膨胀。
- `Frontend/radish.client/src/public/docs/PublicDocsApp.module.css`
  - 主体宽度扩展到 `1376px` 设计宽度；PC 下列表为三栏、搜索和详情为主栏 + 右栏。
  - `1120px` 以下回落单栏并将 rail 改为双列卡片区，`720px` 以下按 `390px` mobile 纵向堆叠。
- `Frontend/radish.client/src/i18n.ts`
  - 补齐 P05/P06 列表 rail、搜索 rail、详情来源返回、阅读元信息、作者入口和相关文档中英文文案。

## 保持不变

- 不新增或修改业务 API。
- 不新增或修改权限键。
- 不修改数据库结构。
- 不修改 public 路由语义。
- 不修改保存 / 提交载荷。
- 不新增或重排 `.pen` 页面结构；仅同步 `P01` 文案口径。
- 不进入 `P3-12-E`。

## 后续 D61 待办

1. `P03-P04` 论坛列表 / 详情已完成首轮前端对齐；后续若用户当轮确认前后端已启动，再补 Gateway PC / mobile 真实 smoke。
2. `P05-P06` 文档列表 / 详情已完成首轮前端对齐；后续若用户当轮确认前后端已启动，再补 Gateway PC / mobile 真实 smoke。
3. 继续对齐 `P07` 商城 / 商品：公开浏览密度、商品详情首屏购买信息、库存 / 售出 / 状态提示和登录购买回流。
4. 继续对齐 `P08-P09` 榜单 / 公开主页：榜单切换、公开内容 tab、关注登录回流和来源返回。
5. 继续对齐 `P10-P14` 移动公开任务流：首屏真实内容、底栏前信息量、筛选 / tab 展开方式和低频入口 `/workbench` 承接。

## 验证

- `npm run build --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`（追加收口后复跑）：通过。
- `npm run build --workspace=radish.client`（P03-P04 论坛列表 / 详情追加实现后复跑）：通过。
- `npm run build --workspace=radish.client`（P05-P06 文档列表 / 详情追加实现后复跑）：通过。
- `git diff --check`（P05-P06 文档列表 / 详情追加实现后复跑）：通过。
- `node Scripts/check-repo-hygiene.mjs ...`（P05-P06 本批 6 个文件显式检查）：通过，未发现文本卫生问题。
- Pencil `P01 - Public App Home`：`snapshot_layout(problemsOnly=true)` 返回 `No layout problems`，截图确认品牌副标题、hero 文案与登录后参与说明已同步。
- Gateway `/discover` PC `1920x1080`：标题为 `发现 - Radish`；右侧存在 `登录 + 工作台`；全局横向溢出 `0`；未出现 `社区发现`、`公开 Web`、`公开社区`、`公开只读`、`Web-first`、`Forum / Docs` 英文来源标签或裸路由路径。
- Gateway `/discover` mobile `390x844`：标题为 `发现 - Radish`；横向溢出 `0`；未发现越界元素；未出现上述旧文案与裸路由路径。

## 未执行

- 本记录首批提交时未执行真实 Gateway smoke；追加收口轮用户已确认前后端启动，并已补 `/discover` PC / mobile 真实页面复核。
- `P03-P04` 论坛列表 / 详情追加实现本轮未执行真实 Gateway smoke；本轮用户未明确说明前后端已经启动，按协作规则仅执行静态构建与 diff 检查。
- `P05-P06` 文档列表 / 详情追加实现本轮未执行真实 Gateway smoke；本轮用户未明确说明前后端已经启动，按协作规则仅执行静态构建与 diff 检查。

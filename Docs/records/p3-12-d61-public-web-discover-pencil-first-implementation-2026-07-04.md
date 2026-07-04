# P3-12-D61 Public Web `/discover` Pencil 首批实现记录

## 基本信息

- 日期：`2026-07-04`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：Public Web Pencil 逐页 UI 与功能缺口实现首批
- 范围：`/discover` 承接 `P01-P02` 公开 App Home / 发现流的首屏结构、真实内容密度和公开参与信息

## 本批结论

本批开始进入 `P3-12-D61 Public Web Pencil 逐页 UI 与功能缺口实现`，优先修正 `/discover` 与 Pencil `P01` 的核心偏差：页面首屏不再以大型“社区发现 / 公开只读”验收型 hero、入口矩阵和说明卡作为主信息，而是回到 Public App Home 的产品级品牌、紧凑公开脉搏、真实帖子讨论流和右侧公共内容亮点。

本批不代表 D61 完成。后续仍需继续覆盖 Public Web 其余页面族：论坛列表 / 详情、文档列表 / 详情、商城 / 商品、榜单、公开主页和移动公开任务流。`P15-P16` 公开聊天室仍按 D60 作为 Public 小专题内的后置产品 / API 缺口记录，不在本批实现。

## 代码范围

- `Frontend/radish.client/src/public/discover/PublicDiscoverApp.tsx`
  - 将 `/discover` 壳层品牌从页面级“社区发现 / 公开社区分发页”调整为 Pencil 公开首页口径的 `Radish / 公开 Web`。
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

## 保持不变

- 不新增或修改业务 API。
- 不新增或修改权限键。
- 不修改数据库结构。
- 不修改 public 路由语义。
- 不修改保存 / 提交载荷。
- 不修改 `.pen` 设计源。
- 不进入 `P3-12-E`。

## 后续 D61 待办

1. 继续对齐 `P03-P04` 论坛列表 / 详情：列表密度、分类 / 标签、最近互动、reaction 展示、神评 / 沙发、轻回应和评论树层级。
2. 继续对齐 `P05-P06` 文档列表 / 详情：目录 / 搜索节奏、相关文档、作者入口、正文首屏和详情页辅助信息位置。
3. 继续对齐 `P07` 商城 / 商品：公开浏览密度、商品详情首屏购买信息、库存 / 售出 / 状态提示和登录购买回流。
4. 继续对齐 `P08-P09` 榜单 / 公开主页：榜单切换、公开内容 tab、关注登录回流和来源返回。
5. 继续对齐 `P10-P14` 移动公开任务流：首屏真实内容、底栏前信息量、筛选 / tab 展开方式和低频入口 `/workbench` 承接。

## 验证

- `npm run build --workspace=radish.client`：通过。

## 未执行

- 未执行真实 Gateway smoke。原因：本轮用户给出 public 截图并要求开始 D61 实现，但本轮未重新明确说明当日前后端已启动；按协作规则，真实 smoke 需要用户当轮确认前后端已启动后再执行。

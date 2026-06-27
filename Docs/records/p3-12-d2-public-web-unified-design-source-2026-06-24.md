# P3-12-D2 公开 Web 统一体验设计源记录

> 日期：2026-06-24（Asia/Shanghai）
>
> 状态：公开 Web 设计源 `P01-P16` 已补齐；不进入视觉代码实现
>
> 结论：`public-web-unified-experience.pen` 当前已重构并二次强化为 `P01-P16` 公开社区 App 页面族，覆盖公开首页、发现流、论坛列表 / 详情、评论树、轻回应、公开聊天室、文档列表 / 详情、商城、榜单、公开个人主页和移动公开任务流。已新增并更新 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design) 作为实现前口径；当前仍不进入视觉代码实现。
>
> 2026-06-25 历史补充：完成首批 `P01-P05` 信息密度收口，降低桌面展示型字号、卡片 padding、section gap 和旧画板高度，修正 `P04` 顶部双激活状态，并补齐移动继续探索 / 登录参与内容。
>
> 2026-06-27 导航一致性回修：`P01-P04` 旧 64 高小标签式 PC header 已统一替换为 `F02` 84 高纸感横匾，保留品牌、公开导航、`/workbench` 和登录动作，并与 PC 页面族保持同一 header 比例、nav pill 和 action rail。
>
> 2026-06-27 公开 App 化重构：按用户反馈，原 `P06-P09` “Matrix” 画板更像路由覆盖图，不像完整公开 App 页面。已将公开设计源重构为 `P01-P14`：PC 端覆盖公开首页、发现流、论坛列表、帖子详情、文档列表、文档详情、商城、榜单、公开个人主页；移动端覆盖发现 / 论坛、帖子详情、文档、商城 / 榜单和公开主页任务流。论坛“神评 / 沙发”已改为评论流内 badge，不再作为帖子详情元字段或后台状态块外露。
>
> 2026-06-27 公开社区特色二次精修：按用户反馈继续强化 `P01 / P02 / P03 / P04 / P11`，补回论坛列表左侧标题 / 摘要 / 标签 / 分类、右侧作者 / 赞评阅 / 最近互动结构，明确神评为父评论、沙发为子回复 / 楼中楼语境，并补充轻回应、表情 reaction、引用回复和公开聊天室。新增 `P15 - Public Chat Room` 与 `P16 - Mobile Chat Reply Flow`；PC 公开页 header 已统一同步 `web-ui-foundation.pen` / `F02` 84 高纸感横匾，并新增“聊天室”公开导航项。
>
> 2026-06-27 reaction 与聊天方向回修：按用户截图反馈，`P03` 论坛列表已去除表情 reaction，只保留神评摘要、作者、赞 / 评 / 阅和评论入口；`P04` 帖子详情改为父评论神评卡、子回复沙发缩进和引用 chip；`P15 / P16` 聊天室改为自己消息在右、他人消息在左的 IM 气泡流。

## 背景

`P3-12-B1` 至 `P3-12-B6` 已让 Web 端按规划口径成为正式 Web 主路径完整 app。下一阶段进入 `P3-12-D` 统一 UI 设计与视觉收束，不继续补旧 WebOS 功能。

`P3-12-D1` 已先定义页面矩阵、设计源拆分和停止线。本轮在 Pencil 可用后，开始补公开 Web 设计源。

## 设计源

文件：

```text
Docs/frontend/design-sources/public-web-unified-experience.pen
```

已同步登记：

- [设计源文件目录](/frontend/design-sources/README)
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)

首批变量：

- `rx-bg-app`
- `rx-bg-surface`
- `rx-bg-muted`
- `rx-text-primary`
- `rx-text-secondary`
- `rx-text-muted`
- `rx-border-soft`
- `rx-brand-primary`
- `rx-brand-soft`
- `rx-accent-jade`
- `rx-accent-ink`
- `rx-accent-earth`
- `rx-accent-purple`
- `rx-pattern-line`
- `rx-font-heading`
- `rx-font-body`
- `rx-font-mono`
- `rx-radius-card`
- `rx-radius-control`
- `rx-space-*`
- `rx-shell-max-width`

变量取值沿用 [视觉主题规范](/frontend/visual-theme-spec) 与 [视觉颜色参考](/frontend/visual-color-reference) 的淡雅新中式、纸色底、低饱和边框和克制品牌色口径。

## 已完成画板

### PC 公开 App 页面

- `P01 - Public App Home`：公开 App 首页，聚合社区脉搏、论坛热帖、神评候选、轻回应、聊天室、文档更新、商城精选、榜单和登录参与入口。
- `P02 - Discover Content Stream`：`/discover` 公开混合内容流，展示论坛、文档、聊天室、商城和榜单的真实内容卡片、筛选和登录回流。
- `P03 - Forum Thread List`：`/forum` 公开帖子列表，覆盖左侧标题 / 摘要 / 标签 / 分类 / 神评摘要、右侧作者 / 赞评阅 / 最近互动和登录发帖入口；列表页不展示表情 reaction。
- `P04 - Forum Thread Detail`：`/forum/post/:id` 公开帖子详情，覆盖作者、正文、帖子级轻回应、登录评论、评论树、父评论神评卡、子回复沙发缩进、引用 chip、表情 reaction 和相关帖子。
- `P05 - Docs Index and Search`：`/docs` 文档库，覆盖目录、搜索筛选、公开文档列表、继续阅读和状态槽。
- `P06 - Docs Article Reading`：`/docs/:slug` 文档详情，覆盖正文阅读、来源返回、目录、作者入口、相关文档和下线 / 权限状态。
- `P07 - Public Shop and Product`：`/shop` 与 `/shop/product/:id?intent=purchase`，覆盖公开商品浏览、商品详情、库存、登录购买回流和私域订单边界。
- `P08 - Public Leaderboards`：`/leaderboard/:type` 公开榜单，覆盖贡献者、热帖、文档和商品排名，明确榜单实体跳转。
- `P09 - Public Profile`：`/u/:id` 公开个人主页，覆盖身份展示、公开内容 tab、关注登录回流、来源返回和隐私限制状态。
- `P15 - Public Chat Room`：`/chat` / `/chat/:room` 公开聊天室，覆盖房间列表、自己右侧 / 他人左侧 IM 气泡、引用帖子、回复、表情 reaction、在线成员、房间上下文和登录发言。

### 移动公开任务流

- `P10 - Mobile Discover Forum`：移动发现 / 论坛列表，覆盖搜索、筛选、帖子卡、文档卡和登录参与。
- `P11 - Mobile Post Detail`：移动帖子详情，覆盖正文、轻回应、父评论神评、子回复沙发、评论输入和登录评论。
- `P12 - Mobile Docs Reading`：移动文档列表 / 文档详情，覆盖目录入口、正文预览、登录收藏和文档任务流。
- `P13 - Mobile Shop Leaderboard`：移动商城 / 榜单，覆盖商品详情、购买 intent、贡献者榜和实体跳转。
- `P14 - Mobile Public Profile`：移动公开主页，覆盖身份头部、公开内容 tab、关注回流和来源返回。
- `P16 - Mobile Chat Reply Flow`：移动聊天室 / 引用回复任务流，覆盖房间头部、引用帖子、左右消息气泡、快捷 reaction、输入框和聊天 tab。

设计口径：

- 公开页必须像可交付 App 页面，不再使用 route rail / Matrix 作为主要画板形态。
- 每个公开页面族必须有自己的 dominant region、主动作和真实内容结构。
- 论坛“神评 / 沙发”只在评论树内作为 badge 出现，不作为帖子元字段或后台状态块；神评属于父评论，沙发属于子回复 / 楼中楼首条回复语境。
- 公开社区页必须体现赞 / 评 / 阅、轻回应、表情 reaction、评论头像、最近互动、引用回复和聊天室上下文。
- 表情 reaction 只在帖子详情页和聊天室展示；论坛列表只展示神评摘要、赞 / 评 / 阅、作者和评论入口。
- 聊天室使用 IM 气泡方向：自己发言在右侧，他人发言在左侧。
- 移动端按真实任务流拆分，不把 PC 三栏压缩成单个移动示意稿。

## 验证

Pencil 侧：

- `P01`：`snapshot_layout` 返回 `No layout problems.`
- `P01`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P01`：2026-06-25 按桌面壳层密度收口，压缩 header / context bar / 主卡片 / 右侧 rail，并补齐公开入口组和运行态 rail；复查无布局问题。
- `P02`：首次生成后发现主体内容被画板高度裁切；已加高画板并复查。
- `P02`：修正 lucide 图标名 `check-circle-2` 为 `circle-check`。
- `P02`：复查 `snapshot_layout` 返回 `No layout problems.`
- `P02`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P02`：2026-06-25 按桌面信息密度收口，压缩 header / lead / toolbar / 卡片 padding / 右侧 rail 和画板高度；复查无布局问题。
- `P03`：`snapshot_layout` 返回 `No layout problems.`
- `P03`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P03`：2026-06-25 按桌面阅读页密度收口，压缩详情 lead、正文卡片 padding、段落字号和右侧 rail；复查无布局问题。
- `P04`：首次生成后发现 lucide 图标名 `check-circle-2` 不存在；已替换为 `circle-check`。
- `P04`：复查 `snapshot_layout` 返回 `No layout problems.`
- `P04`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P04`：2026-06-25 按集合页密度收口，压缩 lead / toolbar / 列表项 / 右侧 rail，修正顶部“论坛 / 文档”双激活为单一激活；复查无布局问题。
- `P05`：`snapshot_layout` 返回 `No layout problems.`
- `P05`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P05`：2026-06-25 按移动单列密度收口，补齐继续探索、公开商城和登录参与内容，收住底部留白；复查无布局问题。
- `P01-P04`：2026-06-27 按 `F02` 公共壳层契约统一 PC header 至 84 高纸感横匾；局部 `snapshot_layout` 均返回 `No layout problems.`，截图抽查 `P01 / P02 / P03` 与 `P06` header 风格一致。
- `P06`：`snapshot_layout` 返回 `No layout problems.`
- `P06`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P07`：`snapshot_layout` 返回 `No layout problems.`
- `P08`：`snapshot_layout` 返回 `No layout problems.`
- `P08`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P09`：`snapshot_layout` 返回 `No layout problems.`
- `P10`：`snapshot_layout` 返回 `No layout problems.`
- `P10`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P11`：`snapshot_layout` 返回 `No layout problems.`
- `P12`：`snapshot_layout` 返回 `No layout problems.`
- `P12`：截图目检未发现明显裁切、坍塌或横向溢出。
- 全局：`public-web-unified-experience.pen` 复查 `snapshot_layout` 返回 `No layout problems.`
- 2026-06-27 公开 App 化重构后：`P01-P09` PC 公开页面逐页 `snapshot_layout` 均返回 `No layout problems.`
- 2026-06-27 公开 App 化重构后：`P10-P14` 移动任务流逐页 `snapshot_layout` 均返回 `No layout problems.`
- 2026-06-27 公开 App 化重构后：截图抽查 `P03` 论坛列表、`P04` 帖子详情、`P05` 文档列表和 `P11` 移动帖子详情，未发现明显裁切、坍塌或横向溢出；帖子详情已移除测试语义，神评 / 沙发改为评论流 badge。
- 2026-06-27 公开 App 化重构后：全局 `snapshot_layout` 返回 `No layout problems.`
- 2026-06-27 公开社区特色二次精修后：`P01 / P02 / P03 / P04 / P11 / P15 / P16` 局部 `snapshot_layout` 均返回 `No layout problems.`
- 2026-06-27 公开社区特色二次精修后：PC 全部公开页 header 已同步 `F02` 84 高纸感横匾，新增“聊天室”公开导航项；修复旧页面 header 在非 flex 父级中 `fill_container` 解析为 0 宽的问题。
- 2026-06-27 公开社区特色二次精修后：新增 `P15 - Public Chat Room` 与 `P16 - Mobile Chat Reply Flow`，补齐公开聊天室、引用帖子、回复、轻回应、在线成员、移动聊天 tab 和登录发言状态。
- 2026-06-27 公开社区特色二次精修后：截图抽查 `P01` 公开首页、`P02` 发现流、`P03` 论坛列表、`P04` 帖子详情、`P11` 移动帖子详情、`P15` 公开聊天室和 `P16` 移动聊天流，未发现明显裁切、坍塌或横向溢出。
- 2026-06-27 公开社区特色二次精修后：全局 `snapshot_layout` 返回 `No layout problems.`
- 2026-06-27 reaction 与聊天方向回修后：`P03 / P04 / P15 / P16` 局部 `snapshot_layout` 均返回 `No layout problems.`
- 2026-06-27 reaction 与聊天方向回修后：截图抽查 `P03` 论坛列表、`P04` 帖子详情、`P15` 公开聊天室和 `P16` 移动聊天流，未发现明显裁切、坍塌或横向溢出；列表页已去除表情 reaction，聊天室已改为自己右侧 / 他人左侧气泡。
- 2026-06-27 reaction 与聊天方向回修后：全局 `snapshot_layout` 返回 `No layout problems.`

仓库侧：

```bash
git diff --check -- Docs/frontend/design-sources/README.md Docs/frontend/public-web-unified-experience-design.md Docs/records/p3-12-d2-public-web-unified-design-source-2026-06-24.md Docs/planning/current.md
```

结果：通过。

## 后续顺序

1. 以 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design) 和 `P01-P16` 作为公开 Web 视觉实现前口径。
2. 下一轮切换到 `private-web-workflows.pen` 后，补齐 `/workbench`、`/me` 子页、资产 / 订单、通知 / 消息 / 圈子 / 宠物、论坛作者态、文档作者态和移动私域任务流。
3. public / private 业务设计源和说明文档确认后，再进入 `radish.client` 视觉实现与 PC / mobile 复核。
4. Console 公共壳层与治理工作台代码实现按 [P3-12-D6 Console 视觉代码实现前盘点](/records/p3-12-d6-console-visual-code-prep-2026-06-27) 后移承接。

## 当前不做

- 不进入 `radish.client` 视觉代码实现。
- 不修改 Console 设计源。
- 不修改 `private-web-workflows.pen`；下一轮由用户切换到该文件后再补。
- 不把 `/desktop` 或 WebOS Dock / 窗口系统纳入公开 Web 视觉基线。
- 不把公开入口改成营销首页或品牌宣传页。

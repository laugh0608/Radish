# P3-12-B5 Web 功能总入口设计

> 日期：2026-06-22（Asia/Shanghai）
>
> 状态：设计边界已确认，待代码实现
>
> 结论：`/messages` 已是正式 Web 的消息 / 聊天入口，本批不重做聊天室；当前缺口是正式 Web 缺少清晰的功能总入口，导致用户难以判断哪些能力已迁移、哪些仍在 WebOS 历史桌面入口中。

## 背景

P3-12 已完成账户资产、商城交易、完整个人中心、论坛作者态、文档作者态和 Console 文档治理的主路径迁移。真实页面复核时确认 `/messages` 已可以作为正式 Web 消息 / 聊天入口使用，但公共壳层仍把“工作台”直接指向 `/desktop`。

这个入口语义会让用户误以为正式 Web 的功能仍集中在 WebOS 桌面里，从而找不到 `/messages`、`/me`、`/shop/orders`、`/docs/mine` 等已经完成的正式 Web 路径。

## 目标

新增正式 Web 功能总入口：

- 路由建议为 `/workbench`。
- 公共壳层中的“工作台”默认指向 `/workbench`。
- `/desktop` 保留为“桌面版 / WebOS 历史入口”，不再作为正式 Web 主动作。
- 功能总入口按用户任务展示所有主要功能，并标注公开可用、登录后可用、管理权限可用和桌面版历史入口。

## 页面职责

`/workbench` 是正式 Web 的功能地图，不是 WebOS 桌面的替代壳，也不是营销首页。

应承接：

- 公开发现：`/discover`
- 论坛阅读与作者态：`/forum`、`/forum/compose`
- 文档阅读与作者态：`/docs`、`/docs/mine`、`/docs/compose`
- 商城与交易：`/shop`、`/shop/orders`、`/shop/inventory`
- 个人中心：`/me`、`/me/content`、`/me/history`、`/me/assets`
- 消息与通知：`/messages`、`/notifications`
- 圈子与关系链：`/circle`
- 排行榜：`/leaderboard`
- 宠物：`/pet`
- Console：`/console/`
- 桌面版兼容入口：`/desktop`

不承接：

- WebOS Dock、窗口系统、桌面背景、窗口几何记忆或 `openApp` 语义。
- 页面级视觉重做；视觉体系仍等待 `P3-12-D` Pencil 设计稿。
- 后端功能可用性探测平台；首批可使用前端静态能力清单。
- Console 权限细节展开；仅提供管理入口与必要权限提示。

## 导航口径

公共壳层默认动作调整：

- “社区发现”继续指向 `/discover`。
- “我的圈子”继续指向 `/circle`。
- “工作台”改指 `/workbench`。

`/desktop` 的用户可见名称建议统一为“桌面版”或“WebOS 桌面版”，只作为功能总入口中的兼容入口出现。

私域功能不在总入口内提前模拟登录态。未登录用户点击登录后功能时，继续使用既有页面守卫和登录回流；已登录用户直接进入对应正式 Web 页面。

## 消息 / 聊天口径

`/messages` 当前视为正式 Web 消息 / 聊天入口，承接频道列表、消息历史、发送、回复、撤回、图片、成员面板、登录恢复和公开个人页来源返回等现有主路径。

不再把“聊天室是否迁移”作为进入 UI 设计专题前的阻断项。私聊、搜索、Reaction、移动系统通知和设备级会话治理仍可作为后续增强专题，但不影响 B5 的功能发现目标。

## 身份展示待讨论口径

本批代码前需继续核对 [用户身份语义与公开索引](/architecture/user-identity-semantics)：

- `PublicId` 当前形态为 `usr_...`，用于公开路由、分享、跨端回流和内部传递，不应作为普通资料页、信息页的可见身份文本。
- `DisplayHandle` 即 `DisplayName#PublicIndex`，是用户可见、可搜索、可艾特的公开身份。
- `LoginName` 在 B6 中将退场；B5 代码实现不应继续把它作为正式 Web 可见身份文本。

B5 的 `/workbench` 不直接改身份展示逻辑；身份展示修正应在代码实现前单独确认影响面。

## 验证口径

设计文档阶段：

- `git diff --check`
- `npm run check:repo-hygiene:changed`

代码实现阶段：

- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`
- 若触达公共壳层路由守卫，补对应前端单元测试或路由契约测试。

阶段验收时，在用户明确说明前后端已启动后，再覆盖 Gateway PC / mobile 页面 smoke：

- `/workbench`
- `/messages`
- `/me`
- `/shop/orders`
- `/docs/mine`
- `/desktop`

## 当前不做

- 不重做聊天室。
- 不把 `/workbench` 做成 WebOS 桌面。
- 不进入视觉美化代码。
- 不新增后端 API。
- 不启动 Flutter 承接。
- 不恢复 PR、tag 或发布流程。

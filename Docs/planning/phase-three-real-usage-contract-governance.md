# 第三开发阶段：真实使用增长与长期契约治理

> 状态：`P3-0 第三阶段定义与工程整备` 已启动
>
> 启动日期：2026-05-13（Asia/Shanghai）
>
> 本页承载第三开发阶段的目标定义、边界、首批任务候选和 `P3-0` 审计口径。快速入口仍以 [当前进行中](/planning/current) 为准；第二阶段事实以 [第二阶段收口评审](/planning/phase-two-closure-review) 与 [已完成摘要](/planning/archive) 为准。

## 阶段判断

第二开发阶段已经完成公开内容壳层、Flutter Android MVP、Tauri + WebOS 桌面壳、多端分工和产品治理收口。当前不应继续把第二阶段低收益尾项当成默认主线，也不应在缺少边界定义时直接开大功能。

第三开发阶段的核心问题不再是“某个入口能不能做出来”，而是：

1. 真实用户能否通过公开内容、移动复访和桌面工作台形成持续使用。
2. 多端之间的路由、对象标识、登录回流和公开链接契约是否足够稳定。
3. 现有代码体量、验证入口和文档入口是否还能支撑继续扩展。

因此第三阶段主题暂定为：**真实使用增长与长期契约治理**。

## 当前先做 `P3-0`

`P3-0` 是第三阶段定义与工程整备窗口，不直接铺大功能。建议周期控制在 `2-4` 个工作日。

### 目标

- 明确第三阶段第一批正式主任务。
- 做一次窄范围主路径与代码热区审计。
- 把候选任务按产品价值、契约风险、维护成本和验证成本排序。
- 给出第一批可开工任务的完成条件与验证口径。

### 不做

- 不继续追加第二阶段 Flutter 低收益微体验。
- 不继续无边界扫 WebOS / Console / 公开壳层零碎按钮、提示或样式。
- 不启动完整 `PublicId` 全量迁移。
- 不启动完整联邦、完整 PWA、完整 E2E、完整可观测性平台或开放平台。
- 不把 Tauri 签名、自动更新、SmartScreen、托盘 / 菜单和公开分发材料提前拉回主线。

## `P3-0` 审计范围

本轮只审计会影响第三阶段主线判断的范围，不做全仓 TODO 清扫。

| 范围 | 审计重点 | 产出 |
| --- | --- | --- |
| 公开内容壳层 | `/discover`、forum、docs、`/u/:id`、leaderboard、shop 的公开直达、来源返回、分享与 SEO 基础 | 判断是否进入 `P3-1 公开内容增长基础` |
| WebOS 桌面工作台 | forum、chat、shop、radish-pit、notification、profile 的已登录主路径 | 只登记成片工作流阻断，不登记低收益边角 |
| Flutter 移动端 | discover / forum / docs / profile、OIDC、handoff、复访与通知回流 | 判断是否只维护 Android MVP，或进入移动留存小闭环 |
| Console / 后端治理 | 权限、内容治理、商城、经验治理、安全授权一致性 | 只登记安全、授权或治理数据可信度问题 |
| 外部 ID 契约 | 公开路由、通知 `extData`、窗口参数、深链、分享链接和前端 `Number(...)` 边界 | 判断 `PublicId` 最小试点边界 |
| 代码热区 | 超大前端页面、超大 Service、Flutter 大页面与测试覆盖 | 排出首批拆分 / 降复杂度候选 |

## `P3-0` 初始审计快照

审计日期：2026-05-13。

本轮先按文档入口、近期提交、目录结构、测试资产和代码热区做初始审计，结论如下：

- 规划入口已经一致确认第二阶段归档 Go，当前不存在必须继续回拉第二阶段尾项的文档冲突。
- 近期提交已经从安全、商城、构建 warning、Flutter 小闭环切到第二阶段归档与下一阶段入口对齐，说明继续补洞的收益开始递减。
- 当前未发现新的资产、安全、登录、购买、转账、权限授权或主路径中断 `P0/P1` 阻断项。
- 代码中仍有 mock / TODO / 占位痕迹，例如 `radish-pit` 未调用通知 hook、通知未读按类型统计、文件访问令牌管理员权限、部分展示型 mock；这些暂归维护池，不作为第三阶段首批主线。
- 代码热区已经明显：`PublicForumApp.tsx`、`ExperienceService.cs`、`ContentModerationService.cs`、`ChatApp.tsx`、`WikiApp.tsx`、Flutter `forum_detail_page.dart` 和 `profile_page.dart` 等文件已超过或接近项目建议维护边界。
- 验证资产较完整：后端有 `Radish.Api.Tests` 与专题 `HttpTest`，前端有 `type-check / node --test`，Flutter 有 topic tests 与 `smoke_test`；第三阶段不需要先建设完整 E2E 平台才能继续推进。

初始判断：`P3-0` 的第一批开工不应从新增业务页开始，而应先把 `P3-1 / P3-2 / P3-3` 的范围、验收和风险写成可执行小批次。

## 首批候选任务

### `P3-1` 公开内容增长基础

目标：让公开内容从“可直达阅读”推进到“可传播、可索引、可回流”。

候选范围：

- sitemap / robots / canonical 口径与生成位置确认。
- forum / docs / profile / shop 的基础 `title / description / og:*` 输出策略。
- 分享卡片与公开链接复制口径复核。
- 公开内容壳层来源返回与外链打开策略复核。

边界：

- 不把公开壳层扩成工作台。
- 不开放发帖、完整评论提交、购买、订单、背包或治理动作。
- SSR / SSG 只先做方案判断，不默认立刻改构建架构。

### `P3-2` `PublicId` 最小试点方案

目标：把长期 `InternalId / PublicId / FederationId` 方向从文档原则推进到可执行试点。

候选范围：

- 选择 `Post / User / WikiDocument` 中 `1-2` 个核心对象作为试点候选。
- 定义 `PublicId` 格式、生成时机、唯一索引、DTO 暴露和兼容查询口径。
- 明确 `LongId` 字符串安全过渡期与 `PublicId` 并行期边界。

边界：

- 不迁移数据库主键。
- 不一次性修改所有 API / 前端路由。
- 不启动 ActivityPub / WebFinger 实现。

### `P3-3` 代码热区拆分与维护成本治理

目标：降低继续扩功能时的变更风险。

首批候选热区：

- `Frontend/radish.client/src/public/forum/PublicForumApp.tsx`
- `Frontend/radish.client/src/i18n.ts`
- `Radish.Service/ExperienceService.cs`
- `Radish.Service/ContentModerationService.cs`
- `Clients/radish.flutter/lib/features/forum/presentation/forum_detail_page.dart`
- `Clients/radish.flutter/lib/features/profile/presentation/profile_page.dart`

边界：

- 只做能降低真实复杂度的拆分，不做空壳抽象。
- 每次拆分必须有对应定向验证。
- 不在拆分批次里顺手改业务行为，除非是拆分暴露出的明确 bug。

### `P3-4` 用户留存轻闭环

目标：把已有通知、复访、轻互动和公开分享串成自然回流路径。

候选范围：

- 公开内容分享后进入正确壳层。
- 已登录用户从通知 / 最近阅读 / 我的轻回应回到上下文。
- 桌面工作台与 Flutter 移动端对“继续使用”的边界保持一致。

边界：

- 不做完整运营平台。
- 不做完整通知中心移动版。
- 不扩完整评论提交、点赞、投票、编辑治理或聊天移动版。

## 首批排序建议

`P3-0` 完成后，默认优先级建议为：

1. `P3-1` 公开内容增长基础。
2. `P3-2` `PublicId` 最小试点方案。
3. `P3-3` 代码热区拆分与维护成本治理。
4. `P3-4` 用户留存轻闭环。

如果 `P3-0` 审计发现资产、安全、登录、购买、转账、权限授权或主路径中断的 `P0/P1`，最多先回拉 `1-2` 个小闭环，再继续上述排序。

## `P3-0` 完成条件

- 当前阶段文档入口已经从“下一阶段主任务选择”切到 `P3-0`。
- 第三阶段目标、非目标、首批候选和排序依据已经写清楚。
- 主路径与代码热区审计形成简短结论。
- 第一批正式开工任务具备明确范围、完成条件和验证入口。
- 第二阶段维护边界仍然成立，没有被低收益尾项重新拉回。

## 验证口径

`P3-0` 以文档和审计为主，默认只需要：

```bash
npm run check:repo-hygiene:changed
```

若审计过程中改动代码，再按改动范围追加：

- 前端公开壳层：`npm run type-check --workspace=radish.client`
- Console：`npm run build --workspace=radish.console`
- 后端契约：`dotnet test Radish.Api.Tests`
- Flutter：对应 `flutter test test/<topic>_test.dart` 与 `flutter analyze`

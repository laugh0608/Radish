# 开发路线图（总览）

> 本页是路线图入口，只保留 **当前阶段、当前主线、下一顺位、并行维护线与明确后置项**。
>
> 今日推进优先看 [当前进行中](/planning/current)。历史批次、命令级验证流水和实现细节写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或专题文档。

## 当前状态

- **当前里程碑**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-6 真实使用运营观察与反馈分流` 已阶段收口，进入维护观察
- **最近结论**：
  - `2026-04-06` 完成首版真实发布 `v26.3.2-release`，第一开发阶段结束
  - `Phase 2-2 移动 Web 形态` 已完成公开内容壳层首批收口，转入稳定维护
  - `Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论
  - Capacitor Android 终止，不进入移动端产品化主线
  - Tauri + WebOS 桌面安装包个人开发阶段验证通过
  - WebOS 桌面工作台已回到产品功能开发推进，并落地首批“继续使用”复访入口、桌面应用恢复入口与萝卜坑工作流补全
  - Console 治理已完成用户详情、个人资料 / 设置真实化与仪表盘真实统计首轮补洞
  - `ID Phase A` 已进入前期治理面：先冻结外部 `long` 扩散，按 `LongId` / 字符串安全收口窗口参数、通知跳转、公开路由与 `Profile / Shop / Wiki / Forum` 边界，不启动数据库主键迁移或全量 `PublicId` 切换
  - `2026-05-13` 第二阶段收口评审窄范围筛查未发现新的必须立即闭环 `P0/P1` 阻断项，`P2-C5-A` 已完成 Flutter 通知回流与个人复访轻操作首批补强，第二阶段归档判断 Go
  - `2026-05-13` 已启动 `P3-0 第三阶段定义与工程整备`，第三阶段主题暂定为“真实使用增长与长期契约治理”，并完成 `P3-0-A` 公开内容增长基础审计
  - `P3-1` 第一批已完成：公开 head / canonical / robots / sitemap seed 基线，以及 forum detail / shop detail 复制 canonical 链接入口均已落地
  - `P3-2 PublicId 最小试点方案` 已完成首批 `Post.PublicId` 实现：新帖生成 `pst_` + UUIDv7 编码体，forum 公开 canonical、分享、通知 `extData`、浏览历史 routePath 与窗口参数已并行支持 `postPublicId`，不启动全量迁移
  - `P3-3` 已完成 `PublicForumApp.tsx` 公开论坛热区首轮低风险拆分和收工复核：helper、状态卡、类型流、搜索页、标签页、列表页与详情页均已抽出，主文件降到约 `208` 行，定向验证通过
  - `P3-4` forum / docs / shop 留存回流矩阵已完成一轮主动验收：forum 公开分享、通知、最近阅读、我的轻回应、公开个人页双向复访已保持 PublicId 优先；docs / shop 旧 long 路径只保留回流 fallback，不在最近阅读或公开 head 普通文案外露
  - `P3-5-A` 已完成公开内容增长后续专题评估：下一批建议先做运行时结构化数据基线；动态 sitemap 与详情首包 HTML 可见性继续单独方案评审
  - `P3-5-B` 已完成运行时结构化数据基线：forum / docs / shop / 公开个人页详情在前端运行时输出 JSON-LD，旧结构化数据会在路由切换或卸载时清理
  - `P3-5` 已完成动态 sitemap、详情首包 head snapshot 注入和公开 head smoke 入口，公开内容增长后续专题阶段收尾
  - `P3-6-A/B/C` 已完成公开增长观察收口：本地 Gateway 与生产公开域名 `https://radishx.com` public head smoke 均通过，sitemap index / 分片 `<loc>` origin 检查已纳入 smoke 自动验证，未发现新的公开访问 `P0/P1`

## 当前主线入口

- [当前进行中](/planning/current)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [第二阶段收口评审](/planning/phase-two-closure-review)
- [第二阶段产品功能补全规划](/planning/phase-two-product-completion)
- [前端多壳层策略](/frontend/shell-strategy)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [验证基线说明](/guide/validation-baseline)

## 当前开发精力

- `45%`：阶段收口后的真实使用维护观察，只处理公开访问、爬虫抓取、分享预览、用户回流或运行日志暴露的高信号问题
- `20%`：公开 head / sitemap / snapshot 缓存维护，只处理真实暴露的抓取、分享或配置问题
- `15%`：`P3-2 / P3-4` PublicId 与留存回流兼容观察，只处理真实暴露的回流断点，不扩全量迁移
- `10%`：`P3-3` 代码热区治理后续观察，暂不继续深拆 `PublicForumDetail` 内部结构
- `10%`：第二阶段收口护栏，只回拉新发现的 `P0/P1` 阻断项

## 已确认的多端方向

1. **Web 浏览器**
   - 使用公开内容壳层，覆盖 PC 浏览器与移动浏览器
   - 重点是公开阅读、分享、SEO、轻互动和低门槛访问
2. **Android / iOS**
   - 使用 Flutter 移动原生安装包路线
   - Android MVP 已完成第一轮；iOS 后续单独评估
   - 不使用 Capacitor 作为登录态移动端产品化路线
3. **Windows / macOS / Linux**
   - 使用 `Tauri 壳 + WebOS 桌面工作台`
   - Tauri 承接系统窗口、登录回跳、deep link 兼容和安装包能力
   - WebOS 继续承接 Dock、窗口系统、多应用容器和桌面业务体验

## 下一顺位

- `P3-6`：已阶段收口；后续只维护公开 head smoke、动态 sitemap、head snapshot 和真实反馈分流，不再作为新的功能池扩张
- `P3-5`：公开内容增长后续专题已阶段收尾，后续只维护运行时 JSON-LD、动态 sitemap、详情首包 head snapshot 和 smoke 入口
- `P3-4`：forum / docs / shop 留存回流矩阵已完成一轮主动验收和收尾判断，后续只从真实使用暴露的公开分享 / 复访断点中选择小闭环
- `P3-3`：首轮拆分与验证记录已收口，不继续无边界深拆详情页内部结构
- `P3-2` 后续只做兼容观察、定向回归或历史 `Post.PublicId` 补齐策略评估，不扩成全量迁移
- 详情首包 HTML 可见性保持窄实现，不在未出现真实证据前直接启动完整 SSR / SSG、正文预渲染或更广泛 Gateway HTML rewrite
- Flutter 移动端：`P2-C5-A` 首批已完成，不默认追加低收益体验批次
- WebOS / PC 工作台：只处理用户主路径中的阻断级缺口，萝卜坑交易导出已补齐，后续不再无限扫零碎入口
- 后端 + Console 治理：经验治理、内容治理、商城管理验收、安全治理尾项和 Console 权限种子一致性转入稳定维护，只处理新暴露的安全 / 授权一致性问题
- Console UI 一致性治理：后续回拉 Console 扩展时，优先复用 `@radish/ui` 组件、交互反馈与主题 token，逐步替换历史自定义样式和重复组件
- Flutter 移动端：聚焦高价值移动已登录链路，不默认追加低增益体验批次
- 公开内容壳层：只做稳定维护和真实问题修复，不继续扩公开入口细节
- Tauri 桌面安装包：签名、自动更新、生产 Auth、SmartScreen、托盘 / 菜单和公开分发方式后置到真实对外分发前

## 长期方向与当前衔接

- 标识体系升级：`InternalId / PublicId / FederationId` 分层，`PublicId` 长期优先 `UUIDv7`
- `P3-0` 只允许形成 `PublicId` 最小试点方案，不启动数据库主键迁移或全量外部契约切换
- 社区联邦化：公开社区对象优先按 `ActivityPub + WebFinger` 方向预留
- 租户语义调整：长期产品语义转向 `instance / node / space / group / category`
- 详细方案见：[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)

## 并行维护

- `M14` 宿主运行与最小可观测性基线
- `M15` 最小交付与部署基线
- 发布记录、回滚预案、回归留痕与 `validate:baseline / validate:baseline:host / validate:ci`
- 桌面壳层、窗口几何记忆、主题切换、聊天室 `P1`、通知中心、商城等既有能力稳定维护

## 明确后置

- 第三开发阶段大功能开发：`P3-2 PublicId` 最小试点已完成首批 `Post` 实现，后续仍不得直接启动全量迁移
- 完整 `PublicId` 全量迁移、数据库主键迁移与 ActivityPub / WebFinger 实现
- `Gateway & BFF` 深化
- Redis 与缓存治理专题：多实例 SignalR Backplane、聊天室在线状态、通知未读原子计数、上传限流、商城 / 萝卜币幂等与并发保护、排行榜 / 热点读模型缓存，详见 [Redis 与缓存治理专题](/planning/redis-cache-governance)
- `Console-ext Phase 2+`
- Console 复用 `@radish/ui` 的组件与视觉 token，保持后台与 Radish 前端入口的 UI 一致性
- 开放平台第三方接入 / SDK
- 邮件通知系统
- 完整 `PWA / Service Worker / 离线能力`
- 完整 Playwright / E2E 平台
- 完整可观测性平台、Tracing / Metrics 大阶段

## 阶段文档规则

- `Docs/index.md`、`Docs/README.md`、`Docs/development-plan.md`、`Docs/planning/current.md` 等关键入口只描述最近阶段和进度，不承载长背景
- 功能批次、验证命令、人工验收记录和历史事实默认写入 `Docs/changelog/`、`Docs/planning/archive.md` 或对应专题文档
- 判断阶段定义时，以本页、[当前进行中](/planning/current)、[第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)、[前端多壳层策略](/frontend/shell-strategy) 与 [已完成摘要](/planning/archive) 为准

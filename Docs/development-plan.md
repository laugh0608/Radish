# 开发路线图（总览）

> 本页是路线图入口，只保留 **当前阶段、当前主线、下一顺位、并行维护线与明确后置项**。
>
> 今日推进优先看 [当前进行中](/planning/current)。历史批次、命令级验证流水和实现细节写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或专题文档。

## 当前状态

- **当前里程碑**：`第三开发阶段定义与工程整备`
- **当前主线**：`P3-0 第三阶段定义与工程整备`
- **最近结论**：
  - `2026-04-06` 完成首版真实发布 `v26.3.2-release`，第一开发阶段结束
  - `Phase 2-2 移动 Web 形态` 已完成公开内容壳层首批收口，转入稳定维护
  - `Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论
  - Capacitor Android 终止，不进入移动端产品化主线
  - Tauri + WebOS 桌面安装包个人开发阶段验证通过
  - WebOS 桌面工作台已回到产品功能开发推进，并落地首批“继续使用”复访入口、桌面应用恢复入口与萝卜坑工作流补全
  - Console 治理已完成用户详情、个人资料 / 设置真实化与仪表盘真实统计首轮补洞
  - `ID Phase A` 已进入当前治理面：先冻结外部 `long` 扩散，按 `LongId` / 字符串安全收口窗口参数、通知跳转、公开路由与 `Profile / Shop / Wiki / Forum` 边界，不启动数据库主键迁移或全量 `PublicId` 切换
  - `2026-05-13` 第二阶段收口评审窄范围筛查未发现新的必须立即闭环 `P0/P1` 阻断项，`P2-C5-A` 已完成 Flutter 通知回流与个人复访轻操作首批补强，第二阶段归档判断 Go
  - `2026-05-13` 已启动 `P3-0 第三阶段定义与工程整备`，第三阶段主题暂定为“真实使用增长与长期契约治理”，先做目标、边界、审计和第一批任务排序

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

- `45%`：`P3-0` 第三阶段定义与工程整备，完成目标、边界、审计和第一批任务排序
- `20%`：公开内容增长基础预研，聚焦 SEO、分享、canonical、sitemap 与公开回流口径
- `15%`：外部 ID 契约与 `PublicId` 最小试点方案，继续保持 `LongId` 字符串安全过渡边界
- `10%`：代码热区拆分候选评估，优先看超大公开页面、超大 Service 与 Flutter 大页面
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

- `P3-0`：完成第三阶段定义与工程整备，产出第一批正式开工任务
- 第一批候选顺序：公开内容增长基础、`PublicId` 最小试点方案、代码热区拆分、用户留存轻闭环
- Flutter 移动端：`P2-C5-A` 首批已完成，不默认追加低收益体验批次
- WebOS / PC 工作台：只处理用户主路径中的阻断级缺口，萝卜坑交易导出已补齐，后续不再无限扫零碎入口
- 后端 + Console 治理：经验治理、内容治理、商城管理验收、安全治理尾项和 Console 权限种子一致性转入稳定维护，只处理新暴露的安全 / 授权一致性问题
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

- 第三开发阶段大功能开发：需先完成 `P3-0` 审计、第一批任务排序和验证口径定义
- 完整 `PublicId` 全量迁移、数据库主键迁移与 ActivityPub / WebFinger 实现
- `Gateway & BFF` 深化
- `Console-ext Phase 2+`
- 开放平台第三方接入 / SDK
- 邮件通知系统
- 完整 `PWA / Service Worker / 离线能力`
- 完整 Playwright / E2E 平台
- 完整可观测性平台、Tracing / Metrics 大阶段

## 阶段文档规则

- `Docs/index.md`、`Docs/README.md`、`Docs/development-plan.md`、`Docs/planning/current.md` 等关键入口只描述最近阶段和进度，不承载长背景
- 功能批次、验证命令、人工验收记录和历史事实默认写入 `Docs/changelog/`、`Docs/planning/archive.md` 或对应专题文档
- 判断阶段定义时，以本页、[当前进行中](/planning/current)、[第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)、[前端多壳层策略](/frontend/shell-strategy) 与 [已完成摘要](/planning/archive) 为准

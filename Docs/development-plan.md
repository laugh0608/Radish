# 开发路线图（总览）

> 本页是路线图入口，只保留 **当前阶段、当前主线、下一顺位、并行维护线与明确后置项**。
>
> 今日推进优先看 [当前进行中](/planning/current)。历史批次、命令级验证流水和实现细节写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或专题文档。

## 当前状态

- **当前里程碑**：`第二开发阶段：社区深化与多端化`
- **当前主线**：`多端路线收口后的产品功能开发推进`
- **最近结论**：
  - `2026-04-06` 完成首版真实发布 `v26.3.2-release`，第一开发阶段结束
  - `Phase 2-2 移动 Web 形态` 已完成公开内容壳层首批收口，转入稳定维护
  - `Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论
  - Capacitor Android 终止，不进入移动端产品化主线
  - Tauri + WebOS 桌面安装包个人开发阶段验证通过
  - WebOS 桌面工作台已回到产品功能开发推进，并落地首批“继续使用”复访入口

## 当前主线入口

- [当前进行中](/planning/current)
- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [前端多壳层策略](/frontend/shell-strategy)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [验证基线说明](/guide/validation-baseline)

## 当前开发精力

- `35%`：产品功能开发推进，优先 WebOS 桌面工作台、已登录复访、启动续接、高价值应用回流
- `20%`：Android MVP 产品化深化，重点是测试对象、反馈回收、已知问题列表、版本说明、release 前验收与分发留痕
- `20%`：公开内容壳层稳定维护，处理 PC / 移动浏览器响应式、公开阅读质量、来源返回与分享链路问题
- `10%`：社区深化维护，只处理已落地轻回应、通知回流、论坛阅读链路里的真实问题
- `10%`：宿主运行、验证基线、文档同步、发布 / 回滚 / 可观测性维护线
- `5%`：Tauri + WebOS 桌面安装包维护观察；正式分发事项只在真实对外分发前重新评估

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

- WebOS 桌面工作台：继续优先已登录复访、工作台启动效率、高价值应用回流
- Android 产品化：优先测试分发、反馈闭环、版本说明和 release 前验收
- 公开内容壳层：只做稳定维护和真实问题修复，不继续扩公开入口细节
- Tauri 桌面安装包：签名、自动更新、生产 Auth、SmartScreen、托盘 / 菜单和公开分发方式后置到真实对外分发前

## 长期方向（暂不进入当前主线）

- 标识体系升级：`InternalId / PublicId / FederationId` 分层，`PublicId` 长期优先 `UUIDv7`
- 社区联邦化：公开社区对象优先按 `ActivityPub + WebFinger` 方向预留
- 租户语义调整：长期产品语义转向 `instance / node / space / group / category`
- 详细方案见：[标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)

## 并行维护

- `M14` 宿主运行与最小可观测性基线
- `M15` 最小交付与部署基线
- 发布记录、回滚预案、回归留痕与 `validate:baseline / validate:baseline:host / validate:ci`
- 桌面壳层、窗口几何记忆、主题切换、聊天室 `P1`、通知中心、商城等既有能力稳定维护

## 明确后置

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
- 判断阶段定义时，以本页、[当前进行中](/planning/current)、[第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)、[前端多壳层策略](/frontend/shell-strategy) 与 [已完成摘要](/planning/archive) 为准

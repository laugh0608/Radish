# 开发路线图（总览）

> 本页是路线图入口，只保留 **当前阶段、当前主线、下一顺位、并行维护线与明确后置项**。
>
> 今日推进优先看 [当前进行中](/planning/current)。历史批次、命令级验证流水和实现细节写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或专题文档。

## 当前状态

- **当前里程碑**：`第二开发阶段：社区深化与多端化`
- **当前主线**：`第二阶段收口评审与下一主任务选择`
- **最近结论**：
  - `2026-04-06` 完成首版真实发布 `v26.3.2-release`，第一开发阶段结束
  - `Phase 2-2 移动 Web 形态` 已完成公开内容壳层首批收口，转入稳定维护
  - `Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论
  - Capacitor Android 终止，不进入移动端产品化主线
  - Tauri + WebOS 桌面安装包个人开发阶段验证通过
  - WebOS 桌面工作台已回到产品功能开发推进，并落地首批“继续使用”复访入口、桌面应用恢复入口与萝卜坑工作流补全
  - Console 治理已完成用户详情、个人资料 / 设置真实化与仪表盘真实统计首轮补洞
  - `ID Phase A` 已进入当前治理面：先冻结外部 `long` 扩散，按 `LongId` / 字符串安全收口窗口参数、通知跳转、公开路由与 `Profile / Shop / Wiki / Forum` 边界，不启动数据库主键迁移或全量 `PublicId` 切换
  - 当前主线已从“产品功能补全与多端任务重排”进入“第二阶段收口评审与下一主任务选择”：不直接宣布进入第三阶段，也不继续无限补零碎尾项；下一主任务优先评估 Flutter 移动端高价值已登录链路

## 当前主线入口

- [当前进行中](/planning/current)
- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [第二阶段产品功能补全规划](/planning/phase-two-product-completion)
- [前端多壳层策略](/frontend/shell-strategy)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [验证基线说明](/guide/validation-baseline)

## 当前开发精力

- `40%`：第二阶段收口评审，确认已完成、转维护、后置与仍需闭环的 `P0/P1` 项
- `25%`：WebOS / PC 工作台阻断级工作流缺口，只做成片主路径，不继续扫低收益边角
- `20%`：Flutter 移动端高价值已登录链路，作为下一主任务优先候选
- `10%`：公开内容壳层稳定维护，处理真实阅读、分享、响应式和来源返回问题
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

- 第二阶段收口评审：先形成阶段收口清单和下一主任务判断，不把“继续找尾项”作为默认主线
- WebOS / PC 工作台：只处理用户主路径中的阻断级缺口，萝卜坑交易导出已补齐，后续不再无限扫零碎入口
- 后端 + Console 治理：经验治理、内容治理、商城管理验收、安全治理尾项和 Console 权限种子一致性转入稳定维护，只处理新暴露的安全 / 授权一致性问题
- Flutter 移动端：优先作为下一主任务候选，聚焦高价值移动已登录链路，不默认追加低增益体验批次
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

- 第三开发阶段正式启动：需先完成第二阶段收口清单、下一主任务选择与阶段文档归档
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

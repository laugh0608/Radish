# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第二开发阶段：社区深化与多端化`
- **当前主线**：`多端路线收口后的产品功能开发推进`
- **复核日期**：`2026-05-05`
- **最近结论**：
  - `v26.3.2-release` 已于 `2026-04-06` 完成首版真实发布，第一开发阶段结束
  - `Phase 2-2 移动 Web 形态` 已完成 forum / docs / `u/:id` / leaderboard / shop / discover 公开内容壳层首批收口，转入稳定维护
  - `Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论
  - Capacitor Android 因登录 / OIDC 与本机调试复杂度终止，不进入移动端产品化主线
  - Tauri 桌面安装包个人开发阶段验证通过，定位为 `Tauri 壳 + WebOS 桌面工作台`
  - WebOS 桌面工作台已回到产品功能开发推进，并落地首批“继续使用”复访入口

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [前端多壳层策略](/frontend/shell-strategy)
- [Flutter Android MVP RC 验收记录（2026-05-04）](/guide/flutter-android-mvp-rc-acceptance-record-2026-05-04)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **产品功能开发推进**
   - 优先围绕 WebOS 桌面工作台、已登录复访、启动续接和高价值应用回流做小闭环
   - 首批“继续使用”入口已落地最近应用、最近浏览与我的轻回应分组
   - 当前不扩完整历史中心、清空 / 删除、跨端同步或新的后端 API
2. **Android MVP 完成后的产品化整理**
   - Android MVP 第一轮已完成，后续重点是测试对象、反馈回收、已知问题列表、版本说明、release 前验收与分发留痕
   - 不默认追加第 `24` 批及以后低增益 Flutter 微体验修补
3. **多端方向保持收束**
   - Web 浏览器：公开内容壳层
   - Android / iOS：Flutter 移动原生安装包路线
   - Windows / macOS / Linux：`Tauri 壳 + WebOS 桌面工作台`

## 下一顺位

- WebOS 桌面工作台继续推进时，优先选择已登录复访、工作台启动效率和高价值应用回流
- Android 深化若进入执行，优先补测试分发和反馈闭环，不默认扩完整通知中心、系统推送、发帖、完整评论提交、点赞、投票或编辑治理
- Tauri 桌面安装包正式签名、自动更新、生产 Auth、SmartScreen、托盘 / 菜单与公开分发方式，仅在真实对外分发前重新评估
- 公开内容壳层只做必要稳定维护和问题修复，不继续新增公开入口或细节增强

## 并行维护项

- `M14` 宿主运行与最小可观测性基线
- `M15` 最小交付与部署基线
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`
- 桌面壳层、窗口几何记忆、主题切换、聊天室 `P1`、通知中心、商城等既有能力稳定维护

## 当前不做

- 继续沿 `Phase 2-1` 扩张论坛回跳、轻回应或通知尾项
- 未经重新评估继续追加低增益 Flutter 微体验批次
- 未经单独评估启动 Flutter iOS 产品化工程
- 把 Windows / macOS / Linux 作为 Flutter 默认扩平台方向
- 继续扩大 Capacitor Android 登录态或移动端产品化能力
- 把 Tauri 当作原生 UI 重写路线
- “移动版 WebOS”
- 完整 `PWA / 离线能力 / 推送闭环`
- 完整 Playwright / E2E 平台
- 完整可观测性平台或大而全运维平台

## 文档更新规则

- 本页必须保持简短，面向新会话快速读取
- 没有主线切换、优先级变化或新的关键事实，不改本页
- 功能开发细节、命令级验证记录、批次流水和历史背景不写入本页
- 需要记录历史时，写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或对应专题文档
- 若移动 Web、Flutter 移动端或 Tauri + WebOS 桌面端成为正式主线，必须同步：
  - [开发路线图](/development-plan)
  - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
  - [当前进行中](/planning/current)
  - [前端多壳层策略](/frontend/shell-strategy)

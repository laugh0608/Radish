# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第二开发阶段：社区深化与多端化`
- **当前主线**：`多端路线收口后的产品功能开发推进`
- **复核日期**：`2026-05-06`
- **最近结论**：
  - `v26.3.2-release` 已于 `2026-04-06` 完成首版真实发布，第一开发阶段结束
  - `Phase 2-2 移动 Web 形态` 已完成 forum / docs / `u/:id` / leaderboard / shop / discover 公开内容壳层首批收口，转入稳定维护
  - `Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论
  - Capacitor Android 因登录 / OIDC 与本机调试复杂度终止，不进入移动端产品化主线
  - Tauri 桌面安装包个人开发阶段验证通过，定位为 `Tauri 壳 + WebOS 桌面工作台`
  - WebOS 桌面工作台已回到产品功能开发推进，并落地首批“继续使用”复访入口、桌面应用恢复入口与萝卜坑工作流补全
  - Console 治理已完成用户详情、个人资料 / 设置真实化与仪表盘真实统计首轮补洞
  - 当前主线进一步收束为“产品功能补全与多端任务重排”，优先补齐首版后遗留的真实功能坑、治理能力和体验断点

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [第二阶段产品功能补全规划](/planning/phase-two-product-completion)
- [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [前端多壳层策略](/frontend/shell-strategy)
- [Flutter Android MVP RC 验收记录（2026-05-04）](/guide/flutter-android-mvp-rc-acceptance-record-2026-05-04)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **产品功能开发推进**
   - 当前优先补齐 WebOS / PC 工作台、后端与 Console 治理中的真实产品功能缺口
   - 近期优先从萝卜坑工作流、Console 治理、经验 / 等级治理、商城权益效果和移动端高价值已登录链路中选批次
   - 不再继续围绕复访入口做低收益微调
2. **多端任务重新分配**
   - WebOS / PC 工作台：`45%`
   - 后端 + Console 治理：`25%`
   - Flutter 移动端：`15%`
   - 公开 Web 壳层：`10%`
   - Tauri 桌面壳：`5%`
3. **多端方向保持收束**
   - Web 浏览器：公开内容壳层
   - Android / iOS：Flutter 移动原生安装包路线
   - Windows / macOS / Linux：`Tauri 壳 + WebOS 桌面工作台`

## 下一顺位

- WebOS / PC 工作台继续推进时，优先补齐用户能点到但不能真正完成的功能，避免继续围绕已落地复访入口做低收益微调
- 后端 + Console 治理优先处理安全 / 经验治理、商城管理前端缺口和剩余历史构建警告
- Flutter 深化若进入执行，优先补高价值移动已登录链路，不默认转向分发产品化材料或低增益微体验
- Tauri 桌面安装包正式签名、自动更新、生产 Auth、SmartScreen、托盘 / 菜单与公开分发方式，仅在真实对外分发前重新评估
- 公开内容壳层只做必要稳定维护和问题修复，不继续新增公开入口或细节增强

## 并行维护项

- `M14` 宿主运行与最小可观测性基线
- `M15` 最小交付与部署基线
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`
- 桌面壳层、窗口几何记忆、主题切换、聊天室 `P1`、通知中心、商城等既有能力稳定维护

## 当前不做

- 继续沿 `Phase 2-1` 扩张论坛回跳、轻回应或通知尾项
- 继续围绕 WebOS 复访入口做低收益微调
- 在产品功能缺口未重新排序前优先推进 Android / Tauri 分发产品化材料
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

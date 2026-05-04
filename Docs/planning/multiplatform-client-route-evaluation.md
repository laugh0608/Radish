# 多端客户端路线评估方案

> 状态：当前评估入口
>
> 最后更新：2026-05-04（Asia/Shanghai）
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [开发路线图](/development-plan)
> - [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [Flutter Android MVP RC 验收记录（2026-05-04）](/guide/flutter-android-mvp-rc-acceptance-record-2026-05-04)

## 1. 当前结论

当前不立刻推翻 Flutter，也不继续扩大 Flutter。

`Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论，说明 Flutter 线已经能稳定交付一条 Android 原生客户端 MVP。但这不自动等于“长期多端都必须继续用 Flutter”。

下一步固定为一次低成本路线评估：

1. Flutter 保持 Android MVP 已完成状态，不主动开启第 `24` 批体验微调
2. 暂时冻结 Flutter iOS、Windows、macOS、Linux 扩平台决策
3. 用 `2-3` 天做 React 复用路线 spike，重点验证 `Capacitor + Tauri` 是否明显降低长期多端成本

## 2. 已完成前置

Flutter Android MVP RC 已完成：

- RC Gateway：`https://radishx.com`
- APK 类型：release APK
- 设备：小米 15S Pro
- Android 版本：16
- 测试账号：`test`
- 命令级验证：`flutter analyze`、`flutter test`、`smoke_test`、Android JVM 单测、签名检查、release APK 构建与 `git diff --check` 均通过
- 真机复核：登录、退出、会话恢复、`discover / forum / docs / profile`、forum detail、docs 搜索 / 内链、profile 复访、轻回应发布与最小 forum notification 回流均未发现 `P0 / P1`

因此，React spike 的目的不是补救 Flutter 失败，而是判断长期多端路线是否应优先复用现有 React Web 资产。

## 3. 冻结边界

评估期间固定不做：

1. 不启动 Flutter iOS / Windows / macOS / Linux 产品化工程
2. 不继续追加 Flutter 第 `24` 批及以后低增益体验微调
3. 不把 React spike 做成完整产品重写
4. 不改后端 API、认证协议或 Gateway 架构来迁就某个壳层
5. 不推翻 WebOS 桌面工作台、公开内容壳层或 Flutter Android MVP 已完成事实

## 4. React 复用路线 spike

### 4.1 目标

用最小样例验证现有 React Web 资产是否能低成本进入移动 App 壳与桌面原生壳。

推荐评估组合：

- 移动端：`Capacitor`
- 桌面端：`Tauri`

本轮只做技术风险验证，不做正式产品交付。

### 4.2 验证问题

1. 现有 `React Web` 页面能否以较低改造成本跑在移动 App 壳里
2. `@radish/http`、登录态、Gateway 配置、环境变量访问方式能否复用
3. Android / iOS 的深链、登录回调、文件、分享、通知能力接入成本
4. Windows / macOS 的窗口、托盘、自动更新、文件系统能力接入成本
5. 打包体积、启动速度、调试体验与发布链路是否明显优于或劣于 Flutter 路线

### 4.3 最小样例范围

移动 App 壳只验证：

1. 加载一个现有公开内容入口，例如 forum 列表或 docs 列表
2. 复用 `@radish/http` 发起 Gateway 请求
3. 验证登录回调或深链入口的最小闭环
4. 记录 Android 包体、启动时间、调试体验与构建复杂度

桌面原生壳只验证：

1. 加载一个现有桌面或公开内容入口
2. 验证窗口生命周期、托盘或文件系统能力中至少一项
3. 记录 Windows 构建产物、启动速度、自动更新方案可行性与调试体验

## 5. 评估输出

React spike 完成后至少产出：

1. 一份 spike 记录，包含命令、环境、样例路径、结论与截图或关键输出摘要
2. 一张 Flutter 与 React 复用路线对比表
3. 一份最终路线建议：继续 Flutter、Flutter 仅保留 Android、或转向 React 复用路线

对比维度固定包括：

| 维度 | Flutter Android MVP | Capacitor 移动壳 | Tauri 桌面壳 |
| --- | --- | --- | --- |
| 现有业务复用 | 已通过原生重写方式验证 | 待 spike | 待 spike |
| `@radish/http` 复用 | 不能直接复用 TypeScript 客户端 | 待 spike | 待 spike |
| 登录 / 深链 | Android 已跑通 | 待 spike | 待 spike |
| 原生能力 | Android 已跑通最小链路 | 待 spike | 待 spike |
| 包体 / 启动 | 已有 release APK 可测 | 待 spike | 待 spike |
| 调试体验 | 已有 Flutter 工具链 | 待 spike | 待 spike |
| 长期维护成本 | 需要维护 Dart 原生页面 | 待 spike | 待 spike |

## 6. 决策门槛

最终按以下规则判断：

1. **Flutter RC 通过，维护成本可接受，React spike 不明显更优**
   - 继续 Flutter 做 Android
   - iOS 后续单独谨慎评估
2. **Flutter RC 通过，但 React spike 明显低成本复用**
   - Flutter 停在 Android MVP
   - 长期多端转向 `Capacitor + Tauri`
3. **Flutter RC 不通过，且问题来自 Flutter 技术线复杂度**
   - 停止继续扩大 Flutter
   - 转 React 复用路线
4. **Flutter RC 不通过，但只是业务 bug 或环境问题**
   - 修完再判断
   - 不把普通缺陷误判为选型失败

当前实际状态已落在“Flutter RC 通过”。因此接下来只需要区分 React spike 是否明显更优。

## 7. 建议执行顺序

1. 新建 React 复用 spike 分支或临时目录，避免污染正式产品目录
2. 先验证 Capacitor 移动壳，优先跑通一个公开内容读取入口
3. 再验证 Tauri 桌面壳，优先跑通窗口生命周期和一个现有入口
4. 整理对比表与结论
5. 回到 [当前进行中](/planning/current) 更新下一阶段主线

## 8. 当前不做

- 不把 Flutter Android MVP 回滚或废弃
- 不立即生成 iOS / Windows / macOS / Linux Flutter 平台工程
- 不把 React spike 扩成完整移动端重构
- 不提前承诺 `Capacitor + Tauri` 一定成为最终路线
- 不在路线评估期间扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票或编辑治理

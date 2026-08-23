# Flutter Native P4-B2 Web-Family Adaptive Shell 实现记录

> 状态：`P4-B2` 已完成；后续 `P4-B3 Discover` 已完成，当前等待 `P4-B4 Forum Detail` 实施授权
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置实现：[P4-B1 Theme / Shared 实现记录](/records/f4-flutter-native-p4b1-theme-shared-implementation-2026-08-23)

## 1. 本批结论

P4-B2 已把 Flutter 的默认 Material 导航改造成与正式 Web 同属一个视觉家族的三档自适应壳层，同时保持原生安全区、触控、键鼠、系统返回和窗口适配边界。现有五个真实主入口、`IndexedStack` 页面状态、Android Back、OIDC 回流、通知、主题、最近阅读和来源返回均继续复用；本批没有扩建 Flutter Chat、Discover 读模型、Forum Detail 布局、平台工程或后端接口。

主要结果：

1. compact 使用 `64px` 品牌栏和安全区内 `358 × 64px` 悬浮胶囊底栏；五个入口保持 `52px` 视觉高度、`18px` Lucide 图标、`11px` 标签与不低于 `48px` 的触控目标。
2. medium / expanded 统一使用 `68px` 顶部全局栏，不再使用全局 `NavigationRail`；expanded 显示带文字的五入口导航，medium 使用紧凑图标入口，为后续页面级双栏保留空间。
3. 常驻环境 / 会话 chip 带已删除；通知、最近阅读、主题和账户进入对应头部动作，登录恢复问题进入任务上下文提示。
4. 通知状态、菜单、Bottom Sheet、列表项和标记已读反馈已从 Shell owner 拆出；账户 / 最近阅读动作也形成独立壳层表面。
5. `radish_flutter_shell.dart` 由 `1920` 行降至 `1445` 行，回到仓库 `1500` 行硬上限内。

## 2. 三档壳层契约

| 窗口等级 | 全局导航 | 页面区域 | 交互边界 |
| --- | --- | --- | --- |
| compact `< 600` | 顶部品牌 / 动作栏 + 底部悬浮胶囊五入口 | 单任务内容面，底栏自行承接底部安全区 | 键盘出现时隐藏底栏；系统返回、触控目标和 reduced-motion 保持原生行为 |
| medium `600–1023` | `68px` 顶部栏、紧凑五入口，无全局 rail | 为后续页面级列表—详情或上下文栏保留宽度 | Tab / 焦点顺序和快捷键继续生效 |
| expanded `>= 1024` | `68px` 顶部品牌、五入口文字导航与操作区 | 全局壳层不提供侧栏，双栏 / 三栏由具体页面 owner 决定 | 导航按内容宽度布局，剩余宽度交给弹性空白，避免中等桌面窗口拥挤 |

顶部和底部壳层只消费 `RadishThemeTokens`、`RadishDensity`、`RadishMotion` 与 `RadishIcons` 受控映射，没有复制 Web DOM / CSS，也没有按主题 ID 分叉组件。

## 3. 状态与行为保持

- 五个真实 destination 仍为“发现 / 论坛 / 文档 / 榜单 / 我的”，未为视觉对齐伪造聊天入口。
- Shell 继续使用 `IndexedStack` 保存页面状态，`Ctrl / Cmd + 1..5` 继续切换入口。
- Android 根返回、详情来源返回、Profile / Docs / Forum handoff、登录回流和草稿恢复沿用既有 owner。
- 已登录通知继续支持 loading / available / empty / error / stale、手动刷新、列表打开、标记已读和失败提示；游客通知动作明确禁用并说明登录边界。
- expanded 将主题与最近阅读作为独立动作；compact / medium 将低频动作收进账户菜单，避免压缩主导航。
- reduced-motion 下底部栏切换时长归零；软键盘占用底部 inset 时不在内容上方保留悬浮导航。

## 4. 职责拆分

| owner | 职责 |
| --- | --- |
| `radish_adaptive_navigation.dart` | 断点解析后的三档全局壳层、品牌、五入口、快捷键、焦点和 compact 安全区底栏 |
| `radish_notification_surface.dart` | 通知动作状态、菜单、通知 Bottom Sheet、列表项和标记已读反馈 |
| `radish_shell_actions.dart` | 账户、主题入口、登录 / 退出与最近阅读动作 |
| `radish_flutter_shell.dart` | 业务页面编排、会话 / OIDC、handoff 和各动作回调，不再拥有通知组件实现 |

该拆分没有把会话、通知或主题状态复制到新组件；新表面只接收 Shell 提供的权威状态和回调。

## 5. 验证结果

| 门禁 | 结果 |
| --- | --- |
| `dart format` | 通过，目标 Dart 文件无格式漂移 |
| 自适应导航定向测试 | `8 / 8` 通过；覆盖 `390 / 800 / 1440`、三档 header、无全局 rail、`358 × 64` 胶囊、安全区、键盘、reduced-motion、焦点、快捷键和五入口操作 |
| Shell Smoke | `51 / 51` 通过；覆盖会话恢复、OIDC、Android Back、五入口 handoff、通知、最近阅读和来源返回 |
| `flutter analyze` | 零问题 |
| `flutter test` | `236 / 236` 通过；相较 P4-B1 基线新增 `3` 个用例 |
| `git diff --check` | 通过 |

本批门禁是静态和 Widget 层验证；未启动 API / Auth / Gateway 或 Flutter 应用，因此不把本结果表述为真实设备、Gateway 或桌面平台运行态验收。

## 6. 停止线与下一步

- 本批未修改 `radish-flutter-native-ui-v1.pen`；实现只读取已冻结的 P3 几何与信息层级，不占用 Pen 插件。
- 本批未安装 / 更新依赖，未生成 iOS / desktop 平台目录，未执行签名、分发或业务数据写入。
- 本批未进入 B3 Discover、B4 Forum Detail、其他页面族或 B5 成组运行态门禁。
- 后续 `P4-B3 Discover 正式读模型与代表页` 已按独立授权完成，详见 [P4-B3 实现记录](/records/f4-flutter-native-p4b3-discover-implementation-2026-08-23)。当前下一顺位为 `P4-B4 Forum Detail 拆分与代表页`，仍需单独确认后实施。

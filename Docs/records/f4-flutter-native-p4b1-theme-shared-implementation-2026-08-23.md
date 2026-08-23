# Flutter Native P4-B1 Theme Foundation 与 Shared Primitives 实现记录

> 状态：`P4-B1` 已完成；后续 `P4-B2–B5` 已完成，P4 静态退出门禁关闭
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置审计：[P4-A 实施就绪审计](/records/f4-flutter-native-p4a-readiness-2026-08-23)

## 1. 本批结论

P4-B1 已把 P3 确认稿中的字体、排版、密度、表面、动效、焦点、共享状态与主题预览—确认收口为 Flutter 正式基座。现有四主题状态机、Shop 权益、账号隔离和业务页面 owner 均继续复用；本批没有改 Shell 布局、Discover 读模型、Forum Detail 页面族、后端接口或平台工程。

主要结果：

1. Noto Sans SC 与 Noto Serif SC 固定变量 TTF、OFL 文本和供应链记录已随包接入。
2. 未使用的 `cupertino_icons` 已由精确版本 `lucide_icons_flutter 3.1.15` 替换；页面只通过 Radish 受控映射消费当前需要的 Lucide 图标。
3. Theme 已按颜色语义、typography、spacing / density、surface / focus、motion 职责拆分，四主题共享同一套产品级契约。
4. `RadishStateSlot`、`RadishStateChip` 与 `RadishSectionSurface` 成为后续页面族可复用的薄组件层。
5. 主题选择器支持本地预览、取消恢复与显式确认；未拥有主题只解释权益并引导 Shop，不建立第二套权益状态机。

## 2. 字体与依赖供应链

### 2.1 字体资产

| 资产 | 固定上游版本 | 字节数 | SHA-256 |
| --- | --- | ---: | --- |
| `NotoSansSC-VF.ttf` | `notofonts/noto-cjk@Sans2.004` | `17,773,132` | `d68bafcb48a2707749396aa12bbbd833cb70401f3a9a689fd2902c7e0d295964` |
| `NotoSerifSC-VF.ttf` | `notofonts/noto-cjk@Serif2.003` | `25,125,232` | `5326cfb097e3ab26fcb39329752b5c0a439bf8d5c4649520e4b492939c352a09` |
| Sans / Serif `LICENSE` | SIL Open Font License 1.1 | 各 `4,301` | `6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2` |

两套字体合计 `42,898,364` bytes；加上两份许可证后为 `42,906,966` bytes。原始文件名、固定 tag、下载地址、日期和校验值统一记录在 `assets/fonts/noto/README.md`。字体通过 `pubspec.yaml` 原生声明随安装包分发，不使用运行时网络字体，也没有按当前 UI 文案裁字。

### 2.2 图标依赖

- 删除零 Dart 引用的 `cupertino_icons ^1.0.8`。
- 新增精确版本 `lucide_icons_flutter: 3.1.15`，不使用 caret 漂移；`pubspec.lock` 只发生对应直接依赖替换。
- 包许可证为 MIT；该包没有新增平台 plugin。
- `RadishIcons` 只暴露当前产品使用的受控映射，后续页面不得各自引入新的图标体系。

## 3. 实现边界

### 3.1 Theme Foundation

- `RadishThemeTokens` 集中维护四主题语义颜色、spacing、圆角、密度、最小触控目标、表面与焦点契约。
- `RadishTypography` 以 Noto Sans SC 承接 UI、正文与数据，以 Noto Serif SC 承接页面主标题和少量品牌强调。
- `RadishMotion` 固定 `120 / 200 / 280ms` 三档，并在 reduced-motion 下归零。
- `buildRadishTheme` 统一映射 Material 组件主题；应用主题切换使用 `200ms` 标准过渡，并消费 preview 后的有效主题。

### 3.2 Shared Primitives 与主题选择

- `RadishSectionSurface` 提供连续内容 section 的统一边界，避免后续页面继续层层叠加大 Card。
- `RadishStateChip` 统一 ready / stale / locked 等轻状态表达。
- `RadishStateSlot` 统一 loading / empty / unavailable 等状态骨架，具体文案、重试动作和旧快照仍由页面 owner 提供。
- 主题选择器点击主题只改变临时预览；取消清除 preview，确认后才通过既有 controller 持久化或激活权益主题。
- 未拥有主题不预览、不伪激活，只提供 Shop 跳转；Shell 本批只新增该回调，不改变壳层几何。

## 4. 验证结果

| 门禁 | 结果 |
| --- | --- |
| `dart format` | 通过，目标文件无格式漂移 |
| Theme / Selector / Shared 定向测试 | `16 / 16` 通过 |
| `flutter analyze` | 零问题 |
| `flutter test` | `233 / 233` 通过；相较 P2 基线新增 `5` 个用例 |
| `git diff --check` | 通过 |

四主题测试覆盖字体族、排版、对比度、密度和圆角契约；主题选择器测试覆盖预览、确认、取消、未拥有主题 Shop 引导与 medium dialog；Shared 测试覆盖三个新原语的关键状态。

## 5. Android release 包体积

使用同一 Flutter `3.44.0` / Dart `3.12.0`、同一 release 编译与 icon tree-shaking 参数，对 Git `HEAD` 的 P3 基线和当前 P4-B1 工作区分别生成 APK / AAB：

| 产物 | P3 基线 | P4-B1 | 增量 |
| --- | ---: | ---: | ---: |
| APK | `54,386,416` bytes | `82,320,135` bytes | `+27,933,719` bytes（`+26.64 MiB`，`+51.36%`） |
| AAB | `53,104,184` bytes | `81,058,796` bytes | `+27,954,612` bytes（`+26.66 MiB`，`+52.64%`） |

产物 SHA-256：

- 基线 APK：`5a34959e7d13bfe33f76ff6df60b61548e344671e622062aa3b7c7a2c22629dc`
- P4-B1 APK：`f12819074815f8e9190b083d9995b476c68fabe84102ad0e4e5da203a2698d21`
- 基线 AAB：`a6f484a42d4d06fbc73d8a6b9a3df57291b38770b8159508f7689353ca32eae7`
- P4-B1 AAB：`f94b7d1733db3ccaa6f6372e5346fdfc089fe9c3dfd7de7d87ee30e03e928e1d`

APK 内 Noto Sans SC 由 `17,773,132` 压缩至 `11,297,997` bytes，Noto Serif SC 由 `25,125,232` 压缩至 `15,477,662` bytes；两份许可证各压缩至 `1,896` bytes。Material Icons 已 tree-shake 为 `10,476` bytes、Lucide 已 tree-shake 为 `4,316` bytes，因此约 `26.6 MiB` 的安装包增量来自已裁决的完整 SC 区域字体资产，而不是图标依赖异常膨胀。

本机首次标准 `flutter build` 需要补齐大体积 Flutter engine / Android Gradle Plugin Lint 依赖，网络下载未在本批时间窗内稳定完成。为验证真实 release 内容，本批先校验并使用 Flutter 官方 engine JAR 本地缓存，再执行相同 release compilation、R8、资源优化、签名与 APK / AAB packaging，仅排除独立的 Lint Vital tasks；因此上述产物和体积对比可用于 B1 资源预算判断，但不替代后续网络 / 缓存稳定环境中的完整标准 `flutter build apk --release` 与 `flutter build appbundle --release` 门禁。静态分析和全量测试均已独立通过。

## 6. 停止线与下一步

- 本批未启动 API / Auth / Gateway、未运行 Flutter 应用、未执行真实 Gateway / Android Smoke，也未改业务数据。
- 本批未进入 B2 Shell、B3 Discover、B4 Forum Detail、其他页面族、新平台工程、签名或分发。
- 字体包体积增量是“完整用户内容字形优先”的已知成本；若后续需要分发预算治理，必须单独裁决动态特性或其他可验证策略，不回退到运行时网络字体，也不按当前文案裁字。
- 后续 `P4-B2 Web-Family Adaptive Shell`、`P4-B3 Discover`、`P4-B4 Forum Detail` 与 `P4-B5 成组静态门禁` 已分别授权并完成，详见 [P4-B2 实现记录](/records/f4-flutter-native-p4b2-adaptive-shell-implementation-2026-08-23)、[P4-B3 实现记录](/records/f4-flutter-native-p4b3-discover-implementation-2026-08-23)、[P4-B4 实现记录](/records/f4-flutter-native-p4b4-forum-detail-implementation-2026-08-23)和 [P4-B5 门禁记录](/records/f4-flutter-native-p4b5-grouped-static-gate-2026-08-23)；P4 静态退出门禁关闭，下一顺位等待 P5 拆批。

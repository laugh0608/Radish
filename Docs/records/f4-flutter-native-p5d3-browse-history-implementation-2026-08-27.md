# Flutter Native P5-D3 Browse History 实现记录

> 状态：`P5-D3` 已完成；下一顺位进入 `P5-E grouped static gate readiness`
>
> 日期：2026-08-27（Asia/Shanghai）
>
> 前置记录：[P5-D3 实施就绪与方案冻结](/records/f4-flutter-native-p5d3-browse-history-readiness-2026-08-24)

## 1. 结论

P5-D3 已按冻结边界完成。Flutter 继续只读取登录账号的 `User/GetMyBrowseHistory`，复用既有 Forum / Docs / Shop 原生详情 handoff，没有新增或修改后端 API、DTO、权限、数据库、migration、移动端 BFF、依赖、lockfile 或平台通道。

账号完整浏览历史已从 widget 内的请求代际、分页、错误字符串和导航组合迁入独立 `BrowseHistoryController`，明确提交 idle / loading / ready / empty / unavailable / stale 与独立 append issue。refresh 整体替换第一页，append 以服务端记录 `VoId` 稳定去重；account、credential、repository generation 与 dispose 的迟到响应均被隔离。

Post / Wiki / Product 已改为显式 typed target。页面不再把任意 route 末段作为通用目标，也不把内部 route、LongId 或原始 ISO 时间持续展示为正文。未知或失效目标仍保留历史记录和不可用原因，不静默丢弃。

账号服务端历史与设备 Forum / Docs recent shortcut 继续由不同 owner 管理。P5-D3 页面不读取、不合并、不迁移、不清理设备 recent store；从历史主动打开 Forum / Docs 后，只沿用既有详情链路自然更新设备快捷记录。

## 2. Owner 与规模

| Owner | 实施后职责 | 行数 |
| --- | --- | ---: |
| `browse_history_page.dart` | route、controller 生命周期、account reopen 与三类原生 handoff | `230` |
| `browse_history_controller.dart` | 私域分页 snapshot、稳定 ID merge、结构化状态与隔离 | `299` |
| `browse_history_issue.dart` | API、响应格式与请求错误分类 | `52` |
| `browse_history_surface.dart` | 数据来源说明、状态、连续历史、分页动作与三档结构 | `496` |
| `profile_models.dart` | Browse History 正 LongId、typed target、时间与展示 fallback | `678` |
| `radish_flutter_shell.dart` | Shell 路由、会话与全局 handoff；本批只补明确 `accountId` 传递 | `1415` |

原 `600` 行 Browse History 页面已按 route、远端状态和呈现职责拆分为 `230` 行；实施后运行时 Dart owner 最大仍为既有 Shell 的 `1415` 行。测试 owner 为 controller `325`、model / repository `236`、page `334`、responsive `210` 行，全部低于 `1500` 行硬上限。

## 3. 状态、目标与账号隔离

- `BrowseHistoryController` 固定使用 `pageSize=20`。首次成功空页是权威 empty；ready 或 empty 刷新失败保留原 snapshot 并进入 stale；恢复成功整体替换第一页。
- append 成功保留服务端顺序并按正值 `VoId` 去重，页码仍推进到服务端返回页；append 失败只提交独立 issue，保留当前快照和页码并重试同一下一页。
- 账号变化立即清空旧私域快照并重新 initial；同账号 credential 更新在 ready 状态保留快照刷新。请求、账号 / credential、repository owner 与 dispose 都受 generation 隔离。
- Shell 打开页面时显式传递当前 session `userId`。页面收到新 repository 时销毁旧 controller 并创建新 owner，旧 owner 的迟到响应不能提交或通知。
- `VoId / VoTargetId` 继续以十进制字符串保存，并只接受正整数，避免 LongId 经过不安全的普通数值转换。
- Post 优先合法内部 `/forum/post/{pst_32hex|positive-id}`，兼容合法 legacy slug，再回落正 `VoTargetId`；外部 route 不参与导航。
- Wiki 优先 `VoTargetSlug`，其次只接受内部 `/docs/{slug}` 或 `/wiki/doc/{slug}`；Product 只信任正 `VoTargetId`。未知或非法目标保持可见但不提交空 handoff。
- Post 测试 fixture 已对齐正式 `pst_` + 32 位十六进制契约；严格解析失败时仍只允许既定正整数 fallback。

## 4. 自适应与主题

- compact `<600px`：页面头、账号历史说明、状态与按服务端顺序排列的单列连续历史；长标题、摘要、时间与指标自然换行。
- medium `600–1023px`：不增加 rail，使用受控主轴和时间顺序密集列表，meta 与动作保持可扫读，不按类型重新分组。
- expanded `>=1024px`：连续历史主轴不超过 `904px`，以 `24px` 间距承接 `280–300px` 的“数据来源说明” rail；rail 只解释账号历史与设备快捷记录边界并汇总当前 snapshot，不发起新请求。
- 页面统一使用 `RadishContentFrame`、`RadishWindowClass`、`RadishSectionSurface`、`RadishStateSlot`、`RadishStateChip` 与四主题语义 token，没有新增主题 ID 分支、业务硬编码色或远程封面读取。
- 显示时间统一转换为本地 `yyyy-MM-dd HH:mm`；非法时间稳定回落“时间未知”。surface 不暴露内部 route 或原始 ISO 时间。

## 5. 验证

- P5-D3 controller：`12 / 12`，覆盖 initial success / empty / unavailable / recover、ready / empty stale、append / retry / `VoId` 去重、account / credential / generation / dispose 隔离。
- P5-D3 model / repository：`9 / 9`，覆盖 bearer endpoint、正 LongId、Post / Wiki / Product typed target、外部 / 非法 target 与本地时间 fallback。
- P5-D3 page：`3 / 3`，覆盖三类原生 handoff、refresh stale 与 repository owner 重建迟到响应。
- P5-D3 responsive：`10 / 10`，精确覆盖 `599 / 600 / 1024 / 1280`、四主题、compact 长内容、非法目标以及 route / ISO 隐藏。
- P5-D3 定向合计 `34 / 34`；Shell Smoke `51 / 51`；成组定向合计 `85 / 85`。
- Flutter 全量由 `374 / 374` 增至 `406 / 406`；`flutter analyze`：零问题。
- 服务端既有 `UserBrowseHistoryServiceTest`：`3 / 3`。
- `dart format`、改动 Dart owner `<1500`、`git diff --check`、文档和仓库卫生门禁通过。

本批未启动服务，未读取或修改 Pen，未执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke。

## 6. 下一顺位

P5-D3 关闭。下一顺位只进入 `P5-E grouped static gate readiness`：先盘点 P4 / P5 已完成页面族的四主题、`599 / 600 / 1024 / 1280`、关键状态、文件上限和既有测试覆盖，识别真实缺口并冻结成组静态门禁；不随本记录自动开始 P5-E 实施、平台工程、服务启动或真实运行态 Smoke。

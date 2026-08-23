# Flutter Native P5-B2 Identity / Revisit 实现记录

> 状态：`P5-B2` 已完成；下一顺位为 `P5-C1 Docs Reader readiness`
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置记录：[P5-B2 实施就绪与方案冻结](/records/f4-flutter-native-p5b2-identity-revisit-readiness-2026-08-23)

## 1. 结论

P5-B2 已按冻结边界完成。Profile 不再用一次 `Future.wait` 整批裁决公开资料：identity、stats、posts、comments 和本人 quick replies 均持有独立权威快照、请求代际与结构化 issue。首次失败只在对应 owner 中显示 unavailable，刷新失败保留旧快照并标记 stale；跨用户迟到响应、已释放 controller 的迟到响应和跨账号私域快照均不会回写当前页面。

compact / medium / expanded 分别形成连续信息流、受控单主轴和 expanded `904px` 主轴 + 身份上下文 rail。公开主页与“我的”共用身份 / 公开活动组件，但 recent Forum / Docs、quick replies 和私域去向仍只属于本人态。资料编辑只消费既有 `GetMyProfile + UpdateMyProfile`，已具备权威草稿、归一化 dirty、saving busy、失败保留、系统返回与丢弃确认保护。

## 2. Owner 与规模

| Owner | 实施后职责 | 行数 |
| --- | --- | ---: |
| `profile_page.dart` | 生命周期、session / target、编辑任务与 Shell handoff 编排 | `279` |
| `profile_controller.dart` | target epoch、五类独立快照、刷新、分页与 issue | `852` |
| `profile_surface.dart` | 根状态与三档页面结构 | `424` |
| `profile_identity_surface.dart` | 身份 hero、stats、公开链接与上下文 rail | `539` |
| `profile_activity_surface.dart` | recent、quick replies、posts / comments 连续流 | `688` |
| `profile_edit_controller.dart` | 可编辑权威快照、dirty、busy 与 issue | `184` |
| `profile_edit_dialog.dart` | compact 全屏 / bounded 任务壳与离开保护 | `389` |

原 `1904` 行页面已拆为上述真实职责。原 `2598` 行测试拆为 `41` 行 library 入口与 controller / adaptive / edit protection / identity-edit / revisit-navigation / activity / auth-boundary / support 各 owner，本批变更的 Dart 文件最大 `878` 行，全部低于 `1500` 行硬上限。

## 3. 权威状态与 target 契约

- `ProfileResourceStatus` 统一为 `idle / loading / ready / unavailable / stale`；`ProfileIssue` 保留 `kind / message / code / statusCode`，`FormatException` 与非 API 异常也会归入稳定本地 issue。
- 页面刷新并行启动五类请求，但每个响应独立提交；任一次级区块失败不拖垮 identity 或其他快照。
- posts、comments 和 quick replies 分别使用本列表 request generation，append 互不取消；首页与 append 均按稳定字符串 ID 去重，LongId / PublicId 不数值化。
- Forum 帖子、评论、轻回应仍使用 `ForumDetailHandoffTarget`；Docs recent 仍使用 `DocsDetailHandoffTarget`。设备 recent 由 Shell owner 注入，完整服务端 Browse History 仍属于 P5-D3。
- 本人退出、账号 / target 切换会立即清理 quick replies 与私域动作；旧 target 和 disposed controller 的迟到响应被 epoch / generation 拒绝。

## 4. 资料编辑保护

- 打开编辑任务后独立读取 `GetMyProfile`；loading、首次 unavailable 和 retry 均留在任务内。
- dirty 比较基于归一化后的 `UpdateMyProfileRequest`：用户名 / 邮箱 / 地址 trim，空地址为 `null`，服务端未设置年龄与空输入同义。
- saving 时字段、取消和重复保存均禁用，系统返回不会销毁任务；dirty 时关闭、取消与系统返回统一经过丢弃确认。
- 保存失败保留草稿与结构化 issue；成功后不用草稿乐观改写公开页，而是关闭任务并重读 Profile 权威快照。
- compact 使用安全区内全屏可滚动任务，medium / expanded 使用最大约 `680px` 的 bounded surface；键盘 inset 与 `disableAnimations` 下仍可访问最后字段和保存动作。

## 5. 验证

- Profile 定向：`52 / 52`（改造前 `32 / 32`，既有用例未减少）。
- Shell 静态 Smoke：`51 / 51`；包含 OIDC、Android Back、Profile / Forum / Docs handoff 与 recent store。
- Flutter 全量：`274 / 274`。
- `flutter analyze`：零问题。
- 文件行数、`git diff --check`、文档链接与仓库文本卫生检查通过。

本批未新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、Pen 或平台工程，未启动服务，未执行真实 Gateway / 浏览器 / Android RC Smoke。

## 6. 下一顺位

P5-B2 关闭。下一顺位是 `P5-C1 Docs Reader readiness`：先反查内联详情与 handoff route 是否真正共用同一正文 owner、旧正文刷新 stale、目录—正文三档结构与测试边界。该顺位仍需独立方案确认，不自动授权 Commerce、派生只读面、平台工程、服务启动或真实运行态 Smoke。

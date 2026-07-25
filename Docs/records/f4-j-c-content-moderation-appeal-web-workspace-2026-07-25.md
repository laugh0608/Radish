# F4-J-C 内容治理申诉与处置纠正正式页面

## 结论

F4-J-C 已完成 Pencil 与正式页面实现，下一顺位进入 F4-J-D 成组验收。

本批在既有内容治理案件工作台内接入申诉复核与处置纠正，没有拆分第二套 Console 应用；本人侧新增 `/me/appeals`，保留历史决定回看并严格裁剪内部治理信息。

## 已完成范围

- `private-web-workflows.pen` 新增 `P33 / P34`，固定本人决定、申诉资格、陈述提交、撤回、公开结果与纠正状态的 PC / mobile 页面结构。
- `console-governance-workbench.pen` 更新 `P02 / P07`：桌面端承接案件与申诉双工作区，移动端只提供申诉队列与公开摘要只读流。
- `radish.client` 新增 `/me/appeals`，支持决定 / 申诉列表、详情深链、20–1000 字本人陈述、提交、撤回和处置结果回看；超期决定仍可查看，但不再允许提交申诉。
- 决定与申诉通知接入 `/me/appeals?case=...`、`/me/appeals?appeal=...`，登录回流保留查询参数。
- `radish.console` 在 `/moderation?view=appeals&appeal=...` 接入申诉队列、原决定和案件证据、受理、补证、复核、纠正执行与事件留痕。
- Console 严格按 `console.moderation.view / appeal / action` 裁剪：仅有 View 权限时只能读取不含陈述、内部备注、事件与动作明细的队列；移动视图不暴露写操作。
- Action-only 执行员可从脱敏队列执行已经获准的纠正；`ExecuteAppealRelief` 使用独立精简结果 VO，不通过写入响应返回申诉正文、内部备注或复核事件。同时具备 Appeal 权限时，页面在执行后重新读取受权详情。
- 409 冲突会重新读取权威详情并刷新 operation key，同时保留尚未成功提交的复核、补证与纠正草稿。
- `@radish/http` 补齐已存在的申诉契约根导出，避免消费者绕过统一客户端入口。
- 服务端队列映射改为专用摘要，防止本人陈述和内部复核信息通过 View 权限泄露；本人决定查询保留超期历史记录，申诉资格仍由服务端截止时间校验。
- 本人决定契约由服务端返回 `voCanAppeal / voIneligibleReason` 与安全目标摘要，页面不再使用浏览器时间推断资格，也不接收目标 LongId 或猜测详情链接。
- 本人申诉只接收专用用户状态动作摘要，完整 `UserModerationActionVo`、内部原因、动作 / 用户 ID 和操作员信息不会进入本人响应；页面同步展示 Mute / Ban 及解除结果。
- Console 申诉详情明确展示原治理决定、原目标处置与原公开结果，避免复核人员从证据标题反推原决定。
- 页面固定文案已补中英文资源，并沿用 Client / Console 既有语义 token 适配四主题。

## 验证边界

本批执行代码侧构建、测试、类型检查、Lint、文档与仓库卫生门禁；Gateway PC / mobile 页面访问、权限矩阵和真实纠正链路留到 F4-J-D 成组验收，不在开发批次重复启动服务。

## 验证结果

- `dotnet build Radish.slnx -c Debug --no-restore`：通过，0 warning / 0 error。
- `dotnet test Radish.Api.Tests --filter FullyQualifiedName~ContentModerationServiceTest -c Debug --no-restore`：16 通过。
- `npm run test --workspace=radish.client`：455 通过。
- `npm run test --workspace=radish.console`：59 通过。
- Client / Console Lint 与类型检查、`@radish/http` 类型检查：通过。
- Client / Console production build：通过；Client 保留既有 chunk size 提示。
- `npm run validate:baseline:quick`：通过。
- `./scripts/check-docs.sh`、`npm run check:repo-hygiene:changed`、`git diff --check`：通过。

## 下一批边界

F4-J-D 应按本人端与 Console 权限矩阵执行 Gateway PC / mobile 成组复核，覆盖提交、撤回、受理、补证、维持 / 部分采纳 / 采纳、纠正成功 / 跳过 / 失败 / 重试、冲突恢复、通知深链与历史回看，并形成阶段验收记录。

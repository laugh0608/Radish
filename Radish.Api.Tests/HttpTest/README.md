# HttpTest 目录说明

本目录只保留三类内容：

- 可直接执行的专题 `.http` 脚本
- 配套的自动化脚本（当前主要是附件、限流）
- 附件上传所需的测试素材 `test-files/`

## 使用约定

- 本目录不再维护服务启动命令，默认你已经按仓库根目录的当前约定启动了本地 `Radish.Api`、`Radish.Auth`、`Radish.Gateway`。
- 需要认证的接口，统一先从 `Radish.Api.AuthFlow.http` 获取 `access_token`，再粘贴到对应文件顶部变量。
- 附件相关脚本依赖 `test-files/` 里的示例文件；如需更真实的图片/大文件/分片文件，可在该目录补充。
- 构建与测试请继续使用仓库统一脚本：
  - `powershell -ExecutionPolicy Bypass -File Scripts\dotnet-local.ps1 build ...`
  - `powershell -ExecutionPolicy Bypass -File Scripts\dotnet-local.ps1 test ...`
- 如果你不确定“改了这个模块该跑哪份专题回归”，优先查看 [专题回归索引](/guide/regression-index)。

## 目录分组

### 认证与基础

- `Radish.Api.AuthFlow.http`
- `Radish.Api.Smoke.http`
- `Radish.Api.Tenant.http`

### 社区与论坛

- `Radish.Api.Community.http`
- `Radish.Api.Forum.Core.http`
- `Radish.Api.Forum.Comment.http`
- `Radish.Api.Forum.Lottery.http`
- `Radish.Api.Forum.Poll.http`
- `Radish.Api.Forum.Question.http`

### 用户与个人中心

- `Radish.Api.User.Profile.http`

### 附件

- `Radish.Api.Attachment.Upload.http`
- `Radish.Api.Attachment.Manage.http`
- `Radish.Api.Attachment.Guardrail.http`
- `Radish.Api.Attachment.Chunk.http`
- `Radish.Api.Attachment.Token.http`
- `test-attachment-upload.ps1`
- `test-attachment-upload.sh`

### 限流

- `Radish.Api.RateLimit.Core.http`
- `Radish.Api.RateLimit.Policy.http`
- `Radish.Api.RateLimit.Edge.http`
- `test-rate-limit.ps1`
- `test-rate-limit.sh`

### 其他专题

- `Radish.Api.Chat.http`
- `Radish.Api.Coin.http`
- `Radish.Api.CommentHighlight.http`
- `Radish.Api.Experience.http`
- `Radish.Api.Sticker.http`
- `Radish.Api.Wiki.http`

## 规范化说明

- 超长脚本已按主题拆分，避免单文件同时混入多个领域。
- 阶段性测试指南、一次性测试报告和过期入口已移除，避免与当前协作规范冲突。
- 后续新增脚本时，优先沿用 `Radish.Api.<Domain>[.<Topic>].http` 命名，并保持“一份文件只覆盖一个明确主题”。

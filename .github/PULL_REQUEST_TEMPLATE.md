## 变更说明

请简要说明本次 PR 的目标、范围和原因。

## 关联信息

- 关联 Issue / 任务：
- 目标分支：`dev` / `master`（如非 `dev`，请说明原因）
- 变更类型：
  - [ ] 功能
  - [ ] 修复
  - [ ] 重构
  - [ ] 文档
  - [ ] 配置 / 部署
  - [ ] 测试 / 验证基线

## 检查清单

- [ ] 本次改动符合当前阶段主线，或已明确说明为何属于例外
- [ ] 已优先从根因、长期维护性和系统一致性出发处理问题，而不是仅做最小修补
- [ ] 已执行与本次改动匹配的最小验证
- [ ] 如触达身份语义 / Claim / Auth 协议输出 / Token 解析，已补 `npm run validate:identity`，并按需记录 `Radish.Api.AuthFlow.http` 与官方顺序回归结果
- [ ] 如修改了架构、规则、接口、流程、视觉口径或协作规范，已同步更新 `Docs/` 与相关协作文件
- [ ] 如修改了宿主、配置、数据库结构、种子或 `DbMigrate`，已说明影响范围与处理方式
- [ ] 未直接向 `master` 提交常规功能改动
- [ ] 默认目标分支为 `dev`；只有阶段性集成、发布或明确收口事项才面向 `master`

## 验证记录

请列出实际执行过的命令，并只保留真实跑过的内容，例如：

```text
npm run validate:baseline:quick
npm run validate:baseline
npm run validate:baseline:host
npm run validate:identity
dotnet build Radish.slnx -c Debug
dotnet test Radish.Api.Tests
npm run build --workspace=radish.client
npm run build --workspace=radish.console
```

如本轮触达身份语义 / Claim / Auth 协议输出，请再补记：

```text
Radish.Api.Tests/HttpTest/Radish.Api.AuthFlow.http
radish-client -> radish-console -> Radish.Api.AuthFlow.http -> radish-scalar
```

## 影响评估

- 文档影响：
  - [ ] 无
  - [ ] 有，已同步更新 `Docs/`
- 配置影响：
  - [ ] 无
  - [ ] 有，已说明 `appsettings` / 环境变量 / 运行时配置变更
- 数据库影响：
  - [ ] 无
  - [ ] 有，已说明是否需要执行 `DbMigrate apply/init/seed`
- 前端影响：
  - [ ] 无
  - [ ] 有，已说明影响的 workspace / 页面范围
- 后端影响：
  - [ ] 无
  - [ ] 有，已说明影响的宿主 / API / 数据链路

## 风险与回滚

- 已知风险：
- 回滚方式：
- 是否涉及发布后额外操作：
  - [ ] 否
  - [ ] 是，请说明

## 后续事项

请说明当前未完成项、后续建议或需要额外跟踪的事项。

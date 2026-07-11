# P3-12-D63 Console 成组静态收口与后置缺口整理记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`radish.console` D63 已实现页面族、D63 记录与规划入口
> 设计源：`Docs/frontend/design-sources/console-governance-workbench.pen`

## 背景

`P3-12-D63 Console` 已完成当前发布前范围内的首批页面族实现：

- `P02-P03 / P07-P08`：内容治理 / 经验治理工作台。
- `P10`：商业运营页面族。
- `P11`：文档治理页面族。
- `P05`：用户管理表格 CRUD。
- `P12`：RBAC 权限矩阵。

按当前规划，本批不继续追加新页面实现，而是对 D63 已覆盖页面族做成组静态收口，确认没有把 Console 设计源里的后置产品能力误实现为当前代码。

## 复核范围

### 代码侧静态复核

- 复核 D63 目标页是否残留 `style=`、硬编码十六进制色、`rgba(...)`、`console.*`、`TODO` / `FIXME`。
- 复核 D63 目标页是否绕过统一 API 客户端，直接使用 `fetch`、`axios`、`apiPost`、`apiPut`、`apiDelete` 或本地存储。
- 复核权限和写操作是否仍停留在现有 `CONSOLE_PERMISSIONS`、现有 API helper、现有路由和现有保存载荷内。
- 复核当前规划、P3-12 专题页和 D63 记录索引是否能指向当前事实。

### 后置缺口整理

以下能力继续作为 D63 内后置产品 / API 缺口记录，不在本批实现：

- 独立移动 Console 应用。
- `P04 / P13 / P18` 对应的内部调度、内部 Jobs、运行审计、失败重试和完整任务平台。
- 新的治理 API、审批流、授权审计写入、权限版本字段或资源种子扩展。
- 跨页面完整工作流编排、批量治理执行中心和新增保存 / 提交载荷。

## 结论

- D63 当前发布前范围的 Console 页面族已完成首批 UI 与现有接口内功能缺口实现。
- 本批静态扫描未发现 D63 目标页新增 inline 样式、硬编码色、直接请求封装、直接本地存储或 `console.*` 残留。
- 现有写操作仍由原本的治理、商业、文档、用户和角色权限 API 承载；权限判断继续使用既有 `CONSOLE_PERMISSIONS`。
- D63 仍不直接进入 `P3-12-E`。下一步应进入 D63 成组验证准备；真实 Gateway PC / mobile smoke 必须在用户当轮明确确认前后端已启动后执行。

## 验证

- `rg` 定向扫描：通过，未发现目标样式 / 直接请求 / 控制台日志残留。
- `npm run build --workspace=radish.console`：通过。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:staged`：通过。
- `git diff --cached --check`：通过。

本批未执行 Gateway PC / mobile 真实页面 smoke；按协作规则，真实 smoke 需要在执行前由用户在当轮明确确认前后端已启动。

## 后续

- 下一顺位进入 D63 成组验证准备，优先准备 Gateway PC / mobile 覆盖矩阵和必要静态补验。
- 若用户确认前后端已启动，再覆盖 Console 登录回流、治理 / 商业 / 文档 / 用户 / 权限矩阵页面的 PC 与移动视口。
- 真实验收完成前不进入 `P3-12-E`，不创建发布 tag，不进入 M15 测试或生产部署流程。

# 验证基线说明

> 本页说明当前仓库认可的最小验证基线，以及 `M13` 首轮统一入口的使用方式。

## 当前目标

`M13` 当前不追求“一次性补齐完整 CI / E2E / 宿主观测平台”，而是先把仓库里已经存在的验证资产收束成统一入口，降低“知道要跑什么，但每次靠口口相传”的协作成本。

## 统一入口

根目录现提供以下命令：

```bash
npm run setup:hooks
npm run check:repo-hygiene
npm run lint:changed
npm run lint:staged
npm run collect:changed
npm run collect:changed:staged
npm run collect:tracked
npm run check:identity-impact
npm run check:identity-impact:staged
npm run validate:baseline
npm run validate:baseline:quick
npm run validate:baseline:host
npm run validate:identity
```

对应关系：

- `setup:hooks`
  - 将 Git `core.hooksPath` 指向仓库内的 `.githooks`
  - 启用 `pre-commit` 与 `commit-msg` 本地拦截
- `check:repo-hygiene`
  - 全量检查仓库已跟踪文本文件的 UTF-8 / BOM / 换行符 / 末尾换行 / 尾随空格
  - 适合治理历史文本问题时使用
- `lint:changed`
  - 只对当前变更中的前端脚本文件执行 `eslint`
  - 适合与 GitHub Actions 的 `Frontend Lint` 对齐
- `lint:staged`
  - 只对 staged 变更中的前端脚本文件执行 `eslint`
  - 适合提交前先看“这次准备提交的文件”是否通过
- `collect:changed`
  - 统一输出当前变更文件列表
  - 适合排查“当前 CI / changed-only 脚本到底看到了哪些文件”
- `collect:changed:staged`
  - 统一输出 staged 变更文件列表
  - 适合与本地 hooks、提交前自检对齐
- `collect:tracked`
  - 统一输出仓库当前所有 tracked files
  - 适合排查全量文本卫生或脚本命中范围
- `check:identity-impact`
  - 只判定“当前变更是否命中身份语义影响面”
  - 适合与 GitHub Actions 的 `Identity Guard` 按改动范围触发逻辑对齐
- `check:identity-impact:staged`
  - 只判定 staged 变更是否命中身份语义影响面
  - 适合提交前先判断“本次准备提交的内容”是否需要追加 `validate:identity`
- `validate:baseline`
  - 运行前端 `type-check`
  - 运行 `radish.client` 现有 `node --test`（当前以 `--test-isolation=none` 兼容受限环境）
  - 运行 `Console` 权限链路扫描
  - 运行身份语义防回归扫描
    - 运行时代码是否重新散落 `FindFirst/FindAll/ClaimTypes/User.IsInRole` 等原始 Claim 读取
    - `Radish.Auth` 协议输出侧是否试图恢复 `ClaimTypes.NameIdentifier / ClaimTypes.Name / ClaimTypes.Role / TenantId / jti` 等历史双写承诺
  - 运行后端 `build`
  - 运行后端 `test`
- `validate:baseline:quick`
  - 只运行前端 `type-check`
  - `radish.client` 最小测试
  - `Console` 权限链路扫描
  - 身份语义防回归扫描
- `validate:baseline:host`
  - 等同于 `validate:baseline`
  - 额外追加 `DbMigrate doctor` / `verify` 只读自检（复用前序构建产物，不重复 build）
- `validate:identity`
  - 身份语义专题聚合入口，不替代默认 baseline
  - 分别执行运行时散点 Claim 读取扫描与协议输出回退扫描
  - 运行身份语义后端定向测试，覆盖 `ClaimsPrincipalNormalizer`、`HttpContextUser`、`AccountController`、`AuthorizationController`、`UserInfoController`

## 分层使用建议

### 1. 日常提交前

优先运行：

```bash
npm run setup:hooks
npm run validate:baseline:quick
```

适用场景：

- 前端页面、共享组件、Console 权限链路调整
- 纯文档之外、但又不需要完整后端回归的日常提交前检查

补充说明：

- `setup:hooks` 只需首次执行一次；之后提交时会自动运行：
  - staged 文件的文本卫生检查
  - staged 变更中的前端脚本 lint
  - Conventional Commits 提交标题校验
- 如果只想看“本次准备提交的内容”是否会命中身份语义专题，可先运行：

```bash
npm run check:identity-impact:staged
```

- 如果提交被 hooks 拦截，优先先修正本次变更，不要绕过 hooks 强行提交

### 2. 合并前 / 跨层改动后

推荐运行：

```bash
npm run validate:baseline
```

适用场景：

- 涉及后端 Service / Repository / API 契约
- 涉及前后端联动字段、权限或路由
- 需要给出最小“本地可构建、可测试”结论时

如果准备发起到 `master` 的 PR，再补一轮：

```bash
npm run lint:changed
npm run check:repo-hygiene
```

说明：

- `master` 当前受规则保护，只允许通过 PR 合并
- GitHub Actions 中的 `Repo Hygiene` 与 `Frontend Lint` 当前仅检查“本次变更文件”，用于先拦新增问题，避免被历史债务拖死
- `Identity Guard` 当前也已改为按变更文件触发：先用 `check:identity-impact` 判定是否命中身份语义影响面，再决定是否执行 `validate:identity`
- 当前 changed-only 入口与 `Repo Quality` 的变更文件收集逻辑已统一复用 `Scripts/collect-changed-files.mjs`
- 工作区级 changed-only 默认使用 `collect:changed`
- 提交前只看 staged 内容时，优先使用 `collect:changed:staged`、`lint:staged` 与 `check:identity-impact:staged`
- `check:repo-hygiene` 本地全量扫描仍建议按需人工执行，适合做历史清理批次时使用

### 3. 身份语义相关改动后

再追加：

```bash
npm run validate:identity
```

适用场景：

- `CurrentUser` / `IHttpContextUser` / `UserClaimReader` / `ClaimsPrincipalNormalizer`
- `UserClaimTypes`、角色 / Scope / 授权策略常量
- `Radish.Auth` 的 `AccountController`、`AuthorizationController`、`UserInfoController`
- 前端 Token 解析、OIDC 回调、`userinfo` 契约说明

补充说明：

- `validate:identity` 是身份语义专题入口，不替代 `validate:baseline`
- 若本轮触达 Auth 输出、`userinfo`、官方客户端 Token 解析或 `Radish.Api.AuthFlow.http`，再按 [身份语义防回归回归手册](/guide/identity-claim-regression-playbook) 补 `AuthFlow` 与官方顺序回归

### 4. 宿主或配置相关改动后

再追加：

```bash
npm run validate:baseline:host
```

适用场景：

- `DbMigrate`
- 连接定义、种子前置状态
- 宿主配置与最小部署自检链路

## 当前仍保留为专题回归的资产

以下内容当前不并入默认自动化脚本，而继续保持专题使用：

- `Radish.Api.Tests/HttpTest/*.http`
- 各专题文档中的最小人工验收顺序
- 需要先启动本地服务的联调链路

专题选择建议见：[专题回归索引](/guide/regression-index)

原因很简单：这些资产依赖运行中的宿主、账号、Token 或人工观察结果，当前阶段更适合作为“专题回归层”，而不是默认本地自动化层。

## 失败时先看哪里

- 前端类型错误：优先看对应 workspace 的 `tsc` 输出
- `radish.client` 最小测试失败：优先看 `Frontend/radish.client/tests/`
- 权限扫描失败：优先看 `Scripts/check-console-permissions.mjs` 输出中的四层对齐差异
- 身份语义扫描失败：优先看 `Scripts/check-identity-claims.mjs` 输出中的命中位置，确认是否回退到原始 Claim 解析或直接字符串判断
- 若命中 `AccountController / AuthorizationController / UserInfoController`：优先按 Phase 4 口径确认是否误恢复了历史输出承诺，而不是直接放宽白名单
- 后端构建 / 测试失败：优先看 `Scripts/dotnet-local.ps1` 包装后的 `dotnet` 输出
- `doctor` / `verify` 失败：优先核对当前环境配置、`MainDb` / `Databases` 与关键 `ConnId`
- 如果是 Wiki / 文档链路，额外确认 `doctor` 是否已报告 `WikiDocument.Visibility`、`AllowedRoles`、`AllowedPermissions` 等缺列；旧 SQLite 库需要重新执行 `DbMigrate apply` 触发自动补齐

## 边界说明

- 当前已接入最小 CI 门禁，但仍以“变更文件优先”为主
- 当前不默认承诺 `HttpTest` 已自动化
- 当前不默认承诺前端具备完整 Vitest / RTL / Playwright 基线
- 当前只是在已有资产之上补“统一入口”和“分层说明”

## Windows 本机宿主占用说明

在 Windows 本机联调时，如果 `Radish.Api`、`Radish.Auth`、`JetBrains.Debugger.Worker` 等进程仍在占用默认 `bin\Debug` 输出目录，`dotnet build` 或 `dotnet test` 可能因为 DLL 被锁而失败。

处理顺序建议如下：

1. 优先停止本机运行中的宿主或调试进程，再执行默认的构建与测试验证。
2. 如果这轮只是做必要的编译 / 测试确认，且暂时不方便停宿主，可改用隔离输出目录完成验证，避免覆盖正在运行的宿主产物。
3. 自动化验证与真实联调尽量拆开执行，不要长时间共用同一个默认输出目录反复覆盖。

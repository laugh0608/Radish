# 验证基线说明

> 本页说明当前仓库认可的最小验证基线，以及 `M13` 首轮统一入口的使用方式。
>
> 关联维护入口：
>
> - [Repo Quality 故障分诊手册](/guide/repo-quality-troubleshooting)
> - [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
> - [`PR -> master` 最小执行清单](/guide/master-pr-minimal-checklist)

## 当前目标

`M13` 当前不追求“一次性补齐完整 CI / E2E / 宿主观测平台”，而是先把仓库里已经存在的验证资产收束成统一入口，降低“知道要跑什么，但每次靠口口相传”的协作成本。

## 统一入口

根目录现提供以下命令：

```bash
npm run setup:hooks
npm run check:repo-hygiene
npm run check:repo-hygiene:changed
npm run check:repo-quality-contract
npm run lint:changed
npm run lint:staged
npm run collect:changed
npm run collect:changed:staged
npm run collect:tracked
npm run collect:m14-host-record
npm run collect:change-regression-record
npm run check:identity-impact
npm run check:identity-impact:staged
npm run validate:baseline
npm run validate:baseline:quick
npm run validate:baseline:host
npm run check:host-runtime
npm run validate:identity
npm run validate:ci
```

对应关系：

- `setup:hooks`
  - 将 Git `core.hooksPath` 指向仓库内的 `.githooks`
  - 启用 `pre-commit` 与 `commit-msg` 本地拦截
- `check:repo-hygiene`
  - 全量检查仓库已跟踪文本文件的 UTF-8 / BOM / 换行符 / 末尾换行 / 尾随空格
  - 适合治理历史文本问题时使用
- `check:repo-hygiene:changed`
  - 只检查当前 worktree 变更文件的文本卫生
  - 适合与 GitHub Actions 的 `Repo Hygiene` changed-only 行为对齐
- `check:repo-quality-contract`
  - 校验 `.github/workflows/repo-quality.yml`、`.github/rulesets/master-protection.json` 与本地 `validate:ci` 的门禁契约是否仍然一致
  - 适合在调整 workflow job、ruleset required checks 或本地 `Repo Quality` 复现入口时先做轻量自校验
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
- `collect:m14-host-record`
  - 汇总 `validate:baseline:host` 与 `check:host-runtime` 已落盘的 Markdown 报告
  - 默认读取 `.tmp/baseline-host-report.md` 与 `.tmp/host-runtime-report.md`
  - 默认输出 `.tmp/m14-host-maintenance-record.md`
  - 适合把启动前与启动后的 `M14` 结论直接沉淀成一份维护记录
- `collect:change-regression-record`
  - 汇总批次级自动化报告，生成一份可直接贴进 PR 或回归记录的 Markdown
  - 默认读取 `.tmp/validate-ci-report.md`
  - `baseline / host / M14` 报告需显式通过参数传入，避免误吃到历史残留的 `.tmp` 文件
  - 默认输出 `.tmp/change-regression-record.md`
  - 会同时复用当前 changed files 判定身份语义影响面，把命中原因与失败归类一并写进记录
- `check:identity-impact`
  - 只判定“当前变更是否命中身份语义影响面”
  - 默认同时输出命中文件与命中原因类别，便于直接回写 PR / 维护记录
  - 适合与 GitHub Actions 的 `Identity Guard` 按改动范围触发逻辑对齐
- `check:identity-impact:staged`
  - 只判定 staged 变更是否命中身份语义影响面
  - 适合提交前先判断“本次准备提交的内容”是否需要追加 `validate:identity`
- `validate:baseline`
  - 运行前端 `type-check`
  - 运行 `radish.client` 现有 `node --test`（当前以 `--test-isolation=none` 兼容受限环境）
  - 运行 `Console` 权限链路扫描
  - 运行 Repo Quality contract 自校验
    - 确保 workflow job 名、ruleset required checks 与本地 `validate:ci` 没有再次分叉
  - 运行身份语义 impact 判定自校验
    - 确保 `check:identity-impact` 与 `Identity Guard` 的命中规则未因脚本或文档入口调整而漂移
  - 运行身份语义防回归扫描
    - 运行时代码是否重新散落 `FindFirst/FindAll/ClaimTypes/User.IsInRole` 等原始 Claim 读取
    - `Radish.Auth` 协议输出侧是否试图恢复 `ClaimTypes.NameIdentifier / ClaimTypes.Name / ClaimTypes.Role / TenantId / jti` 等历史双写承诺
  - 运行后端 `build`
  - 运行后端 `test`
- `validate:baseline:quick`
  - 只运行前端 `type-check`
  - `radish.client` 最小测试
  - `Console` 权限链路扫描
  - Repo Quality contract 自校验
  - 身份语义 impact 判定自校验
  - 身份语义防回归扫描
- `validate:baseline:host`
  - 等同于 `validate:baseline`
  - 额外追加 `DbMigrate doctor` / `verify` 只读自检（复用前序构建产物，不重复 build）
  - 当前也是 `M14` 的默认宿主验证入口
  - 当前已补启动前分流提示：若失败落在默认基线 / `doctor` / `verify` 任一层，会分别提示先修代码回归、配置连接定义或数据库前置，而不是直接跳到运行态排障
  - 当前也已支持 `--report` / `--report-file <path>`，可把启动前验证结果收敛为固定 Markdown 报告或直接落盘
  - 当前其 `Summary` 会固定补 `Route / TriageScope / TriageCode / NextStage` 四个摘要字段，便于与启动后报告直接串联
- `check:host-runtime`
  - 运行态健康检查入口
  - 默认检查 `Gateway / Api / Auth` 的 `/health`
  - 适合在宿主已经启动后，快速判断问题是“宿主没起来”还是“已起来但链路异常”
  - 当前失败时会附带分类提示，例如端口未监听、TLS、超时、宿主自报 `Unhealthy` 或一般 HTTP 状态异常
  - 当前三个宿主都保持“`/health` 只做最小探活，`/healthz` 提供结构化明细”的分层语义
  - `Gateway / Api / Auth` 当前都已提供结构化 `/healthz`，其中 `Api / Auth` 还会补充 JWT / OIDC Issuer 与证书前提明细，适合宿主启动后继续人工分诊具体下游项
  - 当前宿主启动日志也会同步打印关键运行摘要，便于在健康检查前先核对 JWT / OIDC / Gateway 探活模式是否符合当前配置
  - 如需在失败时顺手带出失败宿主的 `/healthz` 关键摘要，可执行 `npm run check:host-runtime -- --details`
  - 如需输出可直接回写到回归记录或 PR 的固定 Markdown 报告，可执行 `npm run check:host-runtime -- --report`
  - 如需把 Markdown 报告直接落到文件，可执行 `npm run check:host-runtime -- --report-file <path>`；当前会自动启用 `--report`，并在必要时自动创建目标目录
  - 当前 `--report` 已拆成 `Summary / Actions` 两段；前者记录事实，后者按 `Gateway / Api / Auth` 与扩展下游条目直接给出下一步建议
  - 当前其 `Summary` 也会固定补 `Route / TriageScope / TriageCode / NextStage` 四个摘要字段，和 `validate:baseline:host` 保持同一套分诊摘要口径
- `validate:identity`
  - 身份语义专题聚合入口，不替代默认 baseline
  - 分别执行运行时散点 Claim 读取扫描与协议输出回退扫描
  - 运行身份语义后端定向测试，覆盖 `ClaimsPrincipalNormalizer`、`HttpContextUser`、`AccountController`、`AuthorizationController`、`UserInfoController`
- `validate:ci`
  - 本地复现当前 `Repo Quality` 的最小执行面
  - 依次运行 `check:repo-hygiene:changed`、`lint:changed`、`validate:baseline:quick`
  - 再按 `check:identity-impact` 的同源规则决定是否追加 `validate:identity`
  - 当前其本地门禁定义也已改为复用同一份 Repo Quality contract，避免 workflow / ruleset / 本地入口继续各自维护
  - 当前也已支持 `--report` / `--report-file <path>`，可把批次级本地门禁结论直接收成固定 Markdown 报告，回写到 `PR -> master` 的回归记录或 PR 描述

## 分层使用建议

### 0. 执行粒度约定

当前仓库的验证与留痕，默认按“开发中 / 准备合并 / 发布部署”三种粒度区分，而不是把同一套重流程压到每一次本地提交：

- 开发中的本地连续提交：
  - 目标是快速发现明显回归
  - 默认只做必要验证，不要求每次都补回归记录
- 准备发起 `PR -> master` 的改动批次：
  - 目标是给出“这一批内容是否具备合并条件”的工程结论
  - 此时再补批次级回归记录、门禁对齐与必要的专题说明
- 正式发布 / 部署 / 回滚：
  - 目标是形成真实可追溯事实
  - 必须保留发布记录、部署后最小复核记录，以及回滚事实或回滚预案

换句话说：

- `commit` 是开发粒度，不等于交付粒度
- 回归记录默认对应“准备合并的一批改动”
- 发布 / 部署留痕默认对应真实环境动作

不要把“每次本地提交都补完整记录”当成默认要求，否则会明显拖慢日常开发效率。

### 0.1 对外 ID 参数额外检查

涉及公开路由、通知 `extData`、窗口参数、分享链接、浏览记录回跳或其他深链参数时，除默认自动化外，当前还应额外人工确认：

- 对外对象 ID 是否仍被解析为前端 `number`
- 大整数 ID 是否在解析、序列化、回写 URL 或透传 app params 时保持原始字符串
- forum 当前主线相关入口若命中上述场景，默认按阻断级回归处理，因为仓库里已经发生过 Snowflake 大整数被截断导致“对象存在但打开失败”的真实故障

### 1. 日常提交前

优先运行：

```bash
npm run setup:hooks
npm run validate:baseline:quick
```

适用场景：

- 前端页面、共享组件、Console 权限链路调整
- 纯文档之外、但又不需要完整后端回归的日常提交前检查
- 开发中的中间态小提交、本地保存点、连续修正批次

补充说明：

- `setup:hooks` 只需首次执行一次；之后提交时会自动运行：
  - `check:repo-hygiene:staged`
  - `lint:staged`
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
- 准备发起 `PR -> master`，需要对这一批改动形成合并判断时

如果准备发起到 `master` 的 PR，再补一轮：

```bash
npm run lint:changed
npm run check:repo-hygiene
```

如果想在本地先复现当前 `Repo Quality` 的最小门禁，也可直接运行：

```bash
npm run validate:ci
```

如果需要把本轮 `Repo Quality` 本地复现结果直接回写到 PR 或批次级回归记录，可执行：

```bash
npm run validate:ci -- --report
```

如果希望报告直接落盘，可执行：

```bash
npm run validate:ci -- --report-file .tmp/validate-ci-report.md
```

如需把当前批次已生成的自动化报告进一步收成一份变更回归记录，可执行：

```bash
npm run collect:change-regression-record -- --title "当前批次" --scope "当前 PR / 改动批次"
```

如果本轮还要一并带上 baseline 或 `M14` 记录，请显式传入对应报告路径，例如：

```bash
npm run collect:change-regression-record -- --title "当前批次" --scope "当前 PR / 改动批次" --baseline-report .tmp/baseline-report.md --host-record .tmp/m14-host-maintenance-record.md
```

说明：

- `master` 当前受规则保护，只允许通过 PR 合并
- 批次级回归记录默认放在这一层补，不要求绑定每一个本地 commit
- GitHub Actions 中的 `Repo Hygiene` 与 `Frontend Lint` 当前仅检查“本次变更文件”，用于先拦新增问题，避免被历史债务拖死
- `Identity Guard` 当前也已改为按变更文件触发：先用 `check:identity-impact` 判定是否命中身份语义影响面，再决定是否执行 `validate:identity`
- 当前 changed-only 入口与 `Repo Quality` 的变更文件收集逻辑已统一复用 `Scripts/collect-changed-files.mjs`
- `check:repo-hygiene:changed` 与 `check:repo-hygiene:staged` 当前也已切到统一 collector，不再单独维护 `git diff` 口径
- `check:repo-quality-contract` 当前会先校验 workflow、ruleset 与本地 `validate:ci` 的 required checks 契约，避免门禁名称或执行面无声漂移
- `check:identity-impact` 的命中范围当前已收口到单一规则源，除身份语义代码、Auth 协议输出、前端 Token 解析与 `AuthFlow` 入口外，也覆盖 `validation-baseline / regression-index / repo-quality-troubleshooting / change-regression-record-template / regression-result-template / dev-first-regression-record / development-plan / planning/current / PR template / repo-quality contract / validate:ci` 等默认执行面文档与门禁资产
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

补充说明：

- 当前默认先从 [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist) 开始，按 `validate:baseline:host -> doctor/verify -> 健康检查/日志 -> 网关与部署链路` 的顺序排障
- 当前主路径已经明确收束为：启动前先跑 `npm run validate:baseline:host`，宿主启动后再跑 `npm run check:host-runtime`
- 不要跳过 `doctor` / `verify` 直接把问题归因到宿主代码或外层反代
- 如果需要把启动前这轮验证直接回写到维护记录、回归记录或 PR，可执行：

```bash
npm run validate:baseline:host -- --report
```

- 如果希望直接把启动前验证报告落盘，可执行：

```bash
npm run validate:baseline:host -- --report-file .tmp/baseline-host-report.md
```

- 如果宿主已经启动，再补一轮：

```bash
npm run check:host-runtime
```

- 这一轮通过后，再继续看更上层的 Gateway / 反代 / OIDC 问题
- 如果需要继续判断 `console` 等扩展下游状态，再直接查看 `https://localhost:5000/healthz`
- 如果默认检查失败，且希望脚本顺手把失败宿主的 `/healthz` 摘要一起打出来，可追加：

```bash
npm run check:host-runtime -- --details
```

- 如果你需要把这轮运行态结论直接回写到维护记录、回归记录或 PR，可追加：

```bash
npm run check:host-runtime -- --report
```

- 如果你希望脚本直接把报告写到文件，而不是从终端复制，可追加：

```bash
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
```

- 如果两段报告都已生成，想把它们直接汇总成一份 `M14` 维护记录，可执行：

```bash
npm run collect:m14-host-record
```

- 默认会读取 `.tmp/baseline-host-report.md` 与 `.tmp/host-runtime-report.md`，并输出 `.tmp/m14-host-maintenance-record.md`

- 如果既要终端分诊，又要固定格式回写，可直接组合：

```bash
npm run check:host-runtime -- --details --report
```

- 如果既要终端分诊，又要把固定格式报告直接落盘，可直接组合：

```bash
npm run check:host-runtime -- --details --report-file .tmp/host-runtime-report.md
```

## 当前仍保留为专题回归的资产

以下内容当前不并入默认自动化脚本，而继续保持专题使用：

- `Radish.Api.Tests/HttpTest/*.http`
- 各专题文档中的最小人工验收顺序
- 需要先启动本地服务的联调链路

专题选择建议见：[专题回归索引](/guide/regression-index)

原因很简单：这些资产依赖运行中的宿主、账号、Token 或人工观察结果，当前阶段更适合作为“专题回归层”，而不是默认本地自动化层。

## 失败时先看哪里

如果你现在面对的是 `Repo Quality`、`validate:ci`、`check:repo-quality-contract`、身份语义条件触发或受限环境边界提示，优先先看：[Repo Quality 故障分诊手册](/guide/repo-quality-troubleshooting)。

- 前端类型错误：优先看对应 workspace 的 `tsc` 输出
- `radish.client` 最小测试失败：优先看 `Frontend/radish.client/tests/`
- 权限扫描失败：优先看 `Scripts/check-console-permissions.mjs` 输出中的四层对齐差异
- 身份语义扫描失败：优先看 `Scripts/check-identity-claims.mjs` 输出中的命中位置，确认是否回退到原始 Claim 解析或直接字符串判断
- 若命中 `AccountController / AuthorizationController / UserInfoController`：优先按 Phase 4 口径确认是否误恢复了历史输出承诺，而不是直接放宽白名单
- 后端构建 / 测试失败：优先看 `Scripts/dotnet-local.ps1` 包装后的 `dotnet` 输出
- `doctor` / `verify` 失败：优先核对当前环境配置、`MainDb` / `Databases` 与关键 `ConnId`
- 如果是 Wiki / 文档链路，额外确认 `doctor` 是否已报告 `WikiDocument.Visibility`、`AllowedRoles`、`AllowedPermissions` 等缺列；旧 SQLite 库需要重新执行 `DbMigrate apply` 触发自动补齐
- `check:repo-quality-contract` 失败：优先回到 workflow / ruleset / 本地 `validate:ci` contract，而不是先改业务代码
- `validate:ci` 失败：优先拆成 `Repo Hygiene changed-only`、`Frontend Lint changed-only`、`Baseline Quick`、条件 `validate:identity` 四类
- 若需要把本轮判断回写到 PR 或维护记录，优先直接复用 `check:identity-impact` 的命中原因类别，以及 `Repo Quality 故障分诊手册` 的失败归类

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

## 受限环境说明

在某些受限 Windows 沙盒中，顶层 shell 可以执行 `npm run ...`，但 Node 脚本内部再次拉起 `node / npm / powershell` 子进程可能被系统直接拒绝。

当前仓库脚本已经尽量减少对 `cmd.exe /c` 的依赖，并统一走共享执行层；但如果仍看到“当前受限环境禁止从 Node 脚本再拉起外部进程”这类提示，说明问题不在业务脚本逻辑，而在当前运行环境的子进程边界。

这种情况下，优先做法是：

1. 直接在外层 shell 执行目标入口，例如 `npm run validate:ci`
2. 或在允许子进程的本机 / 提权环境中执行同一命令

不要把这类失败误判为 `Repo Quality`、`Identity Guard` 或 changed-only 规则本身回归。

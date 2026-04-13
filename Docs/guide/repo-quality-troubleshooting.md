# Repo Quality 故障分诊手册

> 本页用于回答一个维护问题：**当 `Repo Quality`、本地 `validate:ci`、`check:repo-quality-contract` 或受限环境提示失败时，第一时间该把问题归到哪一类、先去哪里看？**
>
> 关联文档：
>
> - [验证基线说明](/guide/validation-baseline)
> - [身份语义防回归回归手册](/guide/identity-claim-regression-playbook)
> - [专题回归索引](/guide/regression-index)

## 1. 先分清是哪一类失败

优先不要把所有失败都笼统归为“CI 挂了”。

当前最常见的分流只有 5 类：

| 你看到的现象 | 实际归类 | 先看哪里 |
| --- | --- | --- |
| `npm run check:repo-quality-contract` 失败 | 门禁契约漂移 | 本页第 2 节 |
| `npm run validate:ci` 失败，但某个子步骤本身报错 | 默认执行面里的具体检查失败 | 本页第 3 节 |
| `validate:ci` 里只在命中身份语义影响面时追加 `validate:identity`，而这一步失败 | 身份语义专题回归失败 | 本页第 4 节 |
| 输出“当前受限环境禁止从 Node 脚本再拉起外部进程”或类似 `EPERM / EINVAL` 提示 | 运行环境边界，不是门禁语义回归 | 本页第 5 节 |
| 后端很多历史 warning、或 Windows 下 DLL 被占用 | 历史噪音 / 本机宿主占用，不等于本轮 `Repo Quality` 回归 | 本页第 6 节 |

如果还没确认属于哪一类，推荐先跑：

```bash
npm run check:repo-quality-contract
npm run validate:ci
```

其中：

- `check:repo-quality-contract` 先回答“门禁定义有没有漂移”
- `validate:ci` 再回答“当前默认执行面里到底是哪一步真的失败了”
- 若需要给 PR、回归记录或维护记录补说明，默认把结论回写成四类之一：`contract 漂移`、`默认执行面失败`、`身份语义专题失败`、`受限环境边界`

## 2. `check:repo-quality-contract` 失败时

这类失败优先怀疑“治理真相源漂移”，不要先去改业务代码。

当前 contract 会校验三层事实：

1. `.github/workflows/repo-quality.yml`
2. `.github/rulesets/master-protection.json`
3. 本地 `npm run validate:ci`

并且不仅校验 required check 名称，还会校验 `repo-quality.yml` 四个 job 的关键命令片段。

### 2.1 常见原因

- workflow job 名改了，但 ruleset required checks 还沿用旧名字
- ruleset required checks 改了，但本地 `validate:ci` 仍在复现旧门禁
- workflow 名字没变，但关键命令片段变了
  - 例如 changed-only 入口不再走统一 collector
  - 或 `Identity Guard` 不再按 `check:identity-impact` 条件触发
- 本地 `validate:ci` 的执行面改了，但 contract 没同步

### 2.2 先看哪些文件

- [repo-quality.yml](/D:/Code/Radish/.github/workflows/repo-quality.yml)
- [master-protection.json](/D:/Code/Radish/.github/rulesets/master-protection.json)
- [repo-quality-contract.mjs](/D:/Code/Radish/Scripts/repo-quality-contract.mjs)
- [check-repo-quality-contract.mjs](/D:/Code/Radish/Scripts/check-repo-quality-contract.mjs)
- [validate-ci.mjs](/D:/Code/Radish/Scripts/validate-ci.mjs)

### 2.3 处理原则

- 如果你改的是门禁定义，必须同时更新 contract 与文档，不要只改 workflow
- 如果你只是想让本地更快通过，不要绕开 contract；先解释为什么 required checks 或命令语义应该变化
- 如果是 `Identity Guard` 触发条件变化，优先回到统一 impact 规则源，而不是在 workflow 里单独补一份路径清单

## 3. `validate:ci` 失败时

`validate:ci` 现在回答的是“本地如何复现当前 `Repo Quality` 的最小执行面”，不是单一脚本。

它当前按顺序执行：

1. `check:repo-hygiene:changed`
2. `lint:changed`
3. `validate:baseline:quick`
4. 仅在命中身份语义影响面时追加 `validate:identity`

因此这类失败必须继续往下拆。

### 3.1 `check:repo-hygiene:changed` 失败

这代表当前变更文件存在文本卫生问题，优先看：

- 编码是否为 UTF-8
- 是否带 BOM
- 换行符是否混乱
- 文件末尾是否缺少换行
- 是否有尾随空格

若怀疑 changed-only 收集范围不对，先运行：

```bash
npm run collect:changed
```

### 3.2 `lint:changed` 失败

这代表前端变更文件里的 `eslint` 规则没通过。

优先确认：

- 当前被 lint 的文件是否确实来自本次改动
- 是否误以为 `lint:changed` 会扫全仓库
- 是否把 `@radish/ui`、`radish.client`、`radish.console` 的历史问题误判成这次新增问题

若怀疑收集范围不对，先运行：

```bash
npm run collect:changed
```

### 3.3 `validate:baseline:quick` 失败

这已经不是单一 `Repo Quality` contract 问题，而是默认基线中的具体子项失败。优先按失败子项分流：

- 前端类型错误：看对应 workspace 的 `tsc` 输出
- `radish.client` 最小测试失败：看 `Frontend/radish.client/tests/`
- `Console` 权限扫描失败：看 `Scripts/check-console-permissions.mjs`
- `Repo Quality contract` 自校验失败：回本页第 2 节
- identity impact 自校验失败：看 `Scripts/check-identity-impact.mjs`
- 身份语义扫描失败：看 `Scripts/check-identity-claims.mjs`

### 3.4 只在命中身份语义影响面时追加 `validate:identity`

若 `validate:ci` 最后多跑了一段 `validate:identity`，说明当前改动已命中统一 impact 规则源，不要先怀疑 workflow 多跑了。

此时先确认两件事：

1. 命中的文件是不是身份语义影响面里的真实入口
2. 失败的是 runtime 守卫、protocol-output 守卫，还是后端定向测试

若需要单独看 impact 判定，可运行：

```bash
npm run check:identity-impact
```

该命令当前会额外输出命中原因类别，记录时默认直接复用：

- `身份运行时入口`
- `Auth 协议输出`
- `官方协议消费者 / Token 解析`
- `默认执行面文档 / 门禁资产`

## 4. 身份语义专题失败时

如果失败已经落到 `validate:identity`、`check:identity-runtime` 或 `check:identity-protocol-output`，说明问题已经不再是通用 `Repo Quality` 故障，而是身份语义防回归本身触发了。

优先看：

- [identity-claim-regression-playbook.md](/D:/Code/Radish/Docs/guide/identity-claim-regression-playbook.md)
- [check-identity-claims.mjs](/D:/Code/Radish/Scripts/check-identity-claims.mjs)
- [check-identity-impact.mjs](/D:/Code/Radish/Scripts/check-identity-impact.mjs)
- [validate-identity-regression.mjs](/D:/Code/Radish/Scripts/validate-identity-regression.mjs)

常见分流：

- 运行时散点 Claim 读取重新出现：优先确认是否回退到了 `FindFirst / ClaimTypes / User.IsInRole / 原始字符串`
- `Radish.Auth` 协议输出守卫失败：优先确认是否误恢复了历史双写承诺
- impact 判定与预期不一致：优先确认默认执行面文档、`AuthFlow`、前端 Token 解析或门禁资产是否被纳入本轮修改

如果这轮还触达了 `Radish.Api.AuthFlow.http`、`userinfo` 或官方协议消费者，自动化之外仍要按身份语义手册补专题回归，不要只看 `validate:ci`。

## 5. 受限环境提示时

如果看到类似提示：

- “当前受限环境禁止从 Node 脚本再拉起外部进程”
- 或 Windows 受限环境下的 `EPERM / EINVAL` 边界提示

优先结论是：**当前运行环境不允许 Node 脚本内部再启动外部进程**，而不是门禁规则本身回归。

当前相关脚本已经统一尽量走共享执行层：

- [process-runner.mjs](/D:/Code/Radish/Scripts/process-runner.mjs)
- [run-with-changed-files.mjs](/D:/Code/Radish/Scripts/run-with-changed-files.mjs)
- [validate-ci.mjs](/D:/Code/Radish/Scripts/validate-ci.mjs)
- [validate-baseline.mjs](/D:/Code/Radish/Scripts/validate-baseline.mjs)
- [validate-identity-regression.mjs](/D:/Code/Radish/Scripts/validate-identity-regression.mjs)

这类情况的正确处理顺序：

1. 先确认外层 shell 能否直接执行目标命令，例如 `npm run validate:ci`
2. 若当前环境本身禁止子进程，改到允许子进程的本机或提权环境验证
3. 不要因为受限环境失败，就直接修改 workflow、contract 或脚本规则

## 6. 历史 warnings 与本机占用

以下情况当前都不应直接归类为 `Repo Quality` 回归：

- `dotnet build` / `dotnet test` 中原本就存在的历史 warnings
- Windows 本机有宿主正在运行，导致 `bin\Debug` 下 DLL 被锁

这两类问题都需要处理，但它们和“required checks 名称漂移”“workflow 语义漂移”“Identity Guard 触发条件漂移”不是一类故障。

处理顺序建议：

1. 先分清是否只是历史噪音
2. 再确认是否影响本轮最小验证结论
3. 不要把所有 warning 或 DLL 锁都回写成 `Phase 5` 门禁资产缺陷

## 7. 维护时的单一真相源

若后续继续调整 `Repo Quality / validate:ci / Identity Guard`，优先把以下文件视为同一组资产一起维护：

- [repo-quality.yml](/D:/Code/Radish/.github/workflows/repo-quality.yml)
- [master-protection.json](/D:/Code/Radish/.github/rulesets/master-protection.json)
- [repo-quality-contract.mjs](/D:/Code/Radish/Scripts/repo-quality-contract.mjs)
- [validate-ci.mjs](/D:/Code/Radish/Scripts/validate-ci.mjs)
- [validation-baseline.md](/D:/Code/Radish/Docs/guide/validation-baseline.md)
- [identity-claim-regression-playbook.md](/D:/Code/Radish/Docs/guide/identity-claim-regression-playbook.md)
- [change-regression-record-template.md](/D:/Code/Radish/Docs/guide/change-regression-record-template.md)
- [regression-result-template.md](/D:/Code/Radish/Docs/guide/regression-result-template.md)
- [PULL_REQUEST_TEMPLATE.md](/D:/Code/Radish/.github/PULL_REQUEST_TEMPLATE.md)

最重要的原则只有一条：

- 不要让 workflow、ruleset、本地入口、默认执行面文档和故障分诊口径再次分叉维护。

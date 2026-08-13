# F4-R R3-C05 Console 仪表与治理派生成组运行态验收

> 日期：2026-08-13（Asia/Shanghai）
>
> 状态：Dashboard、Channel Discoverability、Documents 与 Experience 已完成 Gateway 成组运行态复核；R3-C05 关闭
>
> 范围：本地 migration、宿主健康、种子 Admin / Test、PC / Mobile、双语、URL 状态、权限停止线与 Experience 受控写入

## 1. 结论

`20260813_021_wiki_document_governance` 与 `20260813_022_experience_authoritative_governance` 已在本地 SQLite 幂等应用，strict Doctor 复核全部 schema ready。API、Auth、Gateway、Client 与 Console 经 Gateway 健康检查全部返回 `200`；四个 C05 页面在 PC `1920 × 1080`、Mobile `390 × 844` 和中英文下均可访问，页面宽度没有超过 viewport。

Dashboard 的统计与高频任务路径、Channel 的 URL 筛选与公开资格摘要、Documents 的真实分页与显式治理目标、Experience 的 URL 目标 / 30 天窗口 / 流水 / 治理事件 / 等级预览均消费权威数据。种子 Admin 具备治理表面；种子 Test 进入 Console 时只看到“无 Console 访问权限”停止页，没有治理写入口。

Experience 完成受控 `+1 → -1` 调整和“冻结 → 解冻”复核：总经验最终恢复为 `17`，冻结状态恢复为“未冻结”；调整流水与冻结 / 解冻事件按 append-only 设计保留审计事实。Channel 与 Documents 未执行状态写入，避免为视觉复核制造持久治理事件；其 CAS、幂等和理由草稿继续由代码侧定向与全量回归守卫。

## 2. 运行态发现与根因治理

1. DbMigrate 在旧 schema 上执行 Doctor 时，时间语义审计和既有 Wiki 附件迁移 verifier 读取了包含待新增字段的完整实体，导致 migration apply 前就触发列不存在。两处改为只投影当前审计真正依赖的旧字段，并新增“待迁移字段缺失”回归。
2. 共享 `@radish/ui Button` 未声明默认 `type`，放入 Ant Form 后被浏览器解释为 `submit`，经验调账 / 冻结按钮的异步 handler 因原生表单提交而未发出请求。共享组件统一默认 `type="button"`，调用方仍可显式覆盖为 `submit`，并新增组件契约测试。
3. Console dev 入口在 HMR 后重复 `createRoot`，Experience 初次 URL 目标加载又在 Review Form 尚未挂载时调用 `resetFields`，形成可重复浏览器错误。入口改为通过 `import.meta.hot.data` 复用 React Root，Review Form 只在已有加载目标、已连接时重置。

以上三类问题修复后，以全新浏览器标签重新访问 Experience，稳定态为 `0 warning / 0 error`。

## 3. 浏览器验收矩阵

| 范围 | 结果 |
| --- | --- |
| Dashboard | 权威统计、高频任务路径、Mobile 单列主任务和英文标题通过 |
| Channel Discoverability | `general` URL 筛选、资格摘要、Mobile 频道卡与英文页面通过；未写入频道治理事件 |
| Documents | `E5-B Docs` URL 筛选、显式“设为治理目标”、真实分页、Mobile 卡片和英文页面通过；未写入文档治理事件 |
| Experience | `target=20002`、`stats=30`、6 条流水、2 条治理动作、等级预览、Mobile 与英文页面通过 |
| 权限 | Admin 可见治理能力；Test 被 Console 入口停止页拒绝，页面无横向溢出或浏览器错误 |

## 4. 验证与数据影响

- `Radish.DbMigrate apply` 与 strict Doctor：`_021 / _022` 应用成功，全部 schema ready。
- `npm run check:host-runtime -- --details`：Gateway、API、Auth、Client、Console 均为 `200`。
- DbMigrate 旧 schema 定向回归：`13 passed / 3 skipped`；跳过项为 PostgreSQL 条件测试。
- `@radish/ui`：`32 / 32`；Console：`127 / 127`，类型检查与 production build 通过。
- 后端全量：`1272 passed / 41 skipped`；Console / UI Lint、`git diff --check`、Docs 与 changed hygiene 均通过。
- `validate:baseline:quick` 的类型、HTTP / UI / Client / Console 测试、权限、敏感字面量等门禁均通过，最终只被本批外既有 `Radish.Repository/SystemConfigStorageCoordinator.cs` 三处 `DateTime.Now` 漂移拦截；该项继续留在独立维护线。
- 本地数据库已应用 `_021 / _022`。经验总额与冻结状态已恢复，但 2 条调整流水和 2 条冻结治理事件按审计契约保留；没有修改 Channel / Documents 治理状态，也没有执行等级重算。

## 5. 下一顺位

R3-C05 至此关闭。下一顺位进入 `R3-F02 自服务与边界页` 设计前代码事实审计：反查 Console Settings / Profile、Client / Console 登录、OIDC 回流与 Not Found 的正式路由、身份 / 错误原因、来源返回和 PC / Mobile 承载，先裁决继承与风险拆批，不提前新增认证能力或修改 Pencil。

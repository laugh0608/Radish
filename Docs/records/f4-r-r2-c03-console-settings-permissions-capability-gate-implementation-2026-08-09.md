# F4-R R2-C03 Console 设置与权限矩阵能力门禁实现

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：代码能力门禁已关闭；尚未修改 Pencil，尚未执行运行态 smoke
>
> 正式锚点：`/console/roles`、`/console/roles/:roleId/permissions`、`/console/system-config`、`/console/settings`、`/console/profile`

## 1. 结论

R2-C03 readiness 识别的角色聚合、权限版本、系统设置原子写入和个人资料一致性缺口已经成组关闭。现有 API 路由、权限键、LongId 字符串、注册设置范围与 JSON 产品存储边界保持不变；本批没有开放新的权限种类、High / Critical 设置、动态 key、批量授权、审批或实时刷新。

能力门禁完成后，R2-C03 可以进入代表设计：只需在既有 Console 壳层上确认权限矩阵、内建角色、设置分区、危险确认和必要关键状态，不再用视觉层掩盖弱写入契约。

## 2. 角色与权限聚合

- 角色创建 / 更新改用 `RoleMutationDto + IRoleGovernanceService`，只承载并更新明确业务字段与修改审计。
- `System / Admin` 的 ID、名称和启用状态成为服务端保护身份；改名、停用、删除、同名创建和授权写入均被权威拒绝。
- 新迁移 `20260809_019_console_role_governance` 在 SQLite / PostgreSQL 上固定活动角色名称大小写不敏感唯一性和 `RoleId + ConsoleResourceId` 唯一性，并在写前诊断重复名称、重复关联与内建身份碰撞。
- `/roles/:roleId/permissions` 改由 `roles.view` 放行读取；保存仍要求 `roles.edit`。Mobile 和内建角色只读，角色结构写操作保持 PC-only。
- 授权快照以 Role 的 `ModifyTime / CreateTime` 作为聚合版本。保存先条件更新 Role 版本，再在同一 `Required` 事务中软删除、恢复或新增关联，因此增加、纯删除与清空都推进版本。
- 旧版本返回 `409 RoleAuthorization.VersionConflict`，非法资源返回结构化 `400`；前端保留本地选择，要求人工确认后重新读取。

## 3. 系统设置与个人设置

- `SystemConfigController` 不再把领域校验与版本冲突统一包装为通用 500；非法值、确认缺失和风险拒绝保留稳定 400，CAS 失败返回 `409 SystemConfig.VersionConflict`。
- `SystemConfigStorageCoordinator` 在进程内信号量和跨进程文件锁内检查 `ExpectedVersion`，用恢复日志共同提交覆盖值与审计 JSON；中断后可重放，损坏或空文件按配置错误关闭。
- Medium 设置的风险等级和设置键由操作者显式输入；保存前展示旧值、新值、影响范围和生效方式。Low 通用恢复默认、favicon 上传 / 恢复也复用确认、版本和审计边界。
- 系统设置列表、详情与历史读取失败会清除旧权威数据并提供重试；`409` 保留表单草稿。变更日志 LongId 与操作人 LongId 在前端保持字符串。
- `/settings` 的“恢复默认”文案收窄为真实的“恢复系统时区”。`/profile` 将展示名、其他资料和展示名审计收进一个事务命令，动态长度规则继续由服务端设置提供；加载失败可重试，后段写入失去目标时抛错回滚。

## 4. PC / Mobile 与关键状态

- PC 保留现有角色表、权限矩阵与系统设置工作面；角色启停、删除和权限保存增加明确确认或变更摘要。
- Mobile 角色列表改为无横向表格依赖的紧凑行，只开放权限查看；权限矩阵只读，系统设置只允许编辑 Low，Medium 保持 PC-only。
- 只读 Operator、内建保护、dirty 离开、并发 409、unavailable / stale、空历史与显式 Medium 确认均已有真实代码状态。
- R2-C03 不新增全屏任务或新的移动壳层，继续复用 `AdminLayout` 的 Console 五项入口。

## 5. 验证

- `dotnet test Radish.Api.Tests --no-restore`：通过 `1206`，跳过 `39` 个未配置 PostgreSQL 环境的集成测试。
- `npm run test --workspace=radish.console`：`78 / 78` 通过。
- `npm run type-check --workspace=radish.console`：普通与 strict TypeScript 检查通过。
- `npm run build --workspace=radish.console`：production build 通过。
- `npm run check:console-permissions`：路由、权限常量、资源映射和种子四层对齐通过；仅保留既有 `console.hangfire.replay` 未引用警告。
- `dotnet build Radish.slnx -c Debug --no-restore`：`0 warning / 0 error`；`npm run validate:identity` 通过身份 Claim、协议输出、LongId 与 31 项定向测试。
- `npm run lint:changed`、`npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。
- SQLite 定向迁移测试覆盖迁移重复执行、大小写角色名唯一性、角色资源唯一性、重复名称与内建身份碰撞诊断。

## 6. 下一步与停止线

下一步是在唯一活动 `.pen` 中完成 R2-C03 局部代表设计，确认 PC `1440`、Mobile `390` 和必要关键状态；该动作需要当前任务明确的 Pencil 授权。设计确认后再按代表设计收口正式页面，不提前推进其他 R2 / R3 页面。

本批未启动服务或浏览器，未执行 Gateway smoke，也未修改 Pencil。运行态验收继续放在 R2-C03 成组功能准备验收时执行。

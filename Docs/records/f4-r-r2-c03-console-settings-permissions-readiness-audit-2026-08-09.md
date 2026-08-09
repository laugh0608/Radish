# F4-R R2-C03 Console 设置与权限矩阵设计前代码事实与能力覆盖审计

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：审计完成；R2 分级保持不变，进入代码能力门禁，尚未修改 Pencil
>
> 正式锚点：`/console/roles`、`/console/roles/:roleId/permissions`、`/console/system-config`、`/console/settings`、`/console/profile`

## 1. 结论

`R2-C03 / Console 设置与权限矩阵` 仍适合以 R2 局部设计推进，不需要升级为新的完整 R1 页面类型：现有 `AdminLayout`、Console 五项移动入口、`R1-C01` 的高密度资源表面和 `R1-C02` 的权限 / 冲突状态已经提供了足够的壳层与视觉继承来源，本专题只需确认权限矩阵、设置分区、危险确认和 Mobile 停止线。

但当前代码尚不能直接进入 Pencil。审计发现的阻断不是一般视觉债，而是治理承诺与真实能力不一致：

- `roles.view` Operator 可以进入角色列表，但权限详情路由要求 `roles.edit`，页面已经实现的只读矩阵实际上不可达；
- `System / Admin` 的角色名会触发默认全权限，但角色 CRUD 没有内建角色保护、保留名与唯一性约束，允许改名、停用、删除或创建同名角色；
- 角色授权用“当前未删除关联的最大修改时间”作为版本，纯删除或清空授权不能得到可靠单调版本，读取—比较—多次写入也没有原子事务；
- 角色授权冲突抛出普通异常，系统设置控制器又把校验、风险拒绝和版本冲突统一包装为 `500 System.UnexpectedError`，因此文档中的结构化 `409` 目前没有真实兑现；
- 系统设置的配置覆盖值和变更审计写入两个 JSON 存储边界，`ExpectedVersion` 也是先读后写，不能保证并发 CAS、配置与审计共同成功；
- Medium 设置的前端确认参数由程序自动填充，没有证明操作者真的确认风险；通用恢复默认和变更摘要也没有形成一致交互；
- `/settings` 已有真实时区与密码写入，`/profile` 已有真实资料与头像写入，不应继续描述为主要占位；但资料整组更新仍可能部分提交，失败加载也缺少明确不可用状态。

因此下一步不是画 R2-C03，也不是做 CSS，而是先完成一组成组代码能力门禁。门禁确认并关闭前，不修改 `.pen`，不把当前弱契约包装成“危险操作已安全确认”。

## 2. 审计范围与继承裁决

### 2.1 正式 Web 范围

| 页面族 | 当前真实能力 | R2-C03 责任 |
| --- | --- | --- |
| Roles | 查询、创建、编辑、启停、删除、部门与数据范围 | 固定内建角色边界、读写权限表面和危险操作确认 |
| Role Permissions | 资源树、角色授权快照、预览、保存 | 固定只读矩阵、变更摘要、冲突与离开保护 |
| System Config | 注册设置读取、Low / Medium 编辑、历史、恢复默认、favicon | 固定风险分区、显式确认、原子审计和 Mobile 停止线 |
| Settings | 语言、真实时区更新、真实登录密码修改；其余能力显式禁用 | 继承个人设置表单，不扩展 Console 权限模型 |
| Profile | 真实资料更新、头像上传 | 继承个人资料表单，闭合失败与整组写入一致性 |

`Applications`、Hangfire、`theme-test`、新增设置平台、High / Critical 编辑、任意动态 key 创建与其他 Console 资源 CRUD 不进入本批。

### 2.2 代表身份

- PC 主代表：具备 `console.roles.view/edit` 与 `console.system-config.view/edit` 的受限 Console 管理员，不依赖 `System / Admin` 绕过权限。
- 只读关键状态：只有 `console.roles.view` 与 `console.system-config.view` 的 Operator。
- 保护关键状态：查看内建 `System / Admin` 角色的管理员；角色身份受保护，授权语义不可伪装为普通可编辑角色。
- Mobile 代表：同一受限管理员在 `390px` 下进入“权限”或“更多”，沿用 Console 五项真实入口与 Client 胶囊导航视觉。

## 3. 角色与权限矩阵事实

### 3.1 已具备的基础

- 前后端已有 `roles.view / create / edit / toggle / delete` 与资源映射，LongId 角色 / 资源 ID 在权限矩阵前端链路中保持字符串。
- `RolePermissionPage` 已有稳定排序、脏状态、重复保存禁用、预览和只读文案基础。
- 后端读取允许 `roles.view` 或 `roles.edit`，保存只允许 `roles.edit`，这一资源边界本身合理。

### 3.2 必须先关闭的能力缺口

1. **只读路由不可达**：`routeMeta.ts` 对 `/roles/:roleId/permissions` 要求 `roles.edit`，与服务端读取边界和页面只读实现不一致。路由应按 `roles.view` 放行，写入口继续按 `roles.edit` 双重守卫。
2. **内建角色缺少权威保护**：`System / Admin` 名称会在权限解析中获得全部默认权限，普通 CRUD 却可修改、停用、删除或创建同名角色。服务端必须固定保留名、唯一性和内建角色不可变边界，前端只能呈现权威结果，不能只隐藏按钮。
3. **通用 Role 更新边界过宽**：当前更新以通用 `RoleVo` 映射实体，可能把请求未承载的审计 / 删除字段写回默认值。应改为明确命令 DTO 与 Service 领域更新，不继续以通用实体更新承接治理写入。
4. **授权版本不可靠**：活动关联最大修改时间不能覆盖纯删除 / 清空；读取比较与多次软删除、恢复、插入也不是单一原子操作。必须使用单调聚合版本和事务内的条件更新 / 替换。
5. **错误契约不成立**：并发冲突当前可能成为通用 500。必须提供稳定错误码与 `409`，并保留用户当前选择以供刷新对比；非法角色、保留名、非法范围等使用结构化 400 或明确领域状态。
6. **危险动作与离开保护不一致**：权限保存、角色启停 / 删除应有按风险分层的变更摘要与确认；脏矩阵离开、刷新或返回必须明确提示，不能静默丢弃。

现有基于 `roleId + resourceId + index` 的派生关联 ID 也不应继续承担聚合唯一性。能力门禁应由稳定主键 / 唯一约束和原子聚合写入保证身份，不以列表顺序制造持久化身份。

## 4. 系统设置事实

### 4.1 已具备的领域意图

- 设置由代码注册定义，未知 key 不作为运营设置开放；JSON 只保存覆盖值。
- 当前 Low / Medium 设置已有类型、范围、默认值、版本、原因、确认字段和脱敏审计模型。
- High / Critical、未知设置、不可编辑设置已在 Service 层拒绝；历史查询、favicon 上传和恢复默认入口已经存在。
- `SystemConfig` 注册表 ID 是受控小整数，不属于 Snowflake LongId；变更日志 ID 与操作人 ID 仍属于 LongId，前端契约应改为字符串。

### 4.2 必须先关闭的能力缺口

1. **结构化错误**：控制器不得把值校验、风险拒绝、确认缺失和版本冲突全部包装为 `500 System.UnexpectedError`。至少区分合法性 / 风险拒绝与并发 `409`，保持稳定 code / messageKey。
2. **原子 CAS**：`ExpectedVersion` 必须在持久化写入点参与条件更新，不能只在 Service 先读后比；同一设置的并发写入只能有一个成功。
3. **配置与审计共同提交**：覆盖值变化和变更日志必须形成一个可恢复的原子边界；继续使用现有 JSON 存储时也要提供仓储级协调、锁与失败恢复，不能留下“配置已变、审计未写”的状态。
4. **真实人工确认**：Medium 变更必须让操作者显式确认风险等级与设置 key，前端不能自动代填确认值。提交前展示旧值—新值、影响范围、生效方式和原因。
5. **恢复默认一致性**：所有允许恢复默认的设置复用同一风险、原因、版本与审计契约；favicon 不保留无确认特例。
6. **失败与草稿保持**：`409`、权威读取失败和历史失败要有可区分状态；冲突时保留草稿，提供重新读取与人工比较，不自动覆盖。

本批保持现有 API 路由、注册设置范围和存储产品边界；是否把 JSON 迁移到数据库属于另一个架构决策，不能借视觉批隐式扩张。

## 5. Settings / Profile 边界校正

### 5.1 `/settings`

该页不是纯占位：语言使用正式 i18n 持久化，时区调用 `GetMyTimePreference / UpdateMyTimePreference`，密码调用 `ChangeMyLoginPassword`。通知、主题选择、分页、2FA 和会话超时当前是明确禁用的未来项，本批不得把它们设计成可用能力。

能力门禁只需校正真实范围：把“恢复默认设置”收窄为真实的时区恢复语义，保持密码独立显式确认，不新增系统级配置或权限。

### 5.2 `/profile`

资料与头像均有真实接口，但当前显示名更新和其余资料更新可能分段提交，失败后会出现部分成功；加载失败后页面也缺少可重试的 unavailable 状态，显示名长度规则与服务端动态设置存在漂移。

能力门禁应让一次资料提交具备明确原子语义，或把不同写入拆成用户可理解的独立命令；显示名校验服从服务端当前规则，加载失败可重试。该修正不把 Profile 纳入 `console.*` 权限，它仍是登录用户自己的设置。

## 6. PC / Mobile 设计输入

能力门禁关闭后，R2-C03 只需在唯一活动设计源中维护以下关键区块 / 状态，不复制五个完整路由：

### 6.1 PC

1. 权限矩阵：角色摘要、按资源域分组的矩阵、只读 / 可编辑状态、变更摘要、保存确认和 `409` 对比。
2. 内建角色：明确“系统保护角色”状态，不呈现实际无效或服务端会拒绝的普通角色动作。
3. 系统设置：紧凑设置行、风险 / 当前值 / 默认值 / 生效方式、编辑区、历史与恢复默认。
4. 危险确认：Medium 的旧值—新值、影响范围、原因、显式 risk / key 确认；Low 使用更轻但仍可追溯的确认。

### 6.2 Mobile 390

- 继续使用 `AdminLayout` 的 Console 五项真实入口和 Client 胶囊导航视觉；Roles 归“权限”，System Config / Settings / Profile 归“更多”。
- 权限矩阵在 Mobile 只读，以分组列表 / 摘要呈现，不开放角色 CRUD、启停、删除或授权保存。
- System Config 在 Mobile 默认只读并允许查看历史；仅 Low 设置可以在同一明确版本、确认与审计契约下编辑，Medium 保持 PC-only。
- 个人 Settings / Profile 可以保留其真实低风险自服务写入，不把它们误判为 Console 权限管理动作。
- R2 不新增隐藏全局导航的全屏任务，也不建立新的移动壳层；若后续设计证明必须改变该交互模型，停止并升级为 R1 重新裁决。

## 7. 必要关键状态

- View-only Operator：矩阵与设置可读，所有写入口消失且 handler / 服务端继续拒绝写入。
- Protected built-in role：内建身份、保留名与不可变原因清楚可见。
- Dirty / unsaved：保存前有变更摘要，离开前提示。
- Concurrent `409`：本地草稿保留，展示权威版本变化并允许重新读取。
- Unavailable / stale：列表、详情、矩阵或历史读取失败时不继续展示为最新权威状态。
- Medium confirmation：原因、旧值—新值、影响、生效方式、risk / key 由操作者显式确认。
- Empty permissions / no overrides / no history：真实空态与加载失败分开。

## 8. 代码能力门禁顺序

1. **角色聚合安全**：内建 / 保留角色、唯一性、命令 DTO、服务端字段与范围校验、只读路由可达和 handler 守卫。
2. **权限矩阵一致性**：单调版本、事务内 CAS 聚合替换、稳定结构化 `409`、脏状态离开保护和并发 / 纯删除 / 清空定向测试。
3. **系统设置一致性**：结构化 400 / 409、存储点原子 CAS、配置与审计共同提交、LongId 字符串契约、显式确认和通用恢复默认。
4. **个人设置真实性**：更新权限覆盖矩阵；校正时区恢复文案、Profile 原子 / 分命令写入、动态显示名校验和 unavailable 状态。
5. 通过定向后端测试、Console 测试、类型检查、静态构建与仓库卫生门禁后，再提交能力门禁记录并请求 Pencil 授权。

## 9. 测试与验收口径

能力门禁至少覆盖：

- 保留名重复、内建角色改名 / 停用 / 删除拒绝，普通角色合法更新不破坏审计字段；
- `roles.view` 可读取矩阵但不能写，直接调用写接口仍返回 403；
- 授权增加、纯删除、清空都产生单调版本；同版本并发只有一次成功，失败为结构化 409，事务失败不产生部分矩阵；
- 系统设置非法值 / 确认缺失 / 风险拒绝 / 版本冲突分别具有稳定错误契约；并发只有一次成功，配置与审计同成同败；
- Medium 确认值来自用户显式输入，恢复默认复用同一版本 / 审计边界；
- Profile 整组失败不产生不可解释的部分提交，动态显示名边界和加载失败重试通过；
- PC / Mobile 路由、只读入口、LongId 字符串、无横向表格依赖与现有 Console 导航回归。

真实 Gateway PC / Mobile smoke 仍放在专题成组功能准备验收时执行；本 readiness 不启动服务或浏览器。

## 10. 停止线

- 不新增权限种类、日期范围、排序、批量授权、导入 / 导出、审批流或实时刷新。
- 不开放 High / Critical 设置，不新增任意 key 管理，不把本地环境配置搬进 Console。
- 不把 Mobile 变成危险权限管理终端；Medium 设置、角色结构和授权写入保持 PC-only。
- 不修改 Pencil，直至代码能力门禁完成并获得当前任务明确授权。
- 不提前推进 R3 Applications、Products、Users 等页面，也不进入 `R2-P03 / R2-W02 / R2-A02`。

## 11. 主要证据

- Console：`routeMeta.ts`、`RoleList.tsx`、`RolePermissionPage.tsx`、`SystemConfigList.tsx`、`SystemConfigForm.tsx`、`Settings.tsx`、`UserProfile.tsx`、`AdminLayout.tsx`
- HTTP：`@radish/http` 的 role、console authorization、system config、user profile / preference 契约
- API / Service：`RoleController`、`ConsoleAuthorizationController / Service`、`SystemConfigController / Service`、`UserController`
- Model / Repository：`RoleVo`、`RoleAuthorizationSnapshotVo`、`SystemConfigDto / Vo`、角色资源关联仓储、SystemConfig JSON 覆盖值与变更日志仓储
- 现行文档：[F4-R 代表页审计](/frontend/f4-r-representative-page-audit)、[Console 权限治理](/guide/console-permission-governance)、[权限覆盖矩阵](/guide/console-permission-coverage-matrix)、[系统设置治理](/guide/system-settings-governance)

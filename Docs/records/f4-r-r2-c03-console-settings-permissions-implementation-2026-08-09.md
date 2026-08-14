# F4-R R2-C03 Console 设置与权限矩阵正式实现

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：确认设计后的正式代码、静态验证与 Gateway PC / mobile 运行态验收已完成，R2-C03 已关闭
>
> 范围：`/console/roles`、`/console/roles/:roleId/permissions`、`/console/system-config` 的 PC / mobile 代表差异、双语资源与定向契约测试；未修改 Settings / Profile 自服务实现、API、权限、URL、LongId、CAS、结构化错误、事务或存储边界

## 1. 结论

- PC 角色授权保留既有资源树、API 预览和原子 CAS 写入，新增明确的只读状态与内建角色保护说明；只读 Operator 和 `System / Admin` 均不渲染保存授权入口，角色列表既有重命名、启停和删除保护保持不变。
- PC 系统设置保留现有紧凑表格、详情表单与变更历史；Low 保存增加旧值 / 新值轻量确认，Medium 继续要求风险等级和完整 key 人工复核，编辑面显式展示 dirty、CAS 版本、`409` 草稿保留与重新读取确认。
- Mobile 角色目录、角色权限详情和系统设置分别作为独立页面内容实现，继续由既有 `AdminLayout`、Console 五项入口和共享胶囊导航承载，没有增加外部说明页、多页面合并容器或新移动壳层。
- Mobile 角色与权限保持只读；权限项按服务端真实资源树分组，同时显示技术 key、服务端标题 / 已知本地化含义和允许状态。Mobile 系统设置只允许 Low 项进入共享 `BottomSheet`，Medium 在列表和表单提交两层均失败关闭为 PC-only。

## 2. 页面与状态实现

### 2.1 Roles / Permissions

- Mobile 角色页使用连续目录、角色范围、启用状态、内建标记与唯一“查看权限”路径；成功空集合与列表 unavailable 分开表达。
- Mobile 权限详情始终以已保存授权快照渲染，避免 PC 未提交草稿在响应式切换后伪装为已授权状态；返回与刷新仍服从 dirty 放弃确认。
- PC / Mobile 内建角色均解释不可改名、禁用、删除或保存授权；只读账号不通过 disabled 写按钮暗示可操作能力。
- 权限 key 含义对七项 R2-C03 代表权限提供双语精确说明，其他真实资源使用服务端标题回退；没有新增、改名或复制权限种类。

### 2.2 System Config

- Mobile 列表保留真实搜索、分类、读取错误、历史与筛选空态；Low 项提供编辑入口，非 Low 项只显示 PC-only 边界。
- Low 编辑复用共享 `BottomSheet`，展示当前值、有效约束、影响范围、CAS 版本、修改原因和未保存差异；关闭、重新读取及浏览器离开均保护草稿。
- Low 提交前显示旧值 / 新值确认；Medium 继续使用既有显式风险等级和完整 key 校验。即使在 PC 打开 Medium 后切换窄屏，移动表单和提交 handler 也会双重阻止写入。
- `SystemConfig.VersionConflict` 仍以结构化 `409` 识别并保留本地表单值；重新读取权威值前需要明确放弃草稿。

## 3. 保持不变的契约与停止线

- 角色、资源、设置和审计标识继续按既有 LongId wire contract 传递；未引入 JavaScript `Number` 转换或新 URL 状态。
- 角色授权继续使用现有资源聚合版本和原子 CAS；系统设置继续携带 `expectedVersion`，并由现有配置—审计共同事务提交。
- 未新增 API、权限、批量授权、审批流、High / Critical 设置、任意动态 key、实时刷新或新存储。
- `/console/settings` 与 `/console/profile` 保持现有 `authOnly` 自服务边界，没有纳入 `console.*` 权限或新增能力。

## 4. 自动化与静态验证

- `npm run test --workspace=radish.console`：`83 / 83` 通过；新增五项 R2-C03 代表契约，守卫 Mobile 只读任务流、权限 key 含义、内建 / Operator 无保存表面、Low-only `BottomSheet` 与 dirty / CAS / `409` / Medium 确认。
- `npm run type-check --workspace=radish.console`：普通与 strict TypeScript 检查通过。
- `npm run lint:changed`：通过。
- `npm run build --workspace=radish.console`：production build 通过。
- `npm run check:console-permissions`：通过；前后端 `72` 项权限常量、路由、资源映射与种子保持对齐，既有 `console.hangfire.replay` 未使用警告不属于本批。
- `./scripts/check-docs.sh`、`npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。

## 5. Gateway 运行态验收

- 获得当前任务明确授权后，通过 Gateway `https://localhost:5000/console/` 完成 PC `1440 × 900` 与 Mobile `390 × 844` 复核；没有提前访问或推进其他 R2 / R3 页面。
- PC 角色列表、`System / Admin` 内建角色保护、内建权限矩阵和自定义角色 dirty / 离开保护通过；内建角色保留 `72` 项只读复选框且全部禁用，不渲染保存入口。为避免改写共享权限数据，只读 Operator、授权 `409 / stale` 继续由权限与 CAS 定向测试守卫。
- PC 系统设置 `25` 项真实注册项正常读取；Low 表单覆盖 key、当前值、约束、影响、原因、CAS 与旧值 / 新值确认，Medium 表单继续要求风险等级和完整 key。运行态发现初始 `config` 尚未装载时错误解引用 `voRiskLevel` 的根因并修正，同时新增空值挂载回归守卫；修正后 production build 通过。
- Mobile 三个独立页面均复用既有 `AdminLayout` 与“总览 / 治理 / 交易 / 权限 / 更多”五项 Client 胶囊导航，宽度固定为 `390px` 且无横向溢出。角色目录展示三个真实角色；内建权限页无复选框并显示 key 含义；系统设置仅 `5` 个 Low 可编辑，`20` 个 Medium 均为禁用 `PC-only`。
- Mobile Low `BottomSheet` 覆盖 dirty、放弃与旧值 / 新值确认，取消后没有产生配置写入；不存在的 LongId 权限 URL 呈现独立 unavailable，设置搜索无匹配呈现真实空结果。稳定态干净页签无 `warning / error`。
- 本轮未保存角色授权或系统设置；验收后 `Test` 角色活动资源关系仍为 `0`。服务已停止，并精确删除本轮登录新增的 `1` 条 OpenIddict authorization 与其 `13` 条 token；删除前备份位于 `/private/tmp/radish-r2c03-openiddict-before-cleanup-20260809-1848.db`，清理后数据库 `quick_check` 为 `ok`。

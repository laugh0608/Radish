# P3-12-D22 Console 复杂页面类型边界评估记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`radish.console` 角色权限、内容治理、经验治理和运维任务页的只读代码盘点与页面类型判断。

## 结论

本批不进入复杂页面代码迁移，只确认 D21 后的下一批边界。D14-D21 已覆盖普通表格、商业表格、文档治理首屏、标签 / 分类和贴纸类列表；继续推进时不能把权限矩阵、治理动作台或运维任务页直接套成普通 CRUD 表格。

下一批建议优先进入 `P12` 角色权限首批语义迁移：先迁移 `RoleList` 的页头、指标和工具条，再迁移 `RolePermissionPage` 的角色信息、资源树和权限预览外层结构。该批次只调整 Console 语义组件与布局，不改变角色 API、授权资源树、勾选继承、保存载荷、权限键预览或路由守卫。

内容治理和经验治理已经有工作台结构，适合在角色权限之后按 `P02 / P03` 做治理工作台语义收口；运维任务当前只有 `SystemConfigList` 与 `/hangfire` iframe 外壳，暂不扩展为项目内任务调度平台。

## 页面归属

| 页面 / 入口 | 代码入口 | 当前结构 | 类型判断 | 下一步 |
| --- | --- | --- | --- | --- |
| 角色管理 | `Frontend/radish.console/src/pages/Roles/RoleList.tsx` | 角色表格、启停、删除、权限配置跳转、权限摘要侧栏 | `P12` 权限矩阵入口，外层仍是旧 `adminFeature` 结构 | D23 首批迁移候选 |
| 角色权限配置 | `Frontend/radish.console/src/pages/Roles/RolePermissionPage.tsx` | 资源树、权限键 / 接口映射预览、已生效快照、保存动作 | `P12` RBAC 权限矩阵主体，不是普通列表页 | D23 首批迁移候选 |
| 应用管理 | `Frontend/radish.console/src/pages/Applications/Applications.tsx` | OIDC 客户端表格、创建 / 编辑、删除、重置密钥 | 系统 / 身份客户端管理，含敏感密钥动作，不并入角色权限矩阵 | 角色权限后单独按系统工具页评估 |
| 内容治理 | `Frontend/radish.console/src/pages/Moderation/ModerationPage.tsx` | 举报队列、手动治理动作、治理动作日志、审核弹窗 | `P02` 治理工作台 | 角色权限后推进外层语义组件与 token 收口 |
| 经验治理 | `Frontend/radish.console/src/pages/Experience/ExperienceAdminPage.tsx` | 用户查询、观察摘要、流水、复核、调经验、冻结、等级配置 | `P03` 经验台账工作台 | 内容治理同批或后一批推进 |
| 胡萝卜管理 | `Frontend/radish.console/src/pages/Coins/CoinAdminPage.tsx` | 用户余额查询、管理员调账、交易流水、订单回跳 | `P10` 商业 / 资产运营页，含写操作 | 不作为 D22 后第一顺位 |
| 系统设置 | `Frontend/radish.console/src/pages/SystemConfig/SystemConfigList.tsx` | 设置定义、低风险编辑、历史、favicon 恢复 | `P13` 运维工具代表页 | D21 已收口，维护线 |
| 定时任务 | `Frontend/radish.console/src/router/routerComponents.tsx` `HangfirePage` | 外部 Hangfire iframe | `P13` 运维任务入口，但不是项目内任务队列页面 | 暂不扩展任务平台；后续只做外壳视觉收口或另开专题 |

## 代码盘点

- `Frontend/radish.console/src/router/routeMeta.ts` 已把相关入口放入真实分组：角色、内容治理、经验等级属于 `governance`；系统设置、应用管理、定时任务属于 `system`；胡萝卜管理属于 `commerce`。
- `RoleList.tsx` 约 `353` 行，仍使用 `admin-feature-header / admin-feature-metrics / admin-table-layout`，已有权限摘要侧栏，迁移风险较低。
- `RolePermissionPage.tsx` 约 `421` 行，已经拆出 `rolePermissionHelpers.ts`，主结构是资源树与权限预览双栏。迁移时应保留递归资源节点和保存禁用逻辑，不新增授权模型。
- `ModerationPage.tsx` 约 `918` 行，已拆出 columns、helpers、renderers 和手动治理动作区，主体是 `governance-workbench` 三段结构。下一步应优先替换页头 / 指标 / 状态 chip 和硬编码色，不改变审核与治理动作 API。
- `ExperienceAdminPage.tsx` 约 `712` 行，已拆出观察摘要、流水、复核、动作表单和等级配置模块。下一步应先处理页头、指标、inline 样式和区块层级，不把等级配置混进人工复核动作区。
- `HangfirePage` 当前是 `iframe` 包装，不承载项目内任务失败重试、配置覆盖和运行审计模型；若要实现 `P13` 完整运维任务页，需要单独设计任务数据来源和 API 边界。

## 停止线

- 不在 D22 中改角色、授权、治理、经验、资产或任务 API。
- 不新增项目内定时任务平台，不替代 Hangfire Dashboard。
- 不把内容审核、经验复核、调账、授权保存等高风险动作合并进普通表格工具条。
- 不执行 Gateway PC / mobile smoke；本批为只读代码盘点与文档判断，验证以文档卫生和差异检查为主。

## 下一步

1. `P3-12-D23`：优先迁移角色权限 `P12` 外层语义结构，覆盖 `RoleList` 与 `RolePermissionPage`。
2. 完成 D23 后再评估 `P02 / P03` 内容治理与经验治理工作台外层语义收口。
3. `P13` 运维任务页仅在确认是否需要项目内任务队列后推进；否则只做 Hangfire 外壳视觉一致性修正。

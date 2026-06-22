# P3-12 B4 / D1 阶段运行态 Smoke 记录

> 日期：2026-06-22（Asia/Shanghai）
>
> 状态：通过，带边界说明
>
> 结论：B4 文档作者态、Console 文档治理和消息复访入口在本地 Gateway 下通过本轮 PC / mobile 运行态 smoke；`/messages` 是纯 Web 私域消息复访入口，不等于完整聊天平台迁移完成。若正式版必须包含完整聊天平台，应在进入 UI 视觉实现前新增 `P3-12-B5` 聊天 Web 化设计；若接受完整聊天平台继续后置，则可进入 `P3-12-D` 统一 UI 设计专题。

## 前置条件

用户已明确说明前后端已启动，本轮按 Gateway 真实页面路径执行 smoke。

环境：

- Gateway：`https://localhost:5000`
- API：`http://localhost:5100`
- Auth：`http://localhost:5200`
- PC 视口：`1920x1080`
- Mobile CSS 视口：`390x844`

运行态健康检查：

```bash
npm run check:host-runtime -- --details
```

结果：

- Gateway `/health`：200
- API `/health`：200
- Auth `/health`：200

## 覆盖范围

### 公开文档

| 路径 | 结果 | 备注 |
| --- | --- | --- |
| `/docs` | 通过 | PC / mobile 均加载公开目录，保持只读提示和公开壳层 |
| `/docs/search` | 通过 | 搜索页保持只读边界，不带出治理动作 |
| `/docs/adr-0001-branch-and-pr-governance` | 通过 | 详情页加载公开正文，明确编辑、发布、版本和私域动作不从公开路由打开 |

### 文档作者入口

| 路径 | 结果 | 备注 |
| --- | --- | --- |
| `/docs/mine` | 通过 | 未登录进入统一登录；登录后返回文档作者台 |
| `/docs/compose` | 通过 | 登录态显示新建文档表单，提示发布、归档和权限治理留在 Console |
| `/docs/revisions/:id` | 通过 | 版本回看可加载，不在作者入口执行回滚治理动作 |
| `/docs/edit/:id` | 通过 | 内置文档直开编辑路由时显示只读保护提示 |

### Console 文档治理

| 路径 / 动作 | 结果 | 备注 |
| --- | --- | --- |
| `/console/documents` 未登录 | 通过 | 进入 `radish-console` 登录 / 授权流程 |
| Console 授权 | 通过 | 本地授权确认页无控制台 warning/error；同意后进入 Console |
| `/console/documents` 登录后列表 | 通过 | `AdminGetList` 返回 200，列表显示状态、可见性、来源、版本和动作 |
| 第一条文档详情 | 通过 | `AdminGetById/:id?includeDeleted=true` 返回 200，详情弹层显示只读正文和访问策略摘要 |
| 第一条版本治理 | 通过 | `GetRevisionList/:id` 与 `GetRevisionDetail/:revisionId` 返回 200，版本弹层显示当前版本内容 |
| mobile 视口列表加载 | 通过 | 页面可加载，`AdminGetList` 返回 200；本轮不把后台表格动作窄屏点击作为完整操作验收 |

本轮 smoke 暴露并已修复一个 Console 运行态噪声：Ant Design `Space direction` 已废弃，浏览器将其记为 console error。已将 Console 页面中的 `Space direction` 机械迁移为 `Space orientation`，`npm run type-check --workspace=radish.console` 通过，刷新 `/console/documents` 后 warning/error 为 0。

观察到一个非阻断后续项：首次从 `/console/documents` 进入 `radish-console` consent 后回到 `/console/` 仪表盘；授权完成后再次直开 `/console/documents` 可以进入治理页。后续如要提升 Console 深链体验，应单独检查 `returnTo` 保留。

### 消息 / 聊天边界

| 路径 | 结果 | 备注 |
| --- | --- | --- |
| `/messages` 未登录 | 通过 | 进入统一登录，保留私域入口边界 |
| `/messages` 登录后 | 通过 | PC / mobile 均能加载频道、消息、输入区和成员信息 |
| `/desktop?app=chat` | 通过 | WebOS 历史聊天室入口仍可访问 |

边界说明：

- `/messages` 当前承接纯 Web 私域消息复访、会话 / 消息定位、登录恢复和公开个人页来源返回。
- 完整聊天平台、私聊、搜索、Reaction、移动系统通知和设备级会话治理仍按既有规划后置。
- 因此不能把“完整聊天室已迁移完毕”写作本轮结论。

## 本轮未覆盖

- 没有执行发布、下架、归档、恢复、删除、权限保存、导入、导出或回滚等写操作。
- 没有执行完整 `validate:baseline`、`validate:identity` 或发布候选总回归。
- Mobile 仅记录 CSS 视口 `390x844`，未声明 DPR 3 物理高分屏结论。
- 没有验证完整聊天平台后置功能。

## 阶段判断

在“完整聊天平台继续后置”的产品边界下，本轮未发现阻断进入 `P3-12-D` 统一 UI 设计专题的问题。

如果正式版产品范围要求完整聊天室不再依赖 WebOS，则不应直接进入 UI 视觉实现，应先补 `P3-12-B5` 聊天 Web 化设计，明确 `/messages` 与完整聊天平台的能力边界、路由、权限、实时连接、移动视口和验证口径。

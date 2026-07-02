# P3-12-D45 移动响应式抽样记录

## 基本信息

- 日期：`2026-07-02`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 性质：P3-12-D UI 专题内的 Gateway 移动 CSS 视口抽样复核
- 范围：public / private / author / console 已落地页面的导航、主动作、表格 / 列表、抽屉 / 弹层和只读权限态

## 输入依据

- [当前进行中](/planning/current)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-D41 页面开发缺口源码核对记录](/records/p3-12-d41-page-development-gap-source-audit-2026-07-01)
- [P3-12-D44 Console 深层管理动作复核记录](/records/p3-12-d44-console-deep-action-review-2026-07-02)

## 本批结论

`P3-12-D45` 已完成移动响应式抽样，并收口本批唯一代码侧问题：

- 本轮在用户确认前后端已启动后，通过 Gateway `https://localhost:5000` 执行移动 CSS 视口 `390x844` 抽样；Browser 插件不提供 DPR 设置能力，因此不把本轮写成 DPR 3 物理高分屏结论。
- 使用仓库种子管理员账号登录，完成受保护页面和 Console OAuth 授权回流；登录后未发现 private / author 页面误跳登录或 Console 授权页停留。
- public 抽样覆盖 `/discover`、`/forum`、公开论坛详情、`/docs`、`/docs/index`、`/shop` 和 `/leaderboard`；未发现页面级横向溢出或挂载失败，榜单 tab rail 为组件内部可滚动区域。
- private / author 抽样覆盖 `/me`、资产、资产流水、订单、背包、通知、消息、圈子、宠物、论坛发帖、Docs 作者台和 Docs 创建；未发现页面级横向溢出，论坛发帖设置抽屉关闭态的 off-canvas 区域受父容器裁剪，展开后不溢出。
- Console 抽样覆盖 dashboard、商品、订单、用户、文档治理、系统设置、内容治理、角色和胡萝卜管理；表格横向滚动均保留在 `.ant-table-content` 内，折叠侧栏菜单文字裁剪属于既有移动 Console 壳层行为。
- 系统设置页发现品牌卡片在移动视口下把内容区撑出约 `37px` 横向滚动；已修正 `SystemConfigList.css` 中品牌卡、筛选工具条和主列子项的收缩约束，并让移动品牌卡内容列占满可用宽度、描述路径可换行。

## 未改变范围

- 未新增后端 API、权限键、数据库结构、路由接口、保存载荷或 OAuth 配置。
- 未把独立移动 Console 应用、公开聊天室、内部调度中心或内部 Jobs 平台拉回当前批次。
- 未改变 Console 表格内部横向滚动策略；移动窄屏下宽表格继续通过表格容器自身滚动承接。
- 未重做 public / private 页面信息架构或 Pencil 设计源；本批仅处理真实移动抽样命中的系统设置单页溢出问题。

## 验证

- Browser 插件 Gateway 移动 CSS 视口 `390x844`：public / private / author / console 抽样页面均可挂载；受保护页面使用种子账号登录后访问通过。
- Browser 插件复核 `/console/system-config` 修复后：`.admin-content` `scrollWidth = 326`、`clientWidth = 326`，`.branding-card` `scrollWidth = 296`、`clientWidth = 296`；剩余横向滚动仅存在于 `.ant-table-content` 内。
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

结果：均通过。

## 下一步

`P3-12-D45` 可转入维护回拉。D41 排序中的作者态、主路径、Console 深层动作和移动响应式抽样已完成，下一顺位进入 `P3-12-D46 UI 专题退出复核`：汇总 D37-D45 证据，对照 D36 退出条件判断是否可以转入 `P3-12-E` 发布候选准备；如仍有证据缺口，则继续留在 `P3-12-D` 定向补齐。

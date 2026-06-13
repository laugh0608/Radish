# 2026-06-13 收工回顾与明日事项

## 今日提交回顾

`git log --since="2026-06-13 00:00 +0800"` 在本记录提交前回顾到今日 11 个提交。

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `1d7fb1b5 fix(admin): 修复管理后台联调问题` | Console / B6 联调 | 修复 AdminLayout、订单列表、token refresh 与内容治理服务联调问题，保障 Console 高频入口和受保护页面在 Gateway 下可继续访问。 |
| `fbfc6347 docs(planning): 记录 B6 联调结论` | P3-10-B6 / 规划 | 记录 Token 不活跃过期治理 Gateway 首轮联调结论和工具限制。 |
| `eed48955 refactor(web): 收口公共壳层样式一致性` | P3-10-B7 / Web UI | 清理公共页旧头部样式，统一公共壳层样式边界。 |
| `f7816085 feat(web): 回迁论坛公开详情轻互动` | P3-10-B7 / 公开详情 | 公开帖子详情支持登录后直接发布轻回应和根评论，登录回流只放开公开参与意图。 |
| `5b97048a fix(web): 修复论坛公开详情联调问题` | P3-10-B7 / 公开详情 | 修复轻回应删除动作边界、根评论计数重复和帖子摘要评论数同步问题。 |
| `94595706 feat(web): 收口公共头部动作策略` | P3-10-B7 / 公共头部 | 公共页头部统一为“社区发现 / 我的圈子 / 工作台”，`/circle` 只保留“社区发现 + 工作台”。 |
| `04230ebb fix(web): 对齐公开论坛互动边界文案` | P3-10-B7 / 文案 | 公开发现 / 论坛阅读边界文案对齐轻回应与根评论回迁，重交互仍留在工作台。 |
| `3f146c43 feat(web): 补齐公开内容链接契约` | P3-10-C / 公开链接 | 公开发现、论坛列表 / 搜索 / 标签 / 类型流和公开个人页内容项补真实公开 URL，普通点击保留公开壳层来源返回。 |
| `b7c9b474 docs(planning): 记录公开链接契约复核结果` | P3-10-C / 规划 | 记录公开内容链接契约 Gateway PC / 移动视图复核结果。 |
| `b2a6992c feat(web): 补齐圈子公开来源返回契约` | P3-10-C / 圈子来源 | `/circle` 关注动态和关系链用户项进入公开详情 / 公开个人页时，可返回“我的圈子”。 |
| `f0faf707 docs(planning): 记录圈子来源返回复核结果` | P3-10-C / 规划 | 记录圈子来源返回 Gateway PC / 移动视图复核结果和移动输入工具限制。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已记录 P3-10-C 第二批完成，并把明日事项调整为公开参与 / 登录恢复 / 来源返回矩阵复核，随后进入 Console 高频治理入口。
- 已同步专题规划：[P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture) 已记录 B7、P3-10-C 首批和第二批代码 / Gateway 复核结论，以及下一批开发建议。
- 已同步功能说明：[个人圈子](/features/circle) 已补 `/circle` 到公开详情 / 公开个人页的一次性来源转交和验证要点。
- 已同步壳层策略：[前端多壳层策略](/frontend/shell-strategy) 已补公共头部动作、`/circle` 登录态复访入口和公开来源返回边界。
- 已同步开发日志：[2026 年 6 月开发日志](/changelog/2026-06) 与 [2026 年 6 月第 2 周开发日志](/changelog/2026-06/week2) 已补 2026-06-13 日结。
- 已同步记录索引：本记录已加入 [记录与验收索引](/records/)。
- 已复核无需跟随更新范围：今天没有新增视觉 token、Pencil 设计源、部署配置、环境变量、数据库结构、正式发布流程或 Console 权限模型；视觉规范、设计源文件、部署说明和权限说明无需跟随更新。

## 今日验证

- `npm run test --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`
- Gateway / Chrome PC 视图复核：`/discover`、公开帖子详情、公开个人页、`/circle`、`/desktop`。
- Gateway / Chrome 移动视图复核：`412x915 @ DPR 3.5` 覆盖公开头部、圈子到公开帖子详情 / 公开个人页来源返回和无横向溢出。移动端 Chrome 插件鼠标事件派发超时，本轮使用唯一链接 / 按钮键盘激活验证路由事件，不把触控点击写成已验证。

说明：今天没有安装依赖，也没有由 AI 直接启动 `dotnet run` 或 `npm run dev`；运行时服务由用户启动。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture) 和本记录。
2. 第一顺位进入 `P3-10-C` 第三批：围绕 `/discover`、`/forum/post/:id`、`/u/usr_...`、`/circle` 做公开参与 / 登录恢复 / 来源返回矩阵复核。
3. 复核重点包括公开详情 `intent=comment|quickReply` 登录回流、公开个人页内容跳转、圈子登录回流、浏览器刷新 / 返回后的来源状态，以及新开标签 / 复制链接是否仍保持公开 URL。
4. 发现同类问题时按公开路由契约、登录回流、来源状态和 i18n 文案成组修复，并用 `radish.client` 定向测试、构建和 Gateway PC / 移动视图做精准验证。
5. 若公开参与与来源返回矩阵未暴露新增代码缺口，再转入 Console 高频治理入口，优先覆盖 `/console/`、`/console/users`、内容治理和订单 / 用户排障工作流；不做 Console 低频页面逐页筛查。
6. 保留 `P3-10-B6` 发布候选前补验入口；不回到 P3-8-D 购买 / 订单 / 背包重复复核，不把 Flutter 完整能力套件、完整推荐系统、完整联邦社交或 WebOS 新功能承载作为默认主线。

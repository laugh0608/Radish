# 2026-09-19 日终提交回顾与文档审阅

## 回顾范围

按 Asia/Shanghai 的 `2026-09-19 00:00–24:00` 核对本地 Git 历史，日终文档提交前共有 7 个提交，均在当前 `dev` 历史中；`--all` 复核未发现当日其他提交。工作区从干净状态开始，本次只修改 Docs，不继续功能实现，不推送、发布、部署或启动服务。

## 今日提交

| 提交 | 代码 / 文档结果 | 验收边界 |
| --- | --- | --- |
| `5d412a8f` `fix(forum)` | 互动人查询、帖子 / 评论点赞统一按实体映射与 PostgreSQL 小写设置引用物理标识符 | 不更改表或数据；隔离 PostgreSQL 17 与 SQLite 的证据不替代部署默认 PostgreSQL 16 验收 |
| `6c4564a1` `fix(deploy)` | 默认持久化归入 Deploy；API / Auth Data Protection 密钥持久化；Hangfire 增加 Bearer 兑换短期 Cookie 和服务端授权；API 权限种子按职责拆分 | 旧路径需先停机迁移；看板 Cookie 不用于普通 API；未迁移用户服务器 |
| `bbe4582a` `fix(console)` | 紧凑壳层、列表优先与说明折叠；登录页直接进入 OIDC 过渡，移除旧卡片闪现 | 代表布局 / 隔离登录组件验证已记录，不视为生产 OIDC 端到端验收 |
| `e52144c6` `docs(logging)` | 确认统一策略、跨容器采集、文件与内网入库、Console 查询 / 提醒的专题边界 | 方案不是功能上线声明；业务审计仍独立 |
| `bcd0e302` `feat(logging)` | 唯一 JSON 策略、.NET / Node 事件契约、共享 fixture、L1 传输实验 | 发现原 HTTP 上限、长行分片与轮转边界；尚未切换生产 |
| `ef3cbb69` `feat(logging)` | 采集输入规范化、容器来源校验、原生安全摘要、断线 / 队列 / 文件故障实验 | 本机 arm64 与 HTTP mock；文件打开失败不会自动补写缺失事件 |
| `cf00297c` `feat(logging)` | L2 共享引导 / 运行入口、Node 适配、安全限频应急、新旧 sink 互斥开关 | 默认开关 false；SQL / AOP / DbMigrate 等旁路尚未治理完，整个 L2 未完成 |

## 按代码核对后的文档更新

- **当前顺位**：`current.md` 原入口仍把 Windows readiness 列为第一顺位，已按项目所有者今晚安排改为统一日志 L2，并新增 **2026-09-20 明日事项**；Native P8-C 保留为后续平台事项。
- **论坛数据库**：数据库总览补充仓储手写 SQL 的表列映射 / PostgreSQL 小写约束，区分查询标识符与迁移的物理元数据解析；修正同页旧机器绝对链接。
- **部署**：保留已核对的 Deploy 目录、旧路径搬迁、冷备份与固定镜像边界；补充 Frontend 日志运行文件和 collector 尚未进入生产 Compose 的说明。
- **Hangfire**：权限矩阵补会话兑换接口；模块说明改为真实的 System / Admin 或查看权限规则，移除“已无角色特例”的错误说法；去掉旧的本地放行描述和已删除的指标卡描述。
- **Console**：设计专题中的 300px 侧栏 / 84px 顶栏改为当前 224px / 56px，折叠侧栏 64px；列表类型说明与 `ConsoleResourceList` 的下方折叠上下文对齐。历史 Pencil 不修改，登录导航契约已与代码一致，无需重复改写。
- **日志**：主方案末节从“只完成设计”更新为 L1 / L2 子项真实进度；明确现状审计是实施前快照，默认旧 Enricher 与候选白名单输出互斥，Node 已适配而浏览器 / Native 尚未替换。

## 验证与证据归属

本次文档、链接、文本卫生与 `git diff --check` 检查通过；记录索引既有 303 行篇幅提醒未增加，本次没有复跑功能测试。已有实现证据按原记录引用，不将重叠测试数字累计为新的全量验收：

- [部署反馈修复](./test-deployment-maintenance-2026-09-19.md)：论坛 / Hangfire 定向验证、目录与权限契约、Console 构建及代表交互。
- [L1 首轮](./unified-logging-l1-contract-and-transport-2026-09-19.md)与[采集边界](./unified-logging-l1-guarded-collector-2026-09-19.md)：隔离传输、隐私及故障验证。
- [L2 入口](./unified-logging-l2-producer-entry-2026-09-19.md)：.NET 30 项、Node 27 项、静态服务 4 项，共 61 项定向测试；不包含真实业务宿主启动或生产镜像验收。

## 收工与明日接续

明日按[当前规划](../planning/current.md)继续 SQL 慢查询与开发诊断分离、AOP / 事务 / 异常责任、DbMigrate 命令结果与运行日志分离。正式传输上界、磁盘故障、真实入库、查询、告警和切换仍未关闭。本次只记录事项，不创建定时执行或自动提醒，今天不再推进实现。

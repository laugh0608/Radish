# F1-E 商城权益专题验收记录

> 日期：2026-07-14（Asia/Shanghai）
> 状态：通过，F1 商城商品效力与权益履约专题收口
> 范围：订单、背包、Badge / Title 公开身份、Console 排障、schema migration 与 SQLite 运行时

## 1. 验收结论

F1-A 至 F1-D 的静态实现已完成批次级运行态验收。受控旅程真实覆盖徽章激活、称号激活、同类称号切换、逐项停用和公开身份即时更新；订单、背包、公开主页、帖子评论及 Console 排障入口均能正常加载。最终没有已知 `P0/P1` 阻断，F1 专题可以收口，下一顺位进入 F2 主题系统完成度治理。

## 2. 发现与治理

首次激活暴露 `ShopEntitlementOperation.InventoryId` 的历史 `NOT NULL` 约束仍停留在消耗品专用模型，导致 Badge / Title 通用流水插入失败。处理方式不是修改已应用迁移，而是新增 `20260714_001_shop_entitlement_operation_subject_nullability` 前滚迁移：

- SQLite 只放宽 `InventoryId / ConsumableType / Quantity / ItemValue`，重建表时保留既有数据、索引与触发器。
- PostgreSQL 对同一组字段执行 `DROP NOT NULL`。
- verify 同时检查字段可空性和 4 个既有索引，SQLite 回归覆盖旧消耗品行保留、新权益行可插入与重复应用。
- SQLite PRAGMA 改为每个物理连接只初始化一次，避免事务中重复执行 `synchronous = NORMAL`。

迁移应用后，DbMigrate 最终 Doctor 确认全部 schema 已应用，主库业务表完整且可直接执行 init / seed。

## 3. 运行态矩阵

| 链路 | 结果 | 证据 |
| --- | --- | --- |
| 商品能力 | 通过 | Badge / Title 可售、可启用；其余持续权益、实物商品继续关闭，历史商品未自动上架 |
| 背包权益 | 通过 | 徽章与称号可同时激活；称号甲切换称号乙后只保留一个 Title 指针；停用后均回到 Available |
| 通用流水 | 通过 | 激活 Badge、激活 Title A、切换至 Title B、停用 Title B、停用 Badge 共 5 条流水；同类切换正确记录 `RelatedBenefitId` |
| 公开身份 | 通过 | 公开主页、帖子作者和评论作者同步显示 Badge / Title；停用后即时移除，公开 DTO 未带出订单、价格或内部权益信息 |
| Console 排障 | 通过 | 订单列表、Admin 用户详情、持续权益与商城权益流水页签正常加载空态，无 500 |
| PC 布局 | 通过 | 只读矩阵覆盖 `1920×1080`；受控切换在内置浏览器 `1280×720` 复核，背包、主页和帖子均无横向溢出 |
| Mobile 布局 | 通过 | `390×844` CSS 视口下背包、主页和帖子均无横向溢出，超长称号保持可读布局 |
| 运行日志 | 通过 | 页面 warning / error 为 0；受控旅程前后 SQLite PRAGMA 历史告警计数均为 16，没有新增 |

移动结论基于 `390×844` CSS 视口，不宣称真实设备 DPR 覆盖。

## 4. 自动化与构建

- schema migration 套件：12 项通过，4 项 PostgreSQL 环境用例按配置跳过。
- 权益、公开身份与 SQLite PRAGMA 定向测试：33 / 33 通过。
- 后端全量：652 项通过，11 项环境用例按配置跳过。
- `dotnet build Radish.slnx -c Debug --no-restore`：0 warning / 0 error。
- `DbMigrate apply`：前滚迁移、Seed 与最终严格 Doctor 全部通过。
- changed-only 仓库卫生与 `git diff --check`：通过。

## 5. 数据影响与清理

受控验收使用 3 份带 `F1EControlledSmoke` 标记的临时权益，并在写入前创建 SQLite 在线备份。验收后按测试 ID 精确删除 5 条操作流水和 3 份权益；选择指针删除数为 0，因为真实停用流程已先行移除。最终临时权益、选择指针和操作流水残留均为 0，`PRAGMA integrity_check` 返回 `ok`。

本轮不恢复或上架历史 Badge / Title 商品，不开放 Theme 或其他权益类型，不创建 PR、tag，也不执行发布部署。

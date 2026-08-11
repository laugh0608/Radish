# F4-R R3-C04 Console 普通资源设计前代码事实与风险拆批审计

> 日期：2026-08-11（Asia/Shanghai）
>
> 状态：七类普通资源的代码事实、风险、页面所有权、共享壳层、Mobile 转换和实施拆批已确认；R3-C04-A 已完成代码与静态门禁
>
> 范围：Applications、Products、Users、Categories、Tags、Stickers、Coins；本记录不修改 Pencil，不把七类资源压成一套 CRUD 状态机

## 结论

R3-C04 继续继承 `R1-C01` 的高密度资源表面、紧凑筛选、按需详情和 Mobile 单任务方向，但七类资源的读写风险明显不同，不能一次迁移。现有 `ConsolePageHeader / ConsoleMetricGrid / ConsoleToolbar` 与 `adminFeature.css` 只提供视觉结构，没有共享查询、分页、详情或写入状态机；首批只增加无业务状态的响应式资源表面，后续资源按批接入。

实施固定为六批：

1. `R3-C04-A`：Categories、Tags 与共享响应式资源表面。
2. `R3-C04-B`：Users 列表与聚合详情。
3. `R3-C04-C`：Applications。
4. `R3-C04-D`：Products。
5. `R3-C04-E`：Stickers 媒体资源。
6. `R3-C04-F`：Coins 资产操作。

每批先关闭能力门禁，再进入对应页面实现；当前不修改 Pencil。

## 实施进度

- `R3-C04-A` 已按本审计落地，详见 [Categories / Tags 与共享响应式资源表面实现记录](/records/f4-r-r3-c04-a-console-taxonomy-implementation-2026-08-11)。
- 当前顺位进入 `R3-C04-B Users`；其余四批仍严格保持本记录的既定顺序和停止线。

## 代码事实与风险

| 资源 | 页面所有权 | 读写风险与进入条件 |
| --- | --- | --- |
| Categories | `/categories` 列表 + `CategoryForm` Modal | 分页 effect 会在页码变化后重新请求第一页；`voId / voParentId / parentId` 错误声明为 `number`，与服务端 LongId 字符串契约冲突；包含层级与即时附件上传，写 handler 和表单关闭需双重保护。 |
| Tags | `/tags` 列表 + `TagForm` Modal | 与 Categories 存在同类分页回跳和 LongId 类型错误；启停、排序、软删除、恢复均为独立权限，写 handler 不能只依赖按钮隐藏。 |
| Users | `/users` + `/users/:userId` | 前端发送 `status / role`，后端列表只接收 `keyword`；详情聚合 Coins、Experience、Orders、Benefits 与治理回跳，局部失败当前被压成空值，最近十条数据又使用本地分页。 |
| Applications | `/applications` + 创建 / 编辑 / Secret Modal | 固定读取前 `100` 条再本地分页；OIDC 回调、删除和 Secret 轮换属于高风险写入，当前缺少提交 busy、dirty 和 Secret 重置确认。 |
| Products | `/products` + URL 可回访详情 Modal + `ProductForm` | 已有版本 CAS、能力矩阵和上传保护；但 Create / Update DTO 可直接提交 `IsOnSale`，会绕过独立 `products.toggle-sale` 权限。 |
| Stickers | `/stickers` + `/stickers/:groupId/items` + 三类表单 / 批量 Modal | `UpdateGroup` 对 edit / toggle 使用任一权限，却能改写完整分组；级联删除缺少显式共同事务；排序草稿和已上传未保存附件需要独立生命周期。 |
| Coins | `/coins` 查询、调账与流水 | 调账目标可脱离当前已读取用户，缺少权威目标确认和请求幂等键；属于资产写入，必须最后独立实施。 |

## 共享资源表面

共享层只负责以下结构：

- PC：页头、指标、紧凑筛选、连续表格 / 列表、可选上下文 rail。
- Mobile：结果摘要与按需筛选入口、连续资源卡片、轻量上一页 / 下一页、主任务之后的上下文摘要。
- Desktop 表格和 Mobile 资源卡在同一数据快照上渲染，不新增第二套请求或缓存。
- 详情、表单、权限、URL、分页查询、错误状态和提交载荷继续由各资源所有者维护。

停止线：不建立万能 CRUD hook、万能 DTO、统一 API 控制器或只转发参数的抽象；不在首批机械迁移其余五类资源。

## 分批停止线

### R3-C04-A Categories / Tags

- 先把所有 LongId 收敛为字符串，并修正分页 / 筛选查询真相。
- 写 handler 复核对应权限；Form 提交复核 create / edit 权限，提交或上传中禁止关闭，dirty 关闭要求确认。
- PC 使用连续表格，Mobile 使用共享资源表面的紧凑卡片与按需筛选，不保留整表横向滚动作为主路径。
- 不修改公开 Forum 分类 / 标签语义、数据库、migration、权限键或 Pencil。

### 后续批次

- Users：真实实现或移除 `status / role` 筛选，修正详情字段与局部权威状态；不新增用户写能力。
- Applications：服务端分页 / 搜索、Secret 一次性展示与轮换确认；不新增第三方审核和 Scope 模型。
- Products：先关闭上架权限绕行，保留现有 CAS、能力矩阵、附件和订单回跳；不改履约状态机。
- Stickers：收紧 toggle-only 写入、级联原子性、排序草稿和附件生命周期；不新增网格、拖拽或 Reaction 运营。
- Coins：绑定权威目标、确认和幂等契约；不扩展转账、退款、提现或万能资产编辑器。

## 验证矩阵

| 层级 | 验证范围 |
| --- | --- |
| 后端契约 | 最小权限身份；Product 上架权限；Sticker toggle-only / 删除事务；Users 权威筛选；Coins 幂等重放、冲突与资产原子性。 |
| 前端定向 | LongId 字符串、分页 / 筛选真相、handler 权限、dirty / busy、loading / empty / unavailable / stale、PC / Mobile DOM 主任务顺序。 |
| 静态门禁 | Console 全量测试、type-check、Lint、production build、`check:console-permissions`、`check:long-id-safety`、变更卫生和 `git diff --check`。 |
| Gateway | 获得当轮启动授权后覆盖 PC `1440 × 900`、Mobile `390 × 844`、只读与动作级最小权限、双语、深层 Modal / 详情、无横向溢出和稳定页签 `0 warning / 0 error`。 |
| 写入验收 | 使用可精确清理的临时分类、标签、应用、商品与贴纸；Coins 只使用隔离用户 / 数据库和可审计请求，不改正式种子资源。 |

既有三处 `DateTime.Now` baseline 和全仓历史卫生债务继续留在独立维护线，不扩入 R3-C04。

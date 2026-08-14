# F4-R R3-C04-D Console Products 权威列表与独立上下架实现

> 日期：2026-08-12（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当轮约束未启动服务、浏览器或运行态验收
>
> 范围：Console Products 列表、详情、创建 / 编辑、附件、独立上下架与订单回跳

## 本批结论

R3-C04-D 已关闭 Create / Update 直接改写 `IsOnSale` 的权限绕行，并把 Products 收敛为 URL 可回访的权威列表、独立 CAS 上下架和 PC / Mobile 共用快照。既有 `console.products.*` 权限、能力矩阵、附件协议和订单履约状态机保持不变；未新增数据库字段、migration、权限键或 Pencil 画板。

## 服务端契约

- `CreateProductDto / UpdateProductDto` 删除 `IsOnSale`；AutoMapper 显式忽略实体上下架字段，创建始终落为未上架且清空上下架时间，普通编辑保持当前状态。
- 普通编辑先读取商品并校验 `ExpectedVersion`；在售商品若被改成未开放类型会被拒绝，但编辑本身不能绕过 `console.products.toggle-sale` 改写状态。
- 正式上架 / 下架继续只走独立 endpoint，以商品 `Version` 执行 CAS；Console 在提交前显式确认，并在非权威列表快照时冻结操作。
- 管理列表的 `pageIndex / pageSize` 在 Controller 入口限制为正整数且每页不超过 `100`，关键词裁剪为 `100` 字符；服务端固定 `CreateTime desc + Id desc` 稳定排序。
- 未修改商品实体、订单履约、能力可售策略、附件归属或数据库结构。

## Console 页面事实

- 分类、类型、上下架、关键词、页码和页大小统一进入 URL；详情参数和跨订单 `returnTo` 保留列表查询上下文，刷新、历史回访和订单回跳不会丢失当前筛选。
- 列表使用请求代际和查询快照键忽略过期响应；同查询失败显示 `stale`，新查询失败显示 `unavailable`，两者都冻结创建、编辑、删除与上下架。
- 能力元数据独立裁决：缺失时冻结创建、编辑和上架；已有权威列表中的历史在售商品仍可下架，避免不可售商品无法退出销售。
- PC 继续使用连续表格和 URL 详情 Modal；Mobile 复用同一快照的商品卡、轻量分页和筛选 Bottom Sheet，详情 / 表单保持单任务承载。
- 写 handler 与 Form 双重复核权限和权威状态；表单提交、上传均受权限与元数据门禁保护，dirty 关闭及浏览器离开必须确认。创建 / 编辑表单删除上下架开关，并明确引导到独立操作。
- 详情权威读取失败时仍可展示列表快照，但编辑入口冻结；上下架与删除均有显式确认，重复动作由 busy 状态阻断。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| Products Controller / Service / Profile 定向测试 | `25 / 25` 通过，覆盖 DTO 无 `IsOnSale`、分页入口校验、映射保持上下架状态与既有 CAS / 能力契约 |
| `dotnet test Radish.Api.Tests --no-restore` | `1237 passed / 39 skipped`，总计 `1276` |
| `dotnet build Radish.slnx -c Debug --no-restore` | 通过，`0 warning / 0 error` |
| `npm run test --workspace=radish.console` | `99 / 99` 通过，含 URL、回跳、响应式、权限、权威快照与 DTO 合同 |
| Console type-check、strict type-check、Lint、production build | 全部通过 |
| 权限、LongId 与 changed hygiene | `check:console-permissions`、`check:long-id-safety`、`check:repo-hygiene:changed` 全部通过；权限扫描只保留既有 `console.hangfire.replay` 未引用警告 |

## 停止线与未执行项

- 未扩入订单履约状态机、商品业务字段、Stickers、Coins、数据库、migration、权限键或 Pencil。
- 未启动 API、Gateway、Auth、Console dev server 或浏览器；PC / Mobile 真实页面、最小权限账号、真实 CAS 冲突与附件上传留到专题成组运行态验收。
- 既有三处 `DateTime.Now` baseline 与全仓历史卫生债务继续留在独立维护线。
- 下一批进入 `R3-C04-E Stickers`，先关闭 toggle-only 越权写入、级联删除事务、排序草稿和未保存附件生命周期，不提前推进 Coins。

# F4-R R3-C04-B Console Users 权威列表与聚合详情实现

> 日期：2026-08-12（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当轮约束未启动服务、浏览器或运行态验收
>
> 范围：Console Users 列表、`/users/:userId` 聚合详情、用户凭据安全投影

## 本批结论

R3-C04-B 已把 Users 从“客户端伪筛选 + 局部失败伪装空值 + 当前十条本地分页”收敛为服务端权威查询、独立聚合状态和真实分页。列表与详情保持只读，不新增用户写能力；共享层只复用无业务状态的响应式资源表面，用户查询、授权与聚合状态继续由页面 owner 持有。

## 服务端与安全边界

- `ConsoleUserListQueryDto` 明确接收 `pageIndex / pageSize / keyword / isEnabled / roleName`；Repository 在数据库侧完成租户可见用户、软删除、关键词、启用状态和活动角色筛选，并按 `Id DESC` 稳定分页。
- 当前页角色通过一次批量关联查询回填，不再由前端猜测角色或为每个用户逐条读取。
- 用户主资料详情改走 `GetConsoleUserDetailAsync`；角色与派生权限使用独立只读快照，并由既有 `console.roles.view` 保护。未新增权限键、数据库表或 migration。
- `UserVo` 删除密码哈希字段，反向映射显式忽略 `LoginPassword`；Auth 登录改用专用 `UserCredentialSnapshot`，其中 `PasswordHash` 带 `[JsonIgnore]`，不会进入通用用户响应。

## Console 页面事实

- `/users` 将筛选草稿与已应用查询分离，查询和分页进入 URL；请求代际忽略过期响应，查询快照键区分首次 `unavailable` 与同查询刷新 `stale`。
- 列表在同一权威数据快照上使用 PC 连续表格和 Mobile 用户卡，继续复用 `ConsoleResourceList`；详情跳转携带完整 `returnTo`，可精确回到原筛选和页码。
- `/users/:userId` 将主资料、角色 / 权限、余额、经验、萝卜币流水、订单、权益操作和持续权益拆为独立 `loading / ready / unavailable / stale`；局部失败不再清空同查询已有快照。
- 萝卜币流水、订单和权益操作使用服务端 `total / pageIndex / pageSize`；持续权益接口本身返回完整集合，因此只在表面内分页，没有伪造服务端页码。
- `voUpdateTime` 统一显示为“更新时间”，不再误标为“最后登录”。Mobile 详情固定主资料和记录任务先于辅助权限摘要，并保留订单、资产和治理来源回访。
- 中英文文案覆盖权威筛选、读取状态、授权摘要、空态与重试；未引入硬编码颜色或第二套 HTTP 客户端。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| 后端 Users / Auth 定向测试 | 通过，覆盖权威筛选、稳定分页、角色回填和密码哈希投影；最终定向复跑 `8 / 8` |
| `dotnet test Radish.Api.Tests --no-restore` | `1228 passed / 39 skipped`，总计 `1267` |
| `dotnet build Radish.slnx -c Debug --no-restore` | 通过，`0 warning / 0 error` |
| `npm run test --workspace=radish.console` | `90 / 90` 通过，含 R3-C04-B URL / 查询 / 聚合契约 |
| Console type-check、strict type-check、Lint、production build | 全部通过 |
| `npm run check:console-permissions` | 通过；只保留既有 `console.hangfire.replay` 未引用警告 |
| `npm run validate:identity` | 身份运行时 / 协议输出 / LongId 扫描及后端身份定向 `31 / 31` 通过 |

## 停止线与未执行项

- 未新增用户创建、编辑、启停、角色分配或密码管理能力；未修改数据库、migration、权限键或 Pencil。
- 未启动 API、Gateway、Auth、Console dev server 或浏览器；PC `1440 × 900`、Mobile `390 × 844`、最小权限和双语运行态验收留到获得当轮启动授权后的成组验收。
- 既有三处 `DateTime.Now` baseline 与全仓历史卫生债务继续留在独立维护线。
- 下一批只进入 `R3-C04-C Applications`，不提前迁移 Products、Stickers 或 Coins。

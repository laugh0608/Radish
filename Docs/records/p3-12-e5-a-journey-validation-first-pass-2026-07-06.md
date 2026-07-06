# P3-12-E5-A 旅程级验证首轮记录

> 日期：2026-07-06（Asia/Shanghai）
>
> 范围：Gateway 真实页面 PC / mobile 首轮无业务写入复核
>
> 入口：`https://localhost:5000`

## 结论

本轮按 `P3-12-E5-A` 后续收口要求，完成注册 / 登录入口、发帖 / 评论入口、聊天 / 通知回流、商城订单 / 背包 / 资产回流、Docs 作者保存入口和 Console 治理读路径的首轮旅程级复核。

当前未发现需要阻断后续推进的 `P0/P1` 页面问题，也未命中需要本轮修代码的明确缺口。

## 复核方式

- 工具：优先尝试 in-app Browser；其 `goto` 与 CDP 导航在 Gateway 页面均触发会话超时，随后按用户指定优先级切换到 Chrome 插件完成复核。
- 视口：
  - PC：Chrome 当前窗口，CSS 视口约 `1318x810`。
  - Mobile：Chrome CDP 临时设备模拟，`390x844`、`deviceScaleFactor=3`。
- 登录态：使用当前本地种子登录态 `Admin#2`；Console 首次进入时在本地 OIDC 授权页确认 `radish-console` 授权。
- 浏览器日志：Chrome `warn / error` 日志采集结果为 `0`。
- 服务状态：用户已说明前后端持续启动，本轮未修改后端、未重启服务。

## 覆盖矩阵

| 旅程 | PC 覆盖 | Mobile 覆盖 | 结论 |
| --- | --- | --- | --- |
| 公开冷启动与注册 / 登录入口 | `/discover`、`/forum`、`/docs`、`/shop`、`/legal` | `/discover` | 公开页可读，登录入口可见；未执行注册提交。 |
| 登录后复访 | `/workbench`、`/me`、`/circle`、`/pet` | 抽样覆盖 `/notifications`、`/messages`、`/me/assets` | 私域入口、圈子和宠物首屏可达；宠物仍停留在领取入口，未执行领取。 |
| 发帖 / 评论入口 | `/forum/compose`、首个帖子详情 `/forum/post/pst_019f0907b1c37ef5a7a8fbbafd65afb4` | 同一帖子详情 | 发帖编辑器、分类 / 标签区域、轻回应、讨论区和作者动作入口可见；未发布、未评论、未发送轻回应。 |
| 聊天 / 通知回流 | `/messages`、`/notifications` | `/messages`、`/notifications` | 默认频道、消息输入、通知筛选和目标分流说明可达；未发送消息或改已读状态。 |
| 商城购买到账回流 | `/shop`、`/shop/orders`、`/shop/inventory`、`/me/assets` | `/shop/orders`、`/shop/inventory`、`/me/assets` | 公开商品、已完成订单、背包空态和资产流水可达；未执行购买。 |
| Docs 作者保存入口 | `/docs/mine`、`/docs/compose` | `/docs/mine` | 作者库、修订 / 阅读入口和新建草稿表单可见；未保存文档。 |
| Console 治理处理入口 | `/console/`、`/console/moderation`、`/console/documents`、`/console/orders`、`/console/experience` | `/console/moderation`、`/console/documents` | 治理、文档、订单和经验后台读路径可达；未执行审核、备注、发布、回滚、冻结或调整动作。 |

## 观察到的状态

- `/discover` PC 与 mobile 均展示公开内容、论坛 / Docs / 商城入口、登录参与提示和可继续阅读信息。
- `/forum/compose` 进入登录态作者路由，显示发布上下文、Markdown 编辑器、分类、标签和发布准备状态。
- 帖子详情显示轻回应入口、讨论区、编辑和历史入口；评论 / 轻回应提交动作未触发。
- `/messages` 显示公共闲聊与问题求助两个默认频道，默认频道在线且可输入；`/notifications` 为空态但显示通知目标分流规则。
- `/shop/orders` 显示已完成订单 `P3D32-ORDER-20260630-001`；`/shop/inventory` 显示背包空态；`/me/assets` 显示 `0.050` 可用余额和新用户注册奖励流水。
- `/docs/mine` 显示作者库与内置文档列表；`/docs/compose` 显示新建文档表单。
- Console 授权后，仪表盘、内容治理、文档治理、订单管理和经验等级均进入后台页面。
- 移动抽样页面的根级 `scrollWidth` 均等于 `390`，未发现根级横向溢出。

## 限制与后续

- 本轮是无业务写入首轮验证，没有提交注册、发帖、评论、轻回应、消息、购买、Docs 保存或 Console 治理动作。
- 本轮未做断网、弱网、慢接口、服务端错误注入和真实登录过期等待。
- in-app Browser 导航异常属于工具链限制，本轮真实页面结论以 Chrome 插件为准。
- 下一步如继续 `E5-A`，应进入受控写入旅程验证：使用种子账号和可清理数据，覆盖注册 / 登录、发帖、评论、Docs 保存、Console 治理处理和购买到账中的高价值路径；若需要新增服务端诊断编号、错误上报或 Console 支持入口，先补小方案确认边界。

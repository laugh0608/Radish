# F4-R R1-C02 Console 案件治理 / 审计前端能力门禁实现

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：代码与静态验收完成；设计前审计识别的 PostAnswer、URL / mobile task、权威失效和 View-only 写入表面已闭合
>
> 范围：正式 Console `/console/moderation` 的案件与申诉工作区、URL helper、移动壳层、双语资源和定向测试；未修改 `.pen`、API、数据库、权限、LongId、幂等或事务边界，未启动服务或执行浏览器 smoke

## 1. 结论

- Console 已完整识别第六类治理目标 `PostAnswer`，筛选、双语显示和 Restrict 的目标 Revision 前置校验不再停留在原五类。
- 案件公开标识、案件筛选和分页以 `/console/moderation` query 为真相源；申诉继续使用既有 `view=appeals&appeal=...` 深链。刷新、复制链接和浏览器历史可以恢复显式选择，不再自动选中队列首项。
- mobile 选择案件或申诉后进入隐藏 Console 全局 header、breadcrumb 与五项底部导航的全屏任务；任务头返回原队列 URL。申诉 mobile 继续保持既有只读停止线。
- 队列刷新失败显式保留 last-good stale 内容；详情 `404 / 403` 清除旧权威对象并进入 unavailable，其他读取失败只允许查看同一对象的 stale 快照。队列或详情不权威时，写入表面与 handler 同时失败关闭。
- 只有 View 权限的 Operator 不再看到可编辑决定或纠正表单；Review、Action 与 Appeal 继续按现有独立权限和服务端状态机显示。
- `409` 仍与读取失败分离：客户端刷新权威详情、保留未提交草稿并轮换 operation key，不改变既有幂等语义。

能力门禁据此解除。下一步可以在另行明确授权后进入唯一活动 `.pen` 的 R1-C02 PC `1440`、Mobile `390` 与必要关键状态代表设计；本批不提前推进 R2-C03。

## 2. URL 与移动任务边界

案件 query 固定承接：

- `case`：`mod_` 开头的案件公开标识；
- `status / targetType / keyword`：现有服务端已支持的筛选；
- `pageIndex / pageSize`：正整数分页，默认值不写入 URL；
- `returnTo`：继续经过 Console 既有来源归一化；
- `view=appeals&appeal=apl_...`：保持既有申诉深链兼容。

解析层拒绝非法公开标识、未知枚举和越界分页，不新增详情路由或把 LongId 转为 JavaScript `number`。案件和申诉均由显式选择打开；关闭详情只移除当前对象，保留合法队列上下文。

`AdminLayout` 只在 mobile 且 URL 中存在合法案件 / 申诉公开标识时进入全屏任务，订单既有全屏详情判断保持不变。PC 继续使用现有队列—详情工作区，正式高密度结构留给后续代表设计。

## 3. 权威读取与权限停止线

读取状态统一区分：

| 状态 | 队列 / 详情行为 | 写入边界 |
| --- | --- | --- |
| `loading` | 首次读取显示明确加载态 | 不允许写入 |
| `ready` | 当前服务端响应为权威数据 | 仍需通过独立权限与业务状态检查 |
| `stale` | 保留 last-good 内容并显示失效提示 | 决定、证据、纠正、申诉复核和 relief 全部冻结 |
| `unavailable` | `404 / 403` 清除旧详情，提供返回或重试 | 不渲染写入表面 |

案件 / 申诉快速切换使用加载序列隔离旧请求；旧请求即使较晚完成也不能覆盖当前对象，申诉关联案件读取也会在对象切换后提前停止。

权限表面按真实能力拆分：View-only 仅显示证据与结果；Reviewer 可登记案件决定但不能附带 Action；Action Operator 只能在服务端允许的既有状态执行纠正；Appeal Reviewer 的复核与 Action relief 继续分离。移动申诉写面板仍不开放。

## 4. 保持不变的契约

- 继续复用 `@radish/http` 的 Case / Appeal API 与结构化错误，不新增 fetch 封装。
- Case、Appeal、User State expected version、目标 Revision 和 `operationKey` 语义不变。
- `404 / 403 / 409` 仍按结构化错误判断，不依赖展示文案。
- Main 原子事务与 Chat Outbox 可靠任务边界不变。
- 不新增原因 / 证据可用性 / 动作结果筛选、自动审核、风险评分、批量处置、实时刷新、完整私聊浏览或全量审计平台。

## 5. 验证

- Console 全量测试：`73 passed / 0 failed`。
- Console ESLint：`0 warning / 0 error`。
- Console strict type-check 通过。
- Console production build 通过。
- changed-file repo hygiene 与 `git diff --check` 通过。

本批没有启动 API、Gateway、Auth 或前端 dev server，也没有执行 PC / mobile 浏览器 smoke；这些只在后续代表设计完成并准备成组验收时另行申请当前任务授权。

关联记录：[R1-C02 设计前代码事实与能力覆盖门禁](/records/f4-r-r1-c02-console-moderation-readiness-audit-2026-08-09)。

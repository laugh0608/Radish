# L2 表情管理最终消费与降级边界

日期：2026-10-07（Asia/Shanghai）。本批在 `dev` 按项目所有者批准的方案实施，承接[统一日志专题](../features/unified-logging-governance-design.md)及[事件契约第 49 节](../features/unified-logging-contract.md#49-l2-表情管理最终消费与降级边界)。仅关闭本批生成端边界，L2 整体未关闭，`RadishLogging.Enabled` 默认 false 保持。

## 实施范围

- 新增 `StickerInputValidationException` 与 `StickerRequestRejectedException`，继承原异常父类，标记明确参数拒绝、标识冲突、目标缺失和排序快照失效；不按异常消息判断是否为预期拒绝。
- StickerController 四入口在错误响应成功构造后记录消费的依赖故障；参数消息、错误码、本地化及原状态保持。其他异常与响应构造失败仍交给 API，Controller 不新增聚合解包。
- 批量构建失败在逐项结果成功构造后生成 Error，Controller 不重复输出；缩略图失败结果、异常及流释放失败合并为最终降级 Warning。缓存读 / 写 / 失效保留降级与继续执行，日志不再携带异常原文或缓存键。
- 六个事件码注册到共享策略：`sticker.request_failed`、`sticker.batch_item_failed`、`sticker.thumbnail_failed`、`sticker.cache_read_failed`、`sticker.cache_write_failed`、`sticker.cache_invalidation_failed`。自有事件仅保留固定 failureKind；API 最终事件沿用 statusCode / failureKind。

## 行为与验证证据

新增 `StickerControllerLoggingTests` 十组四模式测试：旧 / 候选 × Development / Production，共 40 个测试用例。每个用例包含对应边界矩阵，不能把用例数视为真实 HTTP 请求或并发覆盖量。

| 场景 | 已验证结论 |
| --- | --- |
| 明确拒绝 | 模型预检、标识格式 / 冲突、目标 / 附件缺失、排序快照失效保留响应，日志安静 |
| 依赖消费 | 同业务拒绝文案的普通异常仍记 Error；依赖的 ArgumentException 不伪装成正常参数校验 |
| API 最终处理 | IO、超时、取消、未知异常、BusinessException(503)、聚合异常保留原归属；原 TranAop 仅对事务入口解包单一聚合异常 |
| 响应再次失败 | 异常 Message getter 或本地化抛错时不提前输出 Controller Error，由 API 记录最终故障；可用本地化保留原响应 |
| 成功数据 | 分组 / 单表情 / 批量添加 / 排序实际写入 SQLite，核对规范化、GIF、附件、排序与操作人字段 |
| 写入后失败 | 分组 / 单表情没有事务，写入后故障保留已写数据；批量第二次真实写入后故障与排序部分真实写入均回滚 |
| 批量附件结果 | 第一项失败仍检查第二项；有失败项时不插入任何表情，保留失败列表、行号、消息与原 500 |
| 附件副作用 | 第一项已生成文件并更新附件、第二项失败时：正常返回失败列表保留已更新附件；Message getter 抛错导致事务回滚附件更新，但两种情况下文件均保留 |
| 缩略图降级 | false 结果、存在检查 / 下载 / 路径 / 处理 / 附件回写故障及释放故障均继续保存；false 后释放失败仅输出最终一次 Warning；文件缺失 / 空流保持静默 |
| 缓存与集合边界 | 读缓存失败回源、写 / 失效失败继续业务；批量 null / 空 / 超 50 项、无效字段、已有冲突，排序空 / null / 无效 / 重复 / 跨分组 / 已删除分组保持原结果 |
| 安全输出 | 两类输出均不可检出标记秘密、异常原文、图片处理错误文本、分组 / 附件 / 用户标识、表情内容和缓存键；候选事件核对模式、级别、事件码与属性白名单 |

真实组件：StickerService、BaseRepository、UnitOfWorkManage / TranAop、SQLite StickerGroup / Sticker / Attachment 三表，以及 API 异常与结果过滤器内存管道。存储、图片处理、缓存、附件 URL 与本地化采用可控 mock；缩略图测试文件仅模拟文件副作用，不代表真实图片算法或存储验收。临时文件按每个 fixture 的专属目录清理。

## 本地验证

1. 后端定向回归及编译（启用警告视为错误）：

   ```bash
   dotnet test Radish.Api.Tests --no-restore --filter 'FullyQualifiedName~StickerControllerLoggingTests|FullyQualifiedName~StickerControllerTest|FullyQualifiedName~StickerServiceTest|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~ProducerLoggingTests|FullyQualifiedName~ReactionControllerLoggingTests' --logger 'console;verbosity=minimal' -p:TreatWarningsAsErrors=true
   ```

   结果：114 通过，0 失败，0 跳过。首次沙盒执行受 MSBuild 本地通信管道权限限制，提权后取得真实结果。首轮发现一个旧测试精确断言原异常类型，已更新为批准后的拒绝子类型；最终回归包含该修正。

2. `npm run check:logging-contract`：27 通过。
3. `npm run check:docs`、`npm run check:repo-hygiene:staged`、`git diff --check`：通过。暂存区卫生检查覆盖 12 个文件；records 索引从既有 308 行增至 310 行，触发 300 行建议上限提醒，未达到 500 行硬上限，本批未扩展为索引重组。

## 保留的独立边界

- 单表情目标 / 附件缺失仍返回 `409 / CodeAlreadyExists`；批量依赖 InvalidOperationException 仍映射 `404 / StickerGroupNotFound`；排序写入不足仍映射 `409 / StickerSortSnapshotStale`。本批没有修正接口语义。
- 缩略图附件回写失败允许保存继续，文件与数据库不具备共同事务；不新增补偿 / 清理 / 重试。LocalFileStorage 部分故障折叠成 false / null 的契约仍在，不能据此声称所有存储故障可观测。
- 共享附件解析也被 UpdateSticker 调用，其原父类异常传播不变；没有扩展该入口的消费范围。空请求仍为原 ArgumentNullException。
- 未执行 PostgreSQL、真实并发、真实图片处理、MVC 认证 / 模型绑定、HTTP 宿主或 PC / mobile Smoke；未安装依赖、启动服务 / 容器、访问生产、发布或切换采集链。
- SignalR 最终处理、既有内存 404 框架重抛及早期消费点响应再次失败的日志归属继续按[当前规划](../planning/current.md)推进。

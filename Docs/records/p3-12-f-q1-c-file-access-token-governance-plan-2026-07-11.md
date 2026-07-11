# P3-12-F Q1-C 文件访问令牌审计与实施方案

> 状态：`运行时实施与 SQLite 验证已完成；待目标库 apply / verify 与 PostgreSQL 环境用例`
>
> 日期：`2026-07-11`（Asia/Shanghai）
>
> 范围：只覆盖 Q1-C 文件访问令牌的存储、消费、撤销、权限、迁移与测试，不包含 Q2、Q3、页面调整、服务启动或无关附件重构。

## 摘要

`FileAccessToken` 已是文档正式声明、OpenAPI 可见且允许匿名下载的 API 能力，不是纯内部实验；虽然 client / console 当前没有产品入口，HTTP 示例和文件上传专题均把它列为正式接口。直接保留现状会让数据库中的 token 成为可立即使用的下载凭据，并允许并发请求通过“查询 → 内存加一 → 更新”突破 `MaxAccessCount`。

已按该边界完成治理：使用 256-bit 随机原始 token，只在创建响应返回一次；数据库原 `Token` 列原位保存 SHA-256 Base64Url hash，保持既有列长和唯一索引；DbMigrate 将历史 32 字符 GUID token 原位哈希，因此旧下载链接仍可通过“客户端原 token → 服务端 hash”继续使用。消费已改为带全部有效性条件的单条原子更新，撤销和消费以数据库受影响行数决定胜负。

该迁移不能还原历史明文 token。数据库 apply 后不得回滚到只理解明文 token 的旧二进制；故障处理必须使用兼容桥接版本或前滚修复。这是本批需要明确接受的迁移停止线。

## 一、现状与发布判断

### 1. 已暴露能力

- `POST /api/v1/Attachment/CreateAccessToken`：登录用户创建 token，并返回完整 token 和访问 URL。
- `GET /api/v1/Attachment/DownloadByToken`：匿名入口，token 放在查询字符串。
- `POST /api/v1/Attachment/RevokeAccessToken`：以完整 token 撤销。
- `GET /api/v1/Attachment/GetAttachmentTokens`：返回附件全部有效 token，当前会再次返回完整 token。
- API 启动时注册过期 token 清理任务；`Docs/features/file-upload-design.md` 将上述接口列为正式文件能力。

前端源码没有调用这些接口，因此当前没有页面兼容成本；但这不等于未暴露。匿名下载端点和 OpenAPI 契约已经构成正式攻击面。

### 2. 已确认缺口

- `FileAccessToken.Token` 以 32 字符 GUID 明文存储，数据库泄露即可直接下载。
- 校验先查询实体，再判断撤销 / 过期 / 用户 / IP / 次数，最后 `AccessCount++` 并普通更新；并发请求可读取同一个旧计数并同时成功。
- 消费与撤销没有原子竞争语义；撤销请求和下载请求可能都基于旧快照成功。
- 创建、撤销、查看、列表均只有“创建者 / 上传者”判断，管理员分支保留 TODO。
- 列表接口重新暴露完整 token，破坏“只在创建时返回一次”的凭据边界。
- 现有单元测试只覆盖顺序访问，没有并发争抢、撤销竞争、过期边界和无限次数。
- `DownloadByToken` 直接取 `RemoteIpAddress`；在 Gateway 代理拓扑下必须明确可信代理解析，否则 IP 限制可能只看到代理地址。

## 二、目标数据与接口契约

### 1. token 与 hash

- 原始 token：`RandomNumberGenerator.GetBytes(32)`，Base64Url 无填充编码，约 43 字符，提供 256-bit 随机性。
- 存储 hash：`SHA-256(rawToken)` 后 Base64Url 无填充编码，同为 43 字符。
- 实体属性改名为 `TokenHash`，但继续映射数据库原 `Token` 列；现有 `varchar(50)`、唯一约束和历史表结构可以复用。
- 日志只记录 hash 的短摘要或记录 ID，不记录原始 token、完整 hash 或完整下载 URL。

原始 token 与 hash 长度相同不用于运行时格式判断。运行时永远对输入原始 token 做 hash 后查询；只有 DbMigrate 知道旧格式固定为 32 字符 GUID hex，并负责一次性转换。

### 2. 返回模型

- 创建接口返回专用 `FileAccessTokenCreatedVo`：原始 token、访问 URL、记录 ID、约束和过期时间，只返回一次。
- 列表 / 信息接口返回 `FileAccessTokenSummaryVo`：记录 ID、token 脱敏预览、附件、访问次数、过期 / 撤销状态和创建时间，不返回原始 token、hash 或可直接下载的 URL。
- 撤销主契约改为按记录 ID 撤销，避免要求管理端保存原始凭据。
- 为旧调用保留一个发布周期的原 token 撤销兼容入口：服务端 hash 后定位同一记录；不保留按明文数据库查询的双轨逻辑。

### 3. 权限边界

- 创建：附件上传者或 `System / Admin`。
- 列表、查看、撤销：token 创建者、附件上传者或 `System / Admin`。
- 匿名下载只由 token 自身约束授权，不因此取得附件管理权限。
- Controller 显式把 `Current.IsSystemOrAdmin()` 传入 Service；Service 不读取 `HttpContext`，定时清理仍可独立运行。
- 删除所有管理员权限 TODO，并对普通用户越权、管理员代管和跨附件访问补测试。

## 三、原子消费与撤销语义

### 1. 专属仓储

新增 `IFileAccessTokenRepository`，不让 Service 直接访问 Db。仓储提供：

- `TryConsumeAsync(tokenHash, userId, normalizedIp, now)`
- `TryRevokeByIdAsync(tokenId, now)`
- `TryRevokeByHashAsync(tokenHash, now)`（兼容入口）
- Service 在调用原子撤销前完成创建者、上传者或 System / Admin 权限校验。

### 2. 单条条件更新

`TryConsumeAsync` 的更新条件同时包含：

- `TokenHash == inputHash`
- `IsRevoked == false`
- `ExpiresAt > now`
- `AuthorizedUserId == null || AuthorizedUserId == userId`
- `AuthorizedIp == null || AuthorizedIp == normalizedIp`
- `MaxAccessCount == 0 || AccessCount < MaxAccessCount`

同一 SQL 原子执行 `AccessCount = AccessCount + 1`、`LastAccessedAt = now`、`ModifyTime = now`。受影响行数为 1 才算消费成功，再按唯一 hash 读取 `AttachmentId`；为 0 时统一安全失败，不向匿名调用方区分不存在、过期、撤销、用户 / IP 不匹配或次数耗尽。

`MaxAccessCount = 0` 仍记录访问次数；`AccessCount` 在本批保持既有整数类型，达到数据库整数极限属于不可实现的实际流量，测试只验证多次消费不受上限条件阻断。

### 3. 竞争线性化

- 最后一个额度只能由一个更新取得；其余并发请求受影响行数为 0。
- 撤销先提交时，后续消费失败；消费先提交时，本次下载视为已经取得授权，随后撤销阻止下一次消费。
- 附件流读取失败不返还次数。token 的一次成功消费代表一次下载授权尝试，而不是保证客户端下载完整文件，避免引入跨数据库 / 文件存储补偿。

## 四、历史数据迁移与兼容

### 1. DbMigrate apply

在 Main 数据库结构初始化后执行幂等数据补丁：

1. 查询 `FileAccessToken.Token` 中符合 32 字符十六进制旧格式的行。
2. 在内存中计算 SHA-256 Base64Url hash，按记录 ID 更新原列。
3. 已是 43 字符 hash 的行跳过；异常长度或格式直接使 apply 失败，不猜测、不删除。
4. 重入时更新数为 0；verify 检查不存在旧格式或异常格式，且 hash 唯一约束有效。

迁移前后列名保持 `Token`，只改变内容语义；代码实体使用 `TokenHash` 明确新契约。旧链接持有的原始 32 字符 token 仍会被新运行时 hash 后命中，因此无需双列和兼容查询。

### 2. 部署顺序

1. 备份 Main 数据库并记录旧 token 行数。
2. 停止旧 API 写入或进入维护窗口。
3. 部署包含 hash 读取能力的新二进制和 DbMigrate。
4. 执行 `DbMigrate apply`，再执行 `verify`。
5. 启动 API，并抽查历史 token、创建、单次消费、撤销和列表脱敏。

禁止新代码先运行、旧数据尚未迁移的混合窗口；新代码只按 hash 查询，会暂时无法识别旧明文行。

### 3. 回滚与前滚

- 数据 hash 不可逆，不提供恢复明文 token 的回滚脚本。
- apply 后禁止回滚到旧二进制；需要应用回退时只能部署同时理解 hash 的兼容桥接构建。
- 数据补丁失败则事务回滚并保持旧数据；尚未切换 API 时可以恢复旧运行态。
- 上线后发现问题优先前滚修复；若必须止血，可禁用创建与匿名下载端点并批量撤销 token，不恢复明文。
- 数据库备份只用于灾难恢复，不作为把生产重新降级到明文 token 模式的常规方案。

## 五、可信 IP 与访问 URL

- IP 限制只信任 `RemoteIpAddress`，或经过 API 可信代理配置处理后的转发地址；不得直接信任任意客户端提供的 `X-Forwarded-For`。
- 若本次部署无法配置 API 到 Gateway 的可信代理范围，则 `AuthorizedIp` 创建参数暂时拒绝启用，而不是保存一个无法正确验证的限制。
- 访问 URL 应使用正式公开基址配置，不直接依赖 API 内部请求的 scheme / host；Gateway 域名变化仍由配置治理。
- 以上只修正 token 能力自身的地址和代理边界，不扩展为 Q2 时间治理或全站代理重构。

## 六、测试与验证口径

### 1. hash 与迁移

- 创建响应包含原始 token，数据库只含 hash，二者不相等；hash 可验证但不可直接用于下载。
- 历史 32 字符 token 经 apply 后仍能下载；apply 重入不二次 hash。
- verify 拒绝旧格式、异常格式、重复 hash 和可直接使用的明文 token。
- 列表、信息、日志和异常均不包含原始 token 或完整 hash。

### 2. 并发与边界

- `MaxAccessCount = 1` 下 20 个并发请求只有 1 个成功。
- 剩余 N 次额度在并发争抢后成功数恰好为 N，计数不超限。
- 撤销与消费并发只允许符合数据库提交顺序的结果，撤销完成后不能再次成功。
- 过期前一刻可消费，等于 / 晚于 `ExpiresAt` 失败；测试注入固定 `TimeProvider`，不依赖实际等待。
- `MaxAccessCount = 0` 可重复消费；用户和 IP 限制分别覆盖匹配、不匹配与匿名场景。
- SQLite 单元 / 集成测试验证基本契约，PostgreSQL 环境测试验证条件更新与并发争抢真实语义。

### 3. 权限和协议

- 上传者、创建者、System / Admin 的允许路径与普通用户越权拒绝。
- 创建只返回一次原始 token；列表和按 ID 撤销不需要原始 token。
- 文件成功响应仍是流；token 失败使用统一安全 MessageModel，不泄露失败原因。
- 前端当前无调用，不新增页面；更新 HTTP 示例和文件上传专题契约。

## 七、实施批次与停止线

1. **契约与仓储**：token hash 工具、创建 / 摘要 Vo、专属仓储、原子消费 / 撤销和权限参数。
2. **迁移与 DbMigrate**：历史 token 原位 hash、apply 重入、verify、SQLite / PostgreSQL 迁移测试。
3. **API 与兼容**：创建一次返回、列表脱敏、按 ID 撤销、旧原 token 撤销兼容、可信 IP 和公开基址。
4. **成组验证**：并发、撤销竞争、过期、无限次数、权限、旧 token 和日志 / 响应泄露扫描。

Q1-C 完成后再进入 Q2。当前不新增文件分享页面，不迁移其他 token，不做全仓时间语义重构，也不把附件存储、下载审计或 CDN 设计混入本批。

## 八、实施结果与待验收门禁

已完成：

- 新增 `FileAccessTokenHashing` 与 `IFileAccessTokenRepository`，实体仅持久化 hash；SQLite 以进程内写锁适配其单写器特性，PostgreSQL 依赖数据库条件更新的原子性。
- 创建一次返回、列表安全摘要、按 ID 撤销、旧原 token 撤销兼容、创建者 / 上传者 / System / Admin 权限已落地。
- 历史 32 字符 token 原位 hash 数据补丁、重入、异常格式拒绝和严格 `verify` 已接入 DbMigrate。
- 公开基址与可信代理解析已显式化；Service AOP、审计请求体和 SQL AOP 已防止记录原始 token 或完整 hash。
- `dotnet build Radish.slnx -c Debug --no-restore` 通过，`0` warning / `0` error；`dotnet test Radish.Api.Tests --no-build --no-restore` 通过 `597`、跳过 `2`、失败 `0`。

尚未写入“已完成”的环境门禁：

- 本轮未对用户当前 Main 数据库执行不可逆 `DbMigrate apply`；需在备份后执行 apply，紧接着执行 `verify`。
- `RADISH_TEST_POSTGRES_CONNECTION_STRING` 未配置，Q1-C PostgreSQL 双 Worker 并发条件更新用例因此明确跳过；不以 SQLite 结果代替生产相似并发语义验收。

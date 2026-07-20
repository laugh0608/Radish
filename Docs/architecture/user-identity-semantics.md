# 用户身份语义与公开索引

> 状态：专题方案已确认，首批实现已落地
>
> 最后更新：2026-07-14（Asia/Shanghai）
>
> 关联文档：
>
> - [标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)
> - [身份语义收敛与 Claim 治理设计](/architecture/identity-claim-convergence)
> - [认证与权限](/guide/authentication)
> - [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)

## 1. 结论

Radish 的用户身份需要拆成四类不同语义，后续不再把“用户名、昵称、登录名、邮箱、公开链接、搜索标识”混成同一个字段：

1. **内部索引**：数据库内部主键，只用于本地关联和审计。
2. **稳定公开标识**：`PublicId`，用于公开路由、分享、跨端回流和未来联邦前置。
3. **私有登录凭证**：`Email` 是当前登录凭证；`LoginName` 已从当前实体、注册、登录、Bootstrap、普通 DTO 和新库 schema 退场。
4. **公开展示与搜索**：`DisplayName` 与 `PublicIndex`，组合成类似 `用户名称#1086` 的公开索引。

`PublicId` 已经在当前主线中用于用户公开主页和榜单跳转。`DisplayName#PublicIndex` 不是 `PublicId` 的替代品，而是面向用户搜索、艾特和页面展示的可读索引。

## 2. 字段语义

| 字段 | 语义 | 唯一性 | 是否公开 | 说明 |
| --- | --- | --- | --- | --- |
| `Id` / `InternalId` | 数据库内部主键 | 是 | 否 | 只用于数据库关联、仓储查询和审计，不应出现在公开 URL 或公开 DTO 中 |
| `PublicId` | 稳定公开对象标识 | 是 | 是 | 当前形态为 `usr_...`，用于公开主页、分享链接和跨端回流 |
| `Email` / `UserEmail` | 邮箱登录凭证与未来通知地址 | 是 | 否 | 注册必填；未来支持验证、通知、找回和邮箱登录 |
| `DisplayName` | 页面展示名称 | 否 | 是 | 当前只允许中文、英文字母和数字，可重复 |
| `PublicIndex` | 公开索引号 | 是 | 是 | 使用 `long / Int64` 长整数持久化，数据库列用 `BIGINT` 或等价类型；普通用户从 `1000` 开始；`1-999` 为官方、内部、种子和 bot 保留 |
| `DisplayHandle` | 派生显示句柄 | 派生唯一 | 是 | 由 `DisplayName#PublicIndex` 组合生成，不建议作为唯一持久化真值 |

当前 `User.UserName` 仍是数据库历史列，代码层通过 `DisplayName` 忽略列属性承接公开展示名语义。文档、DTO 和前端文案不得继续把 `UserName` 称为登录用户名；若因兼容窗口保留 `VoUserName`，也只表示展示名兼容字段。

## 3. 登录名退场

`LoginName` 是历史私有登录字段。`P3-12-B6-5` 后当前实体、注册、登录、Bootstrap、普通 DTO、Console 系统设置和新库 schema 均不再保留该字段；登录凭证固定为 `Email + Password`。旧本地 SQLite 不编写兼容迁移，应按 B6 要求删除并重新初始化。

唯一性应按规范化后的值判断，不允许 `Alice` 与 `alice` 成为两个账号。数据库层应有唯一索引兜底，服务层负责给出清晰错误信息。

## 4. 邮箱规则

邮箱是必填字段，即使邮件系统尚未接入也应在注册时收集，原因如下：

- 邮箱是当前唯一登录凭证，避免继续暴露或依赖历史登录名。
- 邮箱是未来邮件通知、找回密码和账号安全提醒的基础。
- 邮箱不展示在公开页面，不参与普通用户搜索。

规则建议：

- 注册必填，默认最大长度 `254`。
- 保存原始展示值时可保留大小写，但唯一性比较必须使用规范化小写。
- 同一邮箱只能绑定一个有效用户。
- 当前没有邮件系统时可先标记为 `Unverified`，但不影响登录；接入邮件后再启用验证状态和换绑流程。
- 登录错误提示保持统一，不区分“邮箱不存在”或“密码错误”。

## 5. 展示名规则

`DisplayName` 是普通用户在页面上看到的名称，不承担登录和唯一性职责。

建议默认规则：

- 长度默认 `2-24` 个字符，最小值和最大值已进入系统设置中心管理。
- 当前实现只允许中文、英文字母和数字。
- 不允许 `#`、`@`、空格、下划线、短横线、点号、URL 分隔符、HTML 控制字符、换行和其他特殊符号。
- 前后空白必须裁剪；裁剪后仍需满足长度和字符集规则。
- 不做唯一性校验，重名用户通过 `PublicIndex` 区分。

正式注册流程要求用户独立填写展示名，不能从登录凭证派生公开展示名。注册页必须明确提示：`DisplayName` 会公开展示在个人主页、帖子、评论、聊天、榜单和艾特搜索等场景中，应谨慎设置；后续修改会受到次数、冷却时间和滚动窗口频率限制。

展示名允许后续修改，但不设计成无限次、即时随意更换：

- 服务端必须按冷却时间、滚动窗口天数和窗口内最大修改次数做最终校验。
- 展示名修改历史应记录改名前后值、操作者、来源、时间和必要原因。
- 当前已由 `UserIdentity.DisplayName.ChangeCooldownDays`、`UserIdentity.DisplayName.ChangeWindowDays`、`UserIdentity.DisplayName.ChangeWindowMaxCount` 三个 `Medium` 风险系统设置控制，默认 `30` 天冷却、`365` 天滚动窗口、窗口内最多 `3` 次；设置为 `0` 时关闭对应限制。
- `UserDisplayNameChangeRecord` 是改名历史真值，个人资料改名必须走 `UserService.ChangeDisplayNameAsync` 服务端治理入口。

迁移阶段如缺失展示名，可临时使用系统生成名或要求用户首次进入时补齐。

## 6. 公开索引

公开索引用于用户搜索、艾特、公开展示和人工辨识。建议采用：

```text
DisplayName#PublicIndex
```

示例：

```text
萝卜SAMA#1086
RadishUser#1087
```

规则如下：

- `PublicIndex` 独立持久化，`DisplayHandle` 动态派生。
- `PublicIndex` 的运行时类型统一为 `long / Int64`，数据库层使用 `BIGINT` 或等价长整数类型，避免未来用户数量、官方保留段、bot 和迁移批次增长后触及 `int` 上限。
- 普通用户从 `1000` 开始分配，但分配器必须跳过已配置的保留靓号。
- `1-999` 只用于项目内部、官方账号、开发种子、手动后台创建账号和未来 bot。
- `1000` 之后允许继续保留一批公开靓号，例如 `1111`、`2222`、`3333`、`1234`、`1314` 等；这些号码不进入普通注册自动分配池。
- 靓号保留可由 Console 维护显式列表，也可由 Console 维护规则，例如重复数字、顺子、回文或运营自定义 JSON 规则；规则变更只影响后续分配，不应自动改写既有用户。
- 已保留靓号如需分配给官方、活动或人工指定账号，应走 Console 管理动作、权限校验、修改原因和审计记录。
- 默认创建的第一个管理员使用 `1`。
- 开发阶段种子用户按固定保留索引顺延，例如 `2-4`。
- 普通用户删除或禁用后不回收 `PublicIndex`。
- 用户改展示名时 `PublicIndex` 不变。
- `DisplayName#PublicIndex` 可重新渲染，但评论、通知和提及关系的真实引用必须绑定 `PublicId` 或内部 `UserId`。

当前已注册的自动分配治理设置为：

- `UserIdentity.PublicIndex.ReservedIndexes`：JSON 数组，元素必须是整数或整数字符串，范围不小于 `1000`，且不得重复；`1-999` 属于系统保留段，不通过该设置维护。
- `UserIdentity.PublicIndex.VanityRules`：JSON 对象，只支持 `repeatedDigits`、`ascendingSequence`、`descendingSequence`、`palindrome` 四个布尔键；未知键或非布尔值视为配置错误。
- 覆盖值解析失败、越界或重复时应直接暴露为配置错误，不静默回退默认值；规则变更只影响后续自动分配，不改写既有用户。

分配实现必须由后端在数据库事务内完成，不应依赖前端计算。PostgreSQL 可优先使用 sequence；SQLite 开发库可使用受事务保护的计数器表或等价机制。分配器读取当前靓号保留配置后，应在同一事务内跳过保留号并写入唯一索引兜底。

### 6.1 公开身份装饰

Badge 与 Title 是公开身份的可选展示层，不改变 `PublicId`、`DisplayName`、`PublicIndex`、角色或权限语义：

- 服务端通过 `UserAdornmentService` 按用户批量读取 Badge / Title 的当前 `UserActiveBenefit` 选择，并再次校验租户、归属、生效时间、到期和撤销状态。
- 公开 DTO 只输出 `UserAdornmentVo`：`VoBadge / VoTitle` 各自只包含 `VoResourceKey / VoName / VoImageUrl`。
- 不得在公开身份装饰中暴露 `BenefitId`、订单 ID、商品 ID、权益流水、内部用户 ID 或撤销原因。
- Badge 只有在附件公开、启用、未删除且未审核拒绝时才输出；附件不满足公开条件时省略 Badge，不回退内部附件地址。
- Title 可以是纯文本，展示层必须为长称号预留收缩、换行或 ellipsis，不得挤压作者名、时间、楼层和操作按钮。
- 当前公开消费面包括公开主页、帖子、问答回答、评论和回复；这些页面应使用同一身份装饰组件，不分别解释权益状态。

身份装饰是服务端派生快照。前端不得根据历史商品、背包记录或本地缓存自行认定 Badge / Title 仍有效。

## 7. 搜索与艾特

普通用户搜索和艾特只允许使用公开字段：

- `DisplayName`
- `PublicIndex`
- 完整 `DisplayName#PublicIndex`
- `PublicId`，主要用于链接回流和排障，不作为普通输入主路径

禁止在公开搜索中匹配：

- `LoginName`
- `Email`
- 内部 `Id`
- 手机号、真实姓名或其他未来私密资料

搜索结果应展示 `DisplayName#PublicIndex`、头像和少量公开资料。重名用户不需要被强行消除歧义，公开索引本身负责区分。

富文本编辑器或评论输入框中的艾特应优先存储结构化引用，例如：

```json
{
  "type": "userMention",
  "publicId": "usr_...",
  "label": "萝卜SAMA#1086"
}
```

纯文本解析可以作为兼容能力，但不应成为唯一真值。

## 8. 隐私边界

公开页面和普通 API 不展示：

- 登录名
- 邮箱
- 内部用户 ID
- 真实姓名
- 账号安全状态
- 管理备注
- 身份装饰对应的权益、订单、商品和治理流水标识

用户本人页面和 Console 可查看邮箱，但应按页面上下文做必要脱敏。日志、审计和错误信息中默认避免输出完整邮箱；如确需排障，应使用结构化字段并控制日志级别和访问权限。

公开用户资料 DTO 后续应逐步移除 `VoUserId`，以 `VoPublicId`、`VoDisplayName`、`VoPublicIndex` 和派生 `VoDisplayHandle` 为主。

当前治理批次先完成公开资料后续内容查询的解耦：公开资料页加载统计、帖子和评论时应使用 `PublicId` / 公开 identifier，不再读取资料 DTO 中的 `VoUserId` 作为后续请求参数。`VoUserId` 暂时保留在 DTO 中仅用于兼容窗口和内部排障，等公开页面、公开 API 和索引入口全部不再依赖内部 LongId 后，再单独安排字段退场。

## 9. 注册与登录流程

正式目标流程：

1. 注册时必填 `Email`、`DisplayName`、密码。
2. 后端统一校验邮箱和展示名规则。
3. 创建用户时分配 `PublicId` 与 `PublicIndex`。
4. 登录页使用 `Email + Password`。
5. 登录成功后前端只把展示名和公开索引用于界面展示。

当前实现口径：

- `Account/Register` 当前收集 `DisplayName`、`Email`、密码和确认密码；注册页已提示展示名公开展示和后续修改频率限制。
- `Email` 已在注册阶段必填并统一小写保存；登录固定按邮箱查询。
- `User.UserName` 当前仍作为公开展示名历史列，语义上承接 `DisplayName`；公开页面和搜索结果不得把这个字段重新解释为登录凭证。
- `UserService.AddAsync` 负责分配 `PublicId` 与 `PublicIndex`，并在自动分配时跳过已配置的公开索引显式保留号与靓号规则命中号；公开资料、榜单、关系链和提及搜索结果输出 `DisplayHandle`。
- `User/SearchForMention` 当前只匹配公开字段：`DisplayName`、`PublicIndex`、完整 `DisplayName#PublicIndex` 与 `PublicId`；不匹配 `LoginName` 或 `Email`。

## 10. 租户与角色

租户、角色不需要复制用户身份的复杂度。长期建议统一为：

| 对象 | 必要字段 | 说明 |
| --- | --- | --- |
| `Tenant` | `Id`、`Slug`、`DisplayName`、`Status` | 产品语义上仅需要稳定机器名和显示名；当前多租户隔离实现可继续保留 |
| `Role` | `Id`、`Slug`、`DisplayName`、`Description`、`Status`、`Sort` | 权限判断优先使用稳定 `Slug`，页面展示使用 `DisplayName` |

现有 `TenantId`、多租户过滤和角色权限表仍是当前系统实现基础，不应因为语义收敛而直接删除。后续应先补 `Slug` 语义，再逐步减少页面和 API 对历史名称字段的依赖。

## 11. 实施阶段建议

当前实现进度：`P3-10-B9` 首批已完成 `PublicIndex` 持久化、公开句柄派生、公开资料 / 榜单 / 关系链 / 艾特搜索 / Console 用户排障展示切换和迁移入口；`P3-12-B6-1` 已固定邮箱 + 密码登录、注册 / Bootstrap 必填 `DisplayName`；`P3-12-B6-2` 已完成公开展示与前端状态收敛；`P3-12-B6-3` 已完成展示名变更记录、冷却和滚动窗口治理；`P3-12-B6-4` 已完成 PublicIndex 显式保留号与靓号规则设置、普通注册 / Bootstrap 自动分配跳过逻辑和定向测试；`P3-12-B6-5` 已删除 `LoginName` / `UserRealName` 实体字段、旧身份回填和真实姓名个人资料输入，并把开发默认种子切到 `system/admin/test@radishx.com`；`P3-12-B6-6` 已补齐 B6 验证契约，覆盖账号实体、种子数据、注册 / 登录、个人资料、PublicIndex 分配和前端兼容字段。详见 [P3-10-B9 用户身份语义首批记录](/records/p3-10-b9-user-identity-first-batch-record-2026-06-15) 与 [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)。

### Phase A：文档与契约冻结

- 固定本页字段语义。
- 明确 `Email` 不公开展示，`LoginName` 不再进入当前实体和新库 schema。
- 明确 `UserName` 仅作为数据库历史列和兼容字段承接 `DisplayName` 语义。
- 将公开资料逐步从 `VoUserId` 切向 `VoPublicId` 与公开索引；公开资料的统计、帖子和评论内容查询已先切换到公开 identifier。

### Phase B：模型与注册链路调整

- 增加 `PublicIndex`。（已完成首批）
- `PublicIndex` 使用 `long / Int64`，数据库列使用 `BIGINT` 或等价类型。（已完成首批）
- 补规范化字段或等价唯一索引：当前登录凭证侧优先是 `EmailNormalized`。首批已通过统一小写保存、查询规范化和注册唯一性检查承接运行时语义，后续如需要大小写保留展示值，再补独立规范化列。
- 注册要求邮箱必填，并拆分登录凭证与展示名。（B6-1 已完成邮箱 + 展示名注册）
- 注册页展示名输入必须补充公开展示和改名限制提示，避免用户误以为展示名可以无限次随意调整。
- 登录固定使用邮箱 + 密码。（B6-1 已完成）
- 开发种子补固定保留公开索引。（已完成首批）
- `PublicIndex` 分配器补充靓号保留列表 / 规则跳过逻辑；`UserIdentity.PublicIndex.ReservedIndexes` 与 `UserIdentity.PublicIndex.VanityRules` 已进入 Console 系统设置，人工分配保留号仍需后续权限动作和审计专题承接。（B6-4 已完成自动分配跳过）

### Phase C：搜索、艾特与 Console 治理

- 用户搜索只基于公开字段。（艾特搜索已完成首批）
- 评论、通知、圈子等提及场景使用结构化用户引用。
- Console 支持按邮箱、公开索引、`PublicId` 和内部 ID 排障搜索。（首批已展示公开句柄，搜索治理继续后置）
- 高风险账号字段变更进入二次确认与审计。
- Console 支持展示展示名修改历史、当前改名剩余额度和 `PublicIndex` 靓号保留状态，便于排障与运营治理。

## 12. 当前不做

- 不迁移数据库内部主键。
- 不把 `DisplayName#PublicIndex` 当作公开路由主键。
- 不在公开页面展示登录名或邮箱。
- 不把邮箱通知系统并入本专题首批实现。
- 不立即实现 ActivityPub / WebFinger handle。
- 不为了兼容测试数据保留混乱命名；当前尚未正式运营，后续实现可以做干净迁移。

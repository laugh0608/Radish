# P3-12-B6 身份语义二次收口设计

> 日期：2026-06-22（Asia/Shanghai）
>
> 更新：2026-06-23 补充注册页 `DisplayName` 慎重设置提示、改名频率限制文案，以及 `PublicIndex` 靓号保留 / Console 配置规则。
>
> 状态：`B6-1 身份基础与注册登录`、`B6-2 公开展示与前端状态收敛`、`B6-3 展示名变更治理` 已完成；后续进入 `B6-4 PublicIndex 保留号治理`
>
> 结论：本专题承接 [用户身份语义与公开索引](/architecture/user-identity-semantics) 的首批实现，进一步移除 `LoginName` 公开 / 登录链路，固定“邮箱 + 密码”作为登录凭证，并把 `DisplayName`、`DisplayHandle`、`PublicId`、关注备注的职责彻底拆开。当前项目尚未上线且无正式数据库，B6 按破坏性 schema 收口处理，不提供旧库兼容迁移；实现完成后删除本地 SQLite 并重新初始化。

## 背景

`P3-10-B9` 已完成 `PublicId`、`PublicIndex` 和 `DisplayHandle` 首批落地，但当前仍存在历史字段混用：

- 注册 / 登录仍保留 `LoginName` 语义。
- `User.UserName` 数据库列沿用历史命名，运行时又被当作 `DisplayName`。
- 部分公开展示仍可能从 `UserRealName`、`LoginName`、`PublicId` 或旧 `UserName` 回退。
- 前端部分页面会把 `usr_...` 当作资料页可见文本展示。

这些混用会导致公开身份不稳定，也会让论坛、聊天室、个人页和搜索 / 艾特出现不一致展示。

## 目标模型

| 字段 | 示例 | 职责 | 可见性 |
| --- | --- | --- | --- |
| `Id` / `InternalId` | `20001` | 数据库内部主键、关联、审计 | 不公开展示 |
| `PublicId` | `usr_019e...` | 公开 URL、分享、跨端回流、前后端传递 | 不作为普通资料 / 信息页可见身份文本 |
| `Email` | `user@example.com` | 登录凭证、找回密码、未来通知地址 | 仅本人账号页和 Console 排障上下文可见 |
| `DisplayName` | `萝卜SAMA`、`admin` | 帖子、聊天室、资料页、榜单等主显示名 | 公开展示，可重复 |
| `PublicIndex` | `1000` | 注册顺序公开编号，系统保留段除外 | 不单独展示，只参与公开句柄 |
| `DisplayHandle` | `萝卜SAMA#1000` | 用户可见唯一身份、搜索、艾特、人工辨识 | 公开展示 |
| `FollowRemark` | `项目作者` | 当前用户给已关注用户的私有备注 | 仅当前用户本人可见 |

## 登录与注册

登录凭证改为：

- `Email`
- `Password`

注册表单必须收集：

- `Email`
- `DisplayName`
- `Password`
- `ConfirmPassword`

不再收集、不再展示、不再允许使用 `LoginName` 登录。

注册页的 `DisplayName` 输入区必须明确提示：该名称会公开展示在个人主页、帖子、评论、聊天、榜单和艾特搜索中，应谨慎设置；后续修改受到次数、冷却时间和滚动窗口频率限制。前端文案只做提示，最终限制由服务端执行。

邮箱规则：

- 邮箱规范化后参与唯一性判断。
- 邮箱后缀必须命中白名单；白名单进入 Console 系统设置。
- 建议设置键：`UserIdentity.Email.AllowedDomains`。
- 开发期允许空白名单表示不限制；生产环境应配置明确域名。
- 首批只支持精确域名，例如 `radishx.com,example.org`，不做通配符和正则，避免误放行。

## DisplayName 规则

`DisplayName` 是公开显示名，不承担唯一性职责。

规则：

- 仅允许中文汉字、英文字母大小写和数字。
- 不允许空格、下划线、短横线、点号、`#`、`@`、URL 分隔符、emoji、控制字符和其他符号。
- 保存前做前后空白裁剪；裁剪后再校验。
- 建议长度继续由 Console 系统设置控制，沿用或替换当前 `UserIdentity.DisplayName.MinLength / MaxLength`。
- 可重复；重名通过 `DisplayHandle` 区分。

修改规则：

- 用户可以修改 `DisplayName`。
- 修改后 `PublicIndex` 不变，`DisplayHandle` 动态重新派生。
- 必须限制修改次数和时间间隔；当前通过 `UserIdentity.DisplayName.ChangeCooldownDays`、`UserIdentity.DisplayName.ChangeWindowDays`、`UserIdentity.DisplayName.ChangeWindowMaxCount` 三个 Console 系统设置治理。
- `UserDisplayNameChangeRecord` 记录历史，承接改名前后值、操作者、来源、时间和原因。
- 个人资料改名必须走 `UserService.ChangeDisplayNameAsync` 服务端入口，不允许 Controller 或前端直接写展示名字段绕过治理。

历史内容：

- 帖子、评论、聊天室消息等主显示名可按当前用户最新 `DisplayName` 渲染。
- 结构化艾特关系必须绑定 `PublicId` 或内部 `UserId`，历史 label 不作为关系真值。

## PublicIndex 与 DisplayHandle

`PublicIndex` 按注册顺序由后端分配：

- `1-999` 继续保留给系统、官方、种子、内部账号和 bot。
- 普通注册用户从 `1000` 起，但自动分配必须跳过已配置的保留靓号。
- `1000` 之后允许继续保留公开靓号，例如 `1111`、`2222`、`3333`、`1234`、`1314` 等。
- 靓号保留可以由 Console 维护显式列表，也可以由 Console 维护规则，例如重复数字、顺子、回文或运营自定义 JSON 规则。
- 靓号规则变更只影响后续分配，不自动改写既有用户；人工指定保留号必须有权限、修改原因和审计。
- 删除、禁用或注销用户后不回收。
- 用户改名不改变 `PublicIndex`。
- 分配必须由服务端在数据库事务内完成，前端不得计算。
- 数据库保留唯一索引。

`DisplayHandle` 是派生值：

```text
DisplayName#PublicIndex
```

它是用户可见唯一身份，用于：

- 用户搜索
- 艾特
- 聊天成员辨识
- 榜单
- 公开资料辅助展示
- Console 排障辅助信息

它不是公开路由主键，不替代 `PublicId`。

## 关注备注

关注备注是当前用户对已关注用户的私有标注。

设计建议：

- 在关注关系表或独立备注表中保存 `FollowerUserId + TargetUserId + Remark`。
- 备注仅当前用户可见，不进入公开资料、公开榜单、公开结构化数据或对方视角。
- 备注规则可复用 `DisplayName` 字符集或放宽到中文、英文、数字和少量内部可读符号；首批建议先复用 `DisplayName` 规则，降低搜索解析复杂度。
- 搜索 / 艾特时，对当前用户先匹配关注备注，再匹配 `DisplayHandle`、`DisplayName`、`PublicIndex` 和 `PublicId`。
- 搜索结果展示建议为：`备注（DisplayHandle）`，避免备注遮蔽真实身份导致误选。

## 旧字段清理

本专题要求清理混淆源，而不是只在前端隐藏。

必须退场或降级：

- `LoginName`：从注册、登录、登录回流、Bootstrap、CurrentUser、前端 store 和公开页面退出。
- `UserRealName`：不得作为公开作者名、聊天名、资料页名或搜索 fallback。
- `UserName`：直接重命名或替换为 `DisplayName`，代码、DTO、数据库列和前端类型都不得继续使用 `UserName` 表达展示名。
- `usr_...`：只用于路由、传参、分享和内部定位，不作为普通资料 / 信息页的可见身份文本。

需要重点排查：

- Auth 注册 / 登录页面和验证组件。
- `BootstrapController`、`CurrentUserVo`、前端 `userStore`。
- 论坛作者、评论作者、问答回答、神评 / 沙发、轻回应。
- 聊天消息、频道成员、艾特搜索。
- 公开个人页、我的状态页、排行榜、圈子关系链。
- 转账用户搜索、资产流水中的用户展示。
- Console 用户排障、权限授权、审计日志展示。
- 种子数据和本地初始化账号。

## 代码前盘点

`2026-06-23` 已按当前代码完成 B6 实施前触点盘点。

Auth 注册 / 登录触点：

- `Radish.Auth/Controllers/AccountController.cs`：登录表单参数仍为 `username`，登录查询调用 `GetEnabledUserByLoginNameAsync`；注册仍收集 `Username`，并把 `UserName` 默认写成登录名；OIDC `name` / `preferred_username` 仍回填 `VoLoginName`，`given_name` 仍可能输出 `UserRealName`。
- `Radish.Auth/ViewModels/Account/RegisterViewModel.cs`、`Radish.Auth/Views/Account/Register.cshtml`：注册页仍展示“登录名”输入，没有独立 `DisplayName` 输入和慎重设置 / 改名限制提示。
- `Radish.IService/IUserService.cs`、`Radish.Service/UserService.cs`：登录查询方法名与参数仍是 `LoginName` 语义；运行时实际已同时匹配登录名和邮箱，B6 需要改为邮箱凭证语义。

Bootstrap 初始化触点：

- `Radish.Model/DtoModels/BootstrapDto.cs`、`Radish.Model/ViewModels/BootstrapVo.cs`：首个管理员创建 DTO / VO 仍输出 `LoginName`。
- `Radish.Service/BootstrapService.cs`、`Radish.Repository/BootstrapRepository.cs`：首个管理员初始化仍要求 `LoginName`，并把 `UserName` 设置为登录名；`PublicIndex` 分配未跳过 Console 配置的保留靓号。
- `Frontend/radish.client/src/api/bootstrap.ts`、`Frontend/radish.client/src/bootstrap/BootstrapGate.tsx`：首次部署页仍填写“登录账号”，成功页回显登录名。

后端模型、DTO 与映射触点：

- `Radish.Model/User.cs`：`LoginName`、`UserName`、`UserRealName` 仍同时存在；`UserName` 语义上承接公开展示名但代码和数据库属性名仍混淆；`BuildDisplayHandle` 以 `UserName` 派生。
- `Radish.Model/ViewModels/UserVo.cs`、`CurrentUserVo.cs`、`UserProfileVo.cs`、`UserMentionVo.cs` 等：仍保留 `VoLoginName`、`VoUserName`、`VoUserRealName` 或 `VoRealName` 等旧字段。
- `Radish.Extension/AutoMapperExtension/CustomProfiles/UserProfile.cs`：仍把 `UserName` 映射为 `VoUserName`，并额外派生 `VoDisplayName` / `VoDisplayHandle`；反向映射仍允许 `VoLoginName`、`VoUserRealName` 写回实体。
- `Radish.Model/DtoModels/UpdateMyProfileDto.cs` 与 `UserController.UpdateMyProfile`：个人资料更新仍使用 `UserName` 和 `RealName` 字段；展示名改名次数、冷却和滚动窗口尚未接入。

公开展示、搜索和业务服务触点：

- `Radish.Api/Controllers/UserController.cs`：`GetUserByHttpContext` 仍返回 `VoLoginName`，并用 claim `Current.UserName` 兜底；用户列表搜索仍匹配 `LoginName`；用户资料相关输出仍包含 `VoUserName` / `VoUserRealName`。
- `Radish.Service/ForumDisplayNameHelper.cs`：论坛作者展示仍优先使用 `UserRealName`，需要改为只使用 `DisplayName` / `DisplayHandle`。
- `Radish.Service/UserService.cs`：艾特搜索已不匹配登录名和邮箱，但字段仍基于 `UserName`；`PublicIndex` 自动分配未读取保留靓号列表 / 规则。
- 聊天、评论 typing、排行榜、圈子关系链、经验 / 订单 / 资产流水和通知目标显示仍存在 `UserName` 命名；需要区分“历史显示字段命名清理”和“真实公开文本来源修正”。

前端触点：

- `Frontend/radish.client/src/services/authBootstrap.ts`、`tokenClaims.ts`、`stores/userStore.ts`、`desktop/types.ts`：当前用户状态仍存 `loginName`，token fallback 仍从 `preferred_username` / `name` 读取 `userName`。
- `Frontend/radish.client/src/desktop/Dock.tsx`、`me/MeApp.tsx`：用户界面仍以 `loginName` 作为显示兜底。
- 论坛、聊天、公开个人页、排行榜、转账搜索和资产流水已有部分 `DisplayName` / `DisplayHandle` 兜底，但仍存在 `voUserName`、`userName` 和 `usr_...` fallback 风险，需要按页面逐项收敛。
- `Frontend/radish.console/src/types/user.ts`、`api/userManagement.ts`、`pages/Users/UserList.tsx`、`pages/Users/UserDetail.tsx`：Console 用户治理仍展示登录名、邮箱、展示名和公开句柄；B6 后 Console 可保留邮箱排障上下文，但普通“用户名”列应退场或改名为“展示名 / 公开句柄”。

系统设置与迁移触点：

- `Radish.Model/SystemConfigDefaults.cs`：仍有 `UserIdentity.LoginName.MinLength / MaxLength`；需要移除或降级为历史设置，并新增邮箱白名单、展示名改名频率、`PublicIndex` 保留列表 / 靓号规则。
- `Radish.DbMigrate/DbMigrateRunner.cs`：仍有 B9 阶段旧用户 `PublicId` / `PublicIndex` 回填和保留号纠偏逻辑；B6 按未上线破坏性 schema 收口后，应删除旧库兼容回填。
- `Radish.DbMigrate/InitialDataSeeder.Identity.cs`：种子用户仍设置 `UserName = loginName` 与 `UserRealName = "System User" / "Admin User" / "Test User"`；B6 需要改为明确 `DisplayName` 种子和固定保留 `PublicIndex`。
- `Radish.Api.Tests` 中身份、claim、Bootstrap、用户资料、艾特搜索、论坛作者和 DbMigrate 相关测试需要随字段语义同步调整。

## 数据库与迁移口径

当前项目尚未上线，没有正式生产数据库；本专题不为旧身份字段提供兼容式数据迁移。

执行原则：

- 直接删除或重命名旧字段，不做长期兼容层。
- 不为 `LoginName`、旧 `UserName`、`UserRealName` 编写历史库兼容脚本。
- 身份相关 `DbMigrate` 旧库补列、旧用户回填和保留索引纠偏逻辑随 B6 清理。
- 实现完成后提醒开发者删除本地 SQLite 数据库并重新初始化。
- 正式数据库发布 SQL 只在未来真实上线或存在正式数据库后重新生成。
- 当前仓库中未上线阶段的历史发布脚本已确认清理；后续不把本地 SQLite 迭代历史作为生产迁移链。

本专题仍需要更新：

- 实体、DTO、服务和前端类型。
- 种子数据和初始化账号。
- Console 系统设置定义。
- 测试数据构造。
- 文档中“发布前脚本 / PostgreSQL 发布脚本 / 旧库兼容”的阶段性口径。

## Console 设置

建议新增或调整设置项：

- `UserIdentity.Email.AllowedDomains`
- `UserIdentity.DisplayName.MinLength`
- `UserIdentity.DisplayName.MaxLength`
- `UserIdentity.DisplayName.ChangeCooldownDays`
- `UserIdentity.DisplayName.ChangeWindowDays`
- `UserIdentity.DisplayName.ChangeWindowMaxCount`
- `UserIdentity.PublicIndex.ReservedIndexes`
- `UserIdentity.PublicIndex.VanityRules`

风险等级建议：

- 邮箱白名单：`Medium`，修改会影响注册入口。
- 展示名长度：沿用当前风险等级。
- 改名频率：`Medium`，影响用户行为和搜索 / 艾特稳定性。
- PublicIndex 靓号保留列表 / 规则：`Medium`，影响后续注册编号分配和人工身份辨识；人工指定保留号动作应记录审计。

## B6-1 实施记录

`2026-06-23` 已完成首批身份基础与注册登录代码落地：

- Auth 登录表单与登录服务固定为 `Email + Password`，不再使用登录名查询；OIDC `name` 输出 `DisplayName`，`preferred_username` 输出 `DisplayHandle`。
- Auth 注册表单改为 `DisplayName + Email + Password + ConfirmPassword`，注册页已提示展示名公开展示和后续修改频率限制，展示名长度读取 `UserIdentity.DisplayName.MinLength / MaxLength`。
- Bootstrap 首个管理员初始化改为必填 `DisplayName` 和 `Email`，成功响应返回展示名与邮箱；初始化页提示展示名慎重设置并要求邮箱登录。
- `CurrentUserVo` 与前端 auth store 不再承载 `LoginName` 作为普通显示身份，改为 `DisplayName / DisplayHandle / PublicId / PublicIndex`；Dock、我的状态和 Console 用户治理移除登录名展示列。
- 用户列表搜索从 `LoginName` 改为公开标识 / 展示名 / 邮箱排障语义；`UserVo.VoLoginName` 默认不再由实体 `LoginName` 映射输出。
- 种子用户已改为明确邮箱和展示名；邮箱登录索引从 `idx_user_login_active` 改为 `idx_user_email_active`。
- 旧 `LoginName` 字段和 `UserName` 数据库列仍作为历史兼容层保留；全域字段重命名、`UserRealName` 公开 fallback 清理、展示名改名治理、邮箱白名单和 `PublicIndex` 靓号保留不包含在 B6-1。

本批验证：

- `dotnet build Radish.slnx -c Debug`
- `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~AccountControllerTest|FullyQualifiedName~BootstrapServiceTest|FullyQualifiedName~UserIdentitySemanticsServiceTest|FullyQualifiedName~SystemConfigServiceTest"`
- `dotnet test Radish.Api.Tests`
- `npm run build --workspace=radish.client`
- `npm run build --workspace=radish.console`
- `git diff --check`

本批未执行 Gateway 真实页面 smoke；按项目验证分层，留到 B6 成组功能准备验收或用户明确启动前后端后覆盖。

## B6-2 实施记录

`2026-06-23` 已完成公开展示与前端状态收敛代码落地：

- `ForumDisplayNameHelper` 不再从 `UserRealName` 回退公开作者名，论坛作者、评论互动和神评相关显示只走公开展示名语义。
- `UserProfileVo` 补 `VoDisplayName`，前端全局用户状态新增明确 `displayName`，旧 `userName` 仅作为兼容别名保留。
- `radish.client` 新增统一公开身份解析工具，Dock、我的状态、个人中心、公开个人页、结构化数据、榜单、圈子、聊天、论坛艾特、转账搜索和帖子互动过滤 `usr_...` 内部标识可见风险，并优先展示 `DisplayHandle / DisplayName`。
- `radish.console` 新增本地公开身份解析工具，顶栏、当前用户上下文、用户列表、用户详情、个人资料和订单详情展示改为公开身份口径。
- 聊天、胡萝卜流水、经验流水 / 治理摘要、订单用户展示等服务层填充值改为 `DisplayHandle` 优先、无公开索引时回退 `DisplayName`；DTO 字段名仍保留历史 `VoUserName` 兼容。
- AutoMapper 反向映射不再允许 `VoLoginName` 写回实体 `LoginName`，避免普通 VO 再影响历史登录名字段。
- 本批不包含 `B6-3` 展示名改名记录 / 冷却治理、`B6-4` PublicIndex 靓号保留规则、邮箱白名单、数据库列全量重命名或本地 SQLite 重建。

本批验证：

- `dotnet test Radish.Api.Tests`
- `dotnet build Radish.slnx -c Debug`
- `npm run build --workspace=radish.client`
- `npm run build --workspace=radish.console`
- `git diff --check`

本批未执行 Gateway 真实页面 smoke；按项目验证分层，留到 B6 成组功能准备验收或用户明确启动前后端后覆盖。

## B6-3 实施记录

`2026-06-23` 已完成展示名变更治理代码落地：

- 新增 `UserDisplayNameChangeRecord`，记录租户、用户、旧展示名、新展示名、操作人、来源、原因、变更时间和创建信息。
- 新增 `UserIdentity.DisplayName.ChangeCooldownDays`、`UserIdentity.DisplayName.ChangeWindowDays`、`UserIdentity.DisplayName.ChangeWindowMaxCount` 三个 `Medium` 风险系统设置，默认分别为 `30` 天冷却、`365` 天滚动窗口、窗口内最多 `3` 次，设置为 `0` 时关闭对应限制。
- `IUserService.ChangeDisplayNameAsync` 成为展示名修改唯一业务入口，统一校验展示名长度 / 保留字符 / 控制字符、冷却时间和滚动窗口次数，并在事务方法中更新用户展示名和写入审计记录。
- `UserController.UpdateMyProfile` 不再直接写 `UserName` 列；个人资料中的展示名修改委托服务层治理，邮箱、真实姓名、性别、年龄、生日和地址仍沿用个人资料更新路径。
- 本批不包含 `B6-4` PublicIndex 靓号保留列表 / 规则、邮箱白名单、关注备注、字段全量重命名或本地 SQLite 重建。

本批验证：

- `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~UserIdentitySemanticsServiceTest|FullyQualifiedName~UserControllerProfileTest"`
- `dotnet test Radish.Api.Tests`
- `dotnet build Radish.slnx -c Debug`
- `git diff --check`

本批未执行 Gateway 真实页面 smoke；按项目验证分层，留到 B6 成组功能准备验收或用户明确启动前后端后覆盖。

## 实施顺序

1. 冻结本专题设计与字段语义。（已完成）
2. 完成代码前触点盘点和分批方案。（已完成）
3. `B6-1 身份基础与注册登录`：调整 `User` 实体 / DTO / AutoMapper 命名，固定邮箱 + 密码登录；注册和 Bootstrap 必填 `DisplayName`，注册页补慎重设置和改名限制提示；OIDC claim 与 CurrentUser 不再输出登录名作为普通显示身份。（已完成）
4. `B6-2 公开展示与前端状态收敛`：清理 `UserRealName` 公开 fallback、`VoLoginName` 普通前端状态、`VoUserName` 混淆展示；论坛、聊天、榜单、圈子、公开个人页、转账搜索和 Console 用户治理统一使用 `DisplayName` / `DisplayHandle`。（已完成）
5. `B6-3 展示名变更治理`：新增 `UserDisplayNameChangeRecord`，接入冷却时间、滚动窗口和窗口内最大次数设置；个人资料改名走服务端校验和历史记录，不允许绕过频率限制。（已完成）
6. `B6-4 PublicIndex 保留号治理`：新增 `UserIdentity.PublicIndex.ReservedIndexes` 与 `UserIdentity.PublicIndex.VanityRules` 设置，注册和 Bootstrap 分配器在数据库事务内跳过保留靓号；规则变更只影响后续分配。
7. `B6-5 种子与 DbMigrate 收口`：更新 system / admin / test 种子展示名和保留号；删除身份旧库回填与旧兼容纠偏逻辑；实现后提醒删除本地 SQLite 并重新初始化。
8. `B6-6 验证与阶段验收`：补身份语义扫描、Auth / Bootstrap / 用户服务 / 展示名修改 / PublicIndex 保留号定向测试、前端类型检查和 Gateway PC / mobile 页面 smoke。

代码实现前确认点：

- 允许破坏性重命名 `User.UserName` 为 `DisplayName`，并让新建本地库生成新列名；不保留旧 SQLite 兼容。
- `LoginName` 从注册、登录、Bootstrap、CurrentUser、前端 store 和普通页面退场；如数据库层短期仍保留字段，只允许作为历史内部字段，不进入登录凭证和公开展示。
- `UserRealName` 不再作为公开作者名或资料页名；如保留为本人私密资料字段，需从公开 DTO、论坛 helper 和普通前端显示退场。
- B6 实现完成后删除本地 SQLite 数据库并重新初始化，不编写旧库兼容迁移。

## 验证口径

代码实现阶段至少覆盖：

- Auth 注册 / 登录定向测试。
- 用户身份服务定向测试。
- 邮箱白名单设置测试。
- 展示名规则、修改次数和冷却测试。
- 关注备注搜索 / 艾特测试。
- `validate:identity`。
- `npm run build --workspace=radish.client`。
- `npm run build --workspace=radish.console`。
- `dotnet test Radish.Api.Tests` 中身份相关测试。

阶段验收时，在用户明确说明前后端已启动后，再覆盖 Gateway PC / mobile 页面 smoke：

- 注册
- 登录
- `/me`
- `/u/:identifier`
- `/forum`
- `/messages`
- 艾特搜索
- Console 系统设置与用户排障页

## 当前不做

- 不把 `DisplayHandle` 替代 `PublicId` 路由。
- 不启动数据库内部主键迁移。
- 不启动邮箱验证和找回密码完整邮件系统，除非另立安全专题。
- 不启动 ActivityPub / WebFinger。
- 不把关注备注公开给其他用户。
- 不为旧本地 SQLite 编写兼容迁移；实现完成后删除本地库并重建。
- 不在未完成字段清理前进入发布候选。

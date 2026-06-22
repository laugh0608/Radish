# P3-12-B6 身份语义二次收口设计

> 日期：2026-06-22（Asia/Shanghai）
>
> 状态：设计边界已确认，待代码实现
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
- 必须限制修改次数和时间间隔。
- 建议新增 `UserDisplayNameChangeRecord` 记录历史，承接改名前后值、操作者、来源、时间和原因。
- 建议新增 Console 设置：冷却时间、滚动窗口天数、窗口内最大修改次数。

历史内容：

- 帖子、评论、聊天室消息等主显示名可按当前用户最新 `DisplayName` 渲染。
- 结构化艾特关系必须绑定 `PublicId` 或内部 `UserId`，历史 label 不作为关系真值。

## PublicIndex 与 DisplayHandle

`PublicIndex` 按注册顺序由后端分配：

- `1-999` 继续保留给系统、官方、种子、内部账号和 bot。
- 普通注册用户从 `1000` 起。
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

风险等级建议：

- 邮箱白名单：`Medium`，修改会影响注册入口。
- 展示名长度：沿用当前风险等级。
- 改名频率：`Medium`，影响用户行为和搜索 / 艾特稳定性。

## 实施顺序

1. 冻结本专题设计与字段语义。
2. 调整 Auth 注册 / 登录 DTO、校验、页面文案和登录回流。
3. 新增邮箱白名单设置并接入注册校验。
4. 新增 `DisplayName` 明确 DTO / 服务命名，替换公开展示 fallback。
5. 新增展示名修改历史和频率限制。
6. 新增关注备注数据结构、API 和搜索 / 艾特优先级。
7. 清理 `LoginName`、`UserRealName`、`usr_...` 可见展示和旧字段回退。
8. 更新种子数据，清理身份相关 `DbMigrate` 旧库兼容逻辑，并保持上线前不维护历史发布脚本的数据库结构口径。
9. 补身份语义扫描、后端定向测试、前端类型检查和关键页面 smoke。

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

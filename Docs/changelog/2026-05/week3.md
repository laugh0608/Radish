# 2026 年 5 月第 3 周开发日志

> 范围：2026-05-11 至 2026-05-17（Asia/Shanghai）
>
> 本周继续沿“产品功能补全与多端任务重排”推进，执行面集中在后端 + Console 治理，优先收口经验治理、内容治理审核效率与商城管理前端缺口。

## 2026-05-11

### 主线判断

- 当日执行重点继续落在后端 + Console 治理，不再新增大范围产品线，而是把已进入当前主线的经验治理、内容治理和商城管理缺口补成可用工作流。
- `P2-C3` 经验 / 等级治理保持“最小可解释规则 + 人工复核”边界；内容治理继续聚焦人工审核与手动处置；商城治理则优先服务资产 / 订单人工追查。
- 当前这条治理线完成后，下一步不宜继续扩新规则或新平台，而应先做商城管理人工验收，并按结果整理剩余尾项。

### 已完成提交

- `e01901b6 fix(client): resolve desktop external app urls`
  - 修复桌面外部应用 URL 处理中的既有问题，避免继续干扰桌面工作台稳定性。
- `3eb1788e feat(console): improve moderation queue filtering`
  - 提前补了一轮内容治理审核队列筛选能力。
- `2e618ef2 docs(planning): record flutter ui library trigger`
  - 记录 Flutter UI 组件库触发点，保持规划口径连续。
- `494e36e8 feat(console): add experience transaction review`
  - 为经验治理补齐经验流水回看入口。
- `11600f92 feat(console): 收口经验异常规则治理`
  - 统一经验治理页的异常观察口径与管理端展示。
- `a2f624f6 feat(experience): 串起异常复核闭环`
  - 把异常规则摘要、异常日期与经验流水筛选联动起来，支持一键带入复核。
- `a8d99259 feat(experience): 沉淀治理复核留痕`
  - 补齐人工复核记录接口、冻结 / 解冻自动留痕，以及治理快照回看。
- `7cac6909 feat(moderation): 收口审核台效率`
  - 打通举报队列、治理日志、来源举报和目标动作的查询闭环。
- `ae631cb9 feat(moderation): 接入手动治理处置入口`
  - 审核台接入手动禁言 / 封禁 / 解封 / 解除禁言，并和治理日志联动。
- `6841d29a feat(moderation): 前置展示当前治理状态`
  - 在手动治理区直接展示目标用户当前生效中的禁言 / 封禁状态。
- `9edf138b feat(shop): 收口商城后台订单商品治理`
  - 补齐管理端订单详情、商品详情、商品删除拦截，以及订单 / 商品 / 用户三向治理回跳。
  - 收口 `shopApi` 契约漂移，并补商品 / 订单相关测试与 HTTP 示例。

### 验证记录

- `npm run build --workspace=radish.console`
  - 通过。
  - 当日经验治理、内容治理和商城治理批次均已在该构建入口下确认无新增前端失败。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter Experience`
  - 先后通过 `13/13`、`15/15`、`20/20`，覆盖经验异常规则、复核联动与治理留痕批次。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter ContentModeration`
  - 先后通过 `13/13`、`14/14`，覆盖审核效率、手动处置与当前治理状态批次。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter "FullyQualifiedName~ShopControllerTest|FullyQualifiedName~OrderServiceTest|FullyQualifiedName~ProductServiceTest"`
  - 提权环境通过，`18/18`。
  - 首次在沙盒中失败原因仅为无法读取用户级 `NuGet.Config`，不是本轮代码问题。

### 文档与对齐结论

- 今日提交横跨经验治理、内容治理和商城管理前端缺口，不能只保留代码提交；本次回顾后已同步更新 [current.md](/planning/current)、[development-plan.md](/development-plan)、[phase-two-product-completion.md](/planning/phase-two-product-completion)、[Console 系统设计方案](/guide/console-system) 与 [商城系统设计方案](/guide/shop-system)。
- 月度开发日志入口已新增本周条目，并把“明日第一事项”从已完成的内容治理审核效率，切换为商城管理人工验收收口。
- 今日没有新的多端路线、壳层归属或视觉规范变化，因此不额外改动多壳层策略与视觉设计规范文档。

### 剩余风险与下一顺位

- 经验治理和内容治理已完成首轮收口，但应继续保持人工复核和手动处置边界，不扩自动处罚或大而全治理平台。
- 商城管理前端缺口已基本补齐，但仍需要一轮手工验收，重点确认商品上下架、删除拦截、订单详情 / 备注和三向回跳在真实 Console 账号下的可用性。
- 若人工验收稳定，后端 + Console 治理的下一顺位应转向剩余历史构建 warning 与少量安全治理尾项，而不是继续在已收口模块上做低收益微调。

## 2026-05-12

### 商城管理人工验收收口

- 新增 [商城管理人工验收记录（2026-05-12）](/records/shop-console-management-acceptance-record-2026-05-12)，确认商品相关订单、详情长 ID、删除确认、订单跳用户 / 商品返回链路已修复。
- 后端商品删除保护已纳入验收结论：已有任意订单记录的商品不允许删除，避免破坏历史订单追溯。

### 历史构建 warning 与安全治理尾项

- `radish.console` 已从单入口静态页面导入改为路由级懒加载，并按 `React / Router / Ant Design / @radish/ui / @radish/http` 等边界拆分产物，Console 构建不再输出 chunk size warning。
- 后端同步清理一批低风险 warning：
  - `Radish.Gateway` 使用 .NET 10 推荐的 `KnownIPNetworks` 替代过时 `KnownNetworks`。
  - `PermissionRequirementHandler` 过滤空权限 URL / 角色，并使用大小写不敏感的整段正则匹配，减少无效权限数据靠异常吞掉的安全边界噪音。
  - `AttachmentController` 补齐 `GetMyAttachments` XML 参数注释。
  - `ConsoleAuthorizationController` 未找到角色时不再显式写入泛型 `default` 响应体。
- 继续清理后端历史构建 warning：
  - `Radish.Extension` AOP / Redis / SqlSugar / Scalar 的 nullable warning 已清理，补齐 Redis 缓存未命中、Scalar 文档组件和 SqlSugar 操作名解析的空值边界。
  - `Radish.Repository` 事务 / 多租户基础仓储 warning 已清理，`ISqlSugarClient` 类型不符合事务要求时改为明确失败，分库 `TenantAttribute.configId` 缺失时给出显式异常。
  - `Radish.Common` 和 `Radish.Model` 的历史 nullable warning 已按既有语义收口，保留缓存未命中返回默认值、启动期服务后置注入、无数据响应可为空和历史背包道具展示兼容。
  - `AttachmentService` 去重发现物理文件缺失时改为软删除旧附件记录，避免维护任务继续引入物理删除口径。
- `Radish.Gateway` 已将配置源注册从 `ConfigureAppConfiguration` 等价迁移到 `WebApplicationBuilder.Configuration`，保持共享配置、宿主配置、本地覆盖和环境变量的加载顺序不变，清除最后一条 `ASP0013` analyzer warning。
- 安全治理尾项首批处理文件访问令牌：
  - `FileAccessTokenService` 不再在日志和“不存在”异常中输出完整 token，改为日志脱敏摘要。
  - 创建令牌时统一校验附件 ID、有效期、访问次数和授权用户 ID，避免无效附件、负数访问次数、超长有效期或无效授权用户进入持久化。
  - `CreateFileAccessTokenDto` 补齐 `AuthorizedIp`，避免调用方传入 IP 限制但服务端未落库导致限制失效。
  - 补充 `FileAccessTokenServiceTest` 覆盖合法创建、非法请求拒绝、无效授权 IP 拒绝、空 token 不查询、有效 token 访问计数更新，以及不存在 token 异常不泄露原始 token。
- 安全治理尾项第二批处理权限匹配、Console 授权一致性与缓存默认值边界：
  - 新增 `PermissionUrlMatcher`，统一权限 URL 正则整段匹配、根路径要求、无效正则吞吐和匹配超时边界，避免 `PermissionRequirementHandler` 直接执行不受控正则。
  - `ConsolePermissions.GetPermissionsByApiUrl` 支持映射表正则兜底匹配，并补齐 `ConsoleAuthorization`、商城管理商品详情、经验每日统计和经验交易记录等 Console 接口映射。
  - 新增授权边界测试，确认所有声明 `RequireConsolePermission` 的 Action 均有认证入口且不允许匿名访问。
  - `Caching` 与 `ExperienceCalculator` 对 `0` / 负数缓存过期配置回落到默认 TTL，避免异常配置导致缓存写入失败。
- Console 权限种子一致性继续收口：
  - `DbMigrate` 补齐 `ConsoleAuthorization`、商城商品 / 订单详情、经验每日统计、经验流水、治理留痕与复核记录等 `ApiModule` 种子。
  - `ConsoleResourceApiSeed` 同步补齐对应资源关联，避免代码层权限映射存在但授权 UI 预览和资源授权派生缺数据。
  - `check-console-permissions` 扩展为同时校验 `ApiModule.LinkUrl` 与 `ConsoleResourceApiSeed`，覆盖后端映射、接口资源种子和 Console 资源关联三层一致性。

### WebOS / PC 工作台真实功能缺口

- 萝卜坑交易记录导出从占位提示改为真实 CSV 下载：
  - 导出时复用当前类型 / 状态筛选条件拉取全部分页交易记录。
  - 前端补齐日期范围与关键字的导出过滤，避免导出结果绕过用户已填写的筛选条件。
  - CSV 包含交易流水号、创建时间、类型、状态、金额、手续费、发起方、接收方、业务信息与备注，并使用 UTF-8 BOM 兼容常见表格工具。
  - 导出过程中禁用按钮并在页面内展示成功 / 空结果 / 失败提示，不再弹出“开发中”占位。

### 验证记录

- `npm run build --workspace=radish.console`
  - 通过。
  - 构建产物最大 chunk 为 `vendor-antd-_util`，约 `397.53 kB`，未再触发 `500 kB` chunk size warning。
- `dotnet build Radish.slnx -c Debug -v minimal`
  - 提权环境通过。
  - 本次增量构建当前可见 warning 剩余 `24` 条，集中在 `Radish.Extension` 的 AOP、Redis、SqlSugar nullable 存量；Gateway `ASPDEPR005`、权限处理器本轮触达 warning、Attachment XML 注释 warning 与 Console 授权 `default` warning 已消失。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter AttachmentControllerTest -v minimal`
  - 提权环境通过，`3/3`。
- `dotnet build Radish.slnx -c Debug -t:Rebuild -v minimal`
  - 提权环境通过。
  - 完整重建已达成 `0` warning / `0` error。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -v minimal`
  - 提权环境通过，安全治理尾项后为 `359/359`。
- `dotnet test Radish.Api.Tests -v minimal`
  - 提权环境通过，权限匹配、Console 授权一致性与缓存默认值边界治理后为 `373/373`。
- `dotnet test Radish.Api.Tests -v minimal`
  - 提权环境通过，Console 权限种子一致性补齐后为 `374/374`。
- `dotnet build Radish.slnx -c Debug -v minimal`
  - 提权环境通过，完整解决方案 Debug 构建保持 `0` warning / `0` error。
- `dotnet build Radish.slnx -c Debug -t:Rebuild -v minimal`
  - 提权环境通过，完整重建保持 `0` warning / `0` error。
- `npm run check:console-permissions`
  - 通过，后端资源映射 `85` 条 URL、`ApiModule` 种子 `86` 条、`ConsoleResourceApiSeed` `85` 条完成基础对齐。
- `npm run check:repo-hygiene:changed`
  - 通过，未发现文本卫生问题。
- `npm run build --workspace=radish.client`
  - 通过。
  - 本轮萝卜坑交易导出改动可编译；构建仍提示既有 `app-shop` chunk 超过 `500 kB`，与本次交易导出改动无直接关系，后续可作为单独前端打包治理项处理。
- `dotnet build Radish.slnx -c Debug -t:Rebuild -v minimal`
  - 提权环境通过。
  - 完整重建继续保持 `0` warning / `0` error。

### 收工文档复盘

- 今日提交已按“商城验收、构建 warning、安全治理、Console 权限种子一致性、WebOS 萝卜坑导出”五条线完成回顾。
- 已同步更新路线图、产品功能补全规划、Console 说明书、Console 权限治理说明、商城说明书与萝卜坑功能 / 实施说明，避免入口文档继续停留在“商城人工验收 / 安全尾项待处理”的旧口径。
- 明日事项已切换为 WebOS / PC 工作台真实功能缺口；若优先做构建治理，则单独处理 `radish.client` 既有 `app-shop` chunk warning。

### 阶段规划复核

- 复核整体规划后确认：今日商城验收、构建 warning、安全治理、Console 权限种子一致性和萝卜坑导出仍属于第二阶段“产品功能补全与多端任务重排”，没有偏离长期方向。
- 当前已接近“继续补洞”收益递减边界；后续不再把低收益按钮、提示、非阻断 warning 或视觉微调作为默认主线。
- 下一顺位调整为“第二阶段收口评审”：先形成已完成 / 转维护 / 后置 / `P0/P1` 待闭环清单，再决定是否由 Flutter 移动端高价值已登录链路接棒为下一主任务；第三开发阶段暂不直接启动。

### 说明书补齐

- 补齐附件临时访问令牌说明书：记录创建参数边界、授权 IP 落库、空 token 不查询和 token 日志脱敏要求。
- 补齐缓存与经验等级说明书：记录 `ICaching` 与 `ExperienceCalculator` 对非正数 TTL 回退默认值的运行时语义。
- 补齐 Console 权限治理说明：记录 `PermissionUrlMatcher` 的整段匹配、根路径要求、无效正则 / 超时安全失败，以及新增 Console 接口后的三层种子对齐要求。

## 2026-05-13

### 第二阶段收口与 Flutter 主线切换

- 新增 [第二阶段收口评审](/planning/phase-two-closure-review)，将第二阶段成果拆为已完成、转维护、明确后置和 `P0/P1` 筛查清单。
- 按资产、安全、登录、购买、转账和主路径做窄范围筛查，未发现新的必须立即闭环 `P0/P1` 阻断项。
- 当前主线先切到 `P2-C5-A Flutter 移动端高价值已登录链路首批评估`，并在首批完成后转入第二阶段归档判断；不进入第三阶段，也不继续无边界扫低收益尾项。

### Flutter 通知轻入口首批补强

- `RadishFlutterShell` 的论坛通知入口从“有目标才展示”改为可解释状态：
  - 已登录后展示检查状态。
  - 无 forum 回流目标时展示“暂无论坛通知”。
  - 仓储读取失败时展示“通知刷新失败”。
  - 有目标时继续展示“查看论坛通知”，并跳转共享 forum detail。
- 增加“刷新通知”轻入口，允许用户手动重新读取最新 forum 通知目标。
- 保持当前边界：不做完整通知中心、不接系统通知栏、不扩发帖、完整评论提交、点赞、投票、编辑治理、聊天或完整商城工作台。

### Flutter 个人复访轻操作补强

- `ProfilePage` 的“我的轻回应”回流条目补齐目标上下文，展示帖子 ID、轻回应 ID、“轻回应回看”和“原帖回流”标签。
- 保持复访边界：不新增完整浏览历史、收藏、关注、通知中心或资料编辑能力，只让已存在的回流入口更容易判断目标。

### `P2-C5-A` 小收口结论

- `P2-C5-A` 首批建议范围已完成：最小通知入口、forum detail 回流、个人复访轻操作均已有实现和定向测试覆盖。
- 当前没有继续追加 Flutter 低收益微体验的必要；下一步切到第二阶段归档判断。
- 后续若发现资产、安全、登录、购买、转账、权限授权或主路径中断问题，再按 `P0/P1` 小闭环回拉。

### 第二阶段归档判断启动

- [第二阶段收口评审](/planning/phase-two-closure-review) 已补充归档 Go / No-Go 条件、归档候选成果和回拉边界。
- 当前不直接写入 [已完成摘要](/planning/archive)，等归档判断 Go 后再把第二开发阶段作为已完成里程碑归档。

### 第二阶段归档 Go

- 第二阶段归档判断结论为 Go，已写入 [已完成摘要](/planning/archive)。
- 当前主线切到下一阶段主任务选择；第三开发阶段仍需单独形成目标、边界和入口文档口径后再启动。

### `P3-0` 第三阶段定义与工程整备启动

- 新增 [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)，将第三阶段主题暂定为“真实使用增长与长期契约治理”。
- 当前不直接铺第三阶段大功能，先启动 `P3-0` 定义与工程整备窗口，范围包括公开内容壳层、WebOS 工作台、Flutter、Console / 后端治理、外部 ID 契约和代码热区审计。
- 第一批候选顺序暂定为公开内容增长基础、`PublicId` 最小试点方案、代码热区拆分与用户留存轻闭环。
- [当前进行中](/planning/current)、[开发路线图](/development-plan) 与 [Backlog](/planning/backlog) 已同步到 `P3-0` 口径，避免后续继续把第二阶段低收益尾项作为默认主线。

### `P3-0-A` 公开内容增长基础审计

- 已审计公开壳层 SEO / 分享 / canonical / robots / sitemap 现状：当前公开页面有运行时 `document.title` 和路由归一化，但缺统一 `head` 契约、`meta description`、Open Graph、`link[rel="canonical"]`、`robots.txt` 与 sitemap seed。
- docs 公开详情已有复制 canonical 链接能力；forum / shop / profile / discover 暂未形成统一分享入口。
- Gateway catch-all 当前会把未匹配路径转到 frontend，生产前端静态服务可直接服务 dist 内静态文件；第一批可先由前端承载静态 robots 与 sitemap seed，动态详情 sitemap 后置单独评估。
- `P3-1` 第一批建议收口为“公开内容 SEO 与分享基线”：先做统一 head helper、公开页面运行时 head、静态 robots、静态 sitemap seed、forum detail / shop detail 复制 canonical 链接，不直接启动 SSR / SSG 或动态 sitemap。

### `P3-1-A` 公开 head 与抓取入口基线

- 新增 `publicHead` 工具，统一公开路由的 `document.title`、`meta description`、Open Graph、`link[rel="canonical"]` 与 canonical URL 构造。
- `PublicEntry` 已接入运行时 head 基线，覆盖 discover、forum、docs、profile、leaderboard、shop。
- `index.html` 已切到中文语言与 Radish 默认 meta / Open Graph 基线；新增前端静态 `robots.txt` 与 `sitemap.xml`，以 `https://radishx.com` 作为当前公开域名 seed。
- 新增 `publicHead` 与静态 SEO 文件测试，并纳入 `radish.client` 现有 `node --test` 入口。

### `P3-1-B` 公开详情分享入口

- forum detail 已新增复制 canonical 链接按钮，支持复制中、成功、失败反馈。
- shop detail 已新增复制 canonical 链接按钮，支持复制中、成功、失败反馈。
- docs detail 既有复制链接入口保持不变，公开详情分享基线首批收口。
- 下一主线切到 `P3-2 PublicId 最小试点方案`；完整动态 sitemap、详情结构化数据和 SSR / SSG 后置专题评估。

### `P3-2-A` 外部 ID 契约审计

- 已审计公开路由、通知 `extData`、窗口参数、分享链接、API 返回和浏览历史中的外部对象 ID 暴露面。
- 最小试点对象收敛为 `Post`：forum 帖子同时覆盖公开传播、SEO / canonical、分享、通知回流、窗口参数和浏览历史，且现有前端基本按字符串处理 `postId / commentId`，兼容风险可控。
- 首批不扩到 `User / Product / WikiDocument / Comment`：`User` 涉及身份与隐私，`Product` 涉及交易与订单，`WikiDocument` 已有 slug，`Comment` 暂作为帖子详情定位参数保留。
- 兼容策略已写入 [第三开发阶段专题](/planning/phase-three-real-usage-contract-governance) 与 [标识体系与社区联邦长期路线](/architecture/id-and-federation-roadmap)：`PostVo` 并行暴露 `VoPublicId / VoId`，forum 公开路由、canonical、通知 `extData` 和窗口参数优先支持 `postPublicId`，同时保留旧 `postId / VoId / TargetId`。
- 回滚边界已明确：不删除数据库列和值，旧 long 路由和通知字段继续可用；试点稳定前不做数据库主键迁移、全量 DTO 替换或 ActivityPub / WebFinger 实现。

### `P3-2-B` `Post.PublicId` 首批实现

- `Post` 已增加可空唯一 `PublicId`，新发帖生成 `pst_` + UUIDv7 编码体；历史空值继续通过旧 long 链路兼容。
- `PostVo` 并行暴露 `VoPublicId / VoId`，`PostController.GetById` 支持 long 与 PublicId 双读，浏览次数和浏览历史仍以内部 `VoId` 作为关联键。
- forum 公开详情、复制 canonical 链接、运行时 head、浏览历史 routePath、通知 `extData` 和 WebOS forum 窗口参数均支持 `postPublicId`，并保留旧 `postId` 回退。
- 评论定位、轻回应墙和详情加载在通过 PublicId 打开帖子后，会回到真实 `VoId` 调用内部评论 / 轻回应接口，避免把 PublicId 误传给内部 long 接口。
- 首批仍不扩到 `User / Product / WikiDocument / Comment`，不启动历史数据批量补齐、数据库主键迁移或完整 `PublicId` 全量迁移。

### `P3-3-A` `PublicForumApp` 首批低风险拆分

- 已抽出 `publicForumUtils.ts`，承载公开论坛 route key、分页、阅读 guide、PublicId route identifier、评论树 children merge 等纯 helper。
- 已抽出 `PublicStatusCard.tsx`，收口公开论坛多处加载 / 空态 / 错误状态展示。
- 已抽出 `PublicForumTypeFeed.tsx`，独立承载问答 / 投票 / 抽奖类型流页面。
- `PublicForumApp.tsx` 从约 `2911` 行降到约 `2289` 行；本批暂不移动 `PublicForumDetail`，避免扩大刚落地的 PublicId 评论定位风险。

### `P3-3-A` 公开论坛搜索 / 标签页拆分

- 继续小步拆分 `PublicForumApp.tsx`：抽出 `PublicForumSearch.tsx`，独立承载公开搜索页的关键词、排序、时间范围、分页、滚动恢复、神评预览和只读状态展示。
- 抽出 `PublicForumTag.tsx`，独立承载标签详情读取、标签 canonical route 同步、标签帖子列表、分页、神评预览和标签 metadata 展示。
- `PublicForumApp.tsx` 从首批拆分后的约 `2289` 行继续降到约 `1393` 行；当前仍保留 `PublicForumList` 和 `PublicForumDetail` 在主文件内。
- 本批边界保持不变：不移动 `PublicForumDetail`，不改公开论坛路由、数据请求、PublicId / long 双读兼容、搜索参数同步或标签 canonical 行为。
- 已完成提交：
  - `6e338389` `refactor(client): 拆分公开论坛搜索页`
  - `5997cb75` `refactor(client): 拆分公开论坛标签页`

### `P3-3-A` 搜索 / 标签拆分验证

- `npm run type-check --workspace=radish.client`
  - 搜索页拆分后通过。
  - 标签页拆分后通过。
- `node --test --test-isolation=none ./tests/forumNavigation.test.ts ./tests/workspaceNavigation.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicRouteSync.test.ts ./tests/publicHead.test.ts ./tests/publicSeoStatic.test.ts`
  - 搜索页拆分后通过，`73/73`。
  - 标签页拆分后通过，`73/73`。
- `npm run check:repo-hygiene:changed`
  - 搜索页拆分后通过。
  - 标签页拆分后通过。
- `node Scripts/check-repo-hygiene.mjs --stdin-z`
  - 已显式检查新增组件与 `PublicForumApp.tsx`，搜索页 / 标签页两批均通过。
- `git --no-pager diff --check`
  - 搜索页 / 标签页两批均通过。
- `git --no-pager diff --check -- Frontend/radish.client/src/public/forum/PublicForumApp.tsx Frontend/radish.client/src/public/forum/PublicForumSearch.tsx`
  - 通过。
- `git --no-pager diff --check -- Frontend/radish.client/src/public/forum/PublicForumApp.tsx Frontend/radish.client/src/public/forum/PublicForumTag.tsx`
  - 通过。

### `P3-3-A` 公开论坛列表页拆分

- 继续抽出 `PublicForumList.tsx`，独立承载公开论坛默认列表页的分类读取、分类详情读取、分类切换、列表加载、分页、滚动恢复、神评预览、阅读 guide 和只读状态展示。
- `PublicForumApp.tsx` 从约 `1393` 行继续降到约 `862` 行；当前主文件只保留公开论坛外层路由容器与 `PublicForumDetail`。
- 本批仍不移动 `PublicForumDetail`，避免把评论定位、轻回应墙、分享 canonical、PublicId / long 双读和详情加载状态混入同一批结构拆分。
- 已完成提交：
  - `1ae57c8b` `refactor(client): 拆分公开论坛列表页`

### `P3-3-A` 列表页拆分验证

- `npm run type-check --workspace=radish.client`
  - 列表页拆分后通过。
- `node --test --test-isolation=none ./tests/forumNavigation.test.ts ./tests/workspaceNavigation.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicRouteSync.test.ts ./tests/publicHead.test.ts ./tests/publicSeoStatic.test.ts`
  - 列表页拆分后通过，`73/73`。
- `npm run check:repo-hygiene:changed`
  - 列表页拆分后通过。
- `node Scripts/check-repo-hygiene.mjs --stdin-z`
  - 已显式检查新增 `PublicForumList.tsx` 与 `PublicForumApp.tsx`，通过。
- `git --no-pager diff --check`
  - 列表页拆分后通过。
- `git --no-pager diff --check -- Frontend/radish.client/src/public/forum/PublicForumApp.tsx Frontend/radish.client/src/public/forum/PublicForumList.tsx`
  - 通过。

### `P3-3-B` 公开论坛详情页拆分

- 已批准并完成 `PublicForumDetail.tsx` 独立组件拆分，原样承载公开帖子详情加载、PublicId / long 双读后的真实 `VoId` 回落、评论定位、子评论补载、轻回应墙、复制 canonical 链接、滚动高亮和只读状态渲染。
- `PublicForumApp.tsx` 从约 `862` 行降到约 `208` 行，当前只保留公开论坛外层路由容器、滚动恢复、返回入口和子组件接入。
- 本批不拆详情页内部 hooks，不改接口、不改评论定位、不改 PublicId 兼容、不改分享 canonical，不改 UI 和文案。
- 已完成提交：
  - `5f704b59` `refactor(client): 拆分公开论坛详情页`

### `P3-3-B` 详情页拆分验证

- `npm run type-check --workspace=radish.client`
  - 详情页拆分后通过。
- `node --test --test-isolation=none ./tests/forumNavigation.test.ts ./tests/workspaceNavigation.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicRouteSync.test.ts ./tests/publicHead.test.ts ./tests/publicSeoStatic.test.ts`
  - 详情页拆分后通过，`73/73`。
- `npm run check:repo-hygiene:changed`
  - 详情页拆分后通过。
- `node Scripts/check-repo-hygiene.mjs --stdin-z`
  - 已显式检查新增 `PublicForumDetail.tsx` 与 `PublicForumApp.tsx`，通过。
- `git --no-pager diff --check`
  - 详情页拆分后通过。
- `git --no-pager diff --check -- Frontend/radish.client/src/public/forum/PublicForumApp.tsx Frontend/radish.client/src/public/forum/PublicForumDetail.tsx`
  - 通过。

### 当日提交回顾与文档同步

- 今日提交链从 `P3-2-B` `Post.PublicId` 首批实现推进到 `P3-3` 公开论坛热区拆分收口。
- 当日已回顾提交：`7b7d95be` `Post.PublicId` 首批试点、`a6877316` 公共 helper / 状态卡 / 类型流拆分、`6e338389` 搜索页拆分、`5997cb75` 标签页拆分、`1ae57c8b` 列表页拆分、`5f704b59` 详情页拆分，以及 `f6ab4922 / fd5eb979` 两次文档同步。
- 已复核并同步规划入口：[当前进行中](/planning/current)、[开发路线图](/development-plan) 和 [第三开发阶段专题](/planning/phase-three-real-usage-contract-governance) 均已指向 `P3-3` 公开论坛首轮结构拆分收口。
- 已复核设计 / 说明入口：[前端设计](/frontend/design) 与 [验证基线说明](/guide/validation-baseline) 的公开 forum 只读边界、PublicId / long 兼容、公开路由和 canonical 口径仍适用；本批为结构拆分，不需要改动这些行为说明。
- 本次收工文档只同步 `P3-3` 当前状态、今日提交回顾和明日事项，不新增产品行为或验证基线。

### 明日事项

- 下一事项建议先做 `P3-3` 收工复核与下一主线选择：公开论坛 `PublicForumApp.tsx` 首轮热区拆分已完成，后续不继续无边界深拆详情页内部结构。
- 下一主线候选优先在用户留存轻闭环与公开内容增长后续专题之间选择。
- 若继续围绕 `P3-2`，只做定向回归、真实使用兼容观察或历史 `Post.PublicId` 补齐策略评估。
- 仍不做数据库主键迁移、全量外部契约切换、`User / Product / WikiDocument / Comment` 扩面、动态 sitemap、SSR / SSG 或 ActivityPub / WebFinger。

### 验证记录

- `flutter test test/smoke_test.dart`
  - 提权环境通过，`37/37`。
- `flutter test test/profile_page_test.dart`
  - 提权环境通过，`29/29`。
- `flutter test test/notification_repository_test.dart`
  - 提权环境通过，`3/3`。
- `npm run type-check --workspace=radish.client`
  - 通过。
- `npm run test --workspace=radish.client`
  - 通过，`126/126`。
- `npm run build --workspace=radish.client`
  - 通过；保留既有 `app-shop` chunk size warning。
- `npm run check:repo-hygiene:changed`
  - 通过。
- `npm run check:repo-hygiene:changed`
  - `P3-2-A` 文档同步后通过。
- `git diff --check`
  - `P3-2-A` 文档同步后通过。
- `npm run type-check --workspace=radish.client`
  - `P3-2-B` 首批实现后通过。
- `npm run test --workspace=radish.client -- --test-name-pattern="Forum|forum|Public|public|workspace"`
  - `P3-2-B` 首批实现后通过，`134/134`。
- `dotnet test Radish.Api.Tests --filter "PostControllerTest"`
  - 提权环境通过，`16/16`。
- `npm run check:repo-hygiene:changed`
  - `P3-2-B` 首批实现和文档同步后通过，保留既有 `Docs/frontend/design.md` 与 `Docs/guide/notification-api.md` 篇幅提醒。
- `git diff --check`
  - `P3-2-B` 首批实现和文档同步后通过。
- `npm run type-check --workspace=radish.client`
  - `P3-3-A` 首批拆分后通过。
- `npm run test --workspace=radish.client -- --test-name-pattern="Forum|forum|Public|public|workspace"`
  - `P3-3-A` 首批拆分后通过，`134/134`。
- `npm run check:repo-hygiene:changed`
  - `P3-3-A` 首批拆分后通过。
- `git diff --check`
  - `P3-3-A` 首批拆分后通过。

## 2026-05-16

### `P3-3` 收工复核

- 已按昨日事项完成 `PublicForumApp.tsx` 公开论坛热区首轮拆分后的收工复核。
- 本次复核确认 `P3-3` 只作为首轮热区治理收口，不继续无边界深拆 `PublicForumDetail` 内部 hook / 子结构。
- 定向验证覆盖公开论坛路由、PublicId / long 双读路径、评论定位相关导航、公开 route state / sync、public head 与静态 SEO 产物测试。

### 下一主线选择

- 下一主线选择为 `P3-4 用户留存轻闭环`。
- 选择理由：`P3-1` 已完成公开 SEO / 分享基线，`P3-2` 已完成 `Post.PublicId` 首批试点，`P3-3` 已降低公开论坛热区维护风险；当前更应验证公开分享、通知、最近阅读、我的轻回应和 Flutter 复访能否形成真实回流。
- 首批不直接铺功能，先启动 `P3-4-A` 审计：从通知、最近阅读、我的轻回应、公开分享与 Flutter 复访入口中选择 `1-2` 个最高收益小闭环。
- 公开内容增长后续专题暂后置；动态 sitemap、详情结构化数据、SSR / SSG 仍需单独评估，不并入 `P3-4-A`。

### `P3-4-A` 初步审计

- 首批最高收益小闭环建议先选 Flutter forum notification 回流优先使用 `postPublicId`：服务端 forum 通知 `extData` 已并行写入 `postId / postPublicId / commentId`，WebOS 通知中心也已走统一 forum navigation；Flutter `notification_repository.dart` 当前仍只读 `postId`。
- 第二候选为“我的轻回应”回流并行携带 `VoPostPublicId`：WebOS 和 Flutter 当前都能回原帖，但 `UserPostQuickReplyVo` 只暴露 `VoPostId`，若要切到 PublicId 需要后端 ViewModel/API 契约扩展，建议单独批准后实施。
- 本轮只做审计和文档记录，不直接改运行时代码。

### `P3-4-A1` Flutter 通知 PublicId 回流

- Flutter forum notification 回流已优先使用 `extData.postPublicId`，缺失时继续回退旧 `postId`。
- 评论定位参数 `commentId` 保持不变，旧 forum 通知 payload 和非 forum 通知跳过逻辑保持兼容。
- `notification_repository_test.dart` 已补 `postPublicId + postId` 并存、仅 `postPublicId`、旧 `postId`、非 forum 通知跳过和畸形 payload 忽略场景。

### `P3-4-A2` 我的轻回应 PublicId 回流

- 后端 `UserPostQuickReplyVo` 已并行暴露 `VoPostPublicId / VoPostId`，`PostQuickReplyService.GetMinePageAsync` 会在“我的轻回应”列表中填充帖子 PublicId。
- WebOS “继续使用”和个人页“我的轻回应”回流已优先携带 `postPublicId`，缺失时回退旧 `postId`。
- Flutter Profile “我的轻回应”回流已优先使用 `voPostPublicId`，旧 payload 继续使用 `voPostId`。
- 本批不扩全量 `PublicId`，也不处理历史数据批量补齐；后续只观察真实使用或单独评估最近阅读 / 浏览历史补齐策略。

### `P3-4-A3` 最近阅读 / 浏览历史补齐策略评估

- 已完成最近阅读 / 浏览历史历史数据补齐策略评估：新 Post 浏览历史会写入 PublicId canonical route，旧 long `RoutePath` 仍由 WebOS `workspaceNavigation` 兼容打开。
- 已补后端测试确认 `PostController.GetById` 记录浏览历史时优先写入 `/forum/post/{VoPublicId}`，并确认 `UserBrowseHistoryService.RecordAsync` 可在用户再次访问同一 Post 时把旧 long route 自然刷新为 PublicId route。
- 策略结论为当前不做一次性历史数据批量补齐、不新增维护任务、不扩浏览历史 API 契约；若后续真实使用发现旧数据影响回流，再单独评估只针对 Post 浏览历史的维护脚本。

### `P3-4-B` 至 `P3-4-F` forum 留存回流主动验收

- 已按“开发期主动批量验收 + 成组修复”节奏完成 forum 回流矩阵复核，不再等待开发者逐个手动复测。
- 覆盖公开分享、通知、最近阅读、我的轻回应、公开个人页最近公开帖子 / 评论、forum 详情作者 / 评论作者到公开个人页再返回详情的双向复访。
- WebOS 与 Flutter 均保持帖子 PublicId 优先；long id 仅作为旧数据 fallback，评论回流只附带必要 `commentId`。
- 后端为公开评论和我的轻回应补齐 `VoPostPublicId`，WebOS / Flutter 回流优先使用 PublicId；Flutter forum detail 会先解析真实详情，再用真实 `VoId` 调评论、轻回应和评论定位接口。
- WebOS 通知解析在 payload 同时存在 `postPublicId` 与旧 long `routePath` 时优先使用 PublicId；payload 缺少 PublicId 但 routePath 已是 PublicId 时也优先使用 routePath PublicId。
- WebOS / Flutter forum 普通可见文案不再把帖子 long id、评论 id、作者 id 或分类 id 当 fallback 外露；内部点击、旧 routePath 和旧通知 payload 兼容保留。

### `P3-4-G` docs 留存回流可见性复核

- Flutter docs handoff 详情已隐藏 long numeric slug 的普通可见上下文，保留旧 slug / documentId fallback 打开能力。
- WebOS 最近阅读已隐藏 `/wiki/doc/{long}` 与 `/docs/{long}` 旧文档路径可见文本，仍可通过 `documentId` fallback 打开文档。
- 当前不启动 `WikiDocument.PublicId`、历史文档路由批量补齐或数据库迁移。

### `P3-4-H` shop 留存回流可见性复核

- WebOS 最近阅读已隐藏 `/shop/product/{long}` 商品旧路径可见文本，仍可通过 `productId` fallback 打开商品详情。
- 公开 shop 商品详情 head 不再把 `productId` 写入 title / description，canonical path 保持 `/shop/product/{productId}`，公开路由兼容不变。
- 当前不启动 `Product.PublicId` 或全量外部标识改造。

### 当日提交回顾与文档同步

- 今日提交链从 `P3-3` 收工复核切入 `P3-4`，并完成 forum / docs / shop 留存回流主动验收第一轮。
- 已回顾提交：`86350f24`、`06a093e9`、`8b74474a`、`e3ef6df5`、`94f2fe05`、`731de8ea`、`3aab4f85`、`14f42898`、`c508db06`、`c7134249`、`bd7ce3b0`、`dbe29220`、`ed56c4bb`、`cbf0b2b4`、`537bb9ca`。
- 规划入口需同步：`P3-4` 下一步从继续拆新链路调整为阶段性收尾判断，公开内容增长后续专题作为下一主线候选。
- 设计 / 说明入口复核结论：`Docs/frontend/design.md` 的公开壳层只读边界、`Docs/guide/validation-baseline.md` 的验证入口和 `P3-2` 最小试点不扩全量 PublicId 的口径仍适用，本轮不需要改动。

### 文档同步

- [当前进行中](/planning/current) 已切到 `P3-4 用户留存轻闭环`。
- [第三开发阶段专题](/planning/phase-three-real-usage-contract-governance) 已补 `P3-3` 收工复核、`P3-4-A` 审计口径和初步审计结论。
- [开发路线图](/development-plan) 已同步当前主线、开发精力和下一顺位。
- `2026-05-16` 收工前已再次同步 [当前进行中](/planning/current)、[开发路线图](/development-plan)、[第三开发阶段专题](/planning/phase-three-real-usage-contract-governance) 和本周开发日志，明日事项切到 `P3-4` 阶段性收尾判断。

### 验证记录

- `npm run type-check --workspace=radish.client`
  - 通过。
- `node --test --test-isolation=none ./tests/forumNavigation.test.ts ./tests/workspaceNavigation.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicRouteSync.test.ts ./tests/publicHead.test.ts ./tests/publicSeoStatic.test.ts`
  - 通过，`73/73`。
- `npm run check:repo-hygiene:changed`
  - 通过；当前没有需要检查的变更文件。
- `git diff --check`
  - 通过。
- `flutter test test/notification_repository_test.dart`
  - 提权环境通过，`5/5`。
- `dotnet test Radish.Api.Tests --filter "PostQuickReplyServiceTest" -v minimal`
  - 提权环境通过，`3/3`。
- `flutter test test/profile_page_test.dart`
  - 提权环境通过，`29/29`。
- `npm run type-check --workspace=radish.client`
  - 通过。
- `node --test --test-isolation=none ./tests/workspaceNavigation.test.ts ./tests/forumNavigation.test.ts`
  - 通过，`36/36`。
- `dotnet test Radish.Api.Tests --filter "PostControllerTest|UserBrowseHistoryServiceTest" -v minimal`
  - 首次沙盒内因无法读取用户 NuGet.Config 失败；提权环境通过，`20/20`。
- `npm run type-check --workspace=radish.client`
  - 通过。
- `npm run test --workspace=radish.client -- --test-name-pattern="Forum|forum|Public|public|workspace|notification|browse"`
  - 通过，`135/135`。
- `flutter test test/notification_repository_test.dart test/profile_page_test.dart`
  - 首次沙盒启动失败；提权环境通过，`34/34`。
- `flutter test test/profile_page_test.dart test/forum_detail_page_test.dart`
  - `P3-4-D / E` 提权环境通过，覆盖公开个人页 forum 回流、forum detail 作者 / 评论作者回流和可见文案收口。
- `flutter test test/forum_follow_up_store_test.dart test/notification_repository_test.dart test/profile_page_test.dart test/forum_detail_page_test.dart`
  - `P3-4-F` 提权环境通过，`49/49`。
- `flutter test test/docs_page_test.dart test/profile_page_test.dart`
  - `P3-4-G` 提权环境通过，`44/44`。
- `node --test --test-isolation=none ./tests/forumNavigation.test.ts ./tests/workspaceNavigation.test.ts ./tests/publicProfileNavigation.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicHead.test.ts`
  - `P3-4-F` WebOS / 公开路由矩阵通过，`79/79`。
- `node --test --test-isolation=none ./tests/workspaceNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicHead.test.ts ./tests/wikiApp.helpers.test.ts`
  - `P3-4-G` WebOS docs / workspace / public route 矩阵通过，`59/59`。
- `node --test --test-isolation=none ./tests/workspaceNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicHead.test.ts`
  - `P3-4-H` shop / workspace / public head 矩阵通过，`47/47`。
- `npm run type-check --workspace=radish.client`
  - `P3-4-D` 至 `P3-4-H` 各批均通过。
- `npm run check:repo-hygiene:changed`
  - `P3-4-D` 至 `P3-4-H` 各批均通过。
- `git diff --check`
  - `P3-4-D` 至 `P3-4-H` 各批均通过。

## 2026-05-17

### `P3-4-I` 公开 head 标识可见性补洞

- 按 `P3-4` 收尾节奏继续做主动复核，而不是只写阶段判断。
- 发现公开 head 仍存在一组同类可见性缺口：forum 详情 / 分类、docs 数字兼容路径和公开个人页初始 head 会把路由标识写进 title / description。
- 已将这些初始 head 收口为通用公开阅读文案；canonical path 和旧路径打开兼容保持不变。
- 本轮不启动 `User / Product / WikiDocument / Comment` 外部标识改造，不扩全量 `PublicId` 或数据库主键迁移。

### `P3-4` 首轮收尾判断

- forum / docs / shop 留存回流矩阵首轮已完成主动验收、成组补洞和定向验证。
- 当前未发现新的 `P0/P1` 阻断项，`P3-4` 首轮可以收尾。
- 下一步进入公开内容增长后续专题评估，优先比较动态 sitemap、结构化数据和详情首包可见性的收益 / 成本 / 部署风险；未评估前不直接启动 SSR / SSG。

### 验证记录

- `npm run type-check --workspace=radish.client`
  - 通过。
- `node --test --test-isolation=none ./tests/publicHead.test.ts ./tests/forumNavigation.test.ts ./tests/workspaceNavigation.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicProfileNavigation.test.ts`
  - 通过，`80/80`。
- `npm run test --workspace=radish.client -- --test-name-pattern="Forum|forum|Public|public|workspace|notification|browse"`
  - 通过，`142/142`。
- `flutter test test/notification_repository_test.dart test/profile_page_test.dart test/docs_page_test.dart`
  - 首次沙盒启动失败；提权环境通过，`49/49`。
- `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~PostControllerTest|FullyQualifiedName~UserBrowseHistoryServiceTest|FullyQualifiedName~PostQuickReplyServiceTest|FullyQualifiedName~Comment" -v minimal`
  - 首次沙盒内因无法读取用户 NuGet.Config 失败；提权环境通过，`34/34`。
- `npm run check:repo-hygiene:changed`
  - 通过。
- `git diff --check`
  - 通过。

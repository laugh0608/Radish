# F4-R R1-P01 mobile 与统一公开读模型设计记录

> 日期：2026-08-05（Asia/Shanghai）
>
> 状态：`Mobile 390` 正式代表页与统一公开读模型已完成；后续代码侧落地见[成组实现记录](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05)
>
> 范围：本记录只保存活动设计源、`/discover` 功能专题与实现前裁决；后续运行时代码、migration、locale 与测试不回写为本轮设计动作

## 1. 本轮结论

`R1-P01 / 社区发现 / Mobile 390` 没有缩放已确认 PC，也没有把 PC 右侧洞察区按原顺序堆到长页面底部。mobile 固定一条单列任务轴：

1. 共享公共头部；
2. 搜索与“全部 / 最新 / 问答 / 知识”入口；
3. 页面范围与近 `24` 小时公开活动摘要；
4. 一条可直接参与的精选讨论；
5. 紧凑社区脉搏；
6. 五类“社区正在发生”采用一条连续编号活动轨道，公共频道作为焦点事件，其他类型共享时间、排版和分隔语法；
7. 在第三条内容后嵌入轻量贡献者人物节点；
8. 流后承接知识页与热门主题；
9. 复用共享 mobile tab bar。

活动设计源只新增这一张顶层业务画板，没有复制主题、locale、权限或等价状态页面。PC 中“为你推荐 / 综合热度 / 在线人数 / 实时”一类尚无契约支撑的表达同步收敛为“最新公开 / 近 24h”，不引入 presence 或推荐算法；无法由当前权威数据得出的“今日浏览”同时替换为可数据库聚合的公开频道数。

首轮 mobile 评审指出“社区正在发生”仍由蓝色首条、白色列表和米色贡献者大块拼接，重复的图标盒与逐行箭头也削弱了整体构图。修订后只保留一个墨蓝焦点事件，`02–05` 以连续编号轨道串联，贡献者压缩为轨道内的人物簇；异构内容依靠层级和节奏区分，不再依靠彼此割裂的大面积底色。

## 2. 代码与数据事实

- 当前 `/discover` mobile 已有搜索和四个公开入口，但页面主体仍是 PC 模块的响应式堆叠。
- `ChannelController` 整体 `[Authorize]`；`ChannelType.Public` 只表示登录用户可进入，不能直接解释为匿名消息或在线状态。
- `CommentHighlightController` 当前只按 Post / Comment 标识返回高亮快照，不能独立保证父 Post、当前 Comment 和作者仍具公开资格。
- `/circle` 是当前登录用户的关系流，不是公开动态来源。
- Wiki 已有 `Published + Public + PublishedAt + OwnerUserId` 权威字段，能够在不新增短动态写模型的前提下表达首批成员公开贡献。
- Post / Question 已有公开资格、PublicId、目标路由和结构化状态，应直接复用。

## 3. 统一公开读模型裁决

权威契约写入[公开社区发现应用结构](/features/discover-public-app)，核心裁决为：

- 单一 `GetFeed` 返回 `ChannelSummary / MemberActivity / HighlightedComment / Post / Question`。
- Channel 新增显式 `DiscoverVisibility=Hidden / Summary`，默认 Hidden；Summary 只公开频道元数据和安全聚合，目标仍要求登录。
- MemberActivity 首批仅表达成员拥有的公开 Wiki 首次发布，不新增短动态实体，不复制 `/circle` 或通知。
- HighlightedComment 必须联动当前 Comment、父 Post 和 User 资格，正文来自当前 Comment，不使用历史 ContentSnapshot。
- 时间序固定为 `OccurredAtUtc DESC + KindOrder + SourceId`，游标包含 snapshot cutoff 和最后排序键；不提供推荐参数。
- `VoPulse` 只统计公开频道、近 `24` 小时合格 item 和公开 Wiki 首次发布，窗口与 snapshot cutoff 对齐；不伪造按日浏览量。
- 完整 feed 首批 `Cache-Control: no-store`，只做请求内批量复用；来源失败时整体 `503`，不返回会破坏游标的一半成功页。
- Service 通过仓储读取实体并映射 Vo，禁止直接访问 Db；前端统一走 `@radish/http`。

## 4. 设计验证

| 检查 | 结果 |
| --- | --- |
| 顶层业务画板 | `R1-P01 / 社区发现 / Mobile 390`，`390 × 1547` |
| 设计源规模 | 仅新增一张 mobile 顶层画板；保留 PC、`8` 个组件母版和主题变量 |
| 节点问题 | PC / mobile 均无裁切、溢出或布局问题 |
| 图标 | 问答图标已校正为有效 Lucide 图标；最终无失效引用 |
| 视觉复核 | mobile 完成 `2x` PNG 原始尺寸目检；活动轨道、人物簇和整页比例均无挤压，真实内容仍在首屏前段出现 |
| 保存 | 通过 Pen 活动窗口保存 `radish-web-family-ui-v1.pen` |

本轮没有启动前后端，不执行 Gateway、PC / mobile 浏览器 smoke 或 `check:host-runtime`。这些检查属于页面成组实现后的验收批次。

## 5. 确认结论与代码停止线

1. 用户已确认 mobile 修订稿，`R1-P01` PC / mobile 代表设计门禁通过。
2. 用户随后确认把 Channel migration、Console 管理入口、授权 / 审计、默认 Hidden、统一 feed、`@radish/http` 与 `/discover` 纳入同一实现批。
3. 以上代码侧实现和定向验证现已完成；全局灰玉品牌 token 因影响全部页面族，保留到共享主题批单独治理。
4. 本设计轮与后续代码轮均未启动服务或执行真实 smoke；阶段性 PC / mobile 运行态复核完成前不进入 `R1-P02`。

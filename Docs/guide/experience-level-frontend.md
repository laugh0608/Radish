# 经验值与等级系统：前端展示设计

> 入口页：[经验值与等级系统设计方案](/guide/experience-level-system)
>
> 当前状态：纯 Web 正式入口与 WebOS 历史兼容入口已实现；本文记录现行组件、数据和多语言契约，不再保留未落地的示例组件。

## 7.1 入口与职责

| 展示面 | 位置 | 职责 |
| --- | --- | --- |
| `/me` 经验摘要 | `Frontend/radish.client/src/me/MeApp.tsx` | 当前等级、经验进度、最近经验流水和进入详情的正式入口 |
| `/me/experience` | `Frontend/radish.client/src/apps/experience-detail/ExperienceDetailApp.tsx` | 等级进度、累计进度、趋势、来源分布和分页流水 |
| WebOS `/desktop` | `Frontend/radish.client/src/desktop/AppRegistry.tsx` | 历史兼容窗口入口，复用同一个 `ExperienceDetailApp` |
| 桌面状态展示 | `Frontend/radish.client/src/desktop/components/ExperienceDisplay.tsx` | 只读经验状态与共享经验条 |
| 共享经验条 | `Frontend/radish.ui/src/components/ExperienceBar/ExperienceBar.tsx` | 纯展示组件，由宿主注入 labels 与 formatter |
| 组件展示页 | `Frontend/radish.client/src/apps/showcase/ShowcaseApp.tsx` | 开发态组件示例，不属于正式业务入口 |

纯 Web 是正式产品主线。WebOS 入口只做阻断级兼容，不再独立演进经验业务；Console 的经验配置和人工治理属于管理域，不从 client 展示实现反推。

## 7.2 API 与数据边界

client 经验 API 位于 `Frontend/radish.client/src/api/experience.ts`，统一使用 `@radish/http`：

- `getMyExperience(t)`：读取当前用户经验摘要；
- `getTransactions(params, t)`：读取当前用户分页经验流水；
- `getLeaderboard(...)`、`getMyRank()`：保留既有排行调用能力，公开排行榜不属于 F3-C6 的返工范围。

当前真实消费的经验摘要与流水 API 必须遵守以下约束：

1. 失败抛出结构化 `ApiResponseError`，保留 HTTP status、`Code`、`MessageKey`、服务端消息和 `TraceId`。
2. 页面提供当前语言的安全 fallback；控制流不得匹配中文或英文错误句子。
3. 后端 `long` ID、当前经验、累计经验、升级阈值和流水前后值在 client 按字符串接收，展示和加法使用整数安全逻辑。
4. `voLevelProgress` 是 `0-1` 比例值；百分比只在展示层格式化，不回写业务数据。

## 7.3 稳定词元与原文内容

集中展示逻辑位于 `Frontend/radish.client/src/experience/experiencePresentation.ts`。

### 7.3.1 系统经验类型

- 只按稳定 `voExpType` 查找 `experience.type.*` 宿主词元。
- `voExpTypeDisplay` 仅作为历史响应兼容字段，不参与控制、分组或本地化。
- 已登记类型包括发帖、评论、点赞、神评、沙发、登录、资料完善、管理员调整和惩罚等系统类型。
- 未登记类型直接显示去除首尾空白后的稳定原值，避免伪造含义。

### 7.3.2 配置与人工内容

以下内容没有稳定本地化标识时必须保持服务端原文：

- `voCurrentLevelName`、`voNextLevelName`；
- 流水 `voRemark`；
- `voFrozenReason`；
- 用户名、头像和其他用户资料。

页面只在等级名称为空时使用 `Lv.{level}` 形式的中性兜底，不建立本地等级翻译表。

## 7.4 locale 与时区

经验域 formatter 显式接收当前语言与展示时区：

- `formatExperienceNumber`：支持超出 `Number.MAX_SAFE_INTEGER` 的整数字符串；
- `formatExperienceSignedNumber`：为正向流水增加本地化数字后的 `+` 标记；
- `formatExperiencePercent`：使用 `Intl.NumberFormat` 百分比格式；
- `formatExperienceDateTime`：使用统一时区 formatter；
- `buildExperienceDailyStats`：先按展示时区归属自然日，再按当前 locale 生成图表日期；
- `buildExperienceSourceStats`：按稳定 `voExpType` 聚合并本地化图例名称。

英文数量文案必须传递数值 `count` 进行 plural 判定；大整数展示值另以已格式化字符串传入，不能为方便插值而把 long 转成不安全的 `number`。

## 7.5 `ExperienceBar` 共享契约

`@radish/ui` 不导入 `react-i18next`，也不读取 client 的语言状态。宿主通过 `ExperienceBarPresentation` 注入：

- 排名、当前进度、下一级、剩余经验、总经验、冻结和升级时间 labels；
- 数字 formatter；
- 百分比 formatter；
- 日期时间 formatter。

`ExperienceBar` 接受 `number | string` 的经验数值，内部只负责布局、进度比例限制、tooltip 和冻结态展示。client 使用 `buildExperienceBarData` 推导当前等级阈值，不能把共享组件扩展为等级计算或业务规则持有者。

## 7.6 经验详情页面

`ExperienceDetailApp` 当前行为：

1. 先读取经验摘要，再读取当前页经验流水；摘要失败显示整体错误和重试，流水失败保留摘要并显示局部错误。
2. 展示当前等级进度与累计进度，两处复用同一 `ExperienceBarPresentation`。
3. 趋势提供 7 天 / 30 天展示窗口；当前图表基于已加载流水构造，缺失日期补零。
4. 来源分布按稳定经验类型聚合；共享 `LineChart / PieChart` 由宿主提供数字和百分比 formatter。
5. 流水列表展示本地化系统类型、locale 时间、等级变化、带符号经验值和原文备注。
6. 分页由正式 `/me/experience` 路由状态或组件内部状态驱动，页码文案使用宿主词元。

公开排行榜维持既有实现和算法，本页面不内嵌或改写排行逻辑。

## 7.7 测试与维护门禁

经验前端改动至少复核：

- `Frontend/radish.client/tests/experienceApiContract.test.ts`：结构化错误和真实消费者字段边界；
- `Frontend/radish.client/tests/experiencePresentation.test.ts`：稳定词元、未知值、long 数字、日期、百分比与时区分组；
- `Frontend/radish.client/tests/i18nResources.test.ts`：中英文资源 parity 和 `0 / 1 / 2` 数量规则；
- `Frontend/radish.ui/tests/experienceBarContract.test.ts`：共享组件的宿主 labels / formatter 契约。

维护时不得：

- 用 `voExpTypeDisplay`、等级名称或备注参与业务判断；
- 在共享 UI 中持有宿主 i18n 实例；
- 将 long 经验值无条件转为 `number` 后计算或格式化；
- 为配置等级名、人工备注或冻结原因自行编造翻译；
- 在前端重算等级公式、每日上限、排行或冻结业务语义。

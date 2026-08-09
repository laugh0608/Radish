# F4-R R1-C02 Console 案件治理 / 审计成组实现

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：正式代码与静态验证已完成；Gateway PC / mobile 运行态验收待另行授权
>
> 范围：`/console/moderation` 的案件 / 申诉工作区、Console Mobile 任务壳层、双语资源与定向契约测试；未修改 Pencil、服务端 API、权限、URL、LongId、幂等、结构化错误或事务边界，未启动服务或浏览器

## 1. 结论

- 正式 `/console/moderation` 已按确认设计从卡片式双栏收敛为治理工作面。PC 使用案件队列—受权证据—决定边界三段结构，只有显式选择后才展示权威详情。
- Mobile 列表使用连续三列案件行，筛选进入共享 `BottomSheet`；案件 / 申诉详情使用 URL 驱动的单任务模式，保留 `60px` Console 品牌栏与 `50px` 任务栏，隐藏五项底部导航。
- Cases / Appeals 共用直接标题、紧凑指标带、工作区切换、队列读取状态与移动任务结构；案件决定和申诉复核仍分别服从既有权限与业务边界。
- View-only、Reviewer 无 Action、`409` 草稿保留、队列 stale、详情 unavailable / stale、筛选空结果、申诉脱敏、Action-only relief 与 Chat 操作结果继续沿用能力门禁中已经闭合的失败关闭逻辑。

## 2. 页面实现

### 2.1 PC 治理工作台

- 案件与申诉入口使用同一双项切换，不建立第二套路由状态；现有 `view=appeals`、`case`、`appeal`、筛选和分页 query 保持唯一真相源。
- 队列改为连续紧凑扫描面；案件身份、证据摘要和状态 / 时间分区展示。无匹配结果时保留筛选事实并提供重置入口。
- 案件详情内部拆为受权证据与决定边界两段，与左侧队列共同形成三段治理桌；读取状态、证据快照、举报、决定表单、纠正动作和事件摘要仍使用现有数据与 handler。

### 2.2 Mobile 队列与任务

- 案件行固定为身份、证据、状态三列；指标与筛选不挤入主列表，筛选按需从底部层展开。
- 列表继续显示 Console 五项真实入口并复用既有胶囊导航视觉；详情打开后只隐藏底部导航，顶部品牌栏保留真实账户入口。
- 第二层任务栏承接返回、对象身份、状态、版本和刷新；案件提交 / 纠正动作在权限和权威读取均允许时进入底部粘性动作区。
- 申诉 Mobile 继续保持受权脱敏与低风险读取边界，不把 PC 的复核 / relief 写表面机械搬入窄屏。

## 3. 保持不变的契约

- API 继续由现有 `@radish/http` moderation client 与 Console API 模块承接；未新增、改名或绕过任何请求。
- 权限继续区分案件读取、Review、Action 与 Appeal；前端入口隐藏和 handler 写前复核均保持既有停止线。
- 案件、申诉、目标、Revision 与用户标识继续全链使用字符串；没有经过 JavaScript `Number` 转换。
- `409` 仍刷新服务端权威版本、保留草稿并轮换操作键；结构化 `403 / 404 / 409`、Main / Chat 事务和幂等边界未改变。
- 未新增原因、证据可用性、动作结果筛选、自动治理、批量处理、完整审计时间线或实时刷新。

## 4. 自动化与静态验证

- `npm run test --workspace=radish.console`：`75 / 75` 通过。
- `npm run lint --workspace=radish.console`：通过，`0 warning`。
- `npm run type-check --workspace=radish.console`：普通与 strict TypeScript 检查通过。
- `npm run build --workspace=radish.console`：production build 通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

新增 / 更新的契约测试固定 PC 三段工作台、Mobile 三列队列、按需筛选、保留品牌栏的全屏任务、工作区切换和图标按钮可访问名称；既有测试继续守卫 URL、权限、冲突、stale / unavailable 与申诉脱敏边界。

## 5. 待验收与停止线

- 本批按任务边界未启动服务或浏览器，不能把静态完成表述为 Gateway 运行态验收完成。
- 下一步应在另行明确授权后启动当前前后端，并按 Gateway 入口复核 PC `1440 × 900`、Mobile `390 × 844`、管理员 / View-only / Reviewer 无 Action、`409`、stale / unavailable、空结果与申诉 / Chat 代表状态。
- 运行态验收完成、临时数据与服务清理后才能关闭 `R1-C02`；当前不提前推进 `R2-C03`。

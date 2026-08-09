# F4-R R1-C02 Console 案件治理 / 审计正式代表设计

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：PC `1440 × 900`、Mobile `390 × 844` 与必要关键状态代表设计已完成，等待确认后进入正式 Console 视觉实现
>
> 活动设计源：`Docs/frontend/design-sources/radish-web-family-ui-v1.pen`
>
> 范围：仅修改唯一活动 `.pen` 与设计 / 规划记录；未修改运行时代码、API、权限、LongId、幂等、结构化错误或事务边界，未启动服务或浏览器

## 1. 结论

- `R1-C02` 继续作为 Console 案件治理 / 审计的独立 R1，正式锚点为 `/console/moderation`，不降为普通表格—明细，也不复制 R1-C01 的订单 inspector。
- PC 固定为“案件队列—受权证据—决定边界”三段治理桌：队列保持连续紧凑扫描，只有显式选择后才展示案件事实；证据工作区承接目标快照与版本；决定区只暴露当前权限允许的决定、内容限制、内部备注和既有目标关系处置。
- Mobile 固定为两个真实业务画板：列表保留 Console 顶部品牌栏与五项真实入口；单案件处理继续保留顶部品牌栏，只隐藏五项底部导航，并在第二层任务栏承接返回、案件身份与刷新。证据、决定和提交动作围绕一个案件连续展开。
- 必要关键状态单独覆盖 View-only Operator、Reviewer 无 Action、`409` 冲突、队列 stale / 详情不可用、筛选空结果，以及申诉脱敏 / Action-only relief / Chat 操作结果，不为每种状态复制完整页面。

## 2. 正式画板

| 代表画板 | Pencil 节点 | 尺寸 | 固定结构 |
| --- | --- | --- | --- |
| Console 案件治理 PC | `Nekq0` | `1440 × 900` | Console 侧栏 + 三段治理桌；内容管理为唯一激活入口 |
| Console 案件队列 Mobile | `R0EfC` | `390 × 844` | 连续案件行、Cases / Appeals 切换、按需筛选、Client 风格五项胶囊导航 |
| Console 案件处理任务 Mobile | `u3hrkE` | `390 × 844` | 保留 Console 顶部品牌栏、隐藏五项底部导航，单案件证据—决定—提交全屏任务 |
| Console 案件治理必要关键状态 | `LvfDn` | `1220 × 900` | 六个局部状态面，不扩为路由或权限镜像 |

PC 代表身份为 Reviewer，具备案件读取与决定权限但没有 Action 权限；因此设计明确显示决定能力，同时不提供用户动作载荷或入口。当前选中案例使用 `PostAnswer / answer` 目标，证据区保留目标 LongId 字符串、案件版本、目标 Revision 与操作键事实。

## 3. 权限、错误与移动边界

- View-only Operator 只读案件、证据、决定结果与事件摘要，不渲染决定表单、内部备注输入或写动作。
- Reviewer 无 Action 可提交既有决定与内容限制，但不能借 UI 组合封禁、冻结或其他用户动作。
- `409` 保留未提交草稿，刷新服务端权威案件 / 目标版本并轮换操作键；不覆盖冲突，也不把失败当成功。
- 队列 stale 保留已标记的旧列表且不自动选择；详情 `404 / 403` 转为 unavailable 并冻结写入；筛选空结果保留筛选事实并提供重置，不自动回退到无筛选列表。
- 申诉处理只展示受权脱敏证据；Action-only relief 与 Chat `Pending / Failed / Succeeded` 继续服从既有权限、Main / Chat 事务和幂等结果，不在代表设计中新增动作类型。

## 4. 设计源检查

- 四个顶层画板均为真实业务尺寸，`placeholder=false`；活动设计源不存在遗留 placeholder。
- PC、Mobile 列表、Mobile 详情与关键状态均完成节点边界检查和 `2x` 导出复核，没有裁切、塌陷、横向溢出或失效图标。
- 复用现有 Console 壳层、Button、State Chip、Text Field、Navigation Item、State Slot、Mobile Tab Bar 与语义 token；没有新增并行 `.pen`、硬编码主题分叉或按 locale / 权限复制完整页面。
- PC 侧栏已清除 R1-C01 订单页复制遗留的激活标记，操作员权限说明改为案件查看 / 审核决定。
- 设计复核已按 R1-C01 订单详情统一移动任务层级：`60px` Console 品牌栏、`50px` 案件任务栏、`670px` 主任务区和 `64px` 粘性动作区完整落在 `390 × 844` 画板内。

## 5. 下一步与停止线

1. 先由用户确认本组正式代表设计；确认前不进入运行时代码。
2. 确认后成组实现 `/console/moderation` 的 PC 三段治理桌、Mobile 连续案件队列与全屏单案件任务，并按本记录关键状态闭合正式视觉表面。
3. 正式实现保持现有 API、权限、URL、LongId、幂等、结构化错误和 Main / Chat 事务边界，不新增原因 / 证据可用性 / 动作结果筛选或自动治理能力。
4. 代码、测试和静态构建完成后，再按专题验收条件另行申请启动服务与 Gateway PC / mobile smoke；当前不推进 `R2-C03`。

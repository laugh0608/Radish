# P3-12-D5 Console 治理工作台设计源重构记录

> 日期：2026-06-27（Asia/Shanghai）
>
> 状态：`console-governance-workbench.pen` 的 `P01-P08` 已完成全量重构；不进入本轮视觉代码实现
>
> 结论：Console 旧设计源整体偏普通浅色后台模板，页面类型分化不足，且未定义设计变量。用户确认后，本轮按 `web-ui-foundation.pen` 共享基座重新设计全部 Console 画板，保留后台工具密度和治理职责边界。

## 背景

`P3-12-D4` 已收束 Web UI 共享基座，明确跨 public / private / console 的 header、按钮、pill、卡片、状态槽和移动 tab 规则。审计 `console-governance-workbench.pen` 后发现：

- 旧 `P01-P06` 多数页面复用同一套左侧栏 + 顶栏 + 表格模板，调度总览、CRUD、设置页和治理工作台的主焦点不够区分。
- 旧移动画板已有任务流雏形，但动作层级、状态槽和底部导航没有同步共享基座。
- 设计源未定义变量，视觉值容易继续分叉。
- `P02` 原有一个局部文本被 Pencil 标记为裁切。

## 已完成

- 新增 `rx-*` 设计变量，覆盖纸感底色、文本、边框、品牌色、状态色、字体和圆角。
- 重构 `P01 - Console Shell Foundation - Layout System`：
  - Console 专用侧栏、84 高顶部命令栏、搜索、登录态、主动作。
  - 指标、表格样板、动作层级和状态槽。
- 重构 `P04 / P05 / P06`：
  - `P04` 改为跨模块治理负载和今日调度中心。
  - `P05` 改为对象管理表格页，侧栏承接选中对象摘要。
  - `P06` 改为策略设置页，使用分组导航、设置列和影响范围侧栏。
- 重构 `P02 / P03`：
  - `P02` 使用举报队列、目标证据、治理动作和最近留痕三栏。
  - `P03` 使用观察候选、用户摘要、趋势证据、流水定位和复核动作。
- 重构 `P07 / P08`：
  - 移动端改为单列任务流。
  - 底部 tab 使用图标上、文字下结构。
  - 状态槽和主次动作同步共享基座口径。

## 设计边界

- Console 保持后台工具属性，不改成公开 Web 阅读壳层。
- 本轮不改变 API、权限键、数据模型、治理动作语义、经验规则或冻结语义。
- 本轮不进入 `radish.console` 代码实现。
- 后续代码实现必须按页面类型分批推进，不把所有页面硬套为治理工作台。

## 验证

Pencil 侧：

- 全文件 `snapshot_layout` 返回 `No layout problems.`
- `P01`、`P02`、`P03`、`P05`、`P06`、`P07`、`P08` 均完成单屏布局检查。
- 抽查截图：`P02` 内容审核、`P06` 设置策略、`P07` 移动审核、`P08` 移动经验台账未发现明显裁切、坍塌或重叠。

仓库侧：

```bash
git diff --check
rg -n "[ \t]+$" Docs/frontend/console-governance-workbench-design.md Docs/records/p3-12-d5-console-governance-workbench-redesign-2026-06-27.md Docs/frontend/design-sources/README.md Docs/planning/current.md
```

结果：通过。

## 后续

1. 用户在 Pencil 内保存 `console-governance-workbench.pen`。
2. 以 `web-ui-foundation.pen`、`console-governance-workbench.pen` 和本说明作为 Console 视觉实现输入。
3. 后续进入代码前先盘点 `radish.console` 当前 token、`AdminLayout`、`adminFeature.css` 和高频页面类型，再分批实现。

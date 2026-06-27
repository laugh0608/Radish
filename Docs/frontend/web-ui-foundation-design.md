# Web UI 共享基座设计说明

> 日期：2026-06-25（Asia/Shanghai）
>
> 更新：2026-06-27（Asia/Shanghai）：`F01` 的 public / private header 合法变体已从 64 高小 pill 收敛为 84 高 PC 纸感横匾，PC 使用横排图标 nav rail、激活态 pill 和身份 action rail；移动端 tab 继续保留图标 / 文案上下排列。
>
> 状态：共享基座设计源 `F01` 已创建；当前作为跨设计源同步口径，不进入视觉代码实现

## 设计源

```text
Docs/frontend/design-sources/web-ui-foundation.pen
```

画板：

| 画板 | 职责 |
| --- | --- |
| `F01 - Web UI Foundation` | 共享 token、public / private header 合法变体、按钮 / pill、卡片 / rail、状态槽、移动 shell / tab 和同步规则 |

## 目标

- 解决多 `.pen` 业务设计源之间 header、按钮、卡片和状态槽样式分叉的问题。
- 保留 `public-web-unified-experience.pen`、`private-web-workflows.pen` 和 `console-governance-workbench.pen` 的端点边界。
- 让业务设计源复制共享样式样板，而不是各自重新发明基础组件。
- 在进入视觉代码实现前，先固定跨端点的视觉契约。

## 分层方案

### 共享基座

`web-ui-foundation.pen` 只承载共享样板和合法变体，不承载业务页面。

当前包含：

- Public header 合法变体。
- Private header 合法变体。
- 84 高 PC 纸感横匾 header、横排图标 nav rail、激活态 pill 和身份 action rail。
- 主按钮、次按钮、激活 pill、普通 pill 和状态 pill。
- 内容卡片、右侧 rail、状态槽。
- 移动 Web shell 与底部 tab 样板。
- 跨设计源同步规则。

### 业务设计源

业务 `.pen` 文件继续按端点拆分：

- `public-web-unified-experience.pen`：公开 Web 阅读 / 浏览端点。
- `private-web-workflows.pen`：登录态私域与作者态端点。
- `console-governance-workbench.pen`：Console 治理端点。

业务源可以有不同信息架构、导航项和页面密度，但不得自行分叉共享样式。

### 评审看板

后续如需阶段级横向审阅，可按需新增轻量 `web-ui-review-board.pen`，只放关键画板截图或代表性 frame，不承载完整编辑源。

当前暂不创建评审看板，避免过早增加同步成本。

## 必须一致

- `rx-*` 变量名称和取值。
- Header 高度、品牌区、Radish 标识、字体层级、nav 图标和 nav rail / pill 形态。
- PC nav 使用图标左、文字右的横排结构；移动端 tab 使用图标上、文字下的纵排结构。
- 主按钮 / 次按钮 / 激活态 / 普通筛选 pill 的样式规则。
- 卡片圆角、弱边框、纸色底、低饱和状态色。
- 状态槽的加载、空态、错误和权限限制表达方式。
- 移动端底部 tab 的高度、胶囊形态、图标 / 文案层级。

## 允许差异

- Public header 的导航项、登录动作和 `/workbench` 入口。
- Private header 的登录态身份、设置动作、消息 / 资产 / 创作入口。
- Console 的表格密度、治理工具条、权限 / 审计状态表达。
- 不同端点的 dominant region：公开阅读、私域复访、作者创作和 Console 治理可以有不同信息密度。

## 同步规则

1. 改共享样式时，先修改 `web-ui-foundation.pen`。
2. 再同步到受影响的业务设计源。
3. 再更新对应设计说明和记录。
4. 不允许只在某个业务 `.pen` 临时修改 header、按钮、卡片或状态槽样式。
5. 如果业务端点确实需要新变体，先把它加入共享基座并说明适用范围。

## Pencil 工作流限制

- Pencil 当前活动窗口一次只打开一个 `.pen`；写入操作必须以当前活动窗口中已打开的目标文件为准。
- MCP 工具可以通过 `filePath` 指定读取、检查和截图不同文件，但不要假设写入会可靠落到非活动窗口文件。
- 切换 `.pen` 前必须在 Pencil 内手动保存当前文件；未保存时切换文件可能丢失更改，或让后续写入误落到上一活动文件。
- 由于 Pencil 组件不能跨文件实时引用，当前方案不是实时组件库，而是“共享样板 + 文档约束 + 按需同步”。
- `.pen` 文件只通过 Pencil 创建、读取和修改；新设计源可从 `empty-design-source-template.pen` 复制后再用 Pencil 写入内容。

## 当前不做

- 不把所有页面合并进一个巨型 `.pen`。
- 不创建跨文件实时组件库。
- 不进入视觉代码实现。
- 不借共享基座重做 public / private / console 全量画板。

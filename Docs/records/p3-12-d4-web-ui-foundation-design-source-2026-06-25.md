# P3-12-D4 Web UI 共享基座设计源记录

> 日期：2026-06-25（Asia/Shanghai）
>
> 状态：共享基座设计源 `F01` 已创建；不进入视觉代码实现
>
> 结论：新增 `web-ui-foundation.pen`，作为 public / private / console 多设计源之间的共享 UI 基座。当前保留业务设计源拆分，不把所有页面合并到一个 `.pen`；后续公共样式先在共享基座确认，再同步到业务设计源。
>
> 2026-06-25 收工补充：Pencil 写入必须以当前活动窗口为准；切换 `.pen` 前必须在 Pencil 内手动保存。后续继续优化 `web-ui-foundation.pen` 时，应先由用户确认 / 手动切换到该文件，写入后保存，再做布局检查和截图目检。
>
> 2026-06-27 优化补充：按用户反馈收口 `F01` header 合法变体，将 public / private header 从 64 高小 pill 调整为 84 高 PC 纸感横匾，统一采用横排图标 nav rail / pill、身份 action rail、放大品牌印章和细线收边；移动端 tab 保留图标 / 文案上下排列；不进入视觉代码实现。
>
> 2026-06-27 样板收束补充：继续收口按钮 / pill、卡片 / rail、状态槽和移动端 tab 样板。PC 控件采用图标左、文字右，移动端 tab 保持图标上、文字下；卡片补内容元信息和动作入口，状态槽补加载 / 空态 / 错误 / 权限的原因与恢复语义。

## 背景

`P3-12-D2` 已完成公开 Web 设计源，`P3-12-D3` 已完成私域 / 作者态设计源。审阅后发现多个 `.pen` 之间存在 header、按钮和卡片样式可能分叉的问题。

用户确认采用“共享基座 `.pen` + 业务设计源 + 按需评审看板”的治理方案。本轮先创建共享基座，不创建评审看板，不进入视觉代码实现。

## 设计源

文件：

```text
Docs/frontend/design-sources/web-ui-foundation.pen
```

已同步登记：

- [设计源文件目录](/frontend/design-sources/README)
- [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design)

## 已完成画板

### `F01 - Web UI Foundation`

职责：

- 统一 `rx-*` token 样板。
- 提供 Public header 与 Private header 两个合法变体。
- 提供按钮 / pill、状态 pill、内容卡片、右侧 rail 和状态槽样板。
- 提供移动 Web shell 与底部 tab 样板。
- 写入跨设计源同步规则。

设计口径：

- Public 和 Private header 允许导航项与右侧动作不同，但高度、品牌区、nav rail / pill、按钮形态和 token 必须一致。
- 当前 `F01` header 合法变体采用 84 高 PC 纸感横匾、横排图标 nav rail / pill、身份 action rail、放大 Radish 印章和上下细线收边，避免回退为小标签式导航或 PC 端纵向占比过高的图标导航。
- 响应式方向：PC 视图宽度更充足，nav 图标和文字左右排列；移动端横向更紧张，底部 tab 图标和文字上下排列。
- 按钮 / pill 采用横排图标控件，主按钮、次按钮、激活筛选、普通筛选和状态 pill 必须有清晰主次层级。
- 卡片用于内容项、数据摘要、状态块和必要工具面板；右侧 rail 只承载理解、决策或下一步动作，不复制主内容。
- 状态槽必须覆盖加载、空态、错误和权限限制，并给出原因、重试或登录恢复路径。
- Console 可使用更高密度表格和治理工具条，但 token、边框、状态色和主次按钮规则保持一致。
- 实现边界和技术停止线只写入文档，不进入真实用户 UI。

## 验证

Pencil 侧：

- `F01`：`snapshot_layout` 返回 `No layout problems.`
- `F01`：截图目检未发现明显裁切、坍塌或横向溢出。
- `F01`：2026-06-27 header 优化后，`Header Variants` 局部 `snapshot_layout` 返回 `No layout problems.`，截图目检未发现裁切或重叠。
- `F01`：2026-06-27 样板收束后，整页 `snapshot_layout` 返回 `No layout problems.`，截图目检未发现核心组件或移动样板被挤压。
- 误写防护：首次写入时 Pencil 活动文件仍指向 `private-web-workflows.pen`，已通过 Pencil 删除误写的 `F01` 节点，并确认 `private-web-workflows.pen` 只保留原有 `P01-P05` 业务画板。
- 已将 Pencil 活动窗口与手动保存约束同步到 [设计源文件目录](/frontend/design-sources/README) 和 [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design)。

仓库侧：

```bash
git diff --check
rg -n "[ \t]+$" Docs/frontend/web-ui-foundation-design.md
rg -n "[ \t]+$" Docs/records/p3-12-d4-web-ui-foundation-design-source-2026-06-25.md
```

结果：通过。

## 后续顺序

1. 以 [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design) 和 `F01` 作为跨 `.pen` 样式同步口径。
2. 明日先继续优化 `web-ui-foundation.pen`，按用户意见收口 header、按钮、卡片、状态槽和移动 tab 的共享样板。
3. 后续补 `console-governance-workbench.pen` 的文档治理差异画板时，先对齐 `web-ui-foundation.pen` 的 token、header、按钮、卡片和状态槽规则。
4. 公开 Web、私域 / 作者态和 Console 文档治理画板确认后，再进入跨页面视觉代码实现。

## 当前不做

- 不把所有业务页面合并到一个 `.pen`。
- 不创建 `web-ui-review-board.pen`。
- 不进入 `radish.client` 或 `radish.console` 视觉代码实现。
- 不重做 public / private 已完成画板。

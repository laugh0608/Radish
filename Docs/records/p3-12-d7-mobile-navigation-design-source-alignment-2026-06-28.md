# P3-12-D7 移动导航设计源统一记录

> 日期：2026-06-28（Asia/Shanghai）
>
> 状态：跨设计源移动底栏样式已统一；当前作为 `radish.client` 视觉实现前口径
>
> 结论：`web-ui-foundation.pen`、`public-web-unified-experience.pen`、`private-web-workflows.pen` 和 `console-governance-workbench.pen` 的移动底栏已统一为浮动胶囊样式。`/workbench` 继续作为正式 Web 功能地图，承接导航无法展示的功能入口。

## 背景

用户复审指出不同 `.pen` 文件中的移动底部工具栏样式不一致，部分旧画板仍是直贴底部横条、4 项矩形 tab 或缺少功能地图入口。

文档侧已确认：

- `/workbench` 是正式 Web 功能地图和功能总入口。
- PC header 与移动底栏只展示高频入口，不应把所有公开、私域或后台功能都塞进一级导航。
- 低频或场景型功能由“工作台”或页面内功能入口回到 `/workbench` 承接。

## 同步范围

- `web-ui-foundation.pen`：`F01 / F02` 移动底栏样板统一为 64px 高浮动胶囊栏。
- `public-web-unified-experience.pen`：`P10-P16` 移动公开底栏统一为 `发现 / 论坛 / 文档 / 工作台 / 我的`。
- `private-web-workflows.pen`：保持当前已达标的 `工作台 / 资产 / 创作 / 消息 / 我的` 胶囊底栏，作为业务源参考样式。
- `console-governance-workbench.pen`：`P07 / P08 / P14-P18` 移动 Console 底栏统一为 `总览 / 治理 / 资产 / 权限 / 运维`。

## 设计口径

- 底栏最多 5 项顶级入口，图标上、文字下。
- 底栏左右 inset，使用浮动胶囊外框，不贴边、不拉成普通底部横条。
- 激活态使用柔和品牌色胶囊，非激活态只保留低优先级图标与文字。
- Public / Private / Console 可以有不同导航项和激活语义，但不得分叉底栏形态。

## 同步文档

- [设计源文件目录](/frontend/design-sources/README)
- [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design)
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)
- [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)
- [Console 治理工作台设计端点](/frontend/console-governance-workbench-design)

## 当前不做

- 不进入视觉代码实现。
- 不新增移动端完整功能总览之外的新产品能力。
- 不把 `/desktop`、WebOS Dock 或窗口系统重新纳入正式 Web 导航。

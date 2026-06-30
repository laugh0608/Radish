# P3-12-D31 radish.console 阶段运行态复核记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`radish.console` D14-D30 视觉迁移后的 Gateway PC / mobile 运行态复核。

## 背景

`P3-12-D14-D30` 已完成 Console 壳层、表格代表页、文档治理、普通列表、角色权限、治理工作台、系统工具、深层表单和详情 / 抽屉静态样式收口。用户确认前后端已启动后，本批按阶段真实验收口径复核 Gateway 下的 Console 登录回流、详情弹窗、文档治理弹窗和空态页面。

本批只做运行态查看和文档留痕，不执行上传、导出、发布 / 下架、删除、回滚、备注保存或其他写操作。

## 复核范围

- Gateway 入口：`https://localhost:5000/console/`
- 健康检查：Gateway / Api / Auth。
- PC 视口：`1920x1080`。
- 移动视口：`390x844` CSS 视口；当前 Playwright CLI `resize` 不会把 `devicePixelRatio` 调整为 `3`，本轮 DPR 记录为 `1`。

## 覆盖结果

| 页面 / 链路 | PC 结果 | Mobile 结果 | 结论 |
| --- | --- | --- | --- |
| Console 登录回流 | 可从 Auth 登录并授权回到 Console | 复用同一登录态 | 通过 |
| `/console/products?productId=100062&openDetail=1` | 商品详情自动打开，商品图标、封面、详情表格和动作区存在 | 弹窗可见，内容可滚动到底，底部操作栏未遮挡最终内容 | 通过；移动窄屏下表格标签会竖排 |
| `/console/documents` 文档详情 | 列表 20 条，普通点击第一条详情可打开弹窗，正文内容框存在 | 详情弹窗可见，状态、路径、权限和正文内容框正常 | 通过 |
| `/console/documents` 版本治理 | 版本弹窗可打开，版本表格和版本内容区存在 | 版本弹窗可见，窄屏下列标题压缩但内容仍可读 | 通过 |
| `/console/orders` | 当前结果为 0，空态显示正常 | 当前结果为 0，移动指标卡纵向排列正常 | 空态通过；未覆盖 `OrderDetail` |
| `/console/stickers` | 全部分组为 0，空态显示正常 | 全部分组为 0，移动指标卡显示正常 | 空态通过；未覆盖批量上传弹窗 |

## 观察与限制

- 当前本地数据没有订单，无法通过真实数据打开 `OrderDetail`；本轮只确认订单列表和空态。
- 当前本地数据没有表情包分组，无法进入分组表情列表，也无法打开 `StickerBatchUploadModal`；本轮只确认表情分组列表和空态。
- 移动复核使用 `390x844` CSS 视口，DPR 为 `1`，没有写成高 DPR 物理视图完整结论。
- 非基线 `1280x720` 自动化点击文档表格详情时曾出现 Ant Table 固定列遮挡 locator 的情况；在本轮 PC 基线 `1920x1080` 下重新使用普通点击复测通过，暂不列为阻断。
- 浏览器 console 过滤 `error` 后为 `0` 条。

## 验证

- `npm run check:host-runtime -- --details`：通过，Gateway / Api / Auth 健康端点均返回正常。
- Playwright Gateway 复核：覆盖 PC `1920x1080` 与 mobile `390x844` CSS 视口。
- `playwright console error`：`Errors: 0, Warnings: 0`。
- 文档同步后执行 `npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。

## 下一步

1. 若要补齐未覆盖链路，优先准备或定位安全测试数据，再复核 `OrderDetail`、分组表情列表和 `StickerBatchUploadModal`。
2. 若继续样式治理，优先按中宽 PC 视口复核 Ant Table 固定列交互和移动窄屏表格可读性，不扩大为全站重构。
3. 若进入 `P3-12-E` 发布候选，应按发布候选口径重新执行完整 baseline、身份守护、host runtime 和 Gateway 页面矩阵。

# P3-12-D32 radish.console 数据补验与 Auth 静态根目录收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`radish.console` D31 未覆盖的 `OrderDetail`、分组表情列表、`StickerBatchUploadModal`，以及 `Radish.Auth` 启动时缺少 `wwwroot` 的运行态告警。

## 背景

`P3-12-D31` 已完成 Console 阶段运行态复核，但本地数据没有订单和表情包分组，导致 `OrderDetail` 与贴纸批量上传弹窗无法覆盖。用户重新启动前后端后，本批先准备可定位的本地安全测试数据，再通过 Gateway 真实页面补验缺口。

本批不执行上传、删除、发布 / 下架、版本回滚、备注保存或其他业务写操作；本地 SQLite 测试数据只用于运行态复核，不进入 Git。

## 本地测试数据

复核前先备份本地库：

```text
/private/tmp/Radish-before-p3d32-console-test-data.sqlite
```

随后在 `DataBases/Radish.db` 中插入带 `P3D32` 前缀的本地数据：

| 表 | ID / Code | 用途 |
| --- | --- | --- |
| `ShopOrder` | `931200000001` / `P3D32-ORDER-20260630-001` | 覆盖 `OrderDetail`；商品快照来自现有 `ShopProduct.Id=100062` |
| `StickerGroup` | `931200000101` / `p3d32_console_smoke` | 覆盖分组表情列表入口 |
| `Sticker` | `931200000102` / `p3d32_smoke_sticker` | 覆盖表情列表缩略图与批量上传弹窗上下文 |

数据使用现有安全种子用户 `UserId=20001`、现有商品 `100062` 和现有附件 `73009`，不新增 API、权限、路由或业务契约。后续如需清理，可按上述 ID / Code 精准删除。

## 代码收口

- `Radish.Auth/wwwroot/.gitkeep`：补齐 Auth Web 根目录占位，消除启动时 `WebRootPath was not found` 告警。原因是 `Radish.Auth` 为 Web 宿主且调用 `UseStaticFiles()`，但仓库未保留空 `wwwroot` 目录。
- `StickerBatchUploadModal.tsx`：将 4 处 AntD `Alert` 的 `message` 属性迁移为 `title`，修复打开批量上传弹窗时浏览器 console 报 `Alert.message` deprecated 的运行态告警；显示文案、上传流程、提交载荷和校验保持不变。

## 运行态复核

工具：优先使用用户指定的 Browser 插件；Gateway 入口为 `https://localhost:5000/console/`。

| 链路 | PC `1920x1080` | Mobile `390x844` CSS 视口 | 结论 |
| --- | --- | --- | --- |
| `/console/orders?orderId=931200000001&openDetail=1` | 订单详情打开，订单号、商品、状态、用户备注、管理员备注和图片资源可见 | 订单详情打开；管理员备注通过 `textarea` value 校验，内容区可读 | 通过 |
| `/console/stickers/931200000101/items` | 表情列表显示 `P3D32 复核表情`、缩略图、Code 和状态 | 表情列表在移动视口可见，缩略图加载正常 | 通过 |
| 批量上传弹窗 | 弹窗打开，提示、步骤、拖拽区、隐藏文件输入存在；未上传文件 | 弹窗宽约 `374px`，落在 `390px` 视口内；隐藏文件输入 `display: none` | 通过 |
| 浏览器 console | 修复前命中 AntD `Alert.message` deprecated；修复后按时间窗复验新增 `error/warn = 0` | 同一时间窗复验新增 `error/warn = 0` | 通过 |

移动视口仍记录为 DPR `1`，未写成高 DPR 物理视图完整结论。

## 验证

- `npm run check:host-runtime -- --details`：通过，Gateway / Api / Auth 健康端点均正常。
- Browser 插件 Gateway 真实联调：PC `1920x1080` 与 mobile `390x844` CSS 视口通过。
- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。
- `rg -n "<Alert|\\bmessage=" Frontend/radish.console/src/pages/Stickers/StickerBatchUploadModal.tsx`：仅剩 `Alert` 标签，目标 `message=` 已清零。

## 后续判断

`OrderDetail` 与 `StickerBatchUploadModal` 的 D31 数据缺口已补齐。后续不继续围绕同一链路反复制造本地数据；下一顺位继续 `P3-12-D` UI 专题收口，按收益评估执行中宽 PC 固定列交互、移动窄屏表格可读性和已迁移页面真实可用性的目标扫描。`P3-12-E` 发布候选准备后置到 UI 专题明确完成后。

# P3-12-D63 Console Gateway 成组真实页面复核记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`P3-12-D63 Console` 已实现页面族 Gateway PC / mobile CSS 视口复核
> 入口：`https://localhost:5000/console/`

## 背景

`P3-12-D63 Console` 已完成当前发布前范围内的治理、商业、文档、用户和权限矩阵页面族首批实现，并完成成组静态收口。用户本轮明确说明前后端已经启动，因此按项目浏览器 smoke 规则补执行真实 Gateway 页面复核。

本批仍不进入 `P3-12-E`，不创建 tag，不进入 M15 测试或生产部署流程。

## 覆盖范围

账号：本地开发种子管理员账号 `admin@radishx.com / admin123456`。

已覆盖页面：

- `/console/`：登录回流与仪表盘授权落点。
- `/console/moderation`：内容治理工作台。
- `/console/experience`：经验等级 / 经验台账工作台。
- `/console/products`：商品运营页面。
- `/console/orders`：订单运营页面。
- `/console/documents`：文档治理页面。
- `/console/users`：用户管理表格 CRUD。
- `/console/roles/10000/permissions`：角色权限矩阵。

视口：

- PC：`1920x1080`。
- 移动：`390x844` CSS 视口。

本轮曾尝试通过 Playwright CDP 设置 `390x844 @ DPR 3`，但当前 CLI headed 会话最终 `window.devicePixelRatio` 仍读取为 `1`，且截图尺寸为 `390x844`。因此本批移动结论只写作移动 CSS 视口复核，不写作完整高 DPR 物理屏 smoke。

## 结果

- Console OIDC 登录、授权确认和 `/console/` 回流通过。
- PC `1920x1080` 下，7 个 D63 目标页均渲染正确标题、关键任务区和主要按钮，无横向溢出。
- 移动 `390x844` CSS 视口下，同一组页面均可直接访问，不回跳登录页，不出现页面级横向溢出。
- 目标页面接口均返回 `200`，包括内容治理队列 / 日志、经验等级配置、商品 / 订单列表、文档治理列表、用户列表、角色列表和权限矩阵资源 / 授权 / 预览接口。
- Playwright console error 检查为 `0`，warning 检查为 `0`。
- 未执行高影响写动作：未保存权限、未执行治理动作、未重试订单、未删除商品、未导入 / 回滚文档。

## 证据

截图留存在 `output/playwright/d63-console-*.png`：

- PC：`d63-console-moderation-desktop-1920.png`、`d63-console-experience-desktop-1920.png`、`d63-console-products-desktop-1920.png`、`d63-console-orders-desktop-1920.png`、`d63-console-documents-desktop-1920.png`、`d63-console-users-desktop-1920.png`、`d63-console-role-permissions-desktop-1920.png`。
- 移动 CSS 视口：`d63-console-moderation-mobile-390-dpr3.png`、`d63-console-experience-mobile-390-dpr3.png`、`d63-console-products-mobile-390-dpr3.png`、`d63-console-orders-mobile-390-dpr3.png`、`d63-console-documents-mobile-390-dpr3.png`、`d63-console-users-mobile-390-dpr3.png`、`d63-console-role-permissions-mobile-390-dpr3.png`。

文件名保留 `dpr3` 是因为本批按该目标尝试设置；记录结论以实际读取到的 DPR 限制为准。

## 后续

- 下一顺位进入 `P3-12-D64 UI 专题候选前集中验收准备`，汇总 D61-D63 重新实现后的 public / private / console 证据、剩余限制和候选前验证清单。
- 在 D64 / 后续验证完成前，仍不直接进入 `P3-12-E`。

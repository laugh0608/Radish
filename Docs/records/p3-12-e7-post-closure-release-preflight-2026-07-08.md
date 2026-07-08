# P3-12-E7 后发布候选进入前复核记录

> 日期：2026-07-08（Asia/Shanghai）
>
> 主线：`P3-12 Web 完全化与 WebOS 收束`
>
> 性质：发布候选进入前复核。本轮不创建 tag，不进入 M15 测试或生产部署。

## 复核结论

`P3-12-E7` 后发布候选进入前复核在工程门禁层面通过。当前未发现新的 Public / Private / Author / Console / Auth 技术阻断。

但本轮工程复核不能替代产品形态判断。2026-07-08 产品负责人明确判断：当前产品形态距离满意的正式版状态仍有明显差距，不能进入 `P3-12-F 正式版发布候选整备`。后续以 [P3-12-E8 产品形态差距回拉决策记录](/records/p3-12-e8-product-shape-gap-decision-2026-07-08) 为准。

## 覆盖范围

- 启动无关验证：`validate:ci -- --report`、`validate:baseline:host -- --report`。
- 运行态健康：Gateway / Api / Auth 健康端点。
- Chrome 插件真实页面复核：
  - PC：`1920x1080 @ DPR 1`
  - Mobile：`390x844 @ DPR 3`
- Public：`/discover`、`/forum`、公开帖子详情、`/docs`、公开文档详情、`/shop`、公开商品详情、`/leaderboard`、公开个人页、`/legal`。
- Private / Author：`/workbench`、`/me`、`/me/content`、`/me/history`、`/me/experience`、`/notifications`、`/messages`、`/circle`、`/pet`、`/docs/mine`、`/forum/compose`、`/shop/orders`、`/shop/inventory`。
- Console / Auth：`/console/`、`/console/moderation`、`/console/orders`、`/console/documents`、`/console/system-config`、`/console/roles`、`/console/login?auto=1`。

## 验证结果

| 项目 | 结果 |
| --- | --- |
| `npm run validate:ci -- --report` | 通过 |
| `npm run validate:baseline:host -- --report` | 通过 |
| `npm run check:host-runtime -- --details --report` | 通过，Gateway / Api / Auth 均为 200 |
| Chrome PC 页面矩阵 | 未发现横向溢出、控制台错误或 E7 内部术语残留 |
| Chrome Mobile 页面矩阵 | 低频复核未发现横向溢出、控制台错误或 E7 内部术语残留 |
| Auth 授权页 | PC / Mobile 均进入授权确认页，首屏优先展示账号、应用、返回位置、权限用途和安全边界 |

## 本轮修正

`validate:ci` 首次失败在 `Frontend/radish.client/tests/publicSeoStatic.test.ts`：测试仍期待 E7-B 之前的旧文案 `private Web routes / 私域 Web 路由`。

已将测试断言更新为当前产品口径：

- 英文：`signed-in personal pages`
- 中文：`登录后的个人页面`
- 新增旧内部术语防回归断言，避免 `private Web routes / 私域 Web 路由` 回潮。

修正后，`radish.client` 定向测试通过，`validate:ci -- --report` 通过。

## 运行态观察

首轮自动化矩阵使用连续硬导航覆盖 60 个 PC / mobile 页面，移动后半段触发过全局 IP 限流，部分页面短暂显示 `系统初始化 / HTTP 429`。

判断：

- 源头为后端全局限流窗口，而不是 `Bootstrap/Status` 的敏感操作策略。
- 等待限流窗口恢复后，以低频节奏重跑移动代表页，`/discover`、公开详情、`/workbench`、`/me`、`/notifications`、`/messages`、`/circle`、`/pet`、`/forum/compose` 和 Auth 授权页均恢复正常。
- 因此本轮不把该现象列为发布候选阻断；后续发布候选压力或爬虫场景可单独评估全局限流与初始化请求的关系。

`/forum/compose` 移动视图检测到设置抽屉的闭合态元素位于右侧视口外，但页面无横向滚动；截图复核显示它表现为右侧抽屉把手，不构成本轮阻断。

## 不做与限制

- 未提交表单、未购买、未发帖、未发送消息、未变更 Console 数据。
- Console 深层页面在当前会话进入 Auth 授权确认页；本轮只确认登录回流和授权页信息层级，不代替已登录 Console 深层写操作验收。
- 本轮不创建发布 tag，不进入 M15 测试，不部署生产。

## 后续顺位

- 暂不启动 `P3-12-F 正式版发布候选整备`。
- 下一顺位进入 `P3-12-E8 产品形态差距回拉`，先形成产品负责人满意度口径下的差距矩阵和阻断清单。
- 若用户截图、真实页面复核或自动化扫描命中新缺口，再按页面族回拉 Public / Private / Console / Auth；跨页面视觉或运行时契约改动仍需先补方案并确认边界。

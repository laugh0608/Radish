# F2 主题系统专题验收记录

> 日期：2026-07-14（Asia/Shanghai）
> 状态：通过，F2 主题系统完成度治理收口
> 范围：正式 Web 主题入口、共享 UI、Theme 商品与权益、PC / mobile 页面族、WebOS 与 Console 兼容边界

## 1. 验收结论

F2 已形成完整的产品主题链路：`default`、`guofeng` 作为匿名可用内建主题，`theme-dark-night`、`theme-sakura` 作为服务端受控的商城 Theme 权益；四者统一驱动根节点、浏览器 `color-scheme`、Ant Design 和 `@radish/ui`。正式 Web Header 在 PC / mobile 均提供入口，登录后的激活、同类切换、停用、刷新校正和跨标签同步均通过。

Public、Private、Author 代表页面在 PC `1920 × 1080` 与 mobile `390 × 844` CSS 视口下完成成组复核，暗夜主题的页面表面、Markdown、列表、弹层与移动底栏保持可读且无横向溢出。当前没有已知 `P0/P1` 阻断，F2 可以收口，下一顺位进入 F3 i18n 完成度治理。

## 2. 实现契约

- `radish.client` 持有稳定主题注册表、设备内建偏好、当前有效主题和权益状态；`@radish/ui` 只消费宿主注入的主题配置。
- React 挂载前恢复最近有效主题；登录后再以 `UserActiveBenefit` 的 Theme 唯一选择校正，登出或认证失效时回到设备内建偏好。
- Theme 商品和权益只接受服务端 `ShopThemeResources` 中登记的暗夜、樱花资源；未知资源不可上架、不可激活。
- Theme 能力已进入可售、可激活矩阵，但既有 seed 保持下架，不自动改变商品销售状态。
- 未拥有主题进入公开商城“主题”关键词检索；当前零上架商品时展示真实空态，不伪造不存在的商品分类。

## 3. 复核中发现与治理

### 3.1 认证失效竞态

匿名主题弹层曾被过期的权益请求回写“请先登录”错误。权益同步增加 generation 失效标记和 promise 身份保护，登出清理前先使在途同步失效，避免旧会话结果污染新状态；匿名复核后不再出现残留错误。

### 3.2 商城入口契约

未拥有主题原先使用未登记的 `category=theme`，公开商城会将其规范化掉。入口改为正式的“主题”关键词查询，保持与现有公开商城路由和分类契约一致。

### 3.3 深色表面语义

暗夜 `/me` 复核发现多处 `color-mix(..., white ...)` 仍把列表表面混成亮灰色。新增 `--theme-surface-highlight / --rx-surface-highlight` 语义 token，并统一替换 Client 可主题化 CSS 中的硬编码混色基准；回归测试扫描 `src/**/*.css`，阻止相同问题重新进入。修复后暗夜页面矩阵未再发现系统性亮色表面。

## 4. 运行态矩阵

| 链路 | 结果 | 证据 |
| --- | --- | --- |
| 匿名内建主题 | 通过 | 清朗 / 国风切换、刷新持久化和多标签同步正常；权益主题展示登录或商城归属，不可越权激活 |
| 登录权益旅程 | 通过 | 暗夜激活、刷新校正、暗夜切换樱花、回到清朗停用均成功，服务端唯一指针与操作流水同步变化 |
| Public | 通过 | 发现、论坛、公开 Docs、排行榜、公开商城在 PC / mobile 下保持主题和布局稳定 |
| Private | 通过 | 我的、消息、通知、工作台、圈子、宠物、背包在暗夜主题下可读且无横向溢出 |
| Author | 通过 | Docs 作者入口和公开文档详情在暗夜 / 樱花下保持 Markdown 与操作区可读 |
| Compatibility | 通过 | `/desktop` 国风入口正常；Console production build 通过，Gateway 授权确认边界可达，未绕过授权确认进入管理端 |
| 公开 head | 通过 | Theme 商品公开路径纳入 head snapshot 与 sitemap 登记，静态契约测试通过 |

移动结论基于 `390 × 844` CSS 视口，不宣称真实设备 DPR 覆盖。Console 本轮只验证独立主题不被 Client 主题污染、构建通过与授权边界可达，不包含受权后的逐页管理端 smoke。

## 5. 自动化与构建

- `npm run validate:baseline:host`：通过；后端全量 655 项通过，11 项环境用例按配置跳过，解决方案构建 0 warning / 0 error，DbMigrate doctor / verify 通过。
- `npm run check:host-runtime`：Gateway、API、Auth 健康检查通过。
- `radish.client`：type-check、lint、production build 通过，345 项测试通过。
- `radish.console`：type-check、lint、production build 通过，35 项测试通过。
- Theme 服务端资源校验、商品能力与激活约束测试通过。
- changed-only 仓库卫生与 `git diff --check` 通过。

## 6. 数据影响与清理

受控验收只为 Admin 用户插入 2 份固定 ID、`AcceptanceTest` 来源的临时 Theme 权益，没有创建订单、修改余额或调整商品销售状态。验收真实走服务接口生成暗夜激活、樱花切换和停用流水，随后按固定 ID 精确删除临时权益、选择指针及相关操作流水。

最终临时权益、激活指针和操作流水残留均为 0，`PRAGMA integrity_check` 返回 `ok`。暗夜、樱花 seed 继续保持下架，本轮不创建 PR、tag，也不执行发布部署。

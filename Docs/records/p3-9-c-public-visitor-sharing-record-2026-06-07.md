# P3-9-C 访客公开访问与分享整备记录（2026-06-07）

> 本记录承接 `P3-9-C 访客公开访问与分享整备`。
>
> 本批围绕 `/discover`、`/forum`、`/docs`、`/shop`、`/leaderboard`、`/u/:id` 与公开详情页，复核公开阅读、分享预览、来源返回、登录回流和 LongId / PublicId 字符串契约。

## 批次结论

本批没有恢复移动 Web 公开页逐页打磨，也没有启动完整 Playwright / E2E 平台、完整 PublicId 迁移、WebOS / Tauri 新功能或新的后端接口。

自动化复核确认现有公开路径测试已覆盖根路径进入 `/discover`、公开详情 canonical / OpenGraph / 分享链接、forum PublicId 优先、`/desktop` 登录回流和 LongId 字符串守护。进一步代码复核发现一个发布候选分享缺口：公开入口只有详情页在加载数据后应用 JSON-LD，`/discover`、`/leaderboard` 以及 forum / docs / shop 浏览页缺少通用结构化数据承接。

处理方式是在公开结构化数据 helper 中增加 `buildPublicRouteStructuredData`，由 `PublicEntry` 按当前公开路由统一应用 JSON-LD；公开浏览页输出 `CollectionPage`，公开详情页先输出通用 `WebPage`，随后仍由既有详情页数据覆盖为 `BlogPosting`、`Article`、`Product` 或 `ProfilePage`。

## 处理范围

| 项目 | 处理结果 |
| --- | --- |
| 公开路由 JSON-LD | `PublicEntry` 按当前路由统一应用通用 JSON-LD，并在路由切换 / 卸载时清理 |
| 公开浏览页 | `/discover`、`/leaderboard`、forum / docs / shop 非详情路由输出 `CollectionPage` |
| 公开详情页 | 详情页先有通用 `WebPage`，加载详情数据后继续覆盖为更精确的内容类型 |
| ID 展示契约 | 通用 JSON-LD 的 `name` 使用公开 head 文案，不把数字公开 ID 写成页面名称 |
| 自动化守护 | 补充结构化数据单测和公开入口静态守护 |

## 验收覆盖

| 路径 | 覆盖点 | 自动化守护 |
| --- | --- | --- |
| `/` / `/discover` | 普通浏览器根路径进入公开发现页；发现页 share / JSON-LD 可生成 | `entryRoute.test.ts`、`publicStructuredData.test.ts`、`publicSeoStatic.test.ts` |
| `/forum` / `/forum/post/:id` | 公开列表、详情、PublicId canonical / share、评论定位与详情 JSON-LD 覆盖 | `publicHead.test.ts`、`publicStructuredData.test.ts`、`realUsagePathContracts.test.ts` |
| `/docs` / `/docs/:slug` | 文档列表 / 搜索 / 详情 canonical、锚点分享和详情 JSON-LD 覆盖 | `publicRouteState.test.ts`、`publicHead.test.ts`、`publicStructuredData.test.ts` |
| `/shop` / `/shop/product/:id` | 商品列表 / 详情公开路径、购买回流 `/desktop`、Product JSON-LD 覆盖 | `publicRouteState.test.ts`、`publicSeoStatic.test.ts`、`realUsagePathContracts.test.ts` |
| `/leaderboard` | 榜单类型 / 分页分享路径和 `CollectionPage` JSON-LD | `publicHead.test.ts`、`publicRouteState.test.ts`、`publicStructuredData.test.ts` |
| `/u/:id` | 公开主页 canonical、只读边界、ProfilePage JSON-LD 覆盖和数字 ID 不作名称 | `publicHead.test.ts`、`publicStructuredData.test.ts`、`publicSeoStatic.test.ts` |
| 登录 / 工作台承接 | 公开商品购买和论坛参与进入 `/desktop`，登录回流只接受合法 desktop 相对路径 | `authReturnPath.test.ts`、`desktopEntryNavigation.test.ts`、`realUsagePathContracts.test.ts` |

## 验证记录

执行目录：仓库根目录。

```bash
npm run test --workspace=radish.client
npm run type-check --workspace=radish.client
npm run lint:changed
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
npm run validate:identity
node Scripts/check-public-head-smoke.mjs --self-test
```

结果：

- `npm run test --workspace=radish.client`：通过，`210` 项测试通过。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run lint:changed`：通过，检查 `radish.client` 变更文件。
- `npm run build --workspace=radish.client`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。
- `npm run validate:identity`：通过，仍有项目既有 XML 注释 warning。
- `node Scripts/check-public-head-smoke.mjs --self-test`：通过。未启动本地服务，未执行需要 `--base-url` 的在线 head smoke。

## 后续建议

- 若需要人工复核，推荐由用户启动 Gateway 后访问 `https://localhost:5000/`，按移动视口检查 `/discover`、公开详情分享和登录回流。
- 若本批后续未暴露新的公开路径阻断，下一顺位建议转向 `P3-9-D Console 排障与治理入口整备`，围绕用户、订单、商品、胡萝卜流水和权限授权做管理员路径验收。

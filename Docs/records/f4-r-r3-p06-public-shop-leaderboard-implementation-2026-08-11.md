# F4-R R3-P06 Public Shop 浏览与 Leaderboard 正式实现

> 日期：2026-08-11（Asia/Shanghai）
>
> 状态：正式代码、静态回归与 Gateway PC / mobile 成组验收已完成，R3-P06 关闭
>
> 范围：`/shop` 首页、`/shop/products` 与五类公开榜单；商品详情只做共享回归，不修改 API、数据库、migration、权限、Pencil 或购买状态机

## 结论

R3-P06 已按 `R1-P01 + R2-P03` 的继承边界完成正式实现。Shop 首页与商品列表固定为商品主轴先于购买说明 / 阅读提示，删除重复浏览入口和会被误解为真实统计的 rail 信息；商品行只进入公开详情，匿名与登录态 CTA 分别准确表达“查看详情后购买”和“查看详情并购买”。

Leaderboard 保留主区唯一五类切换、分页、用户公开主页和商品公开详情边界，删除 rail 内重复类型导航、泛化返回说明和误导性的身份摘要。前三名颜色改为四主题语义 surface；后端榜单类型仍决定可用性、分类和排序，但类型名、说明与指标由当前语言包覆盖，避免英文页面继续显示后端中文元数据。

Gateway 已覆盖匿名、种子 Admin、PC `1920 × 1080`、Mobile `390 × 844`、中英文与 `default / guofeng` 代表主题。商品购买意图登录回流准确返回同一详情并打开确认弹窗，本轮主动取消确认，没有创建订单或改写业务数据。种子库热门商品榜为权威空态，因此有数据时的详情链接继续由静态契约与既有路由测试守卫。

## 已完成范围

### Public Shop

- Shop 首页与 products 保持现有 URL、筛选、商品字段、库存 / 售出展示和公开 head，不新增第二套导航或交易入口。
- 商品主内容在 DOM 与 Mobile 视觉顺序中均先于辅助 rail；`1120px` 以下两个辅助面板保持平衡，`720px` 以下转为主内容后的连续单列。
- 删除 App 外层与列表自身重复的“浏览商品”动作；首页仍保留一个清楚的商品列表入口，products 保留返回商城首页。
- 匿名行 CTA 改为“查看详情后购买 / View, then purchase”，登录态改为“查看详情并购买 / View and purchase”；列表链接不携带购买意图。
- 商品详情继续独占 `intent=purchase`、登录回流、购买确认、支付口令、订单和背包边界，本批没有修改其状态机。

### Public Leaderboard

- 主区保留唯一五类切换：Experience、Post count、Comment count、Popularity、Hot products；辅助栏只说明当前榜单类型与阅读边界。
- 用户榜行继续进入 `/u/:publicId`；商品榜行继续进入 `/shop/product/:id`，榜单本身不出现购买、订单、库存或账号写操作。
- “我的排名”只在已登录、当前为用户榜且服务端返回有效排名时显示；商品榜和无排名用户榜不伪造 Guest / `0` 排名。
- 后端类型元数据继续作为可用性、分类、排序和图标来源，已知公开类型的展示名、说明、指标统一由 `zh-CN / en-US` 语言包提供。
- `--theme-rank-first-surface`、`--theme-rank-second-surface`、`--theme-rank-third-surface` 在 `default / guofeng / dark-night / sakura` 四主题中均有语义定义，榜单样式不再硬编码奖牌颜色。

### 文档与契约

- 新增 `publicShopLeaderboardR3Contract.test.ts`，守卫主内容顺序、单一类型切换、CTA 语义、语言覆盖、四主题 token 和页面无组件硬编码色。
- 修正 `validation-baseline.md` 与 `frontend/design.md` 的旧口径：热门商品榜允许进入公开商品详情，但不能直接发起购买或进入订单 / 背包。

## 静态验证

- `radish.client` 测试：`550 / 550` 通过。
- `radish.client` type-check、Lint、production build：通过。
- R3-P06 定向契约：`5 / 5` 通过。
- `check-docs.sh`、`check:repo-hygiene:changed`、`git diff --check`：通过；`validation-baseline.md` 仅保留既有建议行数提示。
- `validate:baseline:quick` 的本批相关门禁通过，总入口仍只被 `Radish.Repository/SystemConfigStorageCoordinator.cs` 既有三处 `DateTime.Now` baseline 漂移拦截；该文件不在本批差异中。
- production build 继续报告仓库既有的大 chunk 提示，本批没有新增依赖。

## Gateway 运行态验收

- 通过 `https://localhost:5000` 覆盖 PC `1920 × 1080` 与 Mobile `390 × 844`；Shop 和 Leaderboard 页面宽度分别稳定为当前视口宽度，无横向溢出。
- 匿名 Shop 首页 / products 的 CTA、列表到详情、详情 `intent=purchase` 登录入口通过；种子 Admin 登录后回到 `/shop/product/100061?intent=purchase` 并打开真实购买确认，随后取消，没有提交购买。
- 五类榜单路由、唯一类型切换、用户公开主页链接、登录用户有效排名、Popularity / Hot products 无排名边界和热门商品权威空态通过。
- 英文验收发现类型名、说明和指标继续消费后端中文元数据；修正为语言包覆盖后，英文五类切换、说明、指标、rail 和行指标复验通过。等级名和商品内容等业务原文继续保留服务端值。
- `default / guofeng` 代表主题通过；暗夜与樱花在当前账号 UI 中是商城获取态，四主题 token 完整性由静态契约守卫，不绕过产品解锁边界制造运行态状态。
- Mobile 固定主内容先于辅助 rail：Shop rail 位于商品主轴之后，Leaderboard rail 位于榜单结果之后；底部导航、筛选带和榜单行无页面横向溢出。
- 最终浏览器控制台 `0 warning / 0 error`。本轮 Gateway、API、Auth、Client、Console 已停止，`5000 / 5100 / 5200 / 3000 / 3100` 端口均释放。

## 下一顺位

R3-P06 关闭。下一顺位进入 `R3-C04 Console 普通资源` 设计前代码事实与风险拆批审计：先按 Applications、Products、Users、Categories、Tags、Stickers、Coins 的读写风险、列表 / 详情 / Modal 所有权和 Mobile 转换分组，再提交实施方案；不复制七套 Mobile 状态机。

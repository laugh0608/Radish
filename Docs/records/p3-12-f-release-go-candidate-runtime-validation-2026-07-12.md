# P3-12-F Release Go 候选运行态验收记录

> 日期：2026-07-12（Asia/Shanghai）
>
> 结论：候选级升级、自检、宿主启动、Gateway PC / mobile 页面 smoke 与候选静态质量门禁通过；Release Go 条件 9 已满足，条件 10 的小规模受控试用仍未执行。

## 一、范围与停止线

本轮覆盖正式 Web 发布矩阵：

- `Radish.Gateway`、`Radish.Api`、`Radish.Auth`、`Radish.DbMigrate`。
- `radish.client` 与 `radish.console` 的 Gateway 入口。
- 公开、登录态私域、论坛 / Docs 作者态与 Console 代表路径。

本轮未创建 tag、未推送镜像、未部署，也未执行发帖、评论、上传、购买、权限保存或其他主动业务写入。页面资源读取仍会按现有契约增加附件下载计数等访问统计。

## 二、升级与宿主准备

### 1. 本地数据库

- 升级前将五个 SQLite 数据库备份到 `DataBases/backups/p3-12-f-pre-release-go-20260712-1605/`。
- 首次真实 `DbMigrate apply` 暴露 split table baseline 接管误要求模板表的问题。
- `f630ddbb` 改为按模板前缀识别并校验现有物理分表；没有物理分表时允许后续运行期创建。
- 修复后 Main / Log / Message / Chat schema baseline、经验自然日迁移、OpenIddict migration adoption、seed 与严格 verify 全部通过。
- `validate:baseline:host` 通过：构建 `0 warning / 0 error`，后端 `624 passed / 7 skipped`，doctor / verify 无待处理迁移。

### 2. 宿主启动

- 端口 `5000` 的 macOS AirPlay Receiver 占用由用户在本会话关闭后再启动服务。
- Gateway、API、Auth、client、Console 分别在 `5000/5001`、`5100`、`5200`、`3000`、`3100` 启动。
- 首次真实启动暴露 `UseExceptionHandler()` 未配置可用处理分支，导致 API 启动失败。
- `f0df59bb` 显式建立 API JSON 异常分支，复用 `ApiExceptionHandler`，非 API 异常继续保留原异常传播；新增宿主管线构建与未知异常脱敏测试。
- 修复后五个服务同时启动，`check:host-runtime -- --details` 的 Gateway / API / Auth 探针全部为 `200`。
- 验收结束后已通过启动脚本 trap 关闭五个服务，`5000 / 5100 / 5200 / 3000 / 3100` 均无监听进程。

## 三、Gateway 浏览器 smoke

### 1. 账号与视图

- 使用本地开发种子管理员 `Admin#2` 完成 OIDC authorization code 登录，不在记录中保存密码。
- PC 使用 `1920x1080` CSS 视口。
- 移动端使用 `390x844` CSS 视口；当前内置浏览器能力不能设置 DPR，因此不把它写成 `@ DPR 3` 物理屏覆盖。

### 2. 公开路径

以下路径在 PC 和移动 CSS 视口下正常渲染，URL 深链稳定且无页面级横向溢出：

- `/discover`、`/forum`、论坛帖子详情。
- Docs 公开详情、商城商品详情。
- `/u/:publicId` 公开主页与作者来源返回。
- client 与 Console 未登录入口均正确进入各自 OIDC client 的统一登录页。

### 3. 登录态、作者态与 Console

PC 登录态覆盖：

- `/workbench`、`/me`、`/notifications`、`/messages`、`/circle`、`/pet`。
- `/shop/orders`、`/shop/inventory`。
- `/forum/compose`、`/docs/mine`、`/docs/compose`。
- `/console/`、订单、商品、用户、角色、文档、内容治理、系统设置与 Hangfire 入口。

移动 CSS 视口复核了个人中心、聊天、通知、圈子、订单、Docs 作者台 / 新建页，以及 Console 总览、订单、用户、角色、系统设置与 Hangfire。异步数据页在加载完成后均保持 `clientWidth = scrollWidth = 390`，没有登录误跳、权限降级或页面级横向溢出。

浏览器页面日志最终无 `error / warning`。自动化工具自身的 Statsig 请求超时与一次截图 CDP 超时不来自 Radish 页面，不计为产品失败。

## 四、公开 head 抽查

`check:public-head-smoke` 通过：

- `robots.txt`、`sitemap.xml` 及 static / forum / docs / shop shards。
- `/discover`、论坛帖子详情、Docs 详情、商品详情。

发现 `/u/:publicId` 初始 HTML 仍返回通用 SPA head，缺少服务端 canonical、Open Graph、Twitter card 与 JSON-LD。浏览器加载后的动态 head 与页面功能正常；该项不构成已知 `P0/P1` 或本轮运行态阻断，按公开 head 并行维护线登记为 P2，后续需补 Gateway snapshot 与公开 profile 服务端快照契约。

## 五、候选静态门禁

`npm run validate:candidate` 的代码质量部分通过：

- 全量仓库卫生检查 `2410` 个已跟踪文件；`110` 个历史问题文件均在 baseline 内，无新增。
- 四个前端 workspace lint 为零 warning，type-check 通过。
- 前端测试：HTTP `6`、UI `3`、client `331`、Console `28`，共 `368` 通过。
- .NET warning-as-error 构建为 `0 warning / 0 error`。
- 后端测试 `625 passed / 7 skipped`；跳过项均为需要 PostgreSQL 环境变量的既有环境集成用例，其候选级真实运行证据已在 Q1 / Q2 留痕。
- LongId、身份语义、敏感字面量、时间语义与 Repo Quality contract 检查通过。

沙盒内 NuGet 审计首次因用户级 NuGet HTTP cache 无写权限异常退出；在沙盒外复跑 `npm run check:dependency-security` 后，npm / NuGet High、Critical 均为 `0`。这是验证环境边界，不是依赖漏洞失败。

## 六、集成与剩余门禁

- `master` 基点：`c5906604`。
- 本记录提交后 `dev` 相对该基点为 `23` 个提交、`308 files changed`、`14206 insertions / 9226 deletions`。
- merge-tree 预检无文本冲突；最终 PR 前仍需以届时 `origin/master` 重新刷新范围和远程 required checks。
- Release Go 条件 1-9 的发布必要证据已形成；条件 10 的小规模受控试用仍是正式 tag / 生产发布前的剩余门禁。
- 当前继续不创建 tag、不推送镜像、不部署。合并 `dev -> master`、受控试用、tag 与生产发布保持独立决策。

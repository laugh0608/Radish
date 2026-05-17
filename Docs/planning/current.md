# 当前进行中

> 本页是新会话快速入口，只维护 **当前阶段、最近进度、当前目标、下一顺位与维护线**。
>
> 历史批次、验证细节和长背景不要继续堆到本页；需要回顾时查看 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 与对应专题文档。

## 当前状态

- **阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-4 用户留存轻闭环` 首轮收尾
- **复核日期**：`2026-05-17`
- **最近结论**：
  - `v26.3.2-release` 已于 `2026-04-06` 完成首版真实发布，第一开发阶段结束
  - `Phase 2-2 移动 Web 形态` 已完成 forum / docs / `u/:id` / leaderboard / shop / discover 公开内容壳层首批收口，转入稳定维护
  - `Phase 2-3 Android MVP` 已完成第一轮 RC 验收并给出 Go 结论
  - Capacitor Android 因登录 / OIDC 与本机调试复杂度终止，不进入移动端产品化主线
  - Tauri 桌面安装包个人开发阶段验证通过，定位为 `Tauri 壳 + WebOS 桌面工作台`
  - WebOS 桌面工作台已回到产品功能开发推进，并落地首批“继续使用”复访入口、桌面应用恢复入口与萝卜坑工作流补全
  - Console 治理已完成用户详情、个人资料 / 设置真实化与仪表盘真实统计首轮补洞
  - `ID Phase A` 已进入前期治理面：先冻结外部 `long` 暴露扩散，按 `LongId` / 字符串安全收口窗口参数、通知跳转、公开路由与 `Profile / Shop / Wiki / Forum` 首批边界；数据库主键迁移与全量 `PublicId` 切换继续后置
  - 当前主线已从“多端路线收口后的产品功能开发推进”“第二阶段收口评审”、`P2-C5-A` Flutter 首批补强、下一阶段主任务选择、`P3-0 / P3-1 / P3-2 / P3-3`，切到 `P3-4`
  - `P2-C3 经验 / 等级治理` 已完成用户概览、冻结 / 解冻、每日统计观察、经验流水回看、最小可解释异常规则、人工复核联动与治理留痕首轮收口
  - Console 内容治理已完成聊天消息定位、帖子评论 / 轻回应回看、失效降级、目标快照、审核效率、手动处置与当前治理状态前置，转入稳定维护
  - 商城管理人工验收已于 `2026-05-12` 收口：商品相关订单、详情长 ID、删除确认、订单跳用户 / 商品返回链路和后端商品删除保护均已确认稳定
  - Console 历史 chunk size warning 已完成首轮治理，后端已收口 Extension / Repository / Service / Infrastructure / Common / Model / 测试项目和 Gateway 配置加载的历史 warning；完整重建已达成 `0` warning / `0` error
  - 安全治理尾项已完成当前小批次收口：文件访问令牌、权限匹配、公开 / Console 授权一致性、缓存默认值和 Console 权限种子一致性均已补测试与验证，完整重建保持 `0` warning / `0` error
  - `2026-05-13` 已完成 [第二阶段收口评审](/planning/phase-two-closure-review) 窄范围筛查，未发现新的必须立即闭环 `P0/P1` 阻断项
  - `P2-C5-A Flutter 移动端高价值已登录链路首批评估` 已完成论坛通知轻入口、forum detail 回流和个人复访轻操作补强；第二阶段归档判断随后给出 Go 结论
  - 第二阶段归档判断结论为 Go，已写入 [已完成摘要](/planning/archive)
  - `2026-05-13` 已批准启动 `P3-0 第三阶段定义与工程整备`，第三阶段主题暂定为“真实使用增长与长期契约治理”
  - `P3-0-A` 公开内容增长基础审计已完成，第一批正式开工任务收口为 `P3-1 公开内容 SEO 与分享基线`
  - `P3-1-A` 已完成公开 head 契约、运行时 canonical、`robots.txt` 与 sitemap seed 首批落地
  - `P3-1-B` 已完成 forum detail / shop detail 复制 canonical 链接入口，公开详情分享基线首批收口
  - `P3-2-A` 已完成外部 ID 契约审计，最小试点对象收敛为 `Post`；首批只做 `Post.PublicId` 并行契约，不牵动数据库主键、`User / Product / WikiDocument / Comment` 或全量 API 切换
  - `P3-2-B` 已完成 `Post.PublicId` 首批实现：新帖生成 `pst_` + UUIDv7 编码体，详情接口支持 long / PublicId 双读，`PostVo.VoPublicId`、forum canonical / 分享、通知 `extData`、浏览历史 routePath 与 WebOS forum 窗口参数均已保留旧 `postId` 并并行支持 `postPublicId`
  - `P3-3-A / P3-3-B` 已完成 `PublicForumApp.tsx` 公开论坛热区低风险拆分：公共 helper、`PublicStatusCard`、`PublicForumTypeFeed`、`PublicForumSearch`、`PublicForumTag`、`PublicForumList` 和 `PublicForumDetail` 已抽出，主文件从约 `2911` 行降到约 `208` 行，未改变业务行为
  - `2026-05-16` 已完成 `P3-3` 收工复核：`radish.client` 类型检查、公开 forum / public route / public head 定向测试、changed-only 文本卫生与 `git diff --check` 均通过；下一主线选择为 `P3-4 用户留存轻闭环`
  - `P3-4-A1` 已完成 Flutter forum notification PublicId 回流：移动端通知解析优先使用 `extData.postPublicId`，缺失时回退旧 `postId`，定向测试通过
  - `P3-4-A2` 已完成“我的轻回应”PublicId 回流：后端并行暴露 `VoPostPublicId`，WebOS 与 Flutter 回流优先使用 PublicId 并保留旧 `VoPostId` 回退
  - `P3-4-A3` 已完成最近阅读 / 浏览历史历史数据补齐策略评估：旧 long `RoutePath` 仍可回流，新访问会自然刷新为 PublicId canonical，当前不做一次性历史数据批量补齐
  - `P3-4-A4` 已完成 forum 回流窄范围真实链路复核：公开分享、通知、最近阅读、我的轻回应回到 WebOS / Flutter 详情时继续保持 PublicId 优先、long id fallback，Flutter 个人页可见文案不再外露 long id
  - `2026-05-16` 已调整第三阶段开发节奏：产品正式上线进入稳定运营前，默认由 AI 主动做批量链路验收、成组修复和一次性交付结论；正式稳定运营后再切回小步谨慎修复
  - `2026-05-16` 已完成 `P3-4-B` 至 `P3-4-H` 主动验收批次：forum 公开分享 / 通知 / 最近阅读 / 我的轻回应 / 公开个人页双向复访在 WebOS 与 Flutter 中保持 PublicId 优先、long id 仅 fallback 且不在普通用户可见文案外露；docs 与 shop 最近阅读 / 公开 head 的 long id 可见性也已收口
  - `2026-05-17` 已完成 `P3-4-I` 公开 head 标识可见性补洞：forum 详情 / 分类、docs 数字兼容路径和公开个人页初始 head 不再把长数字标识写入 title / description，canonical 与旧路径打开兼容保留
  - `P3-4` 首轮收尾判断为可收尾：forum / docs / shop 留存回流矩阵当前未发现新的 `P0/P1` 阻断项，下一步进入公开内容增长后续专题评估

## 当前执行入口

- [开发路线图总览](/development-plan)
- [第三开发阶段：真实使用增长与长期契约治理](/planning/phase-three-real-usage-contract-governance)
- [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
- [第二阶段收口评审](/planning/phase-two-closure-review)
- [第二阶段产品功能补全规划](/planning/phase-two-product-completion)
- [Phase 2-3 Flutter 客户端 MVP](/planning/phase-two-flutter-client-mvp)
- [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
- [前端多壳层策略](/frontend/shell-strategy)
- [Flutter Android MVP RC 验收记录（2026-05-04）](/records/flutter-android-mvp-rc-acceptance-record-2026-05-04)
- [验证基线说明](/guide/validation-baseline)

## 当前目标

1. **`P3-4` 用户留存轻闭环**
   - 首轮 forum / docs / shop 留存回流矩阵已完成主动验收、成组补洞和定向验证
   - 后续只处理真实使用中新暴露的同类回流断点，不再默认扩全量 `PublicId`、数据库主键迁移或低收益微体验
   - 下一步先做下一主线评估，确认公开内容增长后续专题是否进入下一批
2. **第二阶段收口护栏**
   - WebOS / PC 工作台、后端 + Console、公开 Web 与 Tauri 转入稳定维护
   - 若新发现会阻断资产、安全、登录、购买、转账或主路径的 `P0/P1` 缺口，最多挑 `1-2` 个小闭环
   - 若 `P3-0` 审计发现阻断项，最多挑 `1-2` 个小闭环回拉
3. **多端方向保持收束**
   - Web 浏览器：公开内容壳层
   - Android / iOS：Flutter 移动原生安装包路线
   - Windows / macOS / Linux：`Tauri 壳 + WebOS 桌面工作台`

## 下一顺位

- `P3-4` forum / docs / shop 留存回流矩阵首轮已完成阶段性收尾判断；下一步评估公开内容增长后续专题是否进入下一批
- `P3-3` 只保留后续观察，不继续无边界深拆 `PublicForumDetail` 内部结构
- 公开内容增长后续专题后置到 `P3-4` 首批留存链路跑通后，再评估动态 sitemap、结构化数据或 SSR / SSG 是否值得进入下一批
- WebOS / PC 工作台只继续处理成片工作流中的阻断级缺口，不再无限扫零碎按钮或提示
- 后端 + Console 治理转入稳定维护，后续只处理新暴露的安全 / 授权一致性问题
- Flutter 移动端高价值已登录链路首批已完成，后续不默认转向分发产品化材料或低增益微体验
- Tauri 桌面安装包正式签名、自动更新、生产 Auth、SmartScreen、托盘 / 菜单与公开分发方式，仅在真实对外分发前重新评估
- 公开内容壳层只做必要稳定维护和问题修复，不继续新增公开入口或细节增强

## 下一事项

- 下一事项：评估公开内容增长后续专题是否进入下一批，优先比较动态 sitemap、结构化数据和详情首包可见性的收益 / 成本 / 部署风险
- 若继续推进留存链路，只从真实使用中暴露的新断点选择小闭环，不再默认扩全量 `PublicId`、数据库主键迁移或 `User / Product / WikiDocument / Comment` 外部标识改造
- 下一主线候选优先评估公开内容增长后续专题是否进入下一批，例如动态 sitemap、结构化数据或详情首包可见性；未评估前不直接启动 SSR / SSG

## 并行维护项

- `M14` 宿主运行与最小可观测性基线
- `M15` 最小交付与部署基线
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`
- 桌面壳层、窗口几何记忆、主题切换、聊天室 `P1`、通知中心、商城等既有能力稳定维护

## 当前不做

- 继续沿 `Phase 2-1` 扩张论坛回跳、轻回应或通知尾项
- 绕过 `P3-2` 最小试点方案去铺完整 `PublicId` 全量迁移
- 把正式稳定运营前的开发节奏降级为“每修一点等待开发者手动复测一次”
- 继续无边界扫低收益按钮、提示、样式或非阻断 warning
- 继续围绕 WebOS 复访入口做低收益微调
- 在产品功能缺口未重新排序前优先推进 Android / Tauri 分发产品化材料
- 未经重新评估继续追加低增益 Flutter 微体验批次
- 未经单独评估启动 Flutter iOS 产品化工程
- 把 Windows / macOS / Linux 作为 Flutter 默认扩平台方向
- 继续扩大 Capacitor Android 登录态或移动端产品化能力
- 把 Tauri 当作原生 UI 重写路线
- “移动版 WebOS”
- 完整 `PWA / 离线能力 / 推送闭环`
- 完整 `PublicId` 全量迁移或数据库主键迁移
- 完整 Playwright / E2E 平台
- 完整可观测性平台或大而全运维平台

## 文档更新规则

- 本页必须保持简短，面向新会话快速读取
- 没有主线切换、优先级变化或新的关键事实，不改本页
- 功能开发细节、命令级验证记录、批次流水和历史背景不写入本页
- 需要记录历史时，写入 [开发日志](/changelog/)、[已完成摘要](/planning/archive) 或对应专题文档
- 若移动 Web、Flutter 移动端或 Tauri + WebOS 桌面端成为正式主线，必须同步：
  - [开发路线图](/development-plan)
  - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
  - [第二阶段收口评审](/planning/phase-two-closure-review)
  - [当前进行中](/planning/current)
  - [前端多壳层策略](/frontend/shell-strategy)

# 开发路线图（总览）

> 本页是路线图入口，只保留 **产品定位、当前阶段、阶段衔接、下一顺位、维护线与明确后置项**。
>
> 今日执行看 [当前进行中](/planning/current)；实现事实与验证证据查看 [记录索引](/records/)、[开发日志](/changelog/) 和对应专题。

## 当前状态

- **当前里程碑**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前子阶段**：`P3-12-E8 Pre-RC 产品与发布工程硬化`
- **工程第一顺位**：`P3-12-E8-Q0-C 身份验证与敏感日志收紧`
- **产品下一顺位**：`P3-12-E8-B 有限产品收口`
- **最近正式发布**：`v26.3.2-release`（2026-04-06）
- **复核日期**：`2026-07-11`
- **当前结论**：
  - 第二开发阶段完成公开 Web、Flutter Android MVP、Tauri + WebOS 验证与多端分工，2026-05-25 后正式收敛为纯 Web + Flutter。
  - 第三阶段 P3-1 至 P3-11 已完成公开增长基础、PublicId 试点、复访链路、Web-first 信息架构、身份语义、写操作可靠性和发布候选路径验收。
  - P3-12-A-D 已完成正式 Web 主路径迁移、WebOS 收束和 Public / Private / Author / Console 页面族首批实现。
  - P3-12-E1-E7 已完成首批产品成熟度硬化；E8 首日完成正式导航、用户语言、页面滚动、聊天工作区和公开文档口径回拉。
  - 发布工程审计确认 Q0 安全问题仍阻断进入 F；Q1 / Q2 / Q3 的发布必要子集调整为 F 内 Release Go 门禁，Q4 转为持续维护。
  - Q0-A、Q0-B 已于 2026-07-11 完成：依赖安全门禁已恢复，生产性能、Weather、敏感配置、事务演示与测试 Action 已退出；Q0-C 为下一工程批次。
  - 当前不直接进入 Phase 4。第三阶段结束前还需要完成正式 Web 候选与小规模受控试用，形成真实用户反馈闭环。

## V1 产品定位

Radish V1 固定为：

> 面向小规模兴趣或创作社区的可独立部署社区产品：用帖子、评论和问答沉淀内容，用聊天、关注和通知形成复访；Docs 承接知识沉淀，宠物、经验、资产与商城作为可选激励层。

产品优先级：

1. **社区核心**：发现、论坛、评论 / 回答、登录态聊天、关注 / 圈子、通知和信任治理。
2. **社区支撑**：Docs、Workbench 低频能力地图、公开主页和 Console。
3. **辅助激励**：经验、宠物、资产、背包和商城。
4. **长期扩展**：推荐、联邦、PWA、开放平台和多端增强；不进入当前正式 Web 候选。

低频模块必须能回到内容、关系、贡献或复访主轴，不能与社区核心并列争夺默认首页和开发顺位。

## 当前开发精力

- `45%`：Q0-C / Q0-D 身份与 Markdown 安全阻断；Q0-A、Q0-B 已完成。
- `30%`：E8-B 有限产品矩阵，重点收口内容优先首页、社区核心旅程、公开 Docs 契约与 Console 边界。
- `15%`：P3-12-F 进入准备、`master...dev` 范围控制和集成材料。
- `5%`：公开 head、身份契约、验证入口和 WebOS 阻断级兼容维护。
- `5%`：Flutter 已落地主路径维护，不扩完整移动能力套件。

## 已确认的多端方向

1. **Web 浏览器**
   - 纯 Web 是 PC / mobile 浏览器默认正式产品。
   - 根路径 `/` 进入内容优先发现页；`/workbench` 承接低频能力与历史兼容入口。
   - 本次正式发布矩阵包含 Gateway、API、Auth、DbMigrate、client 和 Console。
2. **Flutter**
   - Android MVP 与既有高价值路径继续维护，iOS 后续评估。
   - 首个正式 Web RC 前不恢复完整移动套件；受控试用后再按真实复访价值选择一组移动增强。
3. **WebOS `/desktop`**
   - 只作为历史兼容入口，不承接新增功能。
   - 只处理阻断级问题和迁移所需缺口。
4. **Tauri / PC**
   - 后置为纯 Web 的系统增强壳，不再绑定 WebOS。
   - 本次正式 Web 发布不以 Tauri 构建、签名或分发为阻断。
5. **Console**
   - Console 是桌面优先的社区治理后台。
   - 移动端只承接队列查看、搜索、证据回看和低风险处理，不要求桌面能力完整复制。

## 阶段路线

### 第一开发阶段：首版发布

- `v26.3.2-release` 已完成首版真实发布。
- 认证、基础社区、商城、治理、部署与回滚形成首版基线。

### 第二开发阶段：社区深化与多端验证

- 已完成公开内容壳层、Flutter Android MVP、Tauri + WebOS 验证和多端路线裁决。
- 阶段结论已归档；WebOS 与 PC/Tauri 不再作为新增功能主线。

### 第三开发阶段：Web-first 与正式产品化

- P3-1 至 P3-11 已完成增长基础、长期契约、真实路径验收和 Web-first 转向。
- P3-12-A-D 已完成正式 Web 能力迁移和页面族首批实现。
- 当前 P3-12-E8 负责进入 RC 前的有限产品收口与 Q0 安全阻断。
- P3-12-F 负责候选期可靠性、数据库演进、版本、候选验证与受控试用。

### Phase 4：稳定运营

只有满足以下条件后才进入：

- 正式 Web Release Go 门禁通过并完成可回滚发布。
- 受控试用有真实激活、首次参与、收到回应后的回流、核心任务失败和用户反馈记录。
- 没有未处置的 `P0/P1`，维护与发布节奏能够稳定运行。

## 下一顺位

1. Q0-A、Q0-B 已完成并形成依赖安全、远程门禁与生产暴露面退出证据。
2. 单独确认并实施 Q0-C，随后推进 Q0-D 完成剩余硬门禁。
3. 执行 E8-B 有限矩阵：
   - `/discover` 内容优先；
   - 内容参与、关系复访、聊天回流、举报治理；
   - 公开 Docs `Published + Public`；
   - Console 桌面优先 / 移动低风险边界。
4. Q0 与 E8-B 收口后刷新 `master...dev`，准备集成 PR；不把合并等同于 tag 或生产发布。
5. 进入 P3-12-F，完成 Release Go 门禁与小规模受控试用。
6. 根据真实使用证据判断第三阶段是否收束，不用继续内部 UI 扫描替代用户反馈。

## P3-12-F 门禁分层

### 进入 F

- Q0 全部通过。
- E8-B 有限矩阵通过或形成明确接受后置清单。
- 正式 Web 发布矩阵明确，核心路径没有已知 `P0/P1`。
- 当前集成范围可审阅、可验证。

### F 内 Release Go

- 不可丢失业务写不依赖裸 fire-and-forget。
- 未知异常不返回原始 `ex.Message`。
- 文件访问令牌按本次发布范围选择“完成安全治理”或“退出正式暴露面”。
- PostgreSQL / OpenIddict 升级演练、版本单一真值、候选测试、Gateway smoke 和回滚材料完成。
- 高风险时间语义完成定向治理；全仓时间、strict、大文件和历史卫生债务进入持续治理。

## 并行维护

- 公开 head、动态 sitemap、head snapshot 和生产公开域名配置。
- `validate:baseline / validate:baseline:host / validate:ci / Identity Guard`。
- WebOS `/desktop` 阻断级兼容。
- Flutter analyze / test 与已落地主路径回归。
- Q4 大文件、共享前端边界、全量卫生和文档归档按触达范围持续下降。

## 明确后置

- 创建发布 tag、生产部署和 Phase 4 稳定运营，直到 F Release Go 通过。
- WebOS 新功能、Tauri 分发、完整 Flutter 套件和独立移动 Console。
- 推荐算法、ActivityPub / WebFinger、完整 PublicId / 主键迁移。
- 宠物经济扩展、完整移动商城、完整钱包 / 售后与资产风控平台。
- 完整 PWA、完整 E2E、完整可观测性、Redis 平台化、开放平台和 BFF 深化。
- Q4 全量大文件拆分、历史样式与仓库卫生清零。

## 当前主线入口

- [当前进行中](/planning/current)
- [第三开发阶段](/planning/phase-three-real-usage-contract-governance)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-E 正式产品成熟度与质量硬化](/planning/p3-12-product-maturity-quality-hardening)
- [P3-12-E8-Q 正式发布工程成熟度与安全收口](/planning/p3-12-e8-release-engineering-maturity-security-closure)
- [前端多壳层策略](/frontend/shell-strategy)
- [验证基线说明](/guide/validation-baseline)
- [未来规划](/planning/backlog)

## 文档规则

- 本页只维护总体方向和阶段衔接；今日任务以 `current.md` 为准。
- 历史批次和命令级验证进入 records、changelog 或 archive。
- 产品、工程和发布门禁分别维护在 P3-12-E、E8-Q 与验证 / 部署专题中，不在入口文档重复完整清单。

# P3-12-E4-A 隐私边界与反骚扰首批硬化记录

> 日期：2026-07-05（Asia/Shanghai）
>
> 范围：`Frontend/radish.client`、`Docs/guide`。本批不新增后端 API、权限、数据库结构、屏蔽 / 拉黑能力或申诉工单系统。

## 目标

关闭 E1 中“用户不清楚哪些信息公开、哪些仅本人 / 登录态 / Console 可见”和“反骚扰响应路径分散”的首批发布前建议缺口。让用户在公开承诺页和登录态个人状态页都能看到同一套隐私与安全边界，而不是只依赖分散说明。

## 实现摘要

- 新增 `privacySafetyBoundaryData.ts`，沉淀四类可见范围：公开、本人私域、仅 Console、不可公开。
- 新增 `PrivacySafetyBoundaryPanel`，在 `/legal` 和 `/me` 首页复用同一套边界矩阵和安全响应路径。
- `/legal` 作为公开用户承诺入口展示完整边界矩阵；`/me` 在资产、附件、历史、经验和宠物概览前展示压缩版边界提示。
- i18n 新增 `privacySafety.*` 中英文文案，避免 `/me` 登录态页面只出现中文硬编码。
- 新增 `privacySafetyBoundary.test.ts`，覆盖四类可见边界和四类反骚扰 / 隐私泄露响应路径。
- 更新 `Docs/guide/user-commitments.md`，记录共享组件、边界类别、响应路径和不纳入本批的能力。

## E1 缺口收口

| E1 发现 | 本批结论 | 证据 | 用户影响 | 后续批次 | 验证口径 |
| --- | --- | --- | --- | --- | --- |
| 隐私边界缺少统一用户可见说明 | 发布前建议已完成首批 | `/legal` 与 `/me` 共用 `PrivacySafetyBoundaryPanel`；数据源为 `privacySafetyBoundaryData.ts` | 用户能区分公开主页 / 内容、本人私域账号状态、Console 治理证据和不可公开隐私资料 | `E4-B` 继续输出更完整的公开 / 登录 / 本人 / Console 可见矩阵，覆盖 API 和 Console 用户页 | `radish.client` test / build；Gateway PC / mobile 复核 `/legal` 与 `/me` |
| 反骚扰和隐私泄露响应路径分散 | 发布前建议已完成首批 | 共享面板列出举报原目标、保留时间上下文、私域账号路径核对和紧急风险离开信息流处理 | 用户遇到骚扰、隐私泄露、订单 / 资产异常时知道如何保留证据并选择合适入口 | `E4-B / E5` 继续审计聊天、关注、举报滥用、禁言 / 封禁后体验和错误恢复 | 单测覆盖响应路径；后续旅程级验证覆盖举报和治理回看 |
| 完整屏蔽 / 拉黑、申诉或自动化治理平台缺失 | 后置专题但不阻断发布 | 本批未新增相关 API；现有举报、治理快照、审核、手动治理动作和审计日志继续作为首发替代路径 | 首发能处理核心骚扰和隐私泄露上报；规模化防骚扰体验仍需后续专题补齐 | 反骚扰专题或 `E4` 后续批次，先定义产品范围和后端契约 | E6 发布边界写明首发不承诺完整屏蔽 / 拉黑平台 |
| 个人中心资产、附件、历史等私域状态可能被误解为公开资料 | 发布前建议已完成首批 | `/me` 首页在账号概览前展示同一隐私与安全边界面板 | 用户在查看个人状态时能理解资产、订单、附件、历史和通知队列属于登录态复访数据 | `E4-B` 继续验证公开主页不会外露资产、附件、历史和账号凭证 | Gateway 登录态 `/me` PC / mobile 复核 |

## 不纳入本批

- 不新增屏蔽、拉黑、关注限制、私信限制、申诉工单、退款 / 售后或账号注销 / 数据导出 API。
- 不改变公开主页、资产、订单、附件、聊天、举报或 Console 的数据返回契约。
- 不新增 Console 权限或高风险治理动作；Console 仍通过既有举报审核、手动治理和动作日志承接。
- 不把共享面板当作完整法律文本；正式隐私政策、用户协议和合规材料仍需后续法务专题对齐。

## 验证

- `npm run test --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

Gateway 页面复核：

- 未完成。尝试访问 `https://localhost:5000/legal`、`https://localhost:5000/me`、`http://localhost:3000/legal`、`http://localhost:3100/console/`、`http://localhost:5100/scalar` 和 `http://localhost:5200` 时，本地端口均拒绝连接。
- 本批未把运行态 smoke 写成通过；待前后端重新启动后，使用种子账号复核 `/legal` 与 `/me` 的 PC / mobile 展示。

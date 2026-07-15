# F3 i18n 2026-07-15 日终提交回顾与文档审阅

> 日期：2026-07-15（Asia/Shanghai）
>
> 范围：`904c5b8d^..ab66ab57` 的 7 个当日功能提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- F3-B2 与 F3-C1 至 F3-C7 已连续完成，覆盖 Console 首批业务域、client Docs / 圈子 / 宠物 / 经验 / 萝卜资产域及其实际共享消费者。
- 本批把系统词元、locale formatter、英文复数、人工内容原文和结构化 API 错误落实到真实消费链路，没有采用全仓逐字符串替换，也没有把运营或用户内容自动翻译。
- 高频错误继续沿用统一 `MessageModel + HTTP status + Code + MessageKey + TraceId` 契约，前端通过 `ApiResponseError` 保留控制流与诊断字段。
- 经验、宠物、资产、商品、订单和治理规则均保持原业务语义；今天没有数据库、迁移、依赖、服务启动、浏览器 smoke、PR、tag 或发布操作。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `904c5b8d` | `feat(i18n): 完成首批业务域本地化治理` | F3-B2 收口 Console 用户、内容治理、订单，以及 client Messages / Me 残余。 |
| `bc791dd9` | `feat(i18n): 完成 Console 设置域本地化` | F3-C1 收口个人设置和系统设置；在线词元编辑保持独立后置专题。 |
| `d0e72b2b` | `feat(i18n): 完成商品与文档业务域治理` | F3-C2 / C3 收口商品配置、Docs 作者态、文档治理和实际 API 错误。 |
| `5d7ae5e0` | `feat(i18n): 完成圈子业务域治理` | F3-C4 收口圈子系统词元、locale 展示与共享 UserFollow 错误契约。 |
| `eef594b9` | `feat(i18n): 完成宠物业务域治理` | F3-C5 收口宠物派生词元、次数 / 冷却复数与 Pet 错误契约。 |
| `cf76c647` | `feat(i18n): 统一 client 经验域契约` | F3-C6 统一经验详情、Me、桌面与共享经验条的稳定词元、long 和结构化错误。 |
| `ab66ab57` | `feat(i18n): 统一 client 萝卜资产域契约` | F3-C7 统一萝卜坑及共享消费者的金额、状态、图表、口令和 Coin 错误契约。 |

## 代码边界回顾

### Console 与 Docs

- 用户、内容治理、订单、设置、商品和文档治理已拆入独立中英文业务资源。
- 订单状态、商品能力、文档状态和系统设置控制流只认稳定字段或 key；运营名称、说明、正文和人工备注保持原文。
- 商品、订单、治理与 Wiki 高频失败具备稳定 HTTP status、`Code / MessageKey` 和 API 双语资源。

### client 社区与激励域

- 圈子、宠物、经验和萝卜资产的系统派生内容都由稳定字段映射宿主词元，未知扩展显示稳定原值。
- 日期、时区、数字、金额、百分比、分页、图表和 `0 / 1 / 2` 英文复数进入 formatter 与测试门禁。
- 经验和萝卜 long 字段在 client 使用字符串与整数安全计算；共享 UI 只接收 labels / formatter，不持有宿主 i18n。
- 宠物名、等级名称、交易备注、冻结原因、用户名、IP 和 User-Agent 等用户、配置或审计内容继续保持原文。

### API 错误契约

- client 和 Console API helper 使用 `createApiResponseError`，调用宿主提供本地化 fallback。
- Controller 高频失败同步真实 HTTP status，并由中英文 `.resx` 提供服务端兜底。
- 页面控制流不匹配 `MessageInfo`、`vo*Display` 或中文错误句子；旧展示字段只在兼容类型层保留。

## 最终验证

- `radish.client`：`370` 项测试、type-check、lint、production build 通过。
- `radish.console`：`47` 项测试和 Baseline Quick 中的 type-check 通过；当日相关批次 production build、lint 均已通过。
- `@radish/ui`：`8` 项测试和 type-check 通过。
- `@radish/http`：`11` 项测试和 type-check 通过。
- Coin / PaymentPassword 针对性后端测试：`44` 项通过。
- 完整 `Radish.Api.Tests`：`674` 项通过，`11` 项环境用例按配置跳过。
- `dotnet build Radish.slnx -c Debug`：`0 warning / 0 error`。
- `validate:baseline:quick`、changed / staged repo hygiene 与 `git diff --check` 通过。
- client production build 仅保留既有的大 chunk 提示；今天未把该历史性能项扩为无关重构。

## 文档审阅与修正

- `Docs/planning/current.md` 已补齐 7 个功能提交、最终验证摘要和可直接执行的 2026-07-16 F3-C8 事项。
- `Docs/frontend/i18n-completion-governance.md` 的当前实施基线更新到 `ab66ab57`；F3-C1 至 C7 的完成边界与下一顺位保持一致。
- `Docs/guide/experience-level-system.md` 已补正式 `/me/experience`、WebOS 兼容入口、共享 `ExperienceBar`、稳定 `voExpType` 与人工内容原文边界。
- `Docs/guide/experience-level-frontend.md` 原有未落地组件路径、Recharts 示例和固定中文示例已替换为当前 `ExperienceDetailApp`、`experiencePresentation`、共享 UI 与测试门禁。
- `Docs/guide/experience-level-backend.md` 原 `GetExpTransactions / GetDailyStats / Admin/AdjustExperience` 等旧路由已改为现行 client / Console 接口，并补结构化错误表。
- `Docs/guide/experience-level-roadmap.md` 已把现行正式入口、`experienceApi` 归属、共享图表和 F3-C6 契约补入 v1.7；旧版本记录作为当时事实保留。
- 圈子、宠物、商品与萝卜坑专题已经随对应功能提交更新，审阅后无需重复追加历史流水。
- `Docs/frontend/private-web-workflows-design.md` 的 `/me`、`/me/experience`、资产错误恢复和 `/notifications` 边界与当前代码一致，无需修改。
- `Docs/planning/backlog.md` 仍把在线本地化资源管理作为独立后置专题，没有混入 `SystemConfig`，口径保持有效。

## 明日事项

1. 推进 F3-C8：先建立低频设置、公开用户承诺、页面 head、共享反馈与上传组件的正式路由和真实消费者矩阵。
2. 划清系统词元、运营长文、用户内容和文件元数据边界，再成组补双语资源、locale formatter、英文长文本与 plural 规则。
3. 统一共享反馈 / 上传组件的宿主 labels、进度和错误注入，治理实际消费 API 的高频结构化错误，不让共享 UI 持有 client i18n。
4. 补资源、稳定词元、长文本、共享组件和 API 契约测试，完成 client 静态验证、Baseline Quick、仓库卫生与差异复核。
5. F3-C8 完成后准备 F3-C9 Console 剩余管理域；PC / mobile 真实语言切换和 OIDC 往返统一留到 F3-D 专题验收。

## 当前不做

- 不在日终文档批次修改业务代码、接口实现、数据库、迁移或依赖。
- 不启动服务或执行浏览器 smoke。
- 不扩展 Console 经验 / 萝卜管理，不返工已完成的公开排行榜和已收口业务域。
- 不把在线词元编辑塞入 `SystemConfig`，不启动大而全的翻译平台。

# 萝卜坑应用设计方案

> 版本：v1.1 | 最后更新：2026-07-15 | 状态：已实现，持续维护

## 拆分说明

萝卜坑应用文档按职责拆分；本文只描述产品边界、入口和跨层契约，业务规则、前端实现与后端实现分别查看对应专题。

## 文档导航

- [核心概念](/guide/radish-pit-core-concepts)
- [功能模块](/guide/radish-pit-game-mechanics)
- [后端设计](/guide/radish-pit-backend)
- [前端设计](/guide/radish-pit-frontend)
- [实施计划](/guide/radish-pit-roadmap)

## 1. 系统定位

萝卜坑是 Radish 的萝卜资产管理应用，承接账户总览、用户间转移、交易查询、统计分析和支付口令安全设置。萝卜币只用于站内激励和消费，不提供法币兑换、提现、第三方支付或投资理财能力。

正式 Web 资产摘要位于 `/me/assets`；`/desktop` 中的萝卜坑窗口是历史兼容入口，继续复用同一 API 和展示契约。交易通知由正式 `/notifications` 页面统一承接，萝卜坑不维护独立通知中心或模拟通知数据。

## 2. 当前功能边界

已实现：

- 账户可用余额、冻结余额和累计收支摘要；
- 用户间萝卜转移、支付口令验证和幂等重放；
- 交易列表、筛选、分页、详情和 CSV 导出；
- 月、季度、年趋势及收支分类统计；
- 支付口令设置、修改、锁定状态和安全日志；
- `/me`、Profile 和桌面余额等真实共享消费面。

明确不在本应用内实现：

- 法币充值、提现、兑换、投资或外部支付；
- 独立通知中心；
- Console 胡萝卜管理扩展；
- 前端自行定义余额、转移、排行或奖励规则。

## 3. 分层边界

```text
radish.client
  RadishPit / MeAssets / Profile / Desktop balance
        │
        ├── coinApi ───────────────┐
        └── paymentPasswordApi ────┤
                                   ▼
Radish.Api
  CoinController / PaymentPasswordController
                                   │
                                   ▼
Radish.Service
  CoinService / PaymentPasswordService
                                   │
                                   ▼
Repository + Main / Log data stores
```

- Controller 负责认证、HTTP 语义、双语安全消息和稳定错误契约，不承载资产规则。
- Service 负责余额、流水、并发、幂等和支付口令业务语义。
- client API helper 统一使用 `@radish/http`，失败抛出 `ApiResponseError`。
- 页面只按稳定字段控制状态；服务端展示字段仅保留兼容，不参与本地化或业务判断。

## 4. 跨层稳定契约

### 4.1 系统词元

以下字段是稳定系统词元：

- `voTransactionType`：交易类型；
- `voStatus`：交易状态；
- `voCategory`：统计分类，使用 `IN_* / OUT_*`；
- `voType / voResult`：支付口令安全日志类型与结果。

client 使用宿主中英文资源解析已知词元，未知词元直接显示稳定原值。`voTransactionTypeDisplay`、`voStatusDisplay`、`voAction` 等历史展示字段不得参与控制或本地化。

用户名、交易备注、业务标识、IP、User-Agent 等人工或外部内容保持原文，不自行翻译。

### 4.2 数值与时间

- 后端 `long` ID 和萝卜数量在 client 按字符串接收，避免 JavaScript 安全整数精度损失。
- 金额比较、加减和胡萝卜 / 白萝卜换算使用整数安全运算；图表只在绘制边界显式投影为 `number`。
- 数字、百分比、日期、相对时间和图表日期使用当前 locale；业务时刻仍遵循既有 UTC / 展示时区契约。
- 英文数量文案使用 i18next `_one / _other` 规则。

### 4.3 错误响应

萝卜与支付口令的高频失败统一返回：

```text
HTTP status + Code + MessageKey + MessageInfo + TraceId
```

- `400`：输入、自转移、金额或口令格式错误；
- `404`：交易流水不存在；
- `409`：余额、账户、并发、幂等、口令配置或旧口令升级冲突；
- `429`：支付口令暂时锁定。

`Code` 和 HTTP status 决定控制流，`MessageKey` 驱动 client 本地化，`MessageInfo` 仅作为服务端安全兜底。页面不得匹配中英文消息文本。

## 5. 核心流程

### 5.1 用户间转移

1. client 校验接收方、正整数金额和六位支付口令；
2. 服务端拒绝自转移并验证支付口令状态；
3. 幂等服务按请求键和摘要判定开始、重放、处理中或冲突；
4. `CoinService` 在事务与并发控制下完成双方余额和流水写入；
5. 成功返回字符串流水号，稳定终态失败可按原 `Code / MessageKey` 重放；
6. 后续通知由正式通知系统承接。

### 5.2 支付口令

1. 新口令必须为六位数字且不能六位完全相同；
2. 当前版本使用 Argon2id 编码；可识别的 SHA-256 v1 在成功验证后自动升级，无法安全识别版本的更旧记录要求用户重置；
3. 连续验证失败按既有次数与锁定时长规则处理；
4. 设置、修改、验证和锁定结果使用稳定错误码；
5. 安全日志通过稳定 `voType / voResult` 展示，兼容动作文案不参与本地化。

## 相关文档

- [萝卜币系统](/guide/radish-coin-system)
- [用户认证系统](/guide/authentication)
- [通知系统](/guide/notification-realtime)
- [F3 i18n 完成度治理实施说明](/frontend/i18n-completion-governance)

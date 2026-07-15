# 萝卜坑应用前端设计

> 版本：v1.1 | 最后更新：2026-07-15 | 状态：已实现，持续维护

本文描述萝卜资产域在 `radish.client` 的当前实现边界。业务规则由后端 Service 负责，前端只承担交互、展示、本地化和结构化错误消费。

## 1. 入口与页面职责

### 1.1 正式与兼容入口

- `/me/assets`：正式 Web 资产摘要与最近流水。
- `/me`：资产摘要卡片。
- Profile：`CoinWallet`、`CoinTransactionList` 和用户资料余额摘要。
- `/desktop`：历史 WebOS 兼容入口，萝卜坑窗口和桌面余额继续复用同一业务契约。
- `/notifications`：正式交易通知入口；萝卜坑内部不维护通知标签、模拟数据或第二套通知状态。

### 1.2 萝卜坑窗口

`RadishPitApp` 保留五个业务标签：

1. `overview`：账户总览、累计统计、快捷入口和最近流水；
2. `transfer`：接收方搜索、金额、备注、支付口令、确认与结果；
3. `history`：筛选、分页、详情和 CSV 导出；
4. `statistics`：时间范围、趋势、分类和摘要；
5. `security`：口令状态、设置 / 修改、安全日志和建议。

## 2. 代码边界

```text
src/api/
  coin.ts                     # 萝卜 API、DTO 和结构化错误
  paymentPassword.ts          # 支付口令 API、DTO 和结构化错误
src/coin/
  coinPresentation.ts         # 稳定词元、long 安全运算和 locale formatter
src/apps/radish-pit/
  RadishPitApp.tsx
  hooks/index.ts              # 查询与动作状态
  types/index.ts              # 页面交互类型
  components/
    AccountOverview/
    Transfer/
    TransactionHistory/
    Statistics/
    SecuritySettings/
src/me/
  MeApp.tsx
  MeAssetsPage.tsx
src/apps/profile/components/
  CoinWallet.tsx
  CoinTransactionList.tsx
  UserInfoCard.tsx
src/desktop/components/
  CoinBalance.tsx
```

- API helper 统一使用 `@radish/http`，每个调用由宿主传入 `t` 作为安全 fallback。
- `coinPresentation.ts` 是跨萝卜页面的唯一展示解析层，不持有 React 或宿主语言状态。
- 页面组件不读取服务端固定中文展示字段。
- `@radish/ui` 图表通过 `valueFormatter` 接收宿主金额展示，不反向依赖 client i18n。

## 3. API 契约

### 3.1 coinApi

当前真实消费方法：

```ts
coinApi.getBalance(t)
coinApi.getTransactions(pageIndex, pageSize, transactionType, status, t)
coinApi.getTransactionByNo(transactionNo, t)
coinApi.transfer(request, t)
coinApi.getStatistics(timeRange, t)
```

关键 DTO 规则：

- `CoinAmount` 和 `LongId` 是十进制整数字符串；
- `voTransactionNo` 是字符串；
- `voTransactionType / voStatus / voCategory` 是稳定系统词元；
- `voRemark`、参与方名称和业务字段是原文内容；
- `vo*Display` 只保留响应兼容，不进入页面控制和本地化。

### 3.2 paymentPasswordApi

当前真实消费方法：

```ts
paymentPasswordApi.getPaymentPasswordStatus(t)
paymentPasswordApi.setPaymentPassword(request, t)
paymentPasswordApi.changePaymentPassword(request, t)
paymentPasswordApi.getSecurityLogs(pageIndex, pageSize, t)
```

验证、强度和安全建议 helper 保持同一结构化错误签名。安全日志只按 `voType / voResult` 解析；`voAction`、强度 display 和日期 display 不参与展示。

### 3.3 失败处理

API helper 在以下情况统一抛出 `ApiResponseError`：

- HTTP 或 `MessageModel.IsSuccess` 失败；
- 必需响应数据缺失；
- 服务端返回稳定 `Code / MessageKey`；
- 网络、非 JSON 或通用错误由 `@radish/http` 生成安全 fallback。

页面可以展示 `error.message`，但控制流必须使用 `code / httpStatus / statusCode`。旧支付口令升级只识别 `PAYMENT_PASSCODE_UPGRADE_REQUIRED`，不得匹配中英文错误消息。

## 4. 稳定词元本地化

### 4.1 交易和统计

```ts
formatTransactionType(transaction.voTransactionType, t)
formatTransactionStatus(transaction.voStatus, t)
formatStatisticsCategory(item.voCategory, t)
```

已知词元解析到 `commerce` 域下的 `pit.*` 中英文资源；未知词元显示 `trim()` 后的稳定原值。筛选值、状态样式、方向判断和图标同样只使用稳定字段。

统计分类由服务端返回 `IN_* / OUT_*`。前端不得重新根据中文分类名归组，也不得从翻译结果判断收入或支出。

### 4.2 参与方与人工内容

- 系统参与方由 `voFromUserId / voToUserId === null` 判断并显示宿主词元。
- 当前用户由稳定用户 ID 判断并显示“我 / Me”。
- 用户名、备注、业务标识、IP 和 User-Agent 保持原文。
- 未知且非空的系统类型显示稳定原值，不编造翻译。

### 4.3 支付口令状态

- 口令强度使用数值等级解析 `pit.security.strength.*`。
- 安全日志类型使用 `voType`，结果使用 `voResult`。
- 锁定剩余分钟、失败次数和日志数量使用 i18next plural。
- 服务端 `voSecurityStatus / voSecuritySuggestions` 的历史中文内容不作为当前页面展示来源。

## 5. 金额、日期与图表

### 5.1 long 安全金额

`coinPresentation.ts` 提供：

```ts
compareCoinValues
addCoinValues
subtractCoinValues
divideCoinValue
absoluteCoinValue
formatCoinNumber
formatCoinAmount
```

实现先将十进制整数字符串转为 `BigInt`。白萝卜显示使用整数商、三位余数和当前 locale 的小数分隔符，因此不会因 `Number` 丢失长整数精度。

转移表单的业务输入当前仍是受控正整数 `number`，提交前同时经过页面即时校验和后端权威校验；余额等后端 `long` 字段不得转换为 `number` 后参与完整精度运算。

### 5.2 日期与时区

- 流水、日志和结果时间使用当前语言的 locale。
- 展示时刻继续使用用户 / 浏览器展示时区。
- 趋势接口的 `yyyy-MM-dd` 日期键按 UTC 中午构造，仅用于避免日历标签跨日，不改变服务端统计日期。
- 近七日可使用本地化相对时间，较早数据使用完整 locale 日期时间。

### 5.3 图表

服务端金额保持字符串直到图表适配边界。`toCoinChartNumber` 只服务于图形库坐标；tooltip、标签和摘要仍从原始值或宿主 `valueFormatter` 生成 locale 文本。

## 6. 页面交互要点

### 6.1 转移

- 接收方搜索保留稳定字符串用户 ID；
- 幂等键使用 `coin-transfer:${crypto.randomUUID()}`；
- 提交期间保持同一请求键，返回表单或开始新转移时才生成新键；
- 成功结果展示完整 `voTransactionNo`；
- 旧口令升级结果引导进入安全设置，其他失败保留结构化错误提示。

### 6.2 流水

- 筛选条件使用稳定交易类型和状态；
- 页码、总数和当前范围使用 locale 数字与英文单复数；
- 详情与 CSV 使用完整流水号、原始备注和 locale 日期；
- CSV 的收入 / 支出符号由当前用户与参与方 ID 关系计算。

### 6.3 安全日志

- 服务端分页，页码变化重新请求；
- 当前页可以按成功 / 失败结果过滤并显示数量；
- 操作名称只使用稳定类型映射；
- IP 和 User-Agent 是审计原文，允许换行或截断，不翻译。

## 7. 资源与测试门禁

萝卜域词元位于：

```text
src/locales/zh/commerce.ts
src/locales/en/commerce.ts
```

必须保持：

- 中英文 key parity 和跨域无重复键；
- 资源单文件不超过治理上限；
- `0 / 1 / 2` 英文数量规则；
- 交易类型、状态、统计分类和安全日志未知词元原值回退；
- 超出 JavaScript 安全整数的金额比较、加减、换算与格式化测试；
- API 使用 `createApiResponseError` 且所有真实消费者不读取 `vo*Display / voAction`；
- Radish Pit 不重新出现模拟通知中心；
- client test、type-check、lint 和 production build 通过。

后端同步验证 Coin / PaymentPassword Service、Controller、错误资源 parity、完整 `Radish.Api.Tests` 和解决方案构建。

## 相关文档

- [萝卜坑应用总体设计](/guide/radish-pit-system)
- [萝卜坑核心概念](/guide/radish-pit-core-concepts)
- [萝卜坑后端设计](/guide/radish-pit-backend)
- [F3 i18n 完成度治理实施说明](/frontend/i18n-completion-governance)

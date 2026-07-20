# 通知系统历史方案索引

> 本目录保留 2026-01 至 2026-06 期间形成的通知实现、实时、API 和前端集成方案原文。
>
> 这些资料用于追溯历史决策和代码演进，不是当前实现契约。当前产品说明查看 [通知中心使用说明](/guide/notification-center)，F4-B 设计与实施边界查看 [通知中心深化与通知治理](/features/notification-center-deepening)。

## 历史原文

- [通知系统实现细节（历史索引）](/records/notification-legacy/notification-implementation)
- [通知系统实时推送方案（历史）](/records/notification-legacy/notification-realtime)
- [通知系统 API 方案（历史）](/records/notification-legacy/notification-api)
- [通知系统前端集成指南（历史索引）](/records/notification-legacy/notification-frontend)

## 使用约束

- 不能依据历史示例判断当前 Controller 路由、HTTP 方法、目录或模型字段。
- 不能把历史 `P0 / P1 / P2` 状态直接写回当前规划。
- 需要复用其中方案时，先与当前代码、`Docs/planning/current.md` 和 F4-B 专题交叉核对。

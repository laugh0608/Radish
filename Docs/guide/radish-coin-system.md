# 萝卜币系统设计方案

## 拆分说明

原 `radish-coin-system.md` 内容较长，为便于阅读已拆分为多篇文档；章节编号保持不变。

## 文档导航

- [2. 核心概念](/guide/radish-coin-core-concepts)
- [3-5. 获取/流转/消费机制](/guide/radish-coin-mechanisms)
- [6-8. 精度/对账/数据库设计](/guide/radish-coin-finance)
- [9-11. 安全性/未来扩展/技术实现要点](/guide/radish-coin-security-tech)
- [12-15. 开发计划/风险合规/落地清单](/guide/radish-coin-roadmap)
- [16. 实施落地待完善（后端与接口）](/guide/radish-coin-implementation-review-backend)
- [16. 实施落地待完善（前端与性能）](/guide/radish-coin-implementation-review-frontend)
- [17-18. 参考资料与 M6 总结](/guide/radish-coin-references-summary)

---

## 1. 系统概述

萝卜币（Radish Coin）是 Radish 社区的虚拟积分系统，采用类似货币的设计理念，支持用户间流转、消费和奖励机制。系统设计遵循严格的财务规范，确保每一笔交易可追溯、可对账。

**核心设计原则**：
- **精度优先**：避免浮点运算，使用整数存储，确保分毫不差
- **可追溯性**：每笔交易完整记录，支持审计和对账
- **安全性**：防刷机制、余额锁定、并发控制
- **合规性**：避免使用"货币"等敏感词汇，定位为社区积分

---

## 1.1 当前前端承接状态

- WebOS 和 Console 继续承担萝卜币消费、治理、人工追查与运营管理链路。
- Flutter 登录态我的页提供“查看胡萝卜资产”只读入口，展示可用余额、冻结余额、累计获得、累计支出和最近流水。
- Flutter 最近流水用于用户移动端自查，不替代财务对账、审计后台或运营治理。
- Flutter 当前不开放转账、打赏、调账、支付、冻结治理或管理员复核。

---

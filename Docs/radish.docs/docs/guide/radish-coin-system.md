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


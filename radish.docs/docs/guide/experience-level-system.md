# 经验值与等级系统设计方案

## 拆分说明

原 `experience-level-system.md` 内容较长，为便于阅读已拆分为多篇文档；章节编号保持不变。

## 文档导航

- [2. 核心概念](/guide/experience-level-core-concepts)
- [3. 经验值获取机制](/guide/experience-level-earning)
- [4-6. 后端设计（数据库/业务逻辑/API）](/guide/experience-level-backend)
- [7. 前端展示设计](/guide/experience-level-frontend)
- [8-11. 性能、安全、运维与测试](/guide/experience-level-ops)
- [12-14. 实施计划、待确认问题与参考资料](/guide/experience-level-roadmap)

---

## 1. 系统概述

经验值系统是 Radish 社区的用户成长激励体系,通过社区互动积累经验值,提升用户等级,并获得对应的身份标识和特权。系统设计遵循"易上手、难精通"的成长曲线,让新用户快速获得成就感,同时为活跃用户提供长期目标。

**核心设计原则**:
- **公平性**:经验值获取透明,规则明确,防刷机制健全
- **成长性**:等级曲线合理,既有短期激励,也有长期目标
- **可扩展性**:等级配置化,支持动态调整和扩展
- **文化认同**:修仙体系等级昵称,增强社区文化氛围

---

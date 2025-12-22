# 2025年11月 - 第2周（11月8日-11月10日）

> 项目重建与基础架构搭建

## 2025.11.10

- feat(aop): 引入 `ServiceAop` + `AopLogInfo`，结合 Autofac 动态代理为泛型服务开启接口拦截，输出统一的请求/响应日志。
- docs(spec): 在 DevelopmentSpecifications 中新增 "AOP 与日志" 章节，说明拦截器、日志模型及扩展方式。
- chore(di): `AutofacModuleRegister` 启用服务/仓储拦截，并确保与 AutoMapperSetup、扩展模块协同；后续若添加新拦截器按此模式接入。

## 2025.11.9

- refactor(di): `AutofacPropertyModuleReg` 改为由宿主传入 `Assembly`，避免引用 `Program` 造成循环依赖，同时扩展层负责注册 Service/Repository 泛型实现。
- chore(project): 调整 Radish.Service 与 Radish.Extension 的引用方向，确保 IoC/扩展层仅被宿主引用，其余业务项目不再依赖扩展层。
- docs(spec): 在 DevelopmentSpecifications 中说明新的分层依赖约束与模块传参约定，方便后续贡献者遵循。

## 2025.11.8

- 重新创建项目，完全舍弃之前的代码，包括 ABP 框架与 MongoDB
- 重新使用 .NET 10 + React + Entity Framework Core/SqlSugar + PostgreSQL 技术栈
- 重新设计项目架构与模块划分

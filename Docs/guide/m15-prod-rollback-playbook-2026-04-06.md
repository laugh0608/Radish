# M15 生产环境最小回滚预案（2026-04-06）

> 本页用于沉淀 `M15` 第二批的生产环境最小回滚预案。
>
> 关联入口：
>
> - [M15 最小交付与部署基线](/guide/m15-delivery-baseline)
> - [部署与容器指南](/deployment/guide)
> - [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)

## 当前定位

本预案用于回答四个最小问题：

- 生产环境在什么情况下应该进入回滚判断
- 生产环境优先回滚到哪个版本
- 回滚前谁来确认哪些事实
- 回滚后最少要复核什么

截至 `2026-04-06`，本页仍然只是预案，不代表生产环境已经做过真实回滚演练。

## 当前已知稳定事实

- 当前生产已知稳定版本：`v26.3.2-release`
- 当前生产部署形态：单套 Docker 部署 + 1Panel 默认 HTTPS 反向代理
- 当前仓库内未沉淀出第二个已知可用的 `v*-release` 生产回滚锚点
- 因此现阶段不应为了补记录而在生产环境主动做一次真实回滚

## 回滚触发条件

当前建议至少在以下情况进入生产回滚判断：

- 登录 / 回调 / 登出主链路阻塞，且短窗口内无法通过配置修正恢复
- `Gateway / Api / Auth` 运行态健康检查失败，且不是单点瞬时抖动
- `RADISH_PUBLIC_URL`、Issuer、证书或反代头异常，导致外部入口不可用
- 当前发布引入阻塞主线的 `P0 / P1` 问题

如果问题能够在短窗口内通过修配置、修反代、修证书路径恢复，优先先修配置再复核，不要机械地把所有问题都升级成生产回滚。

## 回滚目标选择规则

生产环境当前不使用“猜测上一版”作为回滚目标。默认规则必须同时满足：

1. 已在真实环境完成过部署
2. 登录 / 回调 / 核心页面 / 最小运行态检查曾真实通过
3. 当前镜像仍可从 `GHCR` 拉取
4. 对应 tag 与部署记录、复核记录可追溯

在当前仓库事实下：

- `v26.3.2-release` 是当前已知稳定版本
- 若未来产生新的稳定发布版本，例如 `v26.3.2-r1-release` 或 `v26.3.3-release`
  且已完成真实生产部署复核，则该版本与 `v26.3.2-release` 之间才形成真正可选的生产回滚锚点

## 回滚前最小确认项

进入生产回滚前，至少先确认以下事实：

1. 当前问题是否已经超出“短窗口内修配置可恢复”的范围
2. 当前回滚目标 tag 是否仍然存在且可拉取
3. `Deploy/.env.prod` 中五个 `RADISH_*_IMAGE` 是否准备统一切回同一目标版本
4. 当前 `RADISH_PUBLIC_URL`、Auth 证书挂载目录、反代配置是否保持不变
5. 当前是否有人明确记录“回滚前版本 / 回滚目标版本 / 触发原因 / 确认时间”

## 生产最小回滚步骤

1. 在记录中写明：
   - 当前版本
   - 回滚目标版本
   - 触发原因
   - 确认时间
2. 将 `Deploy/.env.prod` 中以下变量统一改回目标 tag：
   - `RADISH_DBMIGRATE_IMAGE`
   - `RADISH_FRONTEND_IMAGE`
   - `RADISH_API_IMAGE`
   - `RADISH_AUTH_IMAGE`
   - `RADISH_GATEWAY_IMAGE`
3. 先执行静态展开确认：

```bash
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml config
```

4. 再执行：

```bash
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml pull
docker compose --env-file Deploy/.env.prod -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml up -d
```

5. 立即按 `M14` 顺序补最小复核与记录

## 回滚后最小复核

回滚后至少确认：

- `npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md` 通过
- `npm run collect:m14-host-record` 已补记录
- `/health` 可访问
- `/` 可打开 WebOS
- `/console/` 可打开 Console
- 登录 / 回调 / 登出主链路恢复正常
- `Gateway / Api / Auth` 日志中没有新的证书、Issuer、反代头或容器启动阻塞

## 当前边界

本预案当前明确不代表：

- 生产环境已做过真实回滚演练
- 已具备自动回滚能力
- 已具备蓝绿 / 金丝雀 / 多集群切流能力
- 已把生产发布收口为平台化流程

## 当前结论

截至 `2026-04-06`，`M15` 第二批当前达到的状态是：

- 测试环境最小回滚流程已真实验证
- 生产环境最小回滚判断条件、目标选择规则、执行步骤与复核要求已形成书面预案
- 后续只有在出现第二个已知可用 `v*-release` 锚点后，才适合继续评估是否补生产回滚演练

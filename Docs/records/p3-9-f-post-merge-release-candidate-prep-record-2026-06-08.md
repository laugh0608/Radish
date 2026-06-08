# P3-9-F 合并后发布候选准备记录（2026-06-08）

> 本记录承接 `P3-9-F 合并后发布候选落地准备`。
>
> 本页记录 PR #54 合并后的发布候选准备事实，以及后续“跳过发布、继续开发”的阶段决策。

## 当前结论

`P3-9` 首批真实使用主路径已经完成 PR 前扩大验证、人工复核和 `dev -> master` 合并。PR #54 已合并到 `master`，合并提交为 `00540521`，合并前修复提交为 `46db8986`。

GitHub `Repo Quality` 已重新通过：

- `Repo Hygiene`：通过。
- `Frontend Lint`：通过。
- `Baseline Quick`：通过。
- `Identity Guard`：通过。

当前工程判断：P3-9 可以从“PR 前合并准备”收口为“已合并并保留维护回拉”。后续阶段决策为：本轮跳过发布，不创建 tag、不等待镜像、不进入 M15 测试 / 生产部署流程，继续进入 P3-10 开发任务选择。

合并后本地验证已完成：`validate:ci` 通过；因暂存文档命中默认执行面 / 门禁资产，已补 `validate:identity` 并通过。

## 合并范围

| 范围 | 状态 |
| --- | --- |
| 访客公开访问 | 发布候选主路径已完成人工抽查和自动化守护 |
| Flutter 登录移动用户 | 商城 / 钱包缺口已完成修复复测，Flutter 定向验证已通过 |
| Console 管理员排障 | 用户、订单、商品、流水、权限授权和治理路径已完成首批整备 |
| CI 修复 | `RoleForm.tsx` 未使用 `RoleVo` 导入已移除，`Frontend Lint` 已通过 |

## 合并后待执行项

| 项目 | 当前状态 | 下一步 |
| --- | --- | --- |
| 本地合并后验证 | 已执行 | `npm run validate:ci` 通过；`npm run validate:identity` 通过 |
| 宿主发布前验证 | 跳过 | 本轮不进入真实部署准备 |
| Git tag | 跳过 | 本轮不创建测试发布或正式发布 tag |
| Docker 镜像 | 跳过 | 无 tag，因此不等待本轮发布镜像 |
| 测试部署 | 跳过 | 本轮不进入测试部署 |
| 生产部署 | 跳过 | 本轮不进入生产部署，也不宣布进入稳定运营 |
| 下一阶段 | 进入 P3-10 | 评估跨端信息架构、功能 / UI 差距和下一批开发任务 |

## 后续若重新发布时的 smoke 清单

后续若重新决定发布或部署，部署后优先按 Gateway 入口复核：

- `https://localhost:5000/`
- `https://localhost:5000/discover`
- `https://localhost:5000/console/`
- forum / docs / shop 三类公开详情 head smoke。
- Gateway / API / Auth `/health` 与必要 `/healthz` 摘要。

若复核生产域名，公开域名以 `https://radishx.com` 为准，确认 `GatewayService:PublicUrl` / `RADISH_PUBLIC_URL` 与 canonical / OpenGraph 输出一致。

## 本地验证记录

执行目录：仓库根目录 `/Users/luobo/Code/Radish`

```bash
npm run check:repo-hygiene:changed
git diff --check
npm run validate:ci
npm run check:repo-hygiene:staged
npm run lint:staged
npm run check:identity-impact:staged
npm run validate:identity
```

结果：

- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check` / `git diff --cached --check`：通过。
- `npm run validate:ci`：通过，包含 Baseline Quick、Console 权限扫描、Repo Quality contract、自校验和身份 Claim 扫描。
- `npm run check:repo-hygiene:staged`：通过，检查 6 个暂存文件。
- `npm run lint:staged`：通过，本次没有需要 lint 的前端脚本文件。
- `npm run check:identity-impact:staged`：命中 `Docs/development-plan.md` 与 `Docs/planning/current.md`，原因类别为默认执行面文档 / 门禁资产。
- `npm run validate:identity`：通过，包含运行时 Claim 扫描、协议输出守卫、外部 LongId 字符串安全扫描和 14 个身份语义后端定向测试。

## 当前不做

- 不直接切 Phase 4 稳定运营。
- 不启动完整移动商城、完整通知中心、完整资产中心或完整创作器。
- 不启动 Flutter 底部导航重组实现；后续只先评估 `发现 / 消息 / 更多 / 我的` 信息架构。
- 不恢复 P3-8-D 购买 / 订单 / 背包、权限授权或 ID 守护的无限期深挖。

## 记录边界

本记录是 P3-9 合并后决策记录。若后续重新创建 tag、产出镜像、测试部署、生产部署或执行回滚动作，应另按 [M15 发布记录模板](/records/m15-release-record-template) 与 `M14` 部署后复核口径补真实记录。

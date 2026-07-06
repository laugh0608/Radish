# P3-12-E4-B 隐私安全边界与治理证据链复核记录

> 日期：2026-07-06（Asia/Shanghai）
>
> 范围：`Frontend/radish.client`、`Docs/guide`、`Docs/planning`。本批不新增后端 API、权限、数据库结构、屏蔽 / 拉黑、申诉、账号注销、退款 / 售后或独立移动 Console。

## 目标

承接 `P3-12-E4-A` 的用户可见隐私边界面板，完成当天未闭合的 Gateway PC / mobile 运行态复核，并继续审计以下 E4-B 面：

- 公开 / 登录 / 本人 / Console 可见边界。
- 聊天、关注、举报、通知目标缺失和治理证据链的可解释性。
- 现有前端和说明文档中能当天成组修正的用户可见缺口。

## Gateway 运行态复核

用户已确认前后端于 2026-07-06 当日启动。本轮使用浏览器插件访问 Gateway，并用种子管理员账号 `admin@radishx.com / admin123456` 登录。

| 路径 | 视口 | 结果 |
| --- | --- | --- |
| `/legal` | `1920x1080` | 页面标题、用户承诺、公开 / 私域 / Console / 不公开边界、安全响应路径和虚拟商品边界均可见；未发现横向溢出和 console warning / error。 |
| `/legal` | `390x844` | 移动首屏、公开导航和底部移动导航可见；隐私安全内容可继续滚动查看；未发现横向溢出和 console warning / error。 |
| `/me` | `1920x1080` | 登录后进入我的状态页；账号概览、公开主页链接、私域入口和共享隐私安全面板均可见；未发现横向溢出和 console warning / error。 |
| `/me` | `390x844` | 登录态移动首屏可见账号概览、公开主页、私域入口和共享隐私安全面板；底部移动导航不遮挡首屏主要动作；未发现横向溢出和 console warning / error。 |

## 审计结论

### 可见边界

- `/legal` 与 `/me` 共享 `PrivacySafetyBoundaryPanel`，数据源为 `privacySafetyBoundaryData.ts`，四类边界为 `public / private / console / restricted`。
- 公开页不把订单、资产、背包、附件、最近访问、草稿或账号凭证描述成公开资料。
- `/me` 将资产、订单、背包、经验、宠物、附件、历史和通知明确放在登录态本人私域。
- Console 边界明确用于举报快照、目标上下文、审核结果、手动治理动作、订单 / 权益排障和审计复核。

### 反骚扰与举报

- 用户侧举报入口覆盖帖子、评论、轻回应、聊天消息和商品。
- 后端 `ContentModerationService` 在举报创建时保存目标快照，并在审核队列中返回目标类型、目标用户、快照标题 / 摘要、导航状态和处理记录。
- 本轮将举报弹窗说明改为强调“保留原始目标上下文、提交后基于保存快照复核、不二次传播隐私或有害内容”，降低用户把隐私截图扩散到公开区的风险。

### 通知目标缺失

- `notificationActionQueue` 已将通知归入评论、回答、消息、关注、治理、订单、Docs、宠物、经验、点赞和系统等任务域。
- 有明确目标时，通知回到帖子、聊天、关注对象、订单或 Docs 等 Web 路径。
- 治理类、系统类或原始目标缺失时，通知留在通知中心查看上下文、证据、处理状态或诊断信息，不伪造成功跳转。
- 本轮收敛中英文提示，让目标缺失状态明确要求保留通知上下文并反馈。

### Console 治理证据链

- Console `Moderation` 已使用 `Ready / Fallback / Unavailable / Unsupported` 四态解释目标回看。
- `Ready` 可直接回看目标；`Fallback` 降级到所属帖子等上级上下文；`Unavailable` 依赖快照和处理记录；`Unsupported` 仅用于当前未接入直接回看的目标类型。
- 审核队列和动作日志都展示目标快照、回看状态、举报原因、目标用户、处理人和治理动作入口。
- 后端定向测试已覆盖聊天快照、导航状态筛选、举报审核和动作日志来源举报证据，当前未发现需要当天修改后端契约的阻断缺口。

## 修改摘要

- 更新 `Frontend/radish.client/src/i18n.ts`：
  - 举报弹窗描述和成功提示改为快照复核口径。
  - 通知目标缺失提示改为“保留上下文、查看证据 / 诊断、必要时反馈”。
- 更新 `Docs/guide/user-commitments.md`：
  - 增补 E4-B 公开 / 本人私域 / Console / 不公开矩阵。
  - 增补通知目标缺失处理和 Console 证据链四态说明。

## 不纳入本批

- 不新增完整屏蔽 / 拉黑、私信限制、申诉工单、账号注销 / 数据导出、退款 / 售后或自动化治理平台。
- 不新增举报滥用评分、关注频率限制、聊天风控或通知偏好保存 API。
- 不改变公开主页、通知、聊天、举报、Console 审核或治理动作的接口载荷。
- 不把 `Unsupported` 状态强行改造成可回看目标；后续若要支持新目标类型，需先补后端导航契约和 Console 验证。

## 后续

- `E4-B` 未发现阻断级代码缺口，建议顺延进入 `P3-12-E5-A 用户可恢复错误与反馈硬化`。
- E5-A 优先覆盖发帖、评论、聊天、通知和 Workbench 中的请求失败、目标丢失、重试路径、诊断编号和可复制反馈。

## 验证

- `npm run test --workspace=radish.client`：通过，303 项测试通过。
- `npm run build --workspace=radish.client`：通过。
- `npm run check:repo-hygiene:changed`：通过，检查 4 个已修改文本文件，未发现文本卫生问题。
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-e4-b-privacy-safety-governance-boundary-audit-2026-07-06.md`：通过，补验未跟踪新增记录文件。
- `git diff --check`：通过。

# 2026-11-28 日终文档回顾

按北京时间的提交时间核对本地仓库。日终提交前当天共 13 个提交，均在 `dev`，范围为 `b26f1e75..137e074f`。下表左列是提交号，候选版本在需要区分时列于右侧。

## 提交与证据

| 当日提交 | 代码、验收及结论 |
| --- | --- |
| `ff501159` | 记录频道消息修复候选 `b26f1e75` 的 PC 与移动复验。六组场景包含鉴权拒绝、HTTP 响应丢失和 Hub 回执先后顺序。四次手动重试复用了原请求标识，最终六条持久消息在每个订阅端各出现一次。[受保护写入复验](./auth-session-protected-write-2026-11-27.md#candidate-retest) |
| `d3ff7c3e`、`7fb15844` | 记录两种不同的恢复结果。专用密钥轮换保留旧密钥文件与旧保护证书，两端续期后仍是原账号，凭据已用新密钥写回。恢复旧认证库备份时，先离线撤销回档会话、对应授权及仍有效的 refresh token，再开放入口，两端均须重新输入凭据。[密钥轮换](./auth-session-key-rotation-2026-11-28.md) · [回档恢复](./auth-session-backup-restore-2026-11-28.md) |
| `44480a4c`、`03107d53` | 已打开的旧页面跨升级仍可读，普通刷新后各交互登录一次并回到原页。另以直接停用主库账号作对照，两条旧会话分别在各自首次实际续期时被拒绝并撤销。[旧页升级](./auth-session-open-page-upgrade-2026-11-28.md#旧页面跨升级) · [停用对照](./auth-session-account-disabled-2026-11-28.md#自然到期后依次重开) |
| `c50adb21`、`8daa7264` | 先确认 Console 停用方案，再交付命令、独立权限、双库撤销与详情进度。主库在同一事务提交停用和 `AccountDisabled` 任务。认证库故障后由 Outbox 重试。正常样本在两个用户页面仍关闭时就已撤销会话及授权，启用的对照账号能正常续期。[停用与跨库实测](./auth-session-account-disabled-2026-11-28.md#console-停用入口与主动撤销) |
| `d92d6361`、`d8d14891`、`d953e601` | 首轮客户端隔离发现 Scalar 地址写死和 Auth 重定向日志暴露查询参数。修复后 API 与 Auth 共用公开 Issuer，MVC 重定向记录事件类型和追踪标识。两端从 Scalar 按钮完成授权、回调、兑换及受保护读取，Auth 控制台和应用文件各为 0/16 个登录参数样本命中。[两端页面与日志](./auth-session-client-isolation-2026-11-28.md#scalar-gateway-retest) |
| `80448635`、`a296e6f5`、`137e074f` | 核对五组验收证据时发现，同一资料中的普通 Web 会再次清除 WebOS 重登凭据。修复保留首次迁移清理，并为迁移后的历史入口及其回调选用专用存储键。PC 和移动各用一份资料、两个不同账号交替刷新，八次受保护读取均匹配对应账号与租户，登录跳转为零。[共享存储与真实页面复验](./auth-session-open-page-upgrade-2026-11-28.md#两入口交替刷新的浏览器复验) |

## 文档核对与接续

- 按 `UserController` 的两个权限要求和 `AccountDisableVo` 的三种状态补齐 API 索引。Console 模块、权限矩阵及会话专题已有停用范围、受保护账号和重试说明。[API 索引](../guide/api-index.md#用户与个人中心) · [停用契约](../features/auth-session-overnight-recovery-plan.md#web-account-disable-proposal)
- 按 Compose 和密钥环实现更新配置指南，按 Auth 的拒绝与重定向事件更新跳转排障。认证手册同步了 WebOS 回调的存储归属及普通 Web 的跨标签退出通知。[配置指南](../guide/configuration.md#普通-web-会话候选) · [登录排障](../guide/operations-runbook.md#登录成功后跳转异常) · [认证服务](../guide/authentication-service.md)
- 普通 Web 完成隔离候选验收，正式开关保持关闭。最初掉线反馈缺少可追溯的实例和入口，原事件原因未定。建议下一轮先对照帖子分页与通知收件箱在 SQLite 和 PostgreSQL 上的执行计划，再决定是否调整索引。[接下来的事项](../planning/current.md#接下来的事项) · [候选验收矩阵](../features/auth-session-overnight-recovery-plan.md#修复验收)

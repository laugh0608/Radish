# F4-R R3-F02-B 自服务权威状态实现与静态门禁

> 日期：2026-08-15
>
> 状态：代码实现与静态门禁已完成；PC / Mobile 真实页面与故障注入留待 `R3-F02` 最终成组运行态验收
>
> 依据：[R3-F02 自服务与边界页设计前代码事实与风险拆批审计](/records/f4-r-r3-f02-self-service-boundary-readiness-audit-2026-08-13)

## 1. 结论

`R3-F02-B` 已关闭 Console Settings、Client Profile 与 Console Profile 的自服务权威状态缺口：

- Settings 只保留已有服务端契约的时区和密码写入，界面语言明确为浏览器本地能力；通知、主题、分页大小、2FA 与会话管理改为不可写的后置说明，不再以默认值伪装成可提交设置；
- 三处自服务读取均区分 `loading / ready / unavailable / stale`，首次失败 fail closed，刷新失败保留旧快照但冻结依赖陈旧事实的写入；
- Profile 主资料、余额、统计和时间偏好按独立快照与请求代际读取，不再以零值或静默 fallback 混淆局部失败；
- Settings 时区 / 密码与两端 Profile 编辑具备精确 dirty、独立 busy、失败保留草稿和离开保护；Client WebOS 窗口关闭、壳层导航与浏览器关闭使用同一锁定事实；
- Console Profile 不再把 API 未提供的最近登录显示为“无记录”，Mobile 固定资料编辑主任务先于账号摘要；
- 被本批触达的时区、资料和密码失败补齐稳定 `Code / MessageKey`，中英文表面不再把中文 `MessageInfo` 当作唯一契约。

本批没有新增 Profile 字段、通知 / 2FA / 会话能力、数据库实体、migration、权限键、依赖或 Pencil 画板，也没有启动服务或浏览器。

## 2. Console Settings

- 服务端时间偏好是时区唯一权威快照；首次读取失败不渲染默认时区，retry 成功后才开放保存与重置；
- 刷新失败进入 `stale`，旧值继续可读，但保存、重置和时区控件冻结；保存成功只消费服务端响应，不用提交草稿模拟成功快照；
- 保存、重置和密码修改分别维护 busy；失败不清空用户输入，密码成功只清理密码草稿；
- 时区 dirty 与密码 dirty 共同接入 React Router blocker 和 `beforeunload`；主动刷新陈旧快照前要求确认丢弃时区草稿；
- 界面语言继续使用现有 i18n 本地持久化，不与服务端时区提交混合。

## 3. Client Profile

- `@radish/http` 统一承接 `GetMyProfile`、`UpdateMyProfile` 与 `GetUserStats`，移除资料卡内自建 fetch / auth header；
- 主资料、余额、统计和时间偏好分别持有快照、错误与请求代际；同目标刷新失败进入 `stale`，无快照失败进入 `unavailable`；
- 资料草稿按服务端快照逐字段比较，资料保存、头像上传和时区保存拥有独立 busy，失败保留当前编辑内容；
- WebOS 窗口状态新增可选关闭确认文案，Profile 只在 dirty 或写入 busy 时设置；正式 Web 导航锁和浏览器关闭沿用同一布尔事实；
- 时间偏好读取不再隐式写入浏览器时区，用户主动保存后才更新服务端权威快照；外部 Profile 的统计 / 关注局部失败显示不可用占位，不伪造 `0`。

## 4. Console Profile 与稳定错误

- Console Profile 使用请求代际保护主资料，首次失败提供 retry，刷新失败保留旧资料并冻结编辑与头像写入；
- dirty 由当前表单值与权威快照精确比较，取消、刷新、站内导航和浏览器关闭都有明确停止线；头像上传与资料保存分离 busy，成功后重新读取权威资料；
- PC 保持既有分区工作面，Mobile 通过现有响应式布局把编辑主任务移到摘要之前，不复制业务状态；
- `UserSelfServiceErrorCodes` 集中定义时区、资料、用户和密码错误码及 message key；Controller 按现有服务异常语义映射 `400 / 404 / 409 / 503`，保留 `MessageInfo` 作为兼容诊断。

## 5. 验证

- 后端全量：`1278 passed / 41 skipped`；`UserControllerProfileTest` 定向 `8 / 8`；
- Client：`553 / 553`，ESLint 与 production build 通过；新增 Profile 权威状态、统一客户端和窗口关闭守卫契约；
- Console：`133 / 133`，ESLint 与 production build 通过；新增 Settings / Profile 权威状态、权限边界、Mobile 顺序与离开守卫契约；
- `dotnet build Radish.slnx -c Debug --no-restore`：`0 warning / 0 error`；
- 文档检查、changed hygiene、身份专项 `34 / 34` 与 `git diff --check` 通过。

本批按计划没有执行 Gateway、PC / Mobile 浏览器或故障注入。真实 Settings / Profile ready、首次 unavailable、retry、stale、dirty / busy 与双语布局将在 `R3-F02-C` 静态门禁关闭后一次验收。

## 6. 下一步

下一顺位进入 `R3-F02-C 错误与路由边界` 方案确认：区分 Client 未知路由与 `/desktop`，补齐两端根级 ErrorBoundary，并让 Console 权限拒绝、Not Found 与运行时错误保持不同理由；不扩大权限、身份或视觉范围。
